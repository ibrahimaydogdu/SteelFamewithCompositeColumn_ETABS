Imports System.Xml
Imports System.IO
Imports System.Xml.Serialization
Imports System.Linq
Imports System.Configuration
Imports System.Globalization


Public Class ETABS_Class
    Private Const PMM_LOG_MULTIPLIER As Double = 0.5
    Private Const DRIFT_LOG_MULTIPLIER As Double = 0.5
    Private Const UPPER_BOUND_MULTIPLIER As Double = 0.23
    Private Const LOWER_BOUND_MULTIPLIER As Double = 0.23
    Private Const PMM_RATIO_OFFSET As Double = 0.01
    Private Const COORD_TOL As Double = 0.001 'mm
    Private Const STEEL_MATERIAL As String = "A992Fy50"

    Public Frames() As FramePointStoryGroupStructures_.Frame_
    Public Points() As FramePointStoryGroupStructures_.Point_
    Public Stories() As FramePointStoryGroupStructures_.Story_
    Public Groups() As FramePointStoryGroupStructures_.Group_
    Public SteelFrameDesignGroupIDs As List(Of Integer)     'design variable index -> Groups index
    Public SapModel As ETABSv1.cSapModel
    Public ETABSObject As ETABSv1.cOAPI = Nothing
    Public WSections As List(Of SectionStructures_.STEEL_I_SECTION)
    Public encasedSections As List(Of SectionStructures_.RectangularencasedISection_)
    Public FormInfo As MiscellaneousStructures.FormInfo_
    Public GeoCons As MiscellaneousStructures.GeoCons_
    Public ComboNames As Combinations_
    Public StructureHeight As Double
    Public TopDriftX As Double
    Public TopDriftY As Double
    Public TopDriftLimit As Double
    Public Ub() As Integer
    Public Lb() As Integer
    Public StructureWeight As Double
    Public Iter As Integer
    Public A992Fy50Weight As Double
    Public SectionPropertyData As String
    Public ETABS_print As ETABS_Print

    'Name -> index lookups (avoid repeated linear searches)
    Private PointIndex As Dictionary(Of String, Integer)
    Private FrameIndex As Dictionary(Of String, Integer)
    Private GroupIndex As Dictionary(Of String, Integer)
    Private VarIndex As Dictionary(Of String, Integer)      'Group name -> design variable index
    Private DriftCaseNames As List(Of String)             'output selection for displacements
    Private DriftComboNames As List(Of String)
    Private Shared Rng As New Random()

    'Encased composite columns (CompositeColumn.vb)
    Public CompositeSettings As EncasedSettings_
    Public CompositeMat As CompositeMaterial_
    Private CompositeActive As Boolean                    'False while the steel auto-select design (Initilize_UBLB) runs
    Private ReadOnly EncasedCache As New Dictionary(Of Integer, EncasedIShape)
    Private ReadOnly MemberChecks As New Dictionary(Of String, CompositeMemberCheck)
    Private ReadOnly CreatedSections As New HashSet(Of String)
    Private Const NO_DESIGN As Integer = 7
    'A design whose analysis (or design) cannot be completed is infeasible, not a fatal error
    Public AnalysisFailed As Boolean
    Private Const FAILED_PENALTY As Double = 10

    Public Shared Sub SetSeed(ByVal Seed As Integer)
        Rng = New Random(Seed)
    End Sub


    Public Sub New(ByRef FormInfo_ As MiscellaneousStructures.FormInfo_, ByRef ret As Integer)
        ETABS_print = New ETABS_Print
        FormInfo = FormInfo_
        ret = Initilize()
    End Sub
    Public Sub Close(ret As Integer)
        FormInfo.TimerInfo.FinishTime = TimeOfDay.ToString("hh:mm:ss")
        Dim timeSpan As TimeSpan = Date.Now.Subtract(FormInfo.TimerInfo.startDate)
        Dim avtime As Double = If(Iter > 0, Math.Round(timeSpan.TotalSeconds / Iter, 2), 0)
        FormInfo.TimerInfo.TotalTime = timeSpan.Days & "D:" & timeSpan.Hours & "H:" & timeSpan.Minutes & "M:" & timeSpan.Seconds & "S," & "Ave=" & avtime & "sec"

        'Close ETABS
        ETABSObject?.ApplicationExit(False)
        SapModel = Nothing
        ETABSObject = Nothing

        If ret = 0 Then
            MsgBox("API script completed successfully.")
        Else
            MsgBox("API script FAILED to complete.")
        End If
    End Sub

    Private Function Initilize() As Integer
        Dim ret As Integer
        ret = InitilizeETABS()
        If (ret <> 0) Then : Errorlogprint("Problem occured on Function: InitilizeETABS") : Return ret : End If
        If FormInfo.CompositeColumns Then
            ret = InitilizeCompositeSettings()
            If (ret <> 0) Then : Errorlogprint("Problem occured on Function: InitilizeCompositeSettings") : Return ret : End If
        End If
        ret = InitilizePoints()
        If (ret <> 0) Then : Errorlogprint("Problem occured on Function: InitilizePoints") : Return ret : End If
        ret = InitilizeFrames()
        If (ret <> 0) Then : Errorlogprint("Problem occured on Function: InitilizeFrames") : Return ret : End If
        ret = InitilizeStories()
        If (ret <> 0) Then : Errorlogprint("Problem occured on Function: InitilizeStories") : Return ret : End If
        ret = InitilizeGroups()
        If (ret <> 0) Then : Errorlogprint("Problem occured on Function: InitilizeGroups") : Return ret : End If
        ret = InitilizeLoadCases()
        If (ret <> 0) Then : Errorlogprint("Problem occured on Function: InitilizeLoadCases") : Return ret : End If
        ret = InitilizeSections()
        If (ret <> 0) Then : Errorlogprint("Problem occured on Function: InitilizeSections") : Return ret : End If
        ret = InitilizeGeometricCons()
        If (ret <> 0) Then : Errorlogprint("Problem occured on Function: InitilizeGeometricCons") : Return ret : End If
        If FormInfo.CompositeColumns Then
            ret = InitilizeCompositeMaterials()
            If (ret <> 0) Then : Errorlogprint("Problem occured on Function: InitilizeCompositeMaterials") : Return ret : End If
        End If
        '_____________________________________________________
        'Steel design code (set once)
        If SteelFrameDesignGroupIDs.Count > 0 Then
            ret = SapModel.DesignSteel.SetCode(FormInfo.FrameInfo.SteelDesignCode)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :DesignSteel.SetCode, code not available in the ETABS API: " & FormInfo.FrameInfo.SteelDesignCode) : Return ret : End If
        End If
        '_____________________________________________________
        'Upper Lower boundary Def
        If FormInfo.CheckStructure = False Then
            ret = Initilize_UBLB()
            If (ret <> 0) Then : Errorlogprint("Problem occured on Function: Initilize_UBLB") : Return ret : End If
        End If
        CompositeActive = FormInfo.CompositeColumns
        '_____________________________________________________
        'Start Timer
        FormInfo.TimerInfo.startDate = Date.Now
        FormInfo.TimerInfo.StartTime = TimeOfDay.ToString("hh:mm:ss")
        Return ret
    End Function

    'App.config <appSettings> first, environment variable as fallback
    Private Shared Function ReadSetting(ByVal key As String) As String
        Dim value As String = ConfigurationManager.AppSettings(key)
        If String.IsNullOrWhiteSpace(value) Then value = Environment.GetEnvironmentVariable(key)
        Return value
    End Function

    Private Function InitilizeETABS() As Integer
        Dim ret As Integer
        Dim SapFileName As String = FormInfo.FileList.ETABSFile
        'set the following flag to true to attach to a running instance of the program
        'otherwise a new instance of the program will be started
        Dim AttachToInstance As Boolean = False

        Dim ProgramPath As String = ReadSetting("ETABSProgramPath")
        SectionPropertyData = ReadSetting("SectionPropertyDataPath")
        If String.IsNullOrWhiteSpace(ProgramPath) OrElse Not File.Exists(ProgramPath) Then
            Errorlogprint("ETABS program not found. Check 'ETABSProgramPath' in App.config: " & ProgramPath)
            Return -1
        End If
        If String.IsNullOrWhiteSpace(SectionPropertyData) OrElse Not File.Exists(SectionPropertyData) Then
            Errorlogprint("Section property file not found. Check 'SectionPropertyDataPath' in App.config: " & SectionPropertyData)
            Return -1
        End If

        If AttachToInstance Then
            Try
                ETABSObject = DirectCast(System.Runtime.InteropServices.Marshal.GetActiveObject("CSI.ETABS.API.ETABSObject"), ETABSv1.cOAPI)
            Catch ex As Exception
                Errorlogprint("No running instance of the program found or failed to attach.")
                Return -1
            End Try
        Else
            Try
                Dim myHelper As ETABSv1.cHelper = New ETABSv1.Helper
                ETABSObject = myHelper.CreateObject(ProgramPath)
            Catch ex As Exception
                Errorlogprint("Cannot start a new instance of the program: " & ex.Message)
                Return -1
            End Try
            If ETABSObject Is Nothing Then
                Errorlogprint("Failed to create ETABS object.")
                Return -1
            End If
            ret = ETABSObject.ApplicationStart()
            If (ret <> 0) Then : Errorlogprint("Problem occured on :ApplicationStart") : Return ret : End If
        End If

        'Get a reference to cSapModel to access all OAPI classes and functions
        SapModel = ETABSObject.SapModel

        If FormInfo.HideETABS = True Then
            ret = ETABSObject.Hide
            If (ret <> 0) Then : Errorlogprint("Problem occured on :Hide model") : Return ret : End If
        End If

        ret = SapModel.File.OpenFile(SapFileName)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :OpenFile") : Return ret : End If

        If SapModel.GetModelIsLocked = True Then
            ret = SapModel.SetModelIsLocked(False)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :Unlock model") : Return ret : End If
        End If
        '_____________________________________________________
        'set present units to kN-mm (section library is in mm)
        ret = SapModel.SetPresentUnits(ETABSv1.eUnits.kN_mm_C)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :SetPresentUnits") : Return ret : End If
        'material weight per unit volume
        Dim m As Double
        ret = SapModel.PropMaterial.GetWeightAndMass(STEEL_MATERIAL, A992Fy50Weight, m)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :PropMaterial.GetWeightAndMass") : Return ret : End If
        Return ret
    End Function

    Private Function InitilizePoints() As Integer
        Dim ret As Integer
        Dim PNumber As Integer
        Dim PNames() As String = Nothing
        ret = SapModel.PointObj.GetNameList(PNumber, PNames)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :PointObj.GetNamelist") : Return ret : End If
        ReDim Points(PNumber - 1)
        PointIndex = New Dictionary(Of String, Integer)(PNumber)
        For i = 0 To PNumber - 1
            Points(i) = New FramePointStoryGroupStructures_.Point_ With {.PointName = PNames(i)}
            ret = SapModel.PointObj.GetCoordCartesian(Points(i).PointName, Points(i).Xcoord, Points(i).YCoord, Points(i).Zcoord)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :GetCoordCartesian") : Return ret : End If
            PointIndex(PNames(i)) = i
        Next i
        Return ret
    End Function

    Private Function InitilizeFrames() As Integer
        Dim ret As Integer
        Dim FNumber As Integer
        Dim FNames() As String = Nothing
        ret = SapModel.FrameObj.GetNameList(FNumber, FNames)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :GetNameList") : Return ret : End If
        ReDim Frames(FNumber - 1)
        FrameIndex = New Dictionary(Of String, Integer)(FNumber)
        For i = 0 To FNumber - 1
            Frames(i) = New FramePointStoryGroupStructures_.Frame_ With {.FrameName = FNames(i)}
            FrameIndex(FNames(i)) = i
            '_____________________________________________________
            'get names of points
            Dim FirstPoint As String = Nothing
            Dim SecondPoint As String = Nothing
            ret = SapModel.FrameObj.GetPoints(Frames(i).FrameName, FirstPoint, SecondPoint)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :FrameObj.GetPoints") : Return ret : End If

            Dim ind1, ind2 As Integer
            If Not PointIndex.TryGetValue(FirstPoint, ind1) OrElse Not PointIndex.TryGetValue(SecondPoint, ind2) Then
                Errorlogprint("Point not found for frame: " & Frames(i).FrameName)
                Return -1
            End If
            Frames(i).FirstPointName = FirstPoint
            Frames(i).SecondPointName = SecondPoint

            Dim dx As Double = Points(ind2).Xcoord - Points(ind1).Xcoord
            Dim dy As Double = Points(ind2).YCoord - Points(ind1).YCoord
            Dim dz As Double = Points(ind2).Zcoord - Points(ind1).Zcoord
            Dim hx As Boolean = Math.Abs(dx) > COORD_TOL
            Dim hy As Boolean = Math.Abs(dy) > COORD_TOL
            Dim hz As Boolean = Math.Abs(dz) > COORD_TOL
            If hx And Not hy And Not hz Then
                Frames(i).FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.X
            ElseIf Not hx And hy And Not hz Then
                Frames(i).FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Y
            ElseIf Not hx And Not hy And hz Then
                Frames(i).FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Z
            ElseIf hx And Not hy And hz Then
                Frames(i).FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.DiagonalXZ
            ElseIf Not hx And hy And hz Then
                Frames(i).FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.DiagonalYZ
            ElseIf hx And hy And Not hz Then
                Frames(i).FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.DiagonalXY
            Else
                Errorlogprint("Check coordinates of the member: " & Frames(i).FrameName)
            End If
            Frames(i).FrameLenght = Math.Sqrt(dx ^ 2 + dy ^ 2 + dz ^ 2)
            '_____________________________________________________
            'get frame local axis angle
            Dim Advanced As Boolean = False
            ret = SapModel.FrameObj.GetLocalAxes(Frames(i).FrameName, Frames(i).LocalAxisAngle, Advanced)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :FrameObj.GetLocalAxes") : Return ret : End If
            '_____________________________________________________
            'get frame object groups ("All" is always returned)
            Dim NumberGroups As Integer
            Dim FGroups() As String = Nothing
            ret = SapModel.FrameObj.GetGroupAssign(Frames(i).FrameName, NumberGroups, FGroups)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :GetGroupAssign") : Return ret : End If
            If NumberGroups = 1 Then Errorlogprint("No group definition, Check group of frame ID:" & Frames(i).FrameName)
            If NumberGroups > 2 Then Errorlogprint("More group definition than 1, Check group of frame ID:" & Frames(i).FrameName)
            For j = 0 To NumberGroups - 1
                If FGroups(j) <> "All" Then Frames(i).GroupName = FGroups(j)
            Next j
            '_____________________________________________________
            'Get Frame design procedure
            ret = SapModel.FrameObj.GetDesignProcedure(Frames(i).FrameName, Frames(i).FrameDesignProcedure)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :GetDesignProcedure") : Return ret : End If
            If FormInfo.CompositeColumns Then
                'composite sections of a previous run are set to "No Design"; they are design variables
                Dim PropName As String = Nothing, SAuto As String = Nothing
                ret = SapModel.FrameObj.GetSection(Frames(i).FrameName, PropName, SAuto)
                If (ret <> 0) Then : Errorlogprint("Problem occured on :FrameObj.GetSection") : Return ret : End If
                If PropName IsNot Nothing AndAlso PropName.StartsWith(CompositeSettings.SectionPrefix) Then
                    Frames(i).FrameDesignProcedure = FramePointStoryGroupStructures_.DesignProcedure_.SteelFrameDesign
                End If
            End If
        Next i
        Return ret
    End Function

    Private Function InitilizeStories() As Integer
        Dim ret As Integer
        Dim SNumber As Integer
        Dim SNames() As String = Nothing
        Dim FNumber As Integer
        Dim FNames() As String = Nothing
        ret = SapModel.Story.GetNameList(SNumber, SNames)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :Story.GetNameList") : Return ret : End If
        ReDim Stories(SNumber - 1)
        For i = 0 To SNumber - 1
            Stories(i).StoryName = SNames(i)
            '_____________________________________________________
            'point object names on story
            Dim PNumber As Integer
            Dim PNames() As String = Nothing
            ret = SapModel.PointObj.GetNameListOnStory(Stories(i).StoryName, PNumber, PNames)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :PointObj.GetNameListonStory") : Return ret : End If
            Stories(i).StoryPointNames = If(PNames, New String() {})
            '_____________________________________________________
            'story elevation and height
            ret = SapModel.Story.GetElevation(Stories(i).StoryName, Stories(i).StoryLevel)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :Story.GetElevation") : Return ret : End If
            Dim StoryHeight As Double
            ret = SapModel.Story.GetHeight(Stories(i).StoryName, StoryHeight)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :Story.GetHeight") : Return ret : End If
            '_____________________________________________________
            'frame object names on each story
            ret = SapModel.FrameObj.GetNameListOnStory(Stories(i).StoryName, FNumber, FNames)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :FrameObj.GetNameListonStory") : Return ret : End If
            Dim StoryFrames As New List(Of FramePointStoryGroupStructures_.Frame_)
            For j = 0 To FNumber - 1
                Dim k As Integer
                If FrameIndex.TryGetValue(FNames(j), k) Then StoryFrames.Add(Frames(k))
            Next j
            Stories(i).StoryFrames = StoryFrames.ToArray()
            '_____________________________________________________
            'Inter-Story Drift Limit (column length, story height if no column)
            Dim Column = StoryFrames.FirstOrDefault(Function(c) c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Z)
            Dim H As Double = If(Column.FrameName IsNot Nothing, Column.FrameLenght, StoryHeight)
            Stories(i).InterStoryDriftLimit = H / FormInfo.FrameInfo.InterStoryDriftR
        Next i
        StructureHeight = Stories.Max(Function(c) c.StoryLevel)
        TopDriftLimit = StructureHeight / FormInfo.FrameInfo.TopStoryDriftR
        Return ret
    End Function

    Private Function InitilizeGroups() As Integer
        Dim ret As Integer
        Dim GNumber As Integer
        Dim GNames() As String = Nothing
        ret = SapModel.GroupDef.GetNameList(GNumber, GNames)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :GroupDef.GetNameList") : Return ret : End If
        GNames = GNames.Where(Function(item) item <> "All").ToArray()
        GNumber = GNames.Length
        ReDim Groups(GNumber - 1)

        GroupIndex = New Dictionary(Of String, Integer)
        VarIndex = New Dictionary(Of String, Integer)
        SteelFrameDesignGroupIDs = New List(Of Integer)
        For i = 0 To GNumber - 1
            Groups(i).GroupName = GNames(i)
            GroupIndex(GNames(i)) = i
            Dim FNumber As Integer
            Dim FNames() As String = Nothing
            Dim ObjectType() As Integer = Nothing
            ret = SapModel.GroupDef.GetAssignments(Groups(i).GroupName, FNumber, ObjectType, FNames)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :GroupDef.GetAssignments") : Return ret : End If
            If FNumber = 0 Then
                Errorlogprint("check group members of group:" & GNames(i))
                Groups(i).GroupObjectNames = New String() {}
                Groups(i).GroupObjectTypes = New FramePointStoryGroupStructures_.ObjectType_() {}
                Continue For
            End If
            Groups(i).GroupObjectNames = FNames.Take(FNumber).ToArray()
            Groups(i).GroupObjectTypes = ObjectType.Take(FNumber).Select(Function(c) CType(c, FramePointStoryGroupStructures_.ObjectType_)).ToArray()
            If Groups(i).GroupObjectTypes.Distinct().Count() > 1 Then
                Errorlogprint("Different types of object please check group " & Groups(i).GroupName)
                Return -1
            End If

            Groups(i).GroupLength = 0
            If Groups(i).GroupObjectTypes(0) = FramePointStoryGroupStructures_.ObjectType_.Frame Then
                For j = 0 To FNumber - 1
                    Dim k As Integer
                    If Not FrameIndex.TryGetValue(FNames(j), k) Then Continue For
                    Groups(i).GroupLength += Frames(k).FrameLenght
                    If j > 0 AndAlso Groups(i).GroupDesignPocedure <> Frames(k).FrameDesignProcedure Then
                        Errorlogprint("Check design procedure of member " & Frames(k).FrameName & " of group: " & Groups(i).GroupName)
                        Return -1
                    End If
                    Groups(i).GroupDesignPocedure = Frames(k).FrameDesignProcedure
                Next j
                If Groups(i).GroupDesignPocedure = FramePointStoryGroupStructures_.DesignProcedure_.SteelFrameDesign Then
                    VarIndex(Groups(i).GroupName) = SteelFrameDesignGroupIDs.Count
                    SteelFrameDesignGroupIDs.Add(i)
                    'column groups (all members vertical) become encased composite columns
                    Groups(i).IsComposite = FormInfo.CompositeColumns AndAlso FNames.Take(FNumber).All(Function(n) FrameIndex.ContainsKey(n) AndAlso
                                                Frames(FrameIndex(n)).FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Z)
                End If
            End If
        Next i
        Return ret
    End Function

    Private Function InitilizeLoadCases() As Integer
        Dim ret As Integer
        Dim NumberNames As Integer
        Dim MyName As String() = Nothing
        ret = SapModel.RespCombo.GetNameList(NumberNames, MyName)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :RespCombo.GetNameList") : Return ret : End If
        ComboNames.AllCombos = If(MyName, New String() {}).ToList()

        ret = ReadDesignCombos()
        If (ret <> 0) Then Return ret
        '_____________________________________________________
        'Default design combinations (code based, from the load patterns) if the model has none
        If FormInfo.AutoCombos AndAlso ComboNames.DesignSteelStrength.Count = 0 Then
            ret = SapModel.RespCombo.AddDesignDefaultCombos(True, False, False, False)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :RespCombo.AddDesignDefaultCombos") : Return ret : End If
            Dim OldCombos As New HashSet(Of String)(ComboNames.AllCombos)
            MyName = Nothing
            ret = SapModel.RespCombo.GetNameList(NumberNames, MyName)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :RespCombo.GetNameList") : Return ret : End If
            ComboNames.AllCombos = If(MyName, New String() {}).ToList()
            ret = ReadDesignCombos()
            If (ret <> 0) Then Return ret
            Dim NewCombos = ComboNames.AllCombos.Where(Function(c) Not OldCombos.Contains(c)).ToList()
            Errorlogprint("Info: " & NewCombos.Count & " default design combinations created: " & String.Join(", ", NewCombos))
            'flag them for steel design if ETABS did not ("DStlD.." = deflection, others = strength)
            If ComboNames.DesignSteelStrength.Count = 0 Then
                For Each c In NewCombos.Where(Function(x) Not x.Contains("StlD"))
                    SapModel.DesignSteel.SetComboStrength(c, True)
                Next
            End If
            If ComboNames.DesignSteelDeflection.Count = 0 Then
                For Each c In NewCombos.Where(Function(x) x.Contains("StlD"))
                    SapModel.DesignSteel.SetComboDeflection(c, True)
                Next
            End If
            ret = ReadDesignCombos()
            If (ret <> 0) Then Return ret
        End If
        If ComboNames.DesignSteelStrength.Count = 0 Then
            Errorlogprint("No strength design combination in the model (define combinations or enable 'Create default design combos')")
            Return -1
        End If

        MyName = Nothing
        ret = SapModel.LoadCases.GetNameList(NumberNames, MyName)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :LoadCases.GetNameList") : Return ret : End If
        'static / dynamic response cases only: modal, buckling and internal (~) cases are not displacements
        Dim LoadCaseNames As List(Of String) = If(MyName, New String() {}).Where(Function(c) IsResponseCase(c)).ToList()
        '_____________________________________________________
        'Output selection of the drift checks
        If FormInfo.DriftComboMode = MiscellaneousStructures.DriftComboMode_.LateralOnly Then
            Dim Visited As New Dictionary(Of String, Boolean)
            DriftComboNames = ComboNames.AllCombos.Where(Function(c) IsLateralCombo(c, Visited)).ToList()
            DriftCaseNames = If(DriftComboNames.Count > 0, New List(Of String), LoadCaseNames.Where(Function(c) IsLateralCase(c)).ToList())
            If DriftComboNames.Count + DriftCaseNames.Count = 0 Then
                Errorlogprint("No lateral (wind/earthquake) combination or case found for the drift checks")
                Return -1
            End If
        Else
            DriftComboNames = ComboNames.AllCombos
            DriftCaseNames = LoadCaseNames
        End If
        Errorlogprint("Info: drift checks use combos [" & String.Join(", ", DriftComboNames) & "] cases [" & String.Join(", ", DriftCaseNames) & "]")
        Return ret
    End Function

    Private Function ReadDesignCombos() As Integer
        Dim NumberNames As Integer
        Dim MyName As String() = Nothing
        Dim ret As Integer = SapModel.DesignSteel.GetComboStrength(NumberNames, MyName)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :DesignSteel.GetComboStrength") : Return ret : End If
        ComboNames.DesignSteelStrength = If(MyName, New String() {}).ToList()
        MyName = Nothing
        ret = SapModel.DesignSteel.GetComboDeflection(NumberNames, MyName)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :DesignSteel.GetComboDeflection") : Return ret : End If
        ComboNames.DesignSteelDeflection = If(MyName, New String() {}).ToList()
        Return ret
    End Function

    Private Function IsResponseCase(ByVal CaseName As String) As Boolean
        If CaseName.StartsWith("~") Then Return False
        Dim CaseType As ETABSv1.eLoadCaseType
        Dim SubType As Integer
        If SapModel.LoadCases.GetTypeOAPI(CaseName, CaseType, SubType) <> 0 Then Return False
        Return CaseType <> ETABSv1.eLoadCaseType.Modal AndAlso CaseType <> ETABSv1.eLoadCaseType.Buckling
    End Function

    'Load case with wind / earthquake load patterns, lateral accelerations or response spectrum
    Private Function IsLateralCase(ByVal CaseName As String) As Boolean
        Dim CaseType As ETABSv1.eLoadCaseType
        Dim SubType As Integer
        If SapModel.LoadCases.GetTypeOAPI(CaseName, CaseType, SubType) <> 0 Then Return False
        If CaseType = ETABSv1.eLoadCaseType.ResponseSpectrum Then Return True
        Dim NumberLoads As Integer
        Dim LoadType() As String = Nothing
        Dim LoadName() As String = Nothing
        Dim SF() As Double = Nothing
        Dim ret As Integer
        Select Case CaseType
            Case ETABSv1.eLoadCaseType.LinearStatic
                ret = SapModel.LoadCases.StaticLinear.GetLoads(CaseName, NumberLoads, LoadType, LoadName, SF)
            Case ETABSv1.eLoadCaseType.NonlinearStatic
                ret = SapModel.LoadCases.StaticNonlinear.GetLoads(CaseName, NumberLoads, LoadType, LoadName, SF)
            Case Else
                Return False
        End Select
        If ret <> 0 Then Return False
        For i = 0 To NumberLoads - 1
            If LoadType(i) = "Accel" Then
                If LoadName(i).ToUpperInvariant() <> "UZ" Then Return True
            Else
                Dim PatternType As ETABSv1.eLoadPatternType
                If SapModel.LoadPatterns.GetLoadType(LoadName(i), PatternType) = 0 AndAlso
                   (PatternType = ETABSv1.eLoadPatternType.Quake OrElse PatternType = ETABSv1.eLoadPatternType.Wind) Then Return True
            End If
        Next
        Return False
    End Function

    Private Function IsLateralCombo(ByVal ComboName As String, ByVal Visited As Dictionary(Of String, Boolean)) As Boolean
        If Visited.ContainsKey(ComboName) Then Return Visited(ComboName)
        Visited(ComboName) = False
        Dim NumberItems As Integer
        Dim CNameType() As ETABSv1.eCNameType = Nothing
        Dim CName() As String = Nothing
        Dim SF() As Double = Nothing
        If SapModel.RespCombo.GetCaseList(ComboName, NumberItems, CNameType, CName, SF) <> 0 Then Return False
        Dim lateral As Boolean = False
        For i = 0 To NumberItems - 1
            If CNameType(i) = ETABSv1.eCNameType.LoadCombo Then
                lateral = lateral OrElse IsLateralCombo(CName(i), Visited)
            Else
                lateral = lateral OrElse IsLateralCase(CName(i))
            End If
        Next
        Visited(ComboName) = lateral
        Return lateral
    End Function

    'Displacement output selection of the drift checks
    Private Function SelectOutputCases() As Integer
        Return SelectOutput(DriftCaseNames, DriftComboNames)
    End Function

    Private Function SelectOutput(ByVal Cases As IEnumerable(Of String), ByVal Combos As IEnumerable(Of String)) As Integer
        Dim ret As Integer = SapModel.Results.Setup.DeselectAllCasesAndCombosForOutput()
        If (ret <> 0) Then : Errorlogprint("Problem occured on :DeselectAllCasesAndCombosForOutput") : Return ret : End If
        For Each CaseName In Cases
            ret = SapModel.Results.Setup.SetCaseSelectedForOutput(CaseName)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :SetCaseSelectedForOutput " & CaseName) : Return ret : End If
        Next
        For Each Combo In Combos
            ret = SapModel.Results.Setup.SetComboSelectedForOutput(Combo)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :SetComboSelectedForOutput " & Combo) : Return ret : End If
        Next
        Return ret
    End Function

    Private Function InitilizeSections() As Integer
        Dim ret As Integer
        ret = InitilizeSections_ReadXML()
        If (ret <> 0) Then : Errorlogprint("Problem occured on :InitilizeSections_ReadXML") : Return ret : End If
        ret = InitilizeSections_ImportToModel()
        If (ret <> 0) Then : Errorlogprint("Problem occured on :InitilizeSections_ImportToModel") : Return ret : End If
        Return ret
    End Function

    'Reads W sections from a CSI property library (e.g. AISC14M.xml, <PROPERTY_FILE>)
    'or from a file serialized as STEEL_I_SECTION() (<ArrayOfSTEEL_I_SECTION>).
    Private Function InitilizeSections_ReadXML() As Integer
        Try
            Dim doc As XDocument = XDocument.Load(SectionPropertyData)
            Dim sections As List(Of SectionStructures_.STEEL_I_SECTION)
            If doc.Root.Name.LocalName = "PROPERTY_FILE" Then
                Dim ns As XNamespace = doc.Root.Name.Namespace
                Dim units As String = CStr(doc.Root.Element(ns + "CONTROL")?.Element(ns + "LENGTH_UNITS"))
                If units IsNot Nothing AndAlso units.Trim().ToLowerInvariant() <> "mm" Then
                    Throw New InvalidDataException("Section library length units must be mm (model units kN-mm), found: " & units)
                End If
                Dim num = Function(e As XElement, tag As String) As Double
                              Dim x As XElement = e.Element(ns + tag)
                              If x Is Nothing Then Return 0
                              Return Double.Parse(x.Value, NumberStyles.Float, CultureInfo.InvariantCulture)
                          End Function
                sections = doc.Root.Elements(ns + "STEEL_I_SECTION").Select(Function(e) New SectionStructures_.STEEL_I_SECTION With {
                    .SectionName = CStr(e.Element(ns + "LABEL")),
                    .Designation = CStr(e.Element(ns + "DESIGNATION")),
                    .Depth = num(e, "D"),
                    .FlangeLength = num(e, "BF"),
                    .FlangeThickness = num(e, "TF"),
                    .WebThickness = num(e, "TW"),
                    .KDES = num(e, "KDES"),
                    .Area = num(e, "A"),
                    .Imajor = num(e, "I33"),
                    .PlasticModulusMajor = num(e, "Z33"),
                    .ShearAreaMajor = num(e, "AS2"),
                    .Iminor = num(e, "I22"),
                    .PlasticModulusMinor = num(e, "Z22"),
                    .ShearAreaMinor = num(e, "AS3"),
                    .TorsionalConstant = num(e, "J"),
                    .SectionModulusMajorPos = num(e, "S33POS"),
                    .SectionModulusMajorNeg = num(e, "S33NEG"),
                    .SectionModulusMinorPos = num(e, "S22POS"),
                    .SectionModulusMinorNeg = num(e, "S22NEG"),
                    .RadiusofGyrationMajor = num(e, "R33"),
                    .RadiusofGyrationMinor = num(e, "R22")}).ToList()
            Else
                Dim serializer As New XmlSerializer(GetType(SectionStructures_.STEEL_I_SECTION()))
                Using reader = doc.CreateReader()
                    sections = CType(serializer.Deserialize(reader), SectionStructures_.STEEL_I_SECTION()).ToList()
                End Using
            End If

            If sections Is Nothing OrElse sections.Count = 0 Then
                Throw New InvalidDataException("The XML file does not contain valid section data.")
            End If
            WSections = sections.Where(Function(c) c.Designation = "W").OrderBy(Function(c) c.Area).ToList()
            If WSections.Count = 0 Then
                Throw New InvalidDataException("No valid 'W' sections found in the XML file.")
            End If
            Return 0
        Catch ex As Exception
            Errorlogprint("Problem occurred while reading and processing the XML file: " & ex.Message)
            Return -1
        End Try
    End Function

    'Imports the W sections that are not yet defined in the model, so SetSection cannot fail
    Private Function InitilizeSections_ImportToModel() As Integer
        Dim ret As Integer
        Dim NumberNames As Integer
        Dim MyName() As String = Nothing
        ret = SapModel.PropFrame.GetNameList(NumberNames, MyName)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :PropFrame.GetNameList") : Return ret : End If
        Dim existing As New HashSet(Of String)(If(MyName, New String() {}), StringComparer.OrdinalIgnoreCase)
        For Each Section In WSections
            If existing.Contains(Section.SectionName) Then Continue For
            ret = SapModel.PropFrame.ImportProp(Section.SectionName, STEEL_MATERIAL, SectionPropertyData, Section.SectionName, -1, "", "")
            If (ret <> 0) Then : Errorlogprint("Problem occured on :PropFrame.ImportProp " & Section.SectionName) : Return ret : End If
        Next
        Return ret
    End Function

    Private Function InitilizeGeometricCons() As Integer
        Dim ret As Integer
        Dim StoryList As List(Of FramePointStoryGroupStructures_.Story_) = Stories.OrderByDescending(Function(c) c.StoryLevel).ToList()
        Dim Keys As New HashSet(Of String)
        '_____________________________________________________
        'Column to column (upper, lower)
        GeoCons.CtoCList = New List(Of String())
        For i = 0 To StoryList.Count - 2
            Dim Upcolums = StoryList(i).StoryFrames.Where(Function(c) c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Z).ToList()
            Dim DownColumns = StoryList(i + 1).StoryFrames.Where(Function(c) c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Z).ToList()
            For Each upcolumn In Upcolums
                Dim downcolumn = DownColumns.FirstOrDefault(Function(c) c.FirstPointName = upcolumn.FirstPointName _
                                           Or c.SecondPointName = upcolumn.FirstPointName Or c.FirstPointName = upcolumn.SecondPointName Or c.SecondPointName = upcolumn.SecondPointName)
                If downcolumn.FrameName Is Nothing Then Continue For
                If upcolumn.GroupName = downcolumn.GroupName Then Continue For
                If Not VarIndex.ContainsKey(upcolumn.GroupName) OrElse Not VarIndex.ContainsKey(downcolumn.GroupName) Then Continue For
                If Keys.Add("C|" & upcolumn.GroupName & "|" & downcolumn.GroupName) Then
                    GeoCons.CtoCList.Add(New String() {upcolumn.GroupName, downcolumn.GroupName})
                End If
            Next
        Next i
        '_____________________________________________________
        'Beam to column (column, beam, connection type)
        GeoCons.BtoCList = New List(Of String())
        For i = 0 To StoryList.Count - 1
            Dim Level As Double = StoryList(i).StoryLevel
            Dim SteelFrames = StoryList(i).StoryFrames.Where(Function(c) c.FrameDesignProcedure = FramePointStoryGroupStructures_.DesignProcedure_.SteelFrameDesign _
                                                                 AndAlso c.GroupName IsNot Nothing AndAlso VarIndex.ContainsKey(c.GroupName)).ToList()
            Dim Columns = SteelFrames.Where(Function(c) c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Z).ToList()
            Dim BeamsX = SteelFrames.Where(Function(c) c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.X).ToList()
            Dim BeamsY = SteelFrames.Where(Function(c) c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Y).ToList()
            For Each column In Columns
                'column end point at this story level
                Dim PointName As String = Nothing
                For Each pn In {column.FirstPointName, column.SecondPointName}
                    If Math.Abs(Points(PointIndex(pn)).Zcoord - Level) < COORD_TOL Then PointName = pn
                Next
                If PointName Is Nothing Then Continue For
                Dim Rotated As Boolean = Math.Abs(column.LocalAxisAngle - 90) < COORD_TOL Or Math.Abs(column.LocalAxisAngle - 270) < COORD_TOL

                Dim BeamX = BeamsX.FirstOrDefault(Function(c) c.FirstPointName = PointName Or c.SecondPointName = PointName)
                If BeamX.FrameName IsNot Nothing Then
                    Dim ConnectionType As String = If(Rotated, "Depth", "Flange")
                    If Keys.Add("B|" & column.GroupName & "|" & BeamX.GroupName & "|" & ConnectionType) Then
                        GeoCons.BtoCList.Add(New String() {column.GroupName, BeamX.GroupName, ConnectionType})
                    End If
                End If
                Dim BeamY = BeamsY.FirstOrDefault(Function(c) c.FirstPointName = PointName Or c.SecondPointName = PointName)
                If BeamY.FrameName IsNot Nothing Then
                    Dim ConnectionType As String = If(Rotated, "Flange", "Depth")
                    If Keys.Add("B|" & column.GroupName & "|" & BeamY.GroupName & "|" & ConnectionType) Then
                        GeoCons.BtoCList.Add(New String() {column.GroupName, BeamY.GroupName, ConnectionType})
                    End If
                End If
            Next
        Next i
        Return ret
    End Function

    'Creates the auto select list with all W sections of the library if it does not exist
    Private Function EnsureAutoSelectList(ByVal ListName As String) As Integer
        Dim NumberNames As Integer
        Dim MyName() As String = Nothing
        Dim ret As Integer = SapModel.PropFrame.GetNameList(NumberNames, MyName, ETABSv1.eFramePropType.Auto)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :PropFrame.GetNameList (Auto)") : Return ret : End If
        If MyName IsNot Nothing AndAlso MyName.Contains(ListName) Then Return 0
        Dim SectName() As String = WSections.Select(Function(c) c.SectionName).ToArray()
        ret = SapModel.PropFrame.SetAutoSelectSteel(ListName, SectName.Length, SectName, "Median", "Created by optimizer", "")
        If (ret <> 0) Then : Errorlogprint("Problem occured on :PropFrame.SetAutoSelectSteel " & ListName) : Return ret : End If
        Errorlogprint("Info: auto select list '" & ListName & "' created with " & SectName.Length & " W sections")
        Return ret
    End Function

    Public Function Initilize_UBLB() As Integer
        Dim ret As Integer = 0
        Dim BeamList As String = If(ReadSetting("BeamAutoSelectList"), "BeamSectionList")
        Dim ColumnList As String = If(ReadSetting("ColumnAutoSelectList"), "ColumnSectionList")
        ret = EnsureAutoSelectList(BeamList)
        If (ret <> 0) Then Return ret
        ret = EnsureAutoSelectList(ColumnList)
        If (ret <> 0) Then Return ret
        '_______________________________________________________________________________________________
        'Assign Auto Steel Beam
        Dim SteelBeams = Frames.Where(Function(c) c.FrameDesignProcedure = FramePointStoryGroupStructures_.DesignProcedure_.SteelFrameDesign _
                                            And (c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.X Or c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Y))
        For Each SteelBeam In SteelBeams
            ret = SapModel.FrameObj.SetSection(SteelBeam.FrameName, BeamList, 0)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :FrameObj.SetSection " & BeamList) : Return ret : End If
        Next
        '_______________________________________________________________________________________________
        'Assign Auto Steel Column (composite columns too: the steel-only design gives the upper bound)
        Dim SteelColumns = Frames.Where(Function(c) c.FrameDesignProcedure = FramePointStoryGroupStructures_.DesignProcedure_.SteelFrameDesign _
                                    And c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Z)
        For Each SteelColumn In SteelColumns
            ret = SapModel.FrameObj.SetSection(SteelColumn.FrameName, ColumnList, 0)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :FrameObj.SetSection " & ColumnList) : Return ret : End If
            ret = SapModel.FrameObj.SetDesignProcedure(SteelColumn.FrameName, FramePointStoryGroupStructures_.DesignProcedure_.SteelFrameDesign, ETABSv1.eItemType.Objects)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :FrameObj.SetDesignProcedure " & SteelColumn.FrameName) : Return ret : End If
        Next
        ret = E3_Analysis()
        If (ret <> 0) Then : Errorlogprint("Problem occured on :E3_Analysis") : Return ret : End If

        ret = G1_ConsPMM(True)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :G1_ConsPMM") : Return ret : End If

        Dim N As Integer = WSections.Count
        ReDim Ub(SteelFrameDesignGroupIDs.Count - 1)
        ReDim Lb(SteelFrameDesignGroupIDs.Count - 1)
        For i = 0 To SteelFrameDesignGroupIDs.Count - 1
            Dim isec As Integer = SteelFrameDesignGroupIDs(i)
            Dim SecID As Integer = Groups(isec).DesignSecID
            If SecID < 0 Then
                Errorlogprint("Design section '" & Groups(isec).DesignSecName & "' of group " & Groups(isec).GroupName & " is not a W section of the library; mid section used")
                SecID = N \ 2
            End If
            Dim Shift As Double = Math.Log(Groups(isec).PMMRatio + PMM_RATIO_OFFSET) * 0.05 * (N - 1)
            Ub(i) = SecID + CInt(Shift + UPPER_BOUND_MULTIPLIER * (N - 1))
            Lb(i) = SecID + CInt(Shift - LOWER_BOUND_MULTIPLIER * (N - 1))
            If Groups(isec).IsComposite Then Lb(i) = 0     'concrete encasement: smaller W sections are feasible
            If Ub(i) > N - 1 Then Ub(i) = N - 1
            If Lb(i) < 0 Then Lb(i) = 0
        Next i
        Return ret
    End Function

    'applyRepair = False: sections are only checked (no modification of the design variables)
    Public Sub Evaluate(ByRef Member As OptimizationStructure_.Member_, ByRef iter_ As Integer, ByRef ret As Integer, Optional ByVal applyRepair As Boolean = True)
        Dim repair As Boolean = applyRepair AndAlso Not FormInfo.CheckStructure
        Dim Sect_Ind() As Integer = Member.DesignVariables
        Iter = iter_

        ret = SetAndAnalyze(Sect_Ind, repair)
        If ret <> 0 Then : Errorlogprint("Problem occured on :SetAndAnalyze") : Exit Sub : End If

        If AnalysisFailed Then
            Member.Penalty = FAILED_PENALTY
        Else
            Call Penalty(Member.Penalty, Sect_Ind, ret, repair)
        End If
        If ret <> 0 Then : Errorlogprint("Problem occured on :Penalty") : Exit Sub : End If
        Member.CostValue = CostStProfile(Sect_Ind)
        Member.PenalizedCost = Member.CostValue * (1 + Member.Penalty) ^ 3
        iter_ = Iter
    End Sub

    Public Function SetAndAnalyze(ByRef Sect_Ind() As Integer, ByVal applyGeometric As Boolean) As Integer
        If applyGeometric Then Call E1_Modifier_Geometric(Sect_Ind)
        Dim ret As Integer = E2_SetSection(Sect_Ind)
        If ret <> 0 Then Return ret
        Return E3_Analysis()
    End Function

    Private Sub E1_Modifier_Geometric(ByVal Sect_Ind() As Integer)   'array elements are modified in place
        For Each CtoC In GeoCons.CtoCList
            Dim GrNameDown As String = CtoC(1)
            Dim UpVar As Integer = VarIndex(CtoC(0))
            Dim DownVar As Integer = VarIndex(GrNameDown)
            Dim UpArea As Double = WSections(Sect_Ind(UpVar)).Area
            Dim UpDepth As Double = WSections(Sect_Ind(UpVar)).Depth
            Dim DownArea As Double = WSections(Sect_Ind(DownVar)).Area
            Dim DownDepth As Double = WSections(Sect_Ind(DownVar)).Depth

            If UpArea > DownArea Or UpDepth > DownDepth Then
                'lower column must not exceed the column(s) below it; no limit if there is none
                Dim DownDownVars = GeoCons.CtoCList.Where(Function(c) c(0) = GrNameDown).Select(Function(c) VarIndex(c(1))).ToList()
                Dim DownDownDepth As Double = If(DownDownVars.Any(), DownDownVars.Min(Function(v) WSections(Sect_Ind(v)).Depth), Double.MaxValue)
                Dim DownDownArea As Double = If(DownDownVars.Any(), DownDownVars.Min(Function(v) WSections(Sect_Ind(v)).Area), Double.MaxValue)
                Dim PosSections = Enumerable.Range(0, WSections.Count).Where(Function(k) WSections(k).Area >= UpArea And WSections(k).Depth >= UpDepth _
                                                                     And WSections(k).Area <= DownDownArea And WSections(k).Depth <= DownDownDepth).ToList()
                If PosSections.Count > 0 Then Sect_Ind(DownVar) = PosSections(Rng.Next(PosSections.Count))
            End If
        Next

        For Each BtoC In GeoCons.BtoCList
            Dim ColVar As Integer = VarIndex(BtoC(0))
            Dim BeamVar As Integer = VarIndex(BtoC(1))
            Dim Gap As Double = ConnectionGap(WSections(Sect_Ind(ColVar)), BtoC(2))
            If WSections(Sect_Ind(BeamVar)).FlangeLength > Gap Then
                Dim PosSections = Enumerable.Range(0, WSections.Count).Where(Function(k) WSections(k).FlangeLength <= Gap).ToList()
                If PosSections.Count > 0 Then Sect_Ind(BeamVar) = PosSections(Rng.Next(PosSections.Count))
            End If
        Next
    End Sub

    Private Shared Function ConnectionGap(ByRef Column As SectionStructures_.STEEL_I_SECTION, ByVal ConType As String) As Double
        If ConType = "Depth" Then Return Column.Depth - 2 * Column.FlangeThickness
        Return Column.FlangeLength
    End Function

    Public Function E2_SetSection(ByRef Sect_Ind() As Integer) As Integer
        Dim ret As Integer
        If SapModel.GetModelIsLocked = True Then
            ret = SapModel.SetModelIsLocked(False)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :Unlock model") : Return ret : End If
        End If
        For i = 0 To SteelFrameDesignGroupIDs.Count - 1
            Dim isec As Integer = SteelFrameDesignGroupIDs(i)
            Dim PropName As String = WSections(Sect_Ind(i)).SectionName
            If CompositeActive AndAlso Groups(isec).IsComposite Then
                ret = EnsureCompositeSection(Sect_Ind(i))
                If (ret <> 0) Then Return ret
                PropName = CompositeSectionName(Sect_Ind(i))
            End If
            ret = SapModel.FrameObj.SetSection(Groups(isec).GroupName, PropName, ETABSv1.eItemType.Group)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :FrameObj.SetSection " & PropName) : Return ret : End If
            If CompositeActive AndAlso Groups(isec).IsComposite Then
                'designed by CompositeColumn.vb, not by the ETABS steel design
                ret = SapModel.FrameObj.SetDesignProcedure(Groups(isec).GroupName, NO_DESIGN, ETABSv1.eItemType.Group)
                If (ret <> 0) Then : Errorlogprint("Problem occured on :FrameObj.SetDesignProcedure " & Groups(isec).GroupName) : Return ret : End If
            End If
        Next i
        Iter += 1
        Return ret
    End Function

    Public Function E3_Analysis() As Integer
        Dim ret As Integer = SapModel.File.Save(FormInfo.FileList.ETABSFile)
        If (ret <> 0) Then : Errorlogprint("Problem occured in :File.Save") : Return ret : End If
        ret = SapModel.Analyze.RunAnalysis
        If (ret <> 0) Then : Errorlogprint("Problem occured on :RunAnalysis") : Return ret : End If
        'cases that were set to run but did not finish (e.g. unstable / not converged nonlinear cases)
        Dim N1, N2 As Integer
        Dim Names1() As String = Nothing, Names2() As String = Nothing
        Dim Status() As Integer = Nothing
        Dim Run() As Boolean = Nothing
        ret = SapModel.Analyze.GetCaseStatus(N1, Names1, Status)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :Analyze.GetCaseStatus") : Return ret : End If
        ret = SapModel.Analyze.GetRunCaseFlag(N2, Names2, Run)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :Analyze.GetRunCaseFlag") : Return ret : End If
        Dim RunCases As New HashSet(Of String)(Enumerable.Range(0, N2).Where(Function(k) Run(k)).Select(Function(k) Names2(k)))
        Dim Failed = Enumerable.Range(0, N1).Where(Function(k) RunCases.Contains(Names1(k)) AndAlso Status(k) <> 4).Select(Function(k) Names1(k)).ToList()
        AnalysisFailed = Failed.Count > 0
        If AnalysisFailed Then Errorlogprint("Warning: analysis not finished for [" & String.Join(", ", Failed) & "], design penalized")
        Return ret
    End Function

    Private Function G1_1_Design() As Integer
        Dim ret As Integer
        If SteelFrameDesignGroupIDs.Count > 0 Then
            ret = SapModel.DesignSteel.StartDesign
            If (ret <> 0) Then : Errorlogprint("Problem occured on :DesignSteel.StartDesign") : Return ret : End If
        End If
        Return ret
    End Function

    'All constraint values are computed on the final (repaired and re-analysed) model state
    Public Sub Penalty(ByRef Penalty As Double, ByRef Sect_Ind() As Integer, ByRef ret As Integer, Optional ByVal applyRepair As Boolean = True)
        Dim repair As Boolean = applyRepair AndAlso Not FormInfo.CheckStructure
        Penalty = 0
        Call F_Evaluate_Drift(Sect_Ind, repair, ret)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :F_Evaluate_Drift") : Exit Sub : End If
        If AnalysisFailed Then : Penalty = FAILED_PENALTY : Exit Sub : End If

        Call G_Evaluate_PMM(Sect_Ind, repair, ret)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :G_Evaluate_PMM") : Exit Sub : End If
        If AnalysisFailed Then : Penalty = FAILED_PENALTY : Exit Sub : End If

        Call H_Evaluate_GeometricPenalty(Sect_Ind)

        Dim InterStoryDriftPenalty As Double = Math.Max(Stories.Max(Function(c) c.InterStoryDPenaltyX), Stories.Max(Function(c) c.InterStoryDPenaltyY))
        Dim TopStoryDriftPenalty As Double = Math.Max(Math.Max(TopDriftX, TopDriftY) / TopDriftLimit - 1, 0)
        Dim PMMPenalty As Double = 0
        If SteelFrameDesignGroupIDs.Count > 0 Then PMMPenalty = Math.Max(SteelFrameDesignGroupIDs.Max(Function(id) Groups(id).PMMRatio) - 1, 0)
        Dim GeometricPenalty As Double = MaxRatioPenalty(ETABS_print.ColumnToColumnGeometricRatio) + MaxRatioPenalty(ETABS_print.BeamToColumnGeometricRatio)
        Penalty = InterStoryDriftPenalty + TopStoryDriftPenalty + PMMPenalty + GeometricPenalty
    End Sub

    Private Shared Function MaxRatioPenalty(ByVal Ratios As List(Of Double)) As Double
        If Ratios Is Nothing OrElse Ratios.Count = 0 Then Return 0
        Return Math.Max(Ratios.Max() - 1, 0)
    End Function

    Private Sub F_Evaluate_Drift(ByRef Sect_Ind() As Integer, ByVal repair As Boolean, ByRef ret As Integer)
        ret = F1_ConsInterStoryDrift()
        If (ret <> 0) Then : Errorlogprint("Problem occured on :F1_ConsInterStoryDrift") : Exit Sub : End If
        If repair AndAlso F2_Modifier_InterStoryDrift(Sect_Ind) Then
            ret = SetAndAnalyze(Sect_Ind, True)
            If ret <> 0 Then : Errorlogprint("Problem occured on :SetAndAnalyze (F2)") : Exit Sub : End If
            If AnalysisFailed Then Exit Sub
            ret = F1_ConsInterStoryDrift()
            If (ret <> 0) Then : Errorlogprint("Problem occured on :F1_ConsInterStoryDrift") : Exit Sub : End If
        End If

        ret = F3_ConsTopStoryDrift()
        If (ret <> 0) Then : Errorlogprint("Problem occured on :F3_ConsTopStoryDrift") : Exit Sub : End If
        If repair AndAlso F4_Modifier_TopStoryDrift(Sect_Ind) Then
            ret = SetAndAnalyze(Sect_Ind, True)
            If ret <> 0 Then : Errorlogprint("Problem occured on :SetAndAnalyze (F4)") : Exit Sub : End If
            If AnalysisFailed Then Exit Sub
            ret = F1_ConsInterStoryDrift()
            If (ret <> 0) Then : Errorlogprint("Problem occured on :F1_ConsInterStoryDrift") : Exit Sub : End If
            ret = F3_ConsTopStoryDrift()
            If (ret <> 0) Then : Errorlogprint("Problem occured on :F3_ConsTopStoryDrift") : Exit Sub : End If
        End If
    End Sub

    'Reads joint displacements of the current analysis and computes inter-story drifts
    Private Function F1_ConsInterStoryDrift() As Integer
        Dim ret As Integer = F1_1_UpdateJointDisp()
        If (ret <> 0) Then : Errorlogprint("Problem occured on :F1_1_UpdateJointDisp") : Return ret : End If
        Try
            ETABS_print.InterStoryDrift_Ratios = New List(Of List(Of Double))
            For i = 0 To Stories.Length - 1
                Dim DriftX As Double = 0
                Dim DriftY As Double = 0
                For Each Frame In Stories(i).StoryFrames
                    If Frame.FrameDirc <> FramePointStoryGroupStructures_.FrameDirc_.Z Then Continue For
                    Dim P1 = Points(PointIndex(Frame.FirstPointName)).PointDisp
                    Dim P2 = Points(PointIndex(Frame.SecondPointName)).PointDisp
                    For j = 0 To Math.Min(P1.U1.Count, P2.U1.Count) - 1
                        DriftX = Math.Max(DriftX, Math.Abs(P1.U1(j) - P2.U1(j)))
                        DriftY = Math.Max(DriftY, Math.Abs(P1.U2(j) - P2.U2(j)))
                    Next j
                Next
                Dim Limit As Double = Stories(i).InterStoryDriftLimit
                Stories(i).InterStoryDriftX = DriftX
                Stories(i).InterStoryDriftY = DriftY
                Stories(i).InterStoryDPenaltyX = Math.Max(DriftX / Limit - 1, 0)
                Stories(i).InterStoryDPenaltyY = Math.Max(DriftY / Limit - 1, 0)
                ETABS_print.InterStoryDrift_Ratios.Add(New List(Of Double)({DriftX, DriftY}))
            Next i
        Catch ex As Exception
            Errorlogprint("Problem occured on :F1_ConsInterStoryDrift " & ex.Message)
            ret = -1
        End Try
        Return ret
    End Function

    'One API call for all joints (group "All") instead of one call per joint
    Private Function F1_1_UpdateJointDisp() As Integer
        Dim ret As Integer = SelectOutputCases()
        If (ret <> 0) Then Return ret

        Dim NumberResults As Integer
        Dim Obj() As String = Nothing
        Dim Elm() As String = Nothing
        Dim LoadCase() As String = Nothing
        Dim StepType() As String = Nothing
        Dim StepNum() As Double = Nothing
        Dim U1() As Double = Nothing
        Dim U2() As Double = Nothing
        Dim U3() As Double = Nothing
        Dim R1() As Double = Nothing
        Dim R2() As Double = Nothing
        Dim R3() As Double = Nothing
        ret = SapModel.Results.JointDispl("All", ETABSv1.eItemTypeElm.GroupElm, NumberResults, Obj, Elm, LoadCase, StepType, StepNum, U1, U2, U3, R1, R2, R3)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :Results.JointDispl") : Return ret : End If

        For i = 0 To Points.Length - 1
            Points(i).PointDisp = New FramePointStoryGroupStructures_.LoadCaseDisp_ With {
                .LoadCaseName = New List(Of String), .U1 = New List(Of Double), .U2 = New List(Of Double), .U3 = New List(Of Double),
                .R1 = New List(Of Double), .R2 = New List(Of Double), .R3 = New List(Of Double)}
        Next i
        For k = 0 To NumberResults - 1
            Dim i As Integer
            If Not PointIndex.TryGetValue(Obj(k), i) Then Continue For
            With Points(i).PointDisp
                .LoadCaseName.Add(LoadCase(k))
                .U1.Add(U1(k)) : .U2.Add(U2(k)) : .U3.Add(U3(k))
                .R1.Add(R1(k)) : .R2.Add(R2(k)) : .R3.Add(R3(k))
            End With
        Next k
        Return ret
    End Function

    'Design variable indices of the column groups on a story
    Private Function StoryColumnVars(ByVal StoryID As Integer) As IEnumerable(Of Integer)
        Return Stories(StoryID).StoryFrames.Where(Function(c) c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Z _
                    AndAlso c.GroupName IsNot Nothing AndAlso VarIndex.ContainsKey(c.GroupName)).Select(Function(c) VarIndex(c.GroupName)).Distinct()
    End Function

    Private Sub StepVariable(ByRef Sect_Ind() As Integer, ByVal v As Integer, ByVal StepSize As Integer)
        Sect_Ind(v) = Math.Min(Math.Max(Sect_Ind(v) + StepSize, Lb(v)), Ub(v))
    End Sub

    'Returns True if a design variable was modified
    Private Function F2_Modifier_InterStoryDrift(ByRef Sect_Ind() As Integer) As Boolean
        Dim changed As Boolean = False
        For i = 0 To Stories.Length - 1
            Dim Ratio As Double = Math.Max(Stories(i).InterStoryDPenaltyX, Stories(i).InterStoryDPenaltyY) + 1
            If Ratio <= 1 Then Continue For
            changed = True
            For Each v In StoryColumnVars(i)
                StepVariable(Sect_Ind, v, CInt(DRIFT_LOG_MULTIPLIER * Math.Log(Ratio) * WSections.Count))
            Next
        Next i
        Return changed
    End Function

    Private Function F3_ConsTopStoryDrift() As Integer
        Dim ret As Integer = 0
        Try
            ETABS_print.TopStoryDrift_Ratio = New List(Of Double)
            Dim TopPoints = Points.Where(Function(c) Math.Abs(c.Zcoord - StructureHeight) < COORD_TOL AndAlso c.PointDisp.U1 IsNot Nothing AndAlso c.PointDisp.U1.Count > 0).ToList()
            If TopPoints.Count = 0 Then Throw New InvalidOperationException("No top points found at the structure height.")
            TopDriftX = TopPoints.Max(Function(p) p.PointDisp.U1.Max(Function(u) Math.Abs(u)))
            TopDriftY = TopPoints.Max(Function(p) p.PointDisp.U2.Max(Function(u) Math.Abs(u)))
            ETABS_print.TopStoryDrift_Ratio.Add(TopDriftX)
            ETABS_print.TopStoryDrift_Ratio.Add(TopDriftY)
        Catch ex As Exception
            Errorlogprint("Error in F3_ConsTopStoryDrift: " & ex.Message)
            ret = -1
        End Try
        Return ret
    End Function

    Private Function F4_Modifier_TopStoryDrift(ByRef Sect_Ind() As Integer) As Boolean
        Dim Ratio As Double = Math.Max(TopDriftX, TopDriftY) / TopDriftLimit
        If Ratio <= 1 Then Return False
        Dim StepSize As Integer = CInt(DRIFT_LOG_MULTIPLIER * Math.Log(Ratio) * WSections.Count)
        For i = 0 To Stories.Length - 1
            For Each v In StoryColumnVars(i)
                StepVariable(Sect_Ind, v, StepSize)
            Next
        Next
        Return True
    End Function

    Private Sub G_Evaluate_PMM(ByRef Sect_Ind() As Integer, ByVal repair As Boolean, ByRef ret As Integer)
        ret = G1_ConsPMM(False)
        If ret <> 0 Then : Errorlogprint("Problem occured on :G1_ConsPMM") : Exit Sub : End If
        If AnalysisFailed Then Exit Sub

        If repair AndAlso G2_Modifier_PMM(Sect_Ind) Then
            ret = SetAndAnalyze(Sect_Ind, True)
            If ret <> 0 Then : Errorlogprint("Problem occured on :SetAndAnalyze (G2)") : Exit Sub : End If
            If AnalysisFailed Then Exit Sub
            Call F_Evaluate_Drift(Sect_Ind, repair, ret)
            If ret <> 0 Then : Errorlogprint("Problem occured on :F_Evaluate_Drift") : Exit Sub : End If
            If AnalysisFailed Then Exit Sub
            ret = G1_ConsPMM(False)
            If ret <> 0 Then : Errorlogprint("Problem occured on :G1_ConsPMM") : Exit Sub : End If
        End If
    End Sub

    'updateDesignSections: also read the sections selected by ETABS (needed only for auto select lists)
    Private Function G1_ConsPMM(ByVal updateDesignSections As Boolean) As Integer
        Dim ret As Integer = G1_1_Design()
        If (ret <> 0) Then : Errorlogprint("Problem occured on :G1_1_Design") : Return ret : End If
        Try
            ETABS_print.PMM_Ratios = New List(Of Double)
            For i = 0 To SteelFrameDesignGroupIDs.Count - 1
                Dim ID As Integer = SteelFrameDesignGroupIDs(i)
                If CompositeActive AndAlso Groups(ID).IsComposite Then Continue For
                Dim NumberItems As Integer
                Dim FrameName As String() = Nothing
                Dim Ratio As Double() = Nothing
                Dim RatioType As Integer() = Nothing
                Dim Location As Double() = Nothing
                Dim ComboName As String() = Nothing
                Dim ErrorSummary As String() = Nothing
                Dim WarningSummary As String() = Nothing
                ret = SapModel.DesignSteel.GetSummaryResults(Groups(ID).GroupName, NumberItems, FrameName, Ratio, RatioType, Location, ComboName, ErrorSummary, WarningSummary, ETABSv1.eItemType.Group)
                If ret <> 0 OrElse NumberItems = 0 Then
                    Errorlogprint("Warning: no steel design results for group " & Groups(ID).GroupName)
                    If updateDesignSections Then Return -1
                    AnalysisFailed = True
                    Return 0
                End If
                Groups(ID).PMMRatio = Ratio.Max()
                Dim ErrorCount As Integer = ErrorSummary.Count(Function(c) Not String.IsNullOrEmpty(c))
                If ErrorCount > 0 Then Groups(ID).PMMRatio += 1 + ErrorCount / NumberItems
                ETABS_print.PMM_Ratios.Add(Groups(ID).PMMRatio)

                If updateDesignSections Then
                    'largest (by area) design section of the group
                    Dim BestName As String = Nothing
                    Dim BestArea As Double = Double.NegativeInfinity
                    For j = 0 To NumberItems - 1
                        Dim PropName As String = Nothing
                        ret = SapModel.DesignSteel.GetDesignSection(FrameName(j), PropName)
                        If (ret <> 0) Then : Errorlogprint("Problem occured on :DesignSteel.GetDesignSection") : Return ret : End If
                        Dim SectID As Integer = WSections.FindIndex(Function(c) c.SectionName = PropName)
                        Dim Area As Double = If(SectID >= 0, WSections(SectID).Area, 0)
                        If Area > BestArea Then : BestArea = Area : BestName = PropName : End If
                    Next j
                    Groups(ID).DesignSecName = BestName
                    Groups(ID).DesignSecID = WSections.FindIndex(Function(c) c.SectionName = BestName)
                End If
            Next i
            If CompositeActive Then
                ret = G1_2_ConsComposite()
                If (ret <> 0) Then : Errorlogprint("Problem occured on :G1_2_ConsComposite") : Return ret : End If
            End If
        Catch ex As Exception
            Errorlogprint("Problem occured on :G1_ConsPMM " & ex.Message)
            ret = -1
        End Try
        Return ret
    End Function

    '_______________________________________________________________________________________________
    'Encased composite columns: AISC 360-16 check with the frame forces of the strength combinations
    Private Function G1_2_ConsComposite() As Integer
        Dim ret As Integer = SelectOutput(New String() {}, ComboNames.DesignSteelStrength)
        If (ret <> 0) Then Return ret
        ETABS_print.CompositeRatios = New List(Of Double)
        For v = 0 To SteelFrameDesignGroupIDs.Count - 1
            Dim ID As Integer = SteelFrameDesignGroupIDs(v)
            If Not Groups(ID).IsComposite Then Continue For
            Dim NumberResults As Integer
            Dim Obj() As String = Nothing, Elm() As String = Nothing, LoadCase() As String = Nothing, StepType() As String = Nothing
            Dim ObjSta() As Double = Nothing, ElmSta() As Double = Nothing, StepNum() As Double = Nothing
            Dim P() As Double = Nothing, V2() As Double = Nothing, V3() As Double = Nothing, T() As Double = Nothing, M2() As Double = Nothing, M3() As Double = Nothing
            ret = SapModel.Results.FrameForce(Groups(ID).GroupName, ETABSv1.eItemTypeElm.GroupElm, NumberResults, Obj, ObjSta, Elm, ElmSta, LoadCase, StepType, StepNum, P, V2, V3, T, M2, M3)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :Results.FrameForce " & Groups(ID).GroupName) : Return ret : End If
            If NumberResults = 0 Then : Errorlogprint("No frame forces for composite group " & Groups(ID).GroupName) : Return -1 : End If

            'one block = one frame object under one combination / step
            Dim Blocks As New Dictionary(Of String, List(Of Integer))
            For k = 0 To NumberResults - 1
                Dim key As String = Obj(k) & "|" & LoadCase(k) & "|" & StepType(k) & "|" & StepNum(k)
                If Not Blocks.ContainsKey(key) Then Blocks(key) = New List(Of Integer)
                Blocks(key).Add(k)
            Next
            Dim SecID As Integer = CompositeSectionID(Groups(ID).GroupName)
            If SecID < 0 Then : Errorlogprint("Composite section of group " & Groups(ID).GroupName & " not found") : Return -1 : End If
            Dim GroupRatio As Double = Encased(SecID).DetailingRatio()
            For Each Block In Blocks.Values
                Dim ks = Block.OrderBy(Function(k) ObjSta(k)).ToList()
                Dim Check As CompositeMemberCheck = MemberCheck(SecID, Frames(FrameIndex(Obj(ks(0)))).FrameLenght)
                Dim kP As Integer = ks.OrderByDescending(Function(k) Math.Abs(P(k))).First()
                Dim PMM, Shear As Double
                Dim r As Double = Check.Ratio(P(kP), ks.Select(Function(k) M3(k)).ToArray(), ks.Select(Function(k) M2(k)).ToArray(),
                                              ks.Max(Function(k) Math.Abs(V2(k))), ks.Max(Function(k) Math.Abs(V3(k))), PMM, Shear)
                GroupRatio = Math.Max(GroupRatio, r)
            Next
            Groups(ID).PMMRatio = GroupRatio
            ETABS_print.PMM_Ratios.Add(GroupRatio)
            ETABS_print.CompositeRatios.Add(GroupRatio)
        Next v
        Return ret
    End Function

    'W section index of the composite section currently assigned to the group
    Private Function CompositeSectionID(ByVal GroupName As String) As Integer
        Dim PropName As String = Nothing, SAuto As String = Nothing
        If SapModel.FrameObj.GetSection(Groups(GroupIndex(GroupName)).GroupObjectNames(0), PropName, SAuto) <> 0 OrElse PropName Is Nothing Then Return -1
        If PropName.StartsWith(CompositeSettings.SectionPrefix) Then PropName = PropName.Substring(CompositeSettings.SectionPrefix.Length)
        Return WSections.FindIndex(Function(c) c.SectionName = PropName)
    End Function

    Private Function G2_Modifier_PMM(ByRef Sect_Ind() As Integer) As Boolean
        Dim changed As Boolean = False
        For i = 0 To SteelFrameDesignGroupIDs.Count - 1
            Dim Ratio As Double = Groups(SteelFrameDesignGroupIDs(i)).PMMRatio
            If Ratio > 1 Then
                changed = True
                StepVariable(Sect_Ind, i, CInt(PMM_LOG_MULTIPLIER * Math.Log(Ratio) * WSections.Count))
            End If
        Next i
        Return changed
    End Function

    Public Sub H_Evaluate_GeometricPenalty(ByRef Sect_Ind() As Integer)
        ETABS_print.ColumnToColumnGeometricRatio = New List(Of Double)
        For Each CtoC In GeoCons.CtoCList
            Dim Up = WSections(Sect_Ind(VarIndex(CtoC(0))))
            Dim Down = WSections(Sect_Ind(VarIndex(CtoC(1))))
            ETABS_print.ColumnToColumnGeometricRatio.Add(Math.Max(Math.Max(Up.Area / Down.Area, Up.Depth / Down.Depth), 1))
        Next

        ETABS_print.BeamToColumnGeometricRatio = New List(Of Double)
        For Each BtoC In GeoCons.BtoCList
            Dim BeamFlange As Double = WSections(Sect_Ind(VarIndex(BtoC(1)))).FlangeLength
            Dim Gap As Double = ConnectionGap(WSections(Sect_Ind(VarIndex(BtoC(0)))), BtoC(2))
            ETABS_print.BeamToColumnGeometricRatio.Add(If(Gap > 0, BeamFlange / Gap, 2))
        Next
    End Sub

    'Steel design: weight of the design groups [kN]
    'Composite columns: relative cost = steel + rebar (per kN) + concrete (per m³) + formwork (per m²)
    Public Function CostStProfile(ByRef Sect_Ind() As Integer) As Double
        StructureWeight = 0
        Dim RebarWeight As Double = 0, ConcreteVolume As Double = 0, FormworkArea As Double = 0
        For i = 0 To SteelFrameDesignGroupIDs.Count - 1
            Dim G = Groups(SteelFrameDesignGroupIDs(i))
            If FormInfo.CompositeColumns AndAlso G.IsComposite Then
                Dim S As EncasedIShape = Encased(Sect_Ind(i))
                StructureWeight += G.GroupLength * S.SteelArea * A992Fy50Weight
                RebarWeight += G.GroupLength * S.RebarArea * CompositeMat.RebarWeight
                ConcreteVolume += G.GroupLength * S.ConcreteArea * 0.000000001        'mm³ -> m³
                FormworkArea += G.GroupLength * 2 * (S.H + S.B) * 0.000001           'mm² -> m²
            Else
                StructureWeight += G.GroupLength * WSections(Sect_Ind(i)).Area * A992Fy50Weight
            End If
        Next
        If Not FormInfo.CompositeColumns Then Return StructureWeight
        Return CompositeSettings.SteelUnitCost * StructureWeight + CompositeSettings.RebarUnitCost * RebarWeight +
               CompositeSettings.ConcreteUnitCost * ConcreteVolume + CompositeSettings.FormworkUnitCost * FormworkArea
    End Function

    'Printable design variable: "W360X110" or "W360X110 [EC 500x450 8D20]"
    Public Function DescribeVariable(ByVal v As Integer, ByVal SecID As Integer) As String
        Dim txt As String = WSections(SecID).SectionName
        If FormInfo.CompositeColumns AndAlso Groups(SteelFrameDesignGroupIDs(v)).IsComposite Then
            Dim S As EncasedIShape = Encased(SecID)
            txt &= " [EC " & S.H & "x" & S.B & " " & S.RebarPos.Count & "D" & S.BarDiameter & "]"
        End If
        Return txt
    End Function

    '_______________________________________________________________________________________________
    'Composite columns: settings, materials, sections

    Private Function InitilizeCompositeSettings() As Integer
        Dim filePath As String = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EncasedSections.xml")
        Try
            CompositeSettings = If(File.Exists(filePath), EncasedSettings_.Load(filePath), New EncasedSettings_())
            Return 0
        Catch ex As Exception
            Errorlogprint("Problem occurred while reading " & filePath & ": " & ex.Message)
            Return -1
        End Try
    End Function

    'Material properties from the ETABS model; strengths limited by AISC 360-16 I1.3
    Private Function InitilizeCompositeMaterials() As Integer
        Dim ret As Integer
        Dim M As New CompositeMaterial_
        Dim Fu, EFy, EFu, s1, s2, s3, U, A, G, W, Mass As Double
        Dim SS, SH As Integer
        Dim Lw As Boolean
        ret = SapModel.PropMaterial.GetOSteel(STEEL_MATERIAL, M.Fy, Fu, EFy, EFu, SS, SH, s1, s2, s3)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :PropMaterial.GetOSteel " & STEEL_MATERIAL) : Return ret : End If
        ret = SapModel.PropMaterial.GetMPIsotropic(STEEL_MATERIAL, M.Es, U, A, G)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :PropMaterial.GetMPIsotropic " & STEEL_MATERIAL) : Return ret : End If
        ret = SapModel.PropMaterial.GetOConcrete(CompositeSettings.ConcreteMaterial, M.fc, Lw, s1, SS, SH, s2, s3, U, A)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :PropMaterial.GetOConcrete " & CompositeSettings.ConcreteMaterial) : Return ret : End If
        ret = SapModel.PropMaterial.GetMPIsotropic(CompositeSettings.ConcreteMaterial, M.Ec, U, A, G)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :PropMaterial.GetMPIsotropic " & CompositeSettings.ConcreteMaterial) : Return ret : End If
        ret = SapModel.PropMaterial.GetORebar(CompositeSettings.RebarMaterial, M.Fysr, Fu, EFy, EFu, SS, SH, s1, s2, Lw)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :PropMaterial.GetORebar " & CompositeSettings.RebarMaterial) : Return ret : End If
        ret = SapModel.PropMaterial.GetMPUniaxial(CompositeSettings.RebarMaterial, M.Esr, A)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :PropMaterial.GetMPUniaxial " & CompositeSettings.RebarMaterial) : Return ret : End If
        M.SteelWeight = A992Fy50Weight
        ret = SapModel.PropMaterial.GetWeightAndMass(CompositeSettings.ConcreteMaterial, W, Mass) : M.ConcreteWeight = W
        If (ret <> 0) Then : Errorlogprint("Problem occured on :PropMaterial.GetWeightAndMass " & CompositeSettings.ConcreteMaterial) : Return ret : End If
        ret = SapModel.PropMaterial.GetWeightAndMass(CompositeSettings.RebarMaterial, W, Mass) : M.RebarWeight = W
        If (ret <> 0) Then : Errorlogprint("Problem occured on :PropMaterial.GetWeightAndMass " & CompositeSettings.RebarMaterial) : Return ret : End If
        'I1.3 (kN/mm²): 21 MPa <= f'c <= 69 MPa, Fy <= 525 MPa, Fysr <= 550 MPa
        If M.fc < 0.021 Or M.fc > 0.069 Then Errorlogprint("Warning: f'c = " & M.fc * 1000 & " MPa is outside 21-69 MPa (AISC I1.3); limited")
        M.fc = Math.Min(Math.Max(M.fc, 0.021), 0.069)
        If M.Fy > 0.525 Then : Errorlogprint("Warning: Fy limited to 525 MPa (AISC I1.3)") : M.Fy = 0.525 : End If
        If M.Fysr > 0.55 Then : Errorlogprint("Warning: Fysr limited to 550 MPa (AISC I1.3)") : M.Fysr = 0.55 : End If
        CompositeMat = M
        Errorlogprint("Info: composite columns in groups [" & String.Join(", ", Groups.Where(Function(c) c.IsComposite).Select(Function(c) c.GroupName)) &
                      "], Fy=" & M.Fy * 1000 & " fc=" & M.fc * 1000 & " Fysr=" & M.Fysr * 1000 & " MPa")
        Return ret
    End Function

    Public Function Encased(ByVal SecID As Integer) As EncasedIShape
        Dim S As EncasedIShape = Nothing
        If Not EncasedCache.TryGetValue(SecID, S) Then
            S = CompositeSettings.Build(WSections(SecID), CompositeMat)
            EncasedCache(SecID) = S
        End If
        Return S
    End Function

    Private Function MemberCheck(ByVal SecID As Integer, ByVal L As Double) As CompositeMemberCheck
        Dim key As String = SecID & "|" & Math.Round(L, 1)
        Dim C As CompositeMemberCheck = Nothing
        If Not MemberChecks.TryGetValue(key, C) Then
            C = New CompositeMemberCheck(Encased(SecID), L, CompositeSettings.K22, CompositeSettings.K33, CompositeSettings.B2)
            MemberChecks(key) = C
        End If
        Return C
    End Function

    Private Function CompositeSectionName(ByVal SecID As Integer) As String
        Return CompositeSettings.SectionPrefix & WSections(SecID).SectionName
    End Function

    'ETABS General section with transformed properties (steel material, weight/mass by modifiers)
    Private Function EnsureCompositeSection(ByVal SecID As Integer) As Integer
        Dim Name As String = CompositeSectionName(SecID)
        If CreatedSections.Contains(Name) Then Return 0
        Dim S As EncasedIShape = Encased(SecID)
        Dim T As TransformedSection_ = S.Transformed()
        Dim Notes As String = "Encased " & S.Steel.SectionName & " in " & S.H & "x" & S.B & " " & CompositeSettings.ConcreteMaterial & ", " & S.RebarPos.Count & "D" & S.BarDiameter
        Dim ret As Integer = SapModel.PropFrame.SetGeneral(Name, STEEL_MATERIAL, T.T3, T.T2, T.Area, T.As2, T.As3, T.J, T.I22, T.I33, T.S22, T.S33, T.Z22, T.Z33, T.R22, T.R33, -1, Notes, "")
        If (ret <> 0) Then : Errorlogprint("Problem occured on :PropFrame.SetGeneral " & Name) : Return ret : End If
        Dim Modifiers() As Double = {1, 1, 1, 1, 1, 1, T.WeightModifier, T.WeightModifier}
        ret = SapModel.PropFrame.SetModifiers(Name, Modifiers)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :PropFrame.SetModifiers " & Name) : Return ret : End If
        CreatedSections.Add(Name)
        Return ret
    End Function

    Public Function CostSlab() As Double
        Return 0
    End Function

    Public Function CostStud() As Double
        Return 0
    End Function

    Public Sub Errorlogprint(ByVal msg As String)
        Dim Dir As String = Nothing
        If Not String.IsNullOrEmpty(FormInfo.FileList.ETABSFile) Then Dir = Path.GetDirectoryName(FormInfo.FileList.ETABSFile)
        If String.IsNullOrEmpty(Dir) Then Dir = AppDomain.CurrentDomain.BaseDirectory
        Try
            File.AppendAllText(Path.Combine(Dir, "ErrorLog.txt"), Date.Now.ToString("yyyy-MM-dd HH:mm:ss") & " Error message: " & msg & Environment.NewLine)
        Catch
            'logging must never stop the optimization
        End Try
    End Sub
End Class

Public Class ETABS_Print
    Public PMM_Ratios As New List(Of Double)
    Public InterStoryDrift_Ratios As New List(Of List(Of Double))
    Public TopStoryDrift_Ratio As New List(Of Double)
    Public BeamToColumnGeometricRatio As New List(Of Double)
    Public ColumnToColumnGeometricRatio As New List(Of Double)
    Public CompositeRatios As New List(Of Double)
End Class
