Imports System.IO
Imports System.Xml.Serialization
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
    Public FormInfo As MiscellaneousStructures.FormInfo_
    Public GeoCons As MiscellaneousStructures.GeoCons_
    Public ComboNames As Combinations_
    Public StructureHeight As Double
    Public TopDriftX As Double
    Public TopDriftY As Double
    Public TopDriftLimit As Double
    Public Ub() As Integer
    Public Lb() As Integer
    Public Iter As Integer
    Public A992Fy50Weight As Double
    Public SectionPropertyData As String
    Public ETABS_print As ETABS_Print
    'Working copy of the model: all saves / analyses run here, the input model is never modified
    Public WorkFile As String
    Private WorkDir As String
    'True: Close shows no message box (batch runs, tests)
    Public Quiet As Boolean

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
    Private Const COMPOSITE_COLUMN_DESIGN As Integer = 13     'FrameObj design procedure (ETABS 20+); 1 locked the hidden ETABS
    Private Const ANALYSIS_IN_PROCESS As Integer = 1     'SetSolverOption_3 process type: 0 auto, 1 ETABS (GUI) process, 2 separate
    'A design whose analysis (or design) cannot be completed is infeasible, not a fatal error
    Public AnalysisFailed As Boolean
    Private Const FAILED_PENALTY As Double = 10

    'Result cache: design vector before repair -> evaluated member (repaired vector, cost, penalty)
    Private ReadOnly Cache As New Dictionary(Of String, OptimizationStructure_.Member_)
    'Section index currently assigned in ETABS per design variable (-1 = unknown): unchanged groups are not re-assigned
    Private Assigned() As Integer
    'Design vector of the current analysis results (Nothing: model changed since): SetAndAnalyze skips a repeat
    Private LastAnalysed() As Integer
    'Cases set to run (Nothing: read again); changed only by SetRunCases / RestoreRunCases
    Private RunCases As HashSet(Of String)

    'Run statistics: total time and number of calls of each ETABS operation (ErrorLog at Close)
    Private ReadOnly Timing As New Dictionary(Of String, Stopwatch)
    Private ReadOnly Calls As New Dictionary(Of String, Integer)

    Public Shared Sub SetSeed(ByVal Seed As Integer)
        Rng = New Random(Seed)
    End Sub

    'Stopwatch of an operation, counted once per call: Dim c = Clock("Analysis") : c.Start() ... c.Stop()
    Private Function Clock(ByVal Name As String) As Stopwatch
        Dim sw As Stopwatch = Nothing
        If Not Timing.TryGetValue(Name, sw) Then
            sw = New Stopwatch : Timing(Name) = sw : Calls(Name) = 0
        End If
        Calls(Name) += 1
        Return sw
    End Function

    Public Function TimingReport() As String
        Return String.Join("; ", Timing.Select(Function(kv) kv.Key & " " & kv.Value.Elapsed.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) & " s / " & Calls(kv.Key) &
                                                   " = " & (kv.Value.Elapsed.TotalSeconds / Math.Max(Calls(kv.Key), 1)).ToString("F2", CultureInfo.InvariantCulture) & " s"))
    End Function


    Public Sub New(ByRef FormInfo_ As MiscellaneousStructures.FormInfo_, ByRef ret As Integer)
        ETABS_print = New ETABS_Print
        FormInfo = FormInfo_
        ret = Initilize()
    End Sub
    Public Sub Close(ret As Integer)
        Errorlogprint("Info: run time " & Date.Now.Subtract(FormInfo.TimerInfo.startDate).ToString("d\.hh\:mm\:ss") & ", " & Iter & " analyses")
        Errorlogprint("Info: timing " & TimingReport())

        Shutdown()
        If Quiet Then Return

        If ret = 0 Then
            MsgBox("API script completed successfully.")
        Else
            MsgBox("API script FAILED to complete.")
        End If
    End Sub

    'Close ETABS and remove the working folder (no message box)
    Public Sub Shutdown()
        ETABSObject?.ApplicationExit(False)
        SapModel = Nothing
        ETABSObject = Nothing
        DeleteWorkDir()
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
        If FormInfo.PDelta Then
            ret = EnablePDelta()
            If (ret <> 0) Then : Errorlogprint("Problem occured on Function: EnablePDelta") : Return ret : End If
        End If
        If FormInfo.DriftComboMode = MiscellaneousStructures.DriftComboMode_.LateralCasesOnly Then
            ret = EnsureServiceLateralCases()
            If (ret <> 0) Then : Errorlogprint("Problem occured on Function: EnsureServiceLateralCases") : Return ret : End If
        End If
        ret = InitilizeLoadCases()
        If (ret <> 0) Then : Errorlogprint("Problem occured on Function: InitilizeLoadCases") : Return ret : End If
        If FormInfo.SkipUnusedCases AndAlso Not FormInfo.CheckStructure Then
            ret = SetRunCases()
            If (ret <> 0) Then : Errorlogprint("Problem occured on Function: SetRunCases") : Return ret : End If
        End If
        ret = InitilizeSections()
        If (ret <> 0) Then : Errorlogprint("Problem occured on Function: InitilizeSections") : Return ret : End If
        ret = InitilizeGeometricCons()
        If (ret <> 0) Then : Errorlogprint("Problem occured on Function: InitilizeGeometricCons") : Return ret : End If
        If FormInfo.CompositeColumns Then
            ret = InitilizeCompositeMaterials()
            If (ret <> 0) Then : Errorlogprint("Problem occured on Function: InitilizeCompositeMaterials") : Return ret : End If
            ret = DetectEncasedSections()
            If (ret <> 0) Then : Errorlogprint("Problem occured on Function: DetectEncasedSections") : Return ret : End If
        End If
        '_____________________________________________________
        'Steel design code (set once)
        If SteelFrameDesignGroupIDs.Count > 0 Then
            ret = SapModel.DesignSteel.SetCode(FormInfo.FrameInfo.SteelDesignCode)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :DesignSteel.SetCode, code not available in the ETABS API: " & FormInfo.FrameInfo.SteelDesignCode) : Return ret : End If
            Dim CodeName As String = Nothing
            SapModel.DesignSteel.GetCode(CodeName)
            Errorlogprint("Info: steel design code " & CodeName)
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
        FormInfo.TimerInfo.StartTime = Date.Now.ToString("HH:mm:ss")
        Return ret
    End Function

    'App.config <appSettings> first, environment variable as fallback
    Private Shared Function ReadSetting(ByVal key As String) As String
        Dim value As String = ConfigurationManager.AppSettings(key)
        If String.IsNullOrWhiteSpace(value) Then value = Environment.GetEnvironmentVariable(key)
        Return value
    End Function

    'Copies the input model to <WorkFolder or %TEMP%>\SteelFrameOpt\<model>_<time>\<model>.EDB
    Private Function CreateWorkCopy(ByVal SourceFile As String) As Integer
        Try
            If Not File.Exists(SourceFile) Then : Errorlogprint("Model file not found: " & SourceFile) : Return -1 : End If
            Dim Root As String = ReadSetting("WorkFolder")
            If String.IsNullOrWhiteSpace(Root) Then Root = Path.Combine(Path.GetTempPath(), "SteelFrameOpt")
            Dim Name As String = Path.GetFileNameWithoutExtension(SourceFile)
            Dim Stamp As String = Date.Now.ToString("yyyyMMdd_HHmmss")
            WorkDir = Path.Combine(Root, Name & "_" & Stamp)
            Dim k As Integer = 1
            While Directory.Exists(WorkDir)
                k += 1 : WorkDir = Path.Combine(Root, Name & "_" & Stamp & "_" & k)
            End While
            Directory.CreateDirectory(WorkDir)
            WorkFile = Path.Combine(WorkDir, Path.GetFileName(SourceFile))
            File.Copy(SourceFile, WorkFile)
            Errorlogprint("Info: working copy " & WorkFile)
            Return 0
        Catch ex As Exception
            Errorlogprint("Cannot create the working copy of " & SourceFile & ": " & ex.Message)
            Return -1
        End Try
    End Function

    'ETABS may hold the analysis files for a moment after exit: retry, never fail the run
    Private Sub DeleteWorkDir()
        If String.IsNullOrEmpty(WorkDir) OrElse Not Directory.Exists(WorkDir) Then Return
        For i = 1 To 10
            Try
                Directory.Delete(WorkDir, True)
                Return
            Catch
                Threading.Thread.Sleep(500)
            End Try
        Next
        Errorlogprint("Warning: working folder could not be deleted: " & WorkDir)
    End Sub

    'Newest "Computers and Structures\ETABS <n>\ETABS.exe" (ETABS 22, 21, ... 19)
    Private Shared Function FindInstalledETABS() As String
        Dim Root As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Computers and Structures")
        If Not Directory.Exists(Root) Then Return Nothing
        Dim Best As String = Nothing, BestVer As Integer = -1
        For Each d In Directory.GetDirectories(Root, "ETABS *")
            Dim v As Integer
            If Integer.TryParse(Path.GetFileName(d).Substring(6).Trim(), v) AndAlso v > BestVer AndAlso File.Exists(Path.Combine(d, "ETABS.exe")) Then
                BestVer = v : Best = Path.Combine(d, "ETABS.exe")
            End If
        Next
        Return Best
    End Function

    Private Function InitilizeETABS() As Integer
        Dim ret As Integer
        Dim SapFileName As String = FormInfo.FileList.ETABSFile

        Dim ProgramPath As String = ReadSetting("ETABSProgramPath")
        SectionPropertyData = ReadSetting("SectionPropertyDataPath")
        If String.IsNullOrWhiteSpace(ProgramPath) OrElse Not File.Exists(ProgramPath) Then
            Dim Found As String = FindInstalledETABS()
            If Found Is Nothing Then
                Errorlogprint("ETABS program not found. Check 'ETABSProgramPath' in App.config: " & ProgramPath)
                Return -1
            End If
            Errorlogprint("Warning: 'ETABSProgramPath' not found (" & ProgramPath & "), using " & Found)
            ProgramPath = Found
        End If
        If String.IsNullOrWhiteSpace(SectionPropertyData) OrElse Not File.Exists(SectionPropertyData) Then
            'same library file name in the property libraries of the ETABS version in use
            Dim Alt As String = If(String.IsNullOrWhiteSpace(SectionPropertyData), Nothing,
                                   Path.Combine(Path.GetDirectoryName(ProgramPath), "Property Libraries", Path.GetFileName(SectionPropertyData)))
            If Alt Is Nothing OrElse Not File.Exists(Alt) Then
                Errorlogprint("Section property file not found. Check 'SectionPropertyDataPath' in App.config: " & SectionPropertyData)
                Return -1
            End If
            Errorlogprint("Warning: 'SectionPropertyDataPath' not found (" & SectionPropertyData & "), using " & Alt)
            SectionPropertyData = Alt
        End If

        'always a new ETABS instance: attaching to a running one would let Shutdown close the user's ETABS
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

        'Get a reference to cSapModel to access all OAPI classes and functions
        SapModel = ETABSObject.SapModel
        Dim Ver As String = Nothing, VerNum As Double
        If SapModel.GetVersion(Ver, VerNum) = 0 Then Errorlogprint("Info: ETABS " & Ver & " (" & ProgramPath & ")")

        If FormInfo.HideETABS = True Then
            ret = ETABSObject.Hide
            If (ret <> 0) Then : Errorlogprint("Problem occured on :Hide model") : Return ret : End If
        End If

        ret = CreateWorkCopy(SapFileName)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :CreateWorkCopy") : Return ret : End If
        ret = SapModel.File.OpenFile(WorkFile)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :OpenFile " & WorkFile) : Return ret : End If

        If SapModel.GetModelIsLocked = True Then
            ret = SapModel.SetModelIsLocked(False)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :Unlock model") : Return ret : End If
        End If
        '_____________________________________________________
        'set present units to kN-mm (section library is in mm)
        ret = SapModel.SetPresentUnits(ETABSv1.eUnits.kN_mm_C)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :SetPresentUnits") : Return ret : End If
        Dim SolverType, ProcessType, ParallelRuns, MaxFileMB, Threads As Integer, StiffCase As String = Nothing
        If SapModel.Analyze.GetSolverOption_3(SolverType, ProcessType, ParallelRuns, MaxFileMB, Threads, StiffCase) = 0 Then
            Errorlogprint("Info: solver type " & SolverType & ", process " & ProcessType & ", parallel runs " & ParallelRuns & ", threads " & Threads)
            'analysis inside the ETABS process (1) instead of a separate process: about 10 % faster per analysis (525M model)
            If ProcessType <> ANALYSIS_IN_PROCESS AndAlso SapModel.Analyze.SetSolverOption_3(SolverType, ANALYSIS_IN_PROCESS, ParallelRuns, MaxFileMB, Threads, StiffCase) = 0 Then
                Errorlogprint("Info: analysis process set to " & ANALYSIS_IN_PROCESS & " (ETABS process)")
            End If
        End If
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
                Frames(i).FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Other       'not a beam / column / plane brace
                Errorlogprint("Warning: member " & Frames(i).FrameName & " is not a beam, column or plane brace (or has zero length)")
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

    '_______________________________________________________________________________________________
    'P-Delta (working copy): nonlinear static cases with P-Delta geometric nonlinearity, linear cases with the
    'preset P-Delta "Non-iterative Based on Mass" (database table, no OAPI setter) unless the model defines one.
    'The composite column check takes B2 = 1, which requires a second-order analysis.
    Private Const PDELTA_TABLE As String = "P-Delta Option Definition"

    Private Function EnablePDelta() As Integer
        Dim N As Integer, Names() As String = Nothing
        Dim ret As Integer = SapModel.LoadCases.GetNameList(N, Names)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :LoadCases.GetNameList") : Return ret : End If
        Dim Changed As New List(Of String)
        For i = 0 To N - 1
            Dim CT As ETABSv1.eLoadCaseType, ST As Integer, G As Integer
            If SapModel.LoadCases.GetTypeOAPI(Names(i), CT, ST) <> 0 OrElse CT <> ETABSv1.eLoadCaseType.NonlinearStatic Then Continue For
            If SapModel.LoadCases.StaticNonlinear.GetGeometricNonlinearity(Names(i), G) = 0 AndAlso G = 0 Then
                ret = SapModel.LoadCases.StaticNonlinear.SetGeometricNonlinearity(Names(i), 1)
                If (ret <> 0) Then : Errorlogprint("Problem occured on :StaticNonlinear.SetGeometricNonlinearity " & Names(i)) : Return ret : End If
                Changed.Add(Names(i))
            End If
        Next
        'preset P-Delta for the linear cases
        Dim Preset As String = "(table not available)"
        Dim Version, NR As Integer, Fields() As String = Nothing, Data() As String = Nothing
        If SapModel.DatabaseTables.GetTableForEditingArray(PDELTA_TABLE, "", Version, Fields, NR, Data) = 0 AndAlso NR > 0 Then
            Dim iM As Integer = Array.IndexOf(Fields, "AutoMethod")
            If iM >= 0 Then
                Preset = Data(iM)
                If String.IsNullOrWhiteSpace(Preset) OrElse Preset = "None" Then
                    Data(iM) = "Non-iterative Based on Mass"
                    ret = SapModel.DatabaseTables.SetTableForEditingArray(PDELTA_TABLE, Version, Fields, NR, Data)
                    Dim NF, NE, NW, NI As Integer, ImportLog As String = Nothing
                    If ret = 0 Then ret = SapModel.DatabaseTables.ApplyEditedTables(True, NF, NE, NW, NI, ImportLog)
                    If ret <> 0 OrElse NF + NE > 0 Then
                        Errorlogprint("Problem occured on :preset P-Delta (" & PDELTA_TABLE & ")" & Environment.NewLine & ImportLog)
                        Return If(ret <> 0, ret, -1)
                    End If
                    Preset = "Non-iterative Based on Mass"
                End If
            End If
        End If
        Errorlogprint("Info: P-Delta: nonlinear cases set to P-Delta [" & String.Join(", ", Changed) & "], preset P-Delta of linear cases: " & Preset)
        Return 0
    End Function

    'Service drift mode: if the model has no pure lateral case, one linear static case per wind / earthquake load
    'pattern is created in the working copy (SRV_<pattern>, factor App.config ServiceLateralFactor, default 1.0)
    Private Function EnsureServiceLateralCases() As Integer
        Dim N As Integer, Names() As String = Nothing
        Dim ret As Integer = SapModel.LoadCases.GetNameList(N, Names)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :LoadCases.GetNameList") : Return ret : End If
        If Names.Take(N).Any(Function(c) IsPureLateralCase(c)) Then Return 0
        Dim Factor As Double = 1.0
        Dim Setting As String = ReadSetting("ServiceLateralFactor")
        If Not String.IsNullOrWhiteSpace(Setting) AndAlso Not Double.TryParse(Setting.Replace(","c, "."c), NumberStyles.Float, CultureInfo.InvariantCulture, Factor) Then Factor = 1.0
        Dim NP As Integer, Patterns() As String = Nothing
        ret = SapModel.LoadPatterns.GetNameList(NP, Patterns)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :LoadPatterns.GetNameList") : Return ret : End If
        Dim Created As New List(Of String)
        For i = 0 To NP - 1
            If Patterns(i).StartsWith("~") Then Continue For
            Dim PT As ETABSv1.eLoadPatternType
            If SapModel.LoadPatterns.GetLoadType(Patterns(i), PT) <> 0 Then Continue For
            If PT <> ETABSv1.eLoadPatternType.Wind AndAlso PT <> ETABSv1.eLoadPatternType.Quake Then Continue For
            Dim CaseName As String = "SRV_" & Patterns(i)
            ret = SapModel.LoadCases.StaticLinear.SetCase(CaseName)
            If ret = 0 Then ret = SapModel.LoadCases.StaticLinear.SetLoads(CaseName, 1, New String() {"Load"}, New String() {Patterns(i)}, New Double() {Factor})
            If (ret <> 0) Then : Errorlogprint("Problem occured on :StaticLinear.SetCase/SetLoads " & CaseName) : Return ret : End If
            Created.Add(CaseName)
        Next
        If Created.Count > 0 Then Errorlogprint("Info: service drift cases created (factor " & Factor & "): [" & String.Join(", ", Created) & "]")
        Return 0
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
        ElseIf FormInfo.DriftComboMode = MiscellaneousStructures.DriftComboMode_.LateralCasesOnly Then
            DriftComboNames = New List(Of String)
            DriftCaseNames = LoadCaseNames.Where(Function(c) IsPureLateralCase(c)).ToList()
            If DriftCaseNames.Count = 0 Then
                Errorlogprint("No pure lateral load case (wind / earthquake patterns only) for the drift checks: define unfactored lateral cases or select another drift mode")
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

    'Response spectrum, or a linear static case whose loads are all wind / earthquake patterns or lateral accelerations
    '(no gravity part, no load factors of a strength combination built into the case)
    Private Function IsPureLateralCase(ByVal CaseName As String) As Boolean
        Dim CaseType As ETABSv1.eLoadCaseType
        Dim SubType As Integer
        If SapModel.LoadCases.GetTypeOAPI(CaseName, CaseType, SubType) <> 0 Then Return False
        If CaseType = ETABSv1.eLoadCaseType.ResponseSpectrum Then Return True
        If CaseType <> ETABSv1.eLoadCaseType.LinearStatic Then Return False
        Dim NumberLoads As Integer, LoadType() As String = Nothing, LoadName() As String = Nothing, SF() As Double = Nothing
        If SapModel.LoadCases.StaticLinear.GetLoads(CaseName, NumberLoads, LoadType, LoadName, SF) <> 0 OrElse NumberLoads = 0 Then Return False
        For i = 0 To NumberLoads - 1
            If LoadType(i) = "Accel" Then
                If LoadName(i).ToUpperInvariant() = "UZ" Then Return False
            Else
                Dim PatternType As ETABSv1.eLoadPatternType
                If SapModel.LoadPatterns.GetLoadType(LoadName(i), PatternType) <> 0 OrElse
                   (PatternType <> ETABSv1.eLoadPatternType.Quake AndAlso PatternType <> ETABSv1.eLoadPatternType.Wind) Then Return False
            End If
        Next
        Return True
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

    '_______________________________________________________________________________________________
    'SkipUnusedCases: cases used by no strength / deflection design combo and no drift check are not run.
    'Prerequisites (initial and modal cases) of the used cases are kept; modal cases are always kept
    '(automatic seismic loads may use the modal periods). Unknown case types: nothing is switched off.
    Private ReadOnly DisabledCases As New List(Of String)

    Private Function SetRunCases() As Integer
        Dim N As Integer, Names() As String = Nothing, Run() As Boolean = Nothing
        Dim ret As Integer = SapModel.Analyze.GetRunCaseFlag(N, Names, Run)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :Analyze.GetRunCaseFlag") : Return ret : End If
        Dim AllCases As New HashSet(Of String)(Names.Take(N))
        Dim Needed As New HashSet(Of String)
        Dim Pending As New Stack(Of String)
        Dim AddCase = Sub(c As String)
                          If Not String.IsNullOrEmpty(c) AndAlso AllCases.Contains(c) AndAlso Needed.Add(c) Then Pending.Push(c)
                      End Sub
        Dim VisitedCombos As New HashSet(Of String)
        Dim AddCombo As Action(Of String) = Nothing
        AddCombo = Sub(Combo As String)
                       If Not VisitedCombos.Add(Combo) Then Return
                       Dim NI As Integer, CType_() As ETABSv1.eCNameType = Nothing, CName() As String = Nothing, SF() As Double = Nothing
                       If SapModel.RespCombo.GetCaseList(Combo, NI, CType_, CName, SF) <> 0 Then Return
                       For i = 0 To NI - 1
                           If CType_(i) = ETABSv1.eCNameType.LoadCombo Then AddCombo(CName(i)) Else AddCase(CName(i))
                       Next
                   End Sub
        For Each c In ComboNames.DesignSteelStrength.Concat(ComboNames.DesignSteelDeflection).Concat(DriftComboNames)
            AddCombo(c)
        Next
        For Each c In DriftCaseNames
            AddCase(c)
        Next
        For i = 0 To N - 1
            Dim CT As ETABSv1.eLoadCaseType, ST As Integer
            If Names(i).StartsWith("~") Then AddCase(Names(i))      'ETABS internal cases (e.g. ~LLRF live load reduction)
            If SapModel.LoadCases.GetTypeOAPI(Names(i), CT, ST) = 0 AndAlso CT = ETABSv1.eLoadCaseType.Modal Then AddCase(Names(i))
        Next
        While Pending.Count > 0
            Dim c As String = Pending.Pop()
            Dim CT As ETABSv1.eLoadCaseType, ST As Integer
            If SapModel.LoadCases.GetTypeOAPI(c, CT, ST) <> 0 Then CT = CType(-1, ETABSv1.eLoadCaseType)
            Dim Prev As String = Nothing
            Select Case CT
                Case ETABSv1.eLoadCaseType.LinearStatic
                    If SapModel.LoadCases.StaticLinear.GetInitialCase(c, Prev) = 0 Then AddCase(Prev)
                Case ETABSv1.eLoadCaseType.NonlinearStatic
                    If SapModel.LoadCases.StaticNonlinear.GetInitialCase(c, Prev) = 0 Then AddCase(Prev)
                    Prev = Nothing
                    If SapModel.LoadCases.StaticNonlinear.GetModalCase(c, Prev) = 0 Then AddCase(Prev)
                Case ETABSv1.eLoadCaseType.ResponseSpectrum
                    If SapModel.LoadCases.ResponseSpectrum.GetModalCase(c, Prev) = 0 Then AddCase(Prev)
                Case ETABSv1.eLoadCaseType.Modal
                    If SapModel.LoadCases.ModalEigen.GetInitialCase(c, Prev) = 0 Then
                        AddCase(Prev)
                    ElseIf SapModel.LoadCases.ModalRitz.GetInitialCase(c, Prev) = 0 Then
                        AddCase(Prev)
                    End If
                Case Else
                    Errorlogprint("Info: all analysis cases are run (case '" & c & "' of type " & CT.ToString() & " may depend on other cases)")
                    Return 0
            End Select
        End While
        For i = 0 To N - 1
            If Run(i) AndAlso Not Needed.Contains(Names(i)) Then
                ret = SapModel.Analyze.SetRunCaseFlag(Names(i), False)
                If (ret <> 0) Then : Errorlogprint("Problem occured on :Analyze.SetRunCaseFlag " & Names(i)) : Return ret : End If
                DisabledCases.Add(Names(i))
            End If
        Next
        RunCases = Nothing
        Errorlogprint("Info: analysis cases not run (not used by design / drift checks): [" & String.Join(", ", DisabledCases) & "]")
        Return 0
    End Function

    'Before the final analysis of the best design: the saved model contains all results
    Public Function RestoreRunCases() As Integer
        If DisabledCases.Count > 0 Then InvalidateAnalysis()
        RunCases = Nothing
        For Each c In DisabledCases
            Dim ret As Integer = SapModel.Analyze.SetRunCaseFlag(c, True)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :Analyze.SetRunCaseFlag " & c) : Return ret : End If
        Next
        DisabledCases.Clear()
        Return 0
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
        'form: "Column to Column" / "Beam to Column" unchecked
        If FormInfo.SkipCtoC Then GeoCons.CtoCList.Clear()
        If FormInfo.SkipBtoC Then GeoCons.BtoCList.Clear()
        Errorlogprint("Info: geometric constraints: " & GeoCons.CtoCList.Count & " column-column, " & GeoCons.BtoCList.Count & " beam-column")
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
        'only members of design variable groups: other steel members keep the section of the model
        Dim IsVariable = Function(c As FramePointStoryGroupStructures_.Frame_) c.FrameDesignProcedure = FramePointStoryGroupStructures_.DesignProcedure_.SteelFrameDesign AndAlso
                                                                              c.GroupName IsNot Nothing AndAlso VarIndex.ContainsKey(c.GroupName)
        Dim SteelBeams = Frames.Where(Function(c) IsVariable(c) AndAlso (c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.X OrElse c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Y))
        For Each SteelBeam In SteelBeams
            ret = SapModel.FrameObj.SetSection(SteelBeam.FrameName, BeamList, 0)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :FrameObj.SetSection " & BeamList) : Return ret : End If
        Next
        '_______________________________________________________________________________________________
        'Assign Auto Steel Column (composite columns too: the steel-only design gives the upper bound)
        Dim SteelColumns = Frames.Where(Function(c) IsVariable(c) AndAlso c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Z)
        For Each SteelColumn In SteelColumns
            ret = SapModel.FrameObj.SetSection(SteelColumn.FrameName, ColumnList, 0)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :FrameObj.SetSection " & ColumnList) : Return ret : End If
            ret = SapModel.FrameObj.SetDesignProcedure(SteelColumn.FrameName, FramePointStoryGroupStructures_.DesignProcedure_.SteelFrameDesign, ETABSv1.eItemType.Objects)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :FrameObj.SetDesignProcedure " & SteelColumn.FrameName) : Return ret : End If
        Next
        ForgetAssignedSections()
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
            If Ub(i) < 0 Then Ub(i) = 0
            If Lb(i) < 0 Then Lb(i) = 0
            If Lb(i) > Ub(i) Then Lb(i) = Ub(i)        'a very large design ratio shifts both bounds up
        Next i
        Return ret
    End Function

    'applyRepair = False: sections are only checked (no modification of the design variables)
    Public Sub Evaluate(ByRef Member As OptimizationStructure_.Member_, ByRef iter_ As Integer, ByRef ret As Integer, Optional ByVal applyRepair As Boolean = True)
        Dim repair As Boolean = applyRepair AndAlso Not FormInfo.CheckStructure
        Dim Sect_Ind() As Integer = Member.DesignVariables
        Iter = iter_
        'cache: key = design vector before repair; the final check / Check Structure always analyse
        Dim Key As String = If(FormInfo.UseCache AndAlso repair, String.Join(",", Sect_Ind), Nothing)
        Dim Hit As OptimizationStructure_.Member_ = Nothing
        If Key IsNot Nothing AndAlso Cache.TryGetValue(Key, Hit) Then
            Member.DesignVariables = CType(Hit.DesignVariables.Clone(), Integer())
            Member.CostValue = Hit.CostValue : Member.Penalty = Hit.Penalty : Member.PenalizedCost = Hit.PenalizedCost
            Clock("CacheHit")
            ret = 0
            Return
        End If
        Dim Total = Clock("Evaluate") : Total.Start()
        Try
            EvaluateCore(Member, Sect_Ind, ret, repair)
        Finally
            Total.Stop()
        End Try
        iter_ = Iter
        If Key IsNot Nothing AndAlso ret = 0 Then
            Dim Result As New OptimizationStructure_.Member_ With {.DesignVariables = CType(Member.DesignVariables.Clone(), Integer()),
                .CostValue = Member.CostValue, .Penalty = Member.Penalty, .PenalizedCost = Member.PenalizedCost}
            Cache(Key) = Result
            'a feasible result is final: its repaired vector gives the same result (metaheuristics often regenerate it)
            If Member.Penalty = 0 Then Cache(String.Join(",", Member.DesignVariables)) = Result
        End If
    End Sub

    Private Sub EvaluateCore(ByRef Member As OptimizationStructure_.Member_, ByVal Sect_Ind() As Integer, ByRef ret As Integer, ByVal repair As Boolean)
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
    End Sub

    'Analysis of Sect_Ind. Skipped when the model was already analysed with exactly these sections (a repair step that
    'did not change any variable): results and design of the last analysis are still valid.
    Public Function SetAndAnalyze(ByRef Sect_Ind() As Integer, ByVal applyGeometric As Boolean) As Integer
        If applyGeometric Then Call E1_Modifier_Geometric(Sect_Ind)
        If LastAnalysed IsNot Nothing AndAlso LastAnalysed.SequenceEqual(Sect_Ind) Then
            Clock("SkippedAnalysis")
            Return 0
        End If
        Dim ret As Integer = E2_SetSection(Sect_Ind)
        If ret <> 0 Then Return ret
        ret = E3_Analysis()
        If ret = 0 Then LastAnalysed = CType(Sect_Ind.Clone(), Integer())
        Return ret
    End Function

    'The model was changed outside SetAndAnalyze (auto select lists, run case flags, encased sections)
    Private Sub InvalidateAnalysis()
        LastAnalysed = Nothing
    End Sub

    'Section index of variable v within [Lb, Ub] that satisfies Fits and is closest to the current one (-1: none);
    'equal distances are decided by the seeded generator
    Private Function NearestFeasible(ByVal v As Integer, ByVal Current As Integer, ByVal Fits As Func(Of Integer, Boolean)) As Integer
        Dim Lo As Integer = If(Lb IsNot Nothing, Lb(v), 0), Hi As Integer = If(Ub IsNot Nothing, Ub(v), WSections.Count - 1)
        Dim Best As Integer = -1, BestDist As Integer = Integer.MaxValue
        For k = Lo To Hi
            If Not Fits(k) Then Continue For
            Dim d As Integer = Math.Abs(k - Current)
            If d < BestDist OrElse (d = BestDist AndAlso Rng.Next(2) = 0) Then Best = k : BestDist = d
        Next
        Return Best
    End Function

    'Geometric repair (array elements are modified in place): the smallest change that satisfies the constraint
    Private Sub E1_Modifier_Geometric(ByVal Sect_Ind() As Integer)
        For Each CtoC In GeoCons.CtoCList
            Dim GrNameDown As String = CtoC(1)
            Dim UpVar As Integer = VarIndex(CtoC(0))
            Dim DownVar As Integer = VarIndex(GrNameDown)
            Dim UpArea As Double = WSections(Sect_Ind(UpVar)).Area
            Dim UpDepth As Double = WSections(Sect_Ind(UpVar)).Depth
            If UpArea > WSections(Sect_Ind(DownVar)).Area OrElse UpDepth > WSections(Sect_Ind(DownVar)).Depth Then
                'lower column at least as large as the upper one, not larger than the column(s) below it
                Dim DownDownVars = GeoCons.CtoCList.Where(Function(c) c(0) = GrNameDown).Select(Function(c) VarIndex(c(1))).ToList()
                Dim DownDownDepth As Double = If(DownDownVars.Any(), DownDownVars.Min(Function(v) WSections(Sect_Ind(v)).Depth), Double.MaxValue)
                Dim DownDownArea As Double = If(DownDownVars.Any(), DownDownVars.Min(Function(v) WSections(Sect_Ind(v)).Area), Double.MaxValue)
                Dim k As Integer = NearestFeasible(DownVar, Sect_Ind(DownVar), Function(s) WSections(s).Area >= UpArea AndAlso WSections(s).Depth >= UpDepth AndAlso
                                                                                     WSections(s).Area <= DownDownArea AndAlso WSections(s).Depth <= DownDownDepth)
                If k >= 0 Then Sect_Ind(DownVar) = k
            End If
        Next

        For Each BtoC In GeoCons.BtoCList
            Dim ColVar As Integer = VarIndex(BtoC(0))
            Dim BeamVar As Integer = VarIndex(BtoC(1))
            Dim Gap As Double = ConnectionGap(WSections(Sect_Ind(ColVar)), BtoC(2))
            If WSections(Sect_Ind(BeamVar)).FlangeLength > Gap Then
                Dim k As Integer = NearestFeasible(BeamVar, Sect_Ind(BeamVar), Function(s) WSections(s).FlangeLength <= Gap)
                If k >= 0 Then Sect_Ind(BeamVar) = k
            End If
        Next
    End Sub

    Private Shared Function ConnectionGap(ByVal Column As SectionStructures_.STEEL_I_SECTION, ByVal ConType As String) As Double
        If ConType = "Depth" Then Return Column.Depth - 2 * Column.FlangeThickness
        Return Column.FlangeLength
    End Function

    Public Function E2_SetSection(ByRef Sect_Ind() As Integer) As Integer
        Dim c = Clock("SetSection") : c.Start()
        Try
            Return E2_SetSectionCore(Sect_Ind)
        Finally
            c.Stop()
        End Try
    End Function

    'Sections were assigned outside E2 (auto select lists): every group is assigned at the next E2
    Private Sub ForgetAssignedSections()
        ReDim Assigned(SteelFrameDesignGroupIDs.Count - 1)
        For i = 0 To Assigned.Length - 1
            Assigned(i) = -1
        Next
    End Sub

    Private Function E2_SetSectionCore(ByRef Sect_Ind() As Integer) As Integer
        Dim ret As Integer
        If SapModel.GetModelIsLocked = True Then
            ret = SapModel.SetModelIsLocked(False)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :Unlock model") : Return ret : End If
        End If
        If Assigned Is Nothing OrElse Assigned.Length <> SteelFrameDesignGroupIDs.Count Then ForgetAssignedSections()
        If CompositeActive Then
            'missing composite sections in one step (encased sections: one table import)
            Dim Needed As New List(Of Integer)
            For v = 0 To SteelFrameDesignGroupIDs.Count - 1
                If Groups(SteelFrameDesignGroupIDs(v)).IsComposite Then Needed.Add(Sect_Ind(v))
            Next
            ret = EnsureCompositeSections(Needed)
            If (ret <> 0) Then Return ret
        End If
        For i = 0 To SteelFrameDesignGroupIDs.Count - 1
            If Assigned(i) = Sect_Ind(i) Then Continue For
            Dim isec As Integer = SteelFrameDesignGroupIDs(i)
            Dim PropName As String = WSections(Sect_Ind(i)).SectionName
            If CompositeActive AndAlso Groups(isec).IsComposite Then PropName = CompositeSectionName(Sect_Ind(i))
            ret = SapModel.FrameObj.SetSection(Groups(isec).GroupName, PropName, ETABSv1.eItemType.Group)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :FrameObj.SetSection " & PropName) : Return ret : End If
            If CompositeActive AndAlso Groups(isec).IsComposite Then
                'General section: designed by CompositeColumn.vb, not by the ETABS steel design
                ret = SapModel.FrameObj.SetDesignProcedure(Groups(isec).GroupName, NO_DESIGN, ETABSv1.eItemType.Group)
                If (ret <> 0) Then : Errorlogprint("Problem occured on :FrameObj.SetDesignProcedure " & Groups(isec).GroupName) : Return ret : End If
            End If
            Assigned(i) = Sect_Ind(i)
        Next i
        Iter += 1
        Return ret
    End Function

    Public Function E3_Analysis() As Integer
        'no File.Save: the model was opened from WorkFile, so it has a file path (required by RunAnalysis)
        Dim c = Clock("Analysis") : c.Start()
        Dim ret As Integer = SapModel.Analyze.RunAnalysis
        c.Stop()
        If (ret <> 0) Then : Errorlogprint("Problem occured on :RunAnalysis") : Return ret : End If
        'cases that were set to run but did not finish (e.g. unstable / not converged nonlinear cases)
        Dim N1, N2 As Integer
        Dim Names1() As String = Nothing, Names2() As String = Nothing
        Dim Status() As Integer = Nothing
        Dim Run() As Boolean = Nothing
        ret = SapModel.Analyze.GetCaseStatus(N1, Names1, Status)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :Analyze.GetCaseStatus") : Return ret : End If
        If RunCases Is Nothing Then         'changes only with SetRunCases / RestoreRunCases
            ret = SapModel.Analyze.GetRunCaseFlag(N2, Names2, Run)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :Analyze.GetRunCaseFlag") : Return ret : End If
            RunCases = New HashSet(Of String)(Enumerable.Range(0, N2).Where(Function(k) Run(k)).Select(Function(k) Names2(k)))
        End If
        Dim Failed = Enumerable.Range(0, N1).Where(Function(k) RunCases.Contains(Names1(k)) AndAlso Status(k) <> 4).Select(Function(k) Names1(k)).ToList()
        AnalysisFailed = Failed.Count > 0
        If AnalysisFailed Then Errorlogprint("Warning: analysis not finished for [" & String.Join(", ", Failed) & "], design penalized")
        Return ret
    End Function

    Private Function G1_1_Design() As Integer
        Dim ret As Integer
        If SteelFrameDesignGroupIDs.Count > 0 Then
            Dim c = Clock("SteelDesign") : c.Start()
            ret = SapModel.DesignSteel.StartDesign
            c.Stop()
            If (ret <> 0) Then : Errorlogprint("Problem occured on :DesignSteel.StartDesign") : Return ret : End If
        End If
        Return ret
    End Function

    'All constraint values are computed on the final (repaired and re-analysed) model state
    Public Sub Penalty(ByRef Penalty As Double, ByRef Sect_Ind() As Integer, ByRef ret As Integer, Optional ByVal applyRepair As Boolean = True)
        Dim repair As Boolean = applyRepair AndAlso Not FormInfo.CheckStructure
        Penalty = 0
        If repair AndAlso FormInfo.RepairMode = MiscellaneousStructures.RepairMode_.Combined Then
            Call CombinedRepair(Sect_Ind, ret)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :CombinedRepair") : Exit Sub : End If
            If AnalysisFailed Then : Penalty = FAILED_PENALTY : Exit Sub : End If
        Else
            Call F_Evaluate_Drift(Sect_Ind, repair, ret)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :F_Evaluate_Drift") : Exit Sub : End If
            If AnalysisFailed Then : Penalty = FAILED_PENALTY : Exit Sub : End If

            Call G_Evaluate_PMM(Sect_Ind, repair, ret)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :G_Evaluate_PMM") : Exit Sub : End If
            If AnalysisFailed Then : Penalty = FAILED_PENALTY : Exit Sub : End If
        End If

        Call H_Evaluate_GeometricPenalty(Sect_Ind)

        Dim InterStoryDriftPenalty As Double = Math.Max(Stories.Max(Function(c) c.InterStoryDPenaltyX), Stories.Max(Function(c) c.InterStoryDPenaltyY))
        Dim TopStoryDriftPenalty As Double = Math.Max(Math.Max(TopDriftX, TopDriftY) / TopDriftLimit - 1, 0)
        Dim PMMPenalty As Double = 0
        If SteelFrameDesignGroupIDs.Count > 0 Then PMMPenalty = Math.Max(SteelFrameDesignGroupIDs.Max(Function(id) Groups(id).PMMRatio) - 1, 0)
        Dim GeometricPenalty As Double = MaxRatioPenalty(ETABS_print.ColumnToColumnGeometricRatio) + MaxRatioPenalty(ETABS_print.BeamToColumnGeometricRatio)
        Penalty = InterStoryDriftPenalty + TopStoryDriftPenalty + PMMPenalty + GeometricPenalty
    End Sub

    'RepairMode = Combined: constraints of one analysis -> all repair steps at once (largest step per variable)
    '-> one re-analysis -> constraints of the final state
    Private Sub CombinedRepair(ByRef Sect_Ind() As Integer, ByRef ret As Integer)
        ret = EvaluateConstraints()
        If ret <> 0 OrElse AnalysisFailed Then Exit Sub
        Dim Steps() As Integer = RepairSteps()
        Dim Before() As Integer = CType(Sect_Ind.Clone(), Integer())
        For v = 0 To Steps.Length - 1
            If Steps(v) <> 0 Then StepVariable(Sect_Ind, v, Steps(v))
        Next
        If Before.SequenceEqual(Sect_Ind) Then Exit Sub     'steps rounded to 0 or clipped at Ub: nothing to re-analyse
        Dim Analysed() As Integer = LastAnalysed
        ret = SetAndAnalyze(Sect_Ind, True)
        If ret <> 0 Then : Errorlogprint("Problem occured on :SetAndAnalyze (combined repair)") : Exit Sub : End If
        If LastAnalysed Is Analysed Then Exit Sub           'the geometric repair restored the analysed vector
        If AnalysisFailed Then Exit Sub
        ret = EvaluateConstraints()
    End Sub

    'Inter-story drift, top drift and PMM ratios of the current analysis
    Private Function EvaluateConstraints() As Integer
        Dim ret As Integer = F1_ConsInterStoryDrift()
        If (ret <> 0) Then : Errorlogprint("Problem occured on :F1_ConsInterStoryDrift") : Return ret : End If
        ret = F3_ConsTopStoryDrift()
        If (ret <> 0) Then : Errorlogprint("Problem occured on :F3_ConsTopStoryDrift") : Return ret : End If
        ret = G1_ConsPMM(False)
        If (ret <> 0) Then Errorlogprint("Problem occured on :G1_ConsPMM")
        Return ret
    End Function

    'Steps of F2 (story columns), F4 (all columns) and G2 (overstressed groups); largest step per variable
    Private Function RepairSteps() As Integer()
        Dim N As Integer = WSections.Count
        Dim Steps(SteelFrameDesignGroupIDs.Count - 1) As Integer
        For i = 0 To Stories.Length - 1
            Dim Ratio As Double = Math.Max(Stories(i).InterStoryDPenaltyX, Stories(i).InterStoryDPenaltyY) + 1
            If Ratio <= 1 Then Continue For
            Dim s As Integer = CInt(DRIFT_LOG_MULTIPLIER * Math.Log(Ratio) * N)
            For Each v In StoryColumnVars(i)
                Steps(v) = Math.Max(Steps(v), s)
            Next
        Next
        Dim Top As Double = Math.Max(TopDriftX, TopDriftY) / TopDriftLimit
        If Top > 1 Then
            Dim s As Integer = CInt(DRIFT_LOG_MULTIPLIER * Math.Log(Top) * N)
            For i = 0 To Stories.Length - 1
                For Each v In StoryColumnVars(i)
                    Steps(v) = Math.Max(Steps(v), s)
                Next
            Next
        End If
        For i = 0 To Steps.Length - 1
            Dim R As Double = Groups(SteelFrameDesignGroupIDs(i)).PMMRatio
            If R > 1 Then Steps(i) = Math.Max(Steps(i), CInt(PMM_LOG_MULTIPLIER * Math.Log(R) * N))
        Next
        Return Steps
    End Function

    'Governing constraint values of the current analysis (final report): ratio / limit, 1 = at the limit
    Public Function ConstraintSummary() As List(Of String)
        Dim F3 = Function(x As Double) x.ToString("F3", CultureInfo.InvariantCulture)
        Dim L As New List(Of String)
        Dim Inter As Double = Stories.Select(Function(st) Math.Max(st.InterStoryDriftX, st.InterStoryDriftY) / st.InterStoryDriftLimit).DefaultIfEmpty(0).Max()
        L.Add("inter-story drift / limit: " & F3(Inter))
        L.Add("top drift / limit: " & F3(Math.Max(TopDriftX, TopDriftY) / TopDriftLimit))
        Dim SteelIDs = SteelFrameDesignGroupIDs.Where(Function(id) Not Groups(id).IsComposite).ToList()
        If SteelIDs.Count > 0 Then L.Add("steel design ratio (max): " & F3(SteelIDs.Max(Function(id) Groups(id).PMMRatio)))
        Dim CompIDs = SteelFrameDesignGroupIDs.Where(Function(id) Groups(id).IsComposite).ToList()
        If CompIDs.Count > 0 Then
            L.Add("composite strength ratio (max): " & F3(CompIDs.Max(Function(id) Groups(id).CompositeStrength)))
            L.Add("composite detailing ratio (max): " & F3(CompIDs.Max(Function(id) Groups(id).CompositeDetailing)))
        End If
        L.Add("column-column geometric ratio (max): " & F3(If(ETABS_print.ColumnToColumnGeometricRatio, New List(Of Double)).DefaultIfEmpty(0).Max()))
        L.Add("beam-column geometric ratio (max): " & F3(If(ETABS_print.BeamToColumnGeometricRatio, New List(Of Double)).DefaultIfEmpty(0).Max()))
        Return L
    End Function

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
            ETABS_print.InterStoryDrifts = New List(Of List(Of Double))
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
                ETABS_print.InterStoryDrifts.Add(New List(Of Double)({DriftX, DriftY}))
            Next i
        Catch ex As Exception
            Errorlogprint("Problem occured on :F1_ConsInterStoryDrift " & ex.Message)
            ret = -1
        End Try
        Return ret
    End Function

    'One API call for all joints (group "All") instead of one call per joint
    Private Function F1_1_UpdateJointDisp() As Integer
        Dim c = Clock("JointDispl") : c.Start()
        Try
            Return F1_1_UpdateJointDispCore()
        Finally
            c.Stop()
        End Try
    End Function

    Private Function F1_1_UpdateJointDispCore() As Integer
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
                .U1 = New List(Of Double), .U2 = New List(Of Double)}
        Next i
        For k = 0 To NumberResults - 1
            Dim i As Integer
            If Not PointIndex.TryGetValue(Obj(k), i) Then Continue For
            With Points(i).PointDisp
                .U1.Add(U1(k)) : .U2.Add(U2(k))
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

    'F2 / F4 / G2 return True only if a design variable really changed (a step can round to 0 or be clipped at Ub)
    Private Function F2_Modifier_InterStoryDrift(ByRef Sect_Ind() As Integer) As Boolean
        Dim Before() As Integer = CType(Sect_Ind.Clone(), Integer())
        For i = 0 To Stories.Length - 1
            Dim Ratio As Double = Math.Max(Stories(i).InterStoryDPenaltyX, Stories(i).InterStoryDPenaltyY) + 1
            If Ratio <= 1 Then Continue For
            For Each v In StoryColumnVars(i)
                StepVariable(Sect_Ind, v, CInt(DRIFT_LOG_MULTIPLIER * Math.Log(Ratio) * WSections.Count))
            Next
        Next i
        Return Not Before.SequenceEqual(Sect_Ind)
    End Function

    Private Function F3_ConsTopStoryDrift() As Integer
        Dim ret As Integer = 0
        Try
            ETABS_print.TopStoryDrifts = New List(Of Double)
            Dim TopPoints = Points.Where(Function(c) Math.Abs(c.Zcoord - StructureHeight) < COORD_TOL AndAlso c.PointDisp.U1 IsNot Nothing AndAlso c.PointDisp.U1.Count > 0).ToList()
            If TopPoints.Count = 0 Then Throw New InvalidOperationException("No top points found at the structure height.")
            TopDriftX = TopPoints.Max(Function(p) p.PointDisp.U1.Max(Function(u) Math.Abs(u)))
            TopDriftY = TopPoints.Max(Function(p) p.PointDisp.U2.Max(Function(u) Math.Abs(u)))
            ETABS_print.TopStoryDrifts.Add(TopDriftX)
            ETABS_print.TopStoryDrifts.Add(TopDriftY)
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
        Dim Before() As Integer = CType(Sect_Ind.Clone(), Integer())
        For i = 0 To Stories.Length - 1
            For Each v In StoryColumnVars(i)
                StepVariable(Sect_Ind, v, StepSize)
            Next
        Next
        Return Not Before.SequenceEqual(Sect_Ind)
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
            'one call for the whole model (instead of one per group), results grouped by the design groups
            Dim NumberItems As Integer
            Dim FrameName As String() = Nothing
            Dim Ratio As Double() = Nothing
            Dim RatioType As Integer() = Nothing
            Dim Location As Double() = Nothing
            Dim ComboName As String() = Nothing
            Dim ErrorSummary As String() = Nothing
            Dim WarningSummary As String() = Nothing
            Dim ByGroup As New Dictionary(Of String, List(Of Integer))
            If SteelFrameDesignGroupIDs.Any(Function(id) Not (CompositeActive AndAlso Groups(id).IsComposite)) Then
                If SapModel.DesignSteel.GetSummaryResults("All", NumberItems, FrameName, Ratio, RatioType, Location, ComboName, ErrorSummary, WarningSummary, ETABSv1.eItemType.Group) <> 0 Then NumberItems = 0
                For j = 0 To NumberItems - 1
                    Dim F As Integer
                    If Not FrameIndex.TryGetValue(FrameName(j), F) OrElse Frames(F).GroupName Is Nothing Then Continue For
                    If Not ByGroup.ContainsKey(Frames(F).GroupName) Then ByGroup(Frames(F).GroupName) = New List(Of Integer)
                    ByGroup(Frames(F).GroupName).Add(j)
                Next
            End If
            ret = 0
            For i = 0 To SteelFrameDesignGroupIDs.Count - 1
                Dim ID As Integer = SteelFrameDesignGroupIDs(i)
                If CompositeActive AndAlso Groups(ID).IsComposite Then Continue For
                Dim Items As List(Of Integer) = Nothing
                If Not ByGroup.TryGetValue(Groups(ID).GroupName, Items) Then
                    Errorlogprint("Warning: no steel design results for group " & Groups(ID).GroupName)
                    If updateDesignSections Then Return -1
                    AnalysisFailed = True
                    Return 0
                End If
                Groups(ID).PMMRatio = Items.Max(Function(j) Ratio(j))
                Dim ErrorCount As Integer = Items.Where(Function(j) Not String.IsNullOrEmpty(ErrorSummary(j))).Count()
                If ErrorCount > 0 Then Groups(ID).PMMRatio += 1 + ErrorCount / Items.Count

                If updateDesignSections Then
                    'largest (by area) design section of the group
                    Dim BestName As String = Nothing
                    Dim BestArea As Double = Double.NegativeInfinity
                    For Each j In Items
                        Dim PropName As String = Nothing
                        ret = SapModel.DesignSteel.GetDesignSection(FrameName(j), PropName)
                        If (ret <> 0) Then : Errorlogprint("Problem occured on :DesignSteel.GetDesignSection") : Return ret : End If
                        Dim SectID As Integer = WSections.FindIndex(Function(c) c.SectionName = PropName)
                        Dim Area As Double = If(SectID >= 0, WSections(SectID).Area, 0)
                        If Area > BestArea Then : BestArea = Area : BestName = PropName : End If
                    Next
                    Groups(ID).DesignSecName = BestName
                    Groups(ID).DesignSecID = WSections.FindIndex(Function(c) c.SectionName = BestName)
                End If
            Next i
            If CompositeActive Then
                Dim c = Clock("CompositeCheck") : c.Start()
                ret = G1_2_ConsComposite()
                c.Stop()
                If (ret <> 0) Then : Errorlogprint("Problem occured on :G1_2_ConsComposite") : Return ret : End If
            End If
            'in design variable order (steel and composite groups)
            ETABS_print.GroupNames = SteelFrameDesignGroupIDs.Select(Function(id) Groups(id).GroupName).ToList()
            ETABS_print.PMM_Ratios = SteelFrameDesignGroupIDs.Select(Function(id) Groups(id).PMMRatio).ToList()
        Catch ex As Exception
            Errorlogprint("Problem occured on :G1_ConsPMM " & ex.Message)
            ret = -1
        End Try
        Return ret
    End Function

    '_______________________________________________________________________________________________
    'Encased composite columns: AISC 360-16 / 360-22 check with the frame forces of the strength combinations
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
            Dim SecID As Integer = If(Assigned IsNot Nothing AndAlso Assigned(v) >= 0, Assigned(v), CompositeSectionID(Groups(ID).GroupName))
            If SecID < 0 Then : Errorlogprint("Composite section of group " & Groups(ID).GroupName & " not found") : Return -1 : End If
            Dim Detailing As Double = Encased(SecID).DetailingRatio()
            Dim Strength As Double = 0
            For Each Block In Blocks.Values
                Dim ks = Block.OrderBy(Function(k) ObjSta(k)).ToList()
                Dim Check As CompositeMemberCheck = MemberCheck(SecID, Frames(FrameIndex(Obj(ks(0)))).FrameLenght)
                Dim kP As Integer = ks.OrderByDescending(Function(k) Math.Abs(P(k))).First()
                Dim PMM, Shear As Double
                Dim r As Double = Check.Ratio(P(kP), ks.Select(Function(k) M3(k)).ToArray(), ks.Select(Function(k) M2(k)).ToArray(),
                                              ks.Max(Function(k) Math.Abs(V2(k))), ks.Max(Function(k) Math.Abs(V3(k))), PMM, Shear,
                                              ks.Max(Function(k) Math.Abs(T(k))))
                Strength = Math.Max(Strength, r)
            Next
            Dim GroupRatio As Double = Math.Max(Detailing, Strength)
            Groups(ID).CompositeStrength = Strength
            Groups(ID).CompositeDetailing = Detailing
            Groups(ID).PMMRatio = GroupRatio
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
        Dim Before() As Integer = CType(Sect_Ind.Clone(), Integer())
        For i = 0 To SteelFrameDesignGroupIDs.Count - 1
            Dim Ratio As Double = Groups(SteelFrameDesignGroupIDs(i)).PMMRatio
            If Ratio > 1 Then StepVariable(Sect_Ind, i, CInt(PMM_LOG_MULTIPLIER * Math.Log(Ratio) * WSections.Count))
        Next i
        Return Not Before.SequenceEqual(Sect_Ind)
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
        Dim StructureWeight As Double = 0
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
        Dim filePath As String = EncasedSettings_.DefaultPath()
        Try
            CompositeSettings = If(File.Exists(filePath), EncasedSettings_.Load(filePath), New EncasedSettings_())
            'unit costs of the form (MainForm) replace those of the file; old backups have none
            If FormInfo.Costs.IsSet Then
                CompositeSettings.SteelUnitCost = FormInfo.Costs.Steel
                CompositeSettings.RebarUnitCost = FormInfo.Costs.Rebar
                CompositeSettings.ConcreteUnitCost = FormInfo.Costs.Concrete
                CompositeSettings.FormworkUnitCost = FormInfo.Costs.Formwork
            End If
            Errorlogprint("Info: unit costs (" & If(FormInfo.Costs.IsSet, "form", "EncasedSections.xml") & "): steel " & CompositeSettings.SteelUnitCost &
                          " /kN, rebar " & CompositeSettings.RebarUnitCost & " /kN, concrete " & CompositeSettings.ConcreteUnitCost &
                          " /m3, formwork " & CompositeSettings.FormworkUnitCost & " /m2")
            Return 0
        Catch ex As Exception
            Errorlogprint("Problem occurred while reading " & filePath & ": " & ex.Message)
            Return -1
        End Try
    End Function

    'Material properties from the ETABS model; strengths limited by AISC 360-16 / 360-22 I1.3
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
        'below 21 MPa the actual (lower) strength is used: raising it would be unconservative
        If M.fc < 0.021 Then Errorlogprint("Warning: f'c = " & M.fc * 1000 & " MPa is below 21 MPa (AISC I1.3 lower limit); actual value used")
        If M.fc > 0.069 Then : Errorlogprint("Warning: f'c = " & M.fc * 1000 & " MPa limited to 69 MPa for the strength (AISC I1.3)") : M.fc = 0.069 : End If
        If M.Fy > 0.525 Then : Errorlogprint("Warning: Fy limited to 525 MPa (AISC I1.3)") : M.Fy = 0.525 : End If
        If M.Fysr > 0.55 Then : Errorlogprint("Warning: Fysr limited to 550 MPa (AISC I1.3)") : M.Fysr = 0.55 : End If
        CompositeMat = M
        Errorlogprint("Info: composite columns (" & CompositeCodeName(FormInfo.CompositeCode) & ") in groups [" & String.Join(", ", Groups.Where(Function(c) c.IsComposite).Select(Function(c) c.GroupName)) &
                      "], Fy=" & M.Fy * 1000 & " fc=" & M.fc * 1000 & " Fysr=" & M.Fysr * 1000 & " MPa")
        Return ret
    End Function

    Public Shared Function CompositeCodeName(ByVal Code As CompositeCode_) As String
        Return If(Code = CompositeCode_.AISC360_22, "AISC 360-22", "AISC 360-16")
    End Function

    Public Function Encased(ByVal SecID As Integer) As EncasedIShape
        Dim S As EncasedIShape = Nothing
        If Not EncasedCache.TryGetValue(SecID, S) Then
            S = CompositeSettings.Build(WSections(SecID), CompositeMat)
            S.Code = FormInfo.CompositeCode
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

    '_______________________________________________________________________________________________
    'Composite sections in the ETABS model
    ' Search      : General section with transformed properties (CreateGeneralSection), checked by CompositeColumn.vb.
    ' Final/check : ETABS 20+ -> the General sections of the design are replaced (same names) by real encased sections
    '               (type EncasedRectangle + rebar) and checked by the ETABS composite column design
    '               (VerifyCompositeWithETABS). The OAPI has no setter for encased sections, so they are created through
    '               the database tables. Not used during the search: with 289 encased sections in the 525M model one
    '               analysis took 128 s instead of 10 s and a table import about 2 min.
    Private Const ENCASED_TABLE As String = "Frame Section Property Definitions - Conc Encasement Rectangle"
    Private Const COLUMN_REBAR_TABLE As String = "Frame Section Property Definitions - Concrete Column Reinforcing"
    Public UseEncasedSections As Boolean

    Private Function DetectEncasedSections() As Integer
        Dim N As Integer, Keys() As String = Nothing, Names() As String = Nothing, ImportType() As Integer = Nothing, IsEmpty() As Boolean = Nothing
        Dim ret As Integer = SapModel.DatabaseTables.GetAllTables(N, Keys, Names, ImportType, IsEmpty)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :DatabaseTables.GetAllTables") : Return ret : End If
        Dim Available As New HashSet(Of String)(If(Keys, New String() {}).Take(N))
        UseEncasedSections = Available.Contains(ENCASED_TABLE) AndAlso Available.Contains(COLUMN_REBAR_TABLE)
        Errorlogprint("Info: composite columns: General sections during the search, " &
                      If(UseEncasedSections, "final design checked with ETABS encased sections and the ETABS composite column design", "no ETABS composite check (needs ETABS 20+)"))
        Return 0
    End Function

    'Creates the General composite sections that do not exist yet
    Private Function EnsureCompositeSections(ByVal SecIDs As IEnumerable(Of Integer)) As Integer
        Dim Missing As List(Of Integer) = SecIDs.Distinct().Where(Function(id) Not CreatedSections.Contains(CompositeSectionName(id))).ToList()
        If Missing.Count = 0 Then Return 0
        Dim c = Clock("CreateSections") : c.Start()
        Try
            For Each id In Missing
                Dim ret As Integer = CreateGeneralSection(id)
                If ret <> 0 Then Return ret
            Next
            Return 0
        Finally
            c.Stop()
        End Try
    End Function

    'Rebar size of the model with the given diameter [mm]
    Private Function RebarSizeName(ByVal Diameter As Double, ByRef SizeName As String) As Integer
        Dim N As Integer, Names() As String = Nothing
        Dim ret As Integer = SapModel.PropRebar.GetNameList(N, Names)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :PropRebar.GetNameList") : Return ret : End If
        For i = 0 To N - 1
            Dim A, D As Double
            If SapModel.PropRebar.GetRebarProps(Names(i), A, D) = 0 AndAlso Math.Abs(D - Diameter) < 0.5 Then SizeName = Names(i) : Return 0
        Next
        Errorlogprint("No rebar size with diameter " & Diameter & " mm in the model (Define > Section Properties > Reinforcing Bar Sizes)")
        Return -1
    End Function

    Private Function CreateEncasedSections(ByVal Missing As List(Of Integer)) As Integer
        If Missing.Count = 0 Then Return 0
        Dim BarName As String = Nothing, TieName As String = Nothing
        Dim ret As Integer
        'table edits are ignored (without an error) while the model is locked after an analysis
        If SapModel.GetModelIsLocked() Then
            ret = SapModel.SetModelIsLocked(False)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :Unlock model") : Return ret : End If
        End If
        ret = RebarSizeName(CompositeSettings.RebarDiameter, BarName)
        If ret <> 0 Then Return ret
        ret = RebarSizeName(CompositeSettings.TieDiameter, TieName)
        If ret <> 0 Then Return ret
        Dim Fmt = Function(x As Double) x.ToString("R", CultureInfo.InvariantCulture)
        Dim SectionFields() As String = {"Name", "Material", "FromFile", "t3", "t2", "EmbedISect", "EncaseMat", "NotSizeType", "NotAutoFact", "Notes",
                                         "AMod", "A2Mod", "A3Mod", "JMod", "I2Mod", "I3Mod", "MMod", "WMod"}
        Dim RebarFields() As String = {"Name", "RebarMatL", "RebarMatC", "ReinfConfig", "IsDesigned", "Cover", "NumBars3Dir", "NumBars2Dir",
                                       "BarSizeLong", "BarSizeCorn", "BarSizeConf", "SpacingConf", "NumCBars3", "NumCBars2"}
        Dim SectionRows As New List(Of String), RebarRows As New List(Of String)
        For Each id In Missing
            Dim S As EncasedIShape = Encased(id)
            Dim Name As String = CompositeSectionName(id)
            Dim PerFace As Integer = (S.RebarPos.Count + 4) \ 4                          'perimeter layout: 4n - 4 bars
            Dim ClearCover As Double = CompositeSettings.RebarCover - CompositeSettings.TieDiameter - S.BarDiameter / 2
            SectionRows.AddRange({Name, STEEL_MATERIAL, "No", Fmt(S.H), Fmt(S.B), S.Steel.SectionName, CompositeSettings.ConcreteMaterial, "Auto", "1",
                                  "Encased " & S.Steel.SectionName & ", " & S.RebarPos.Count & "D" & S.BarDiameter,
                                  "1", "1", "1", "1", "1", "1", "1", "1"})
            RebarRows.AddRange({Name, CompositeSettings.RebarMaterial, CompositeSettings.RebarMaterial, "Rectangular", "No", Fmt(ClearCover),
                                PerFace.ToString(), PerFace.ToString(), BarName, BarName, TieName, Fmt(CompositeSettings.TieSpacing), "2", "2"})
        Next
        ret = SetTable(ENCASED_TABLE, SectionFields, Missing.Count, SectionRows.ToArray())
        If ret <> 0 Then Return ret
        ret = SetTable(COLUMN_REBAR_TABLE, RebarFields, Missing.Count, RebarRows.ToArray())
        If ret <> 0 Then Return ret
        Dim NFatal, NErr, NWarn, NInfo As Integer, ImportLog As String = Nothing
        ret = SapModel.DatabaseTables.ApplyEditedTables(True, NFatal, NErr, NWarn, NInfo, ImportLog)
        If ret <> 0 OrElse NFatal + NErr > 0 Then
            Errorlogprint("Problem occured on :DatabaseTables.ApplyEditedTables (encased sections), errors " & NFatal + NErr & Environment.NewLine & ImportLog)
            Return If(ret <> 0, ret, -1)
        End If
        For Each id In Missing
            Dim Name As String = CompositeSectionName(id)
            Dim PropType As ETABSv1.eFramePropType
            If SapModel.PropFrame.GetTypeOAPI(Name, PropType) <> 0 OrElse PropType <> ETABSv1.eFramePropType.EncasedRectangle Then
                Errorlogprint("Encased section " & Name & " was not created (warnings " & NWarn & ")" & Environment.NewLine & ImportLog)
                Return -1
            End If
            CreatedSections.Add(Name)
        Next
        Errorlogprint("Info: " & Missing.Count & " encased sections created (" & CompositeSectionName(Missing.First()) & " ...)")
        Return 0
    End Function

    'Adds / replaces records (by "Name") of a database table. The import replaces the whole table (records that are
    'not in the edited table are deleted from the model), so the existing records are written back as well.
    Private Function SetTable(ByVal Key As String, ByVal Fields() As String, ByVal NumberRecords As Integer, ByVal Data() As String) As Integer
        Dim Version As Integer, AllFields() As String = Nothing, N As Integer, Existing() As String = Nothing
        Dim ret As Integer = SapModel.DatabaseTables.GetTableForEditingArray(Key, "", Version, AllFields, N, Existing)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :DatabaseTables.GetTableForEditingArray " & Key) : Return ret : End If
        Dim NF As Integer = AllFields.Length
        Dim iName As Integer = Array.IndexOf(AllFields, "Name")
        Dim NewNames As New HashSet(Of String)(Enumerable.Range(0, NumberRecords).Select(Function(r) Data(r * Fields.Length)))
        Dim Rows As New List(Of String)
        Dim Count As Integer = 0
        For r = 0 To N - 1
            If iName >= 0 AndAlso NewNames.Contains(Existing(r * NF + iName)) Then Continue For
            For f = 0 To NF - 1
                Rows.Add(Existing(r * NF + f))
            Next
            Count += 1
        Next
        For r = 0 To NumberRecords - 1
            For f = 0 To NF - 1
                Dim k As Integer = Array.IndexOf(Fields, AllFields(f))
                Rows.Add(If(k >= 0, Data(r * Fields.Length + k), ""))
            Next
            Count += 1
        Next
        ret = SapModel.DatabaseTables.SetTableForEditingArray(Key, Version, AllFields, Count, Rows.ToArray())
        If (ret <> 0) Then Errorlogprint("Problem occured on :DatabaseTables.SetTableForEditingArray " & Key)
        Return ret
    End Function

    '_______________________________________________________________________________________________
    'ETABS composite column design (AISC 360-16 / 360-22) of the current analysis: verification of the final or
    'checked design. Results come from the database table, cDesignCompositeColumn.GetSummaryResults returns
    'shifted data in ETABS 22.6. Fills ETABS_print.ETABSCompositeCheck / ETABSCompositeRatios.
    Public Function VerifyCompositeWithETABS() As Integer
        ETABS_print.ETABSCompositeCheck = New List(Of String)
        ETABS_print.ETABSCompositeRatios = New List(Of Double)
        If Not (FormInfo.CompositeColumns AndAlso UseEncasedSections) OrElse Assigned Is Nothing Then Return 0
        Dim ret As Integer
        If Not Groups.Any(Function(g) g.IsComposite) Then Return 0
        InvalidateAnalysis()
        '1. the General sections of the current design -> encased sections with the same names
        Dim CompVars As List(Of Integer) = Enumerable.Range(0, SteelFrameDesignGroupIDs.Count).Where(Function(v) Groups(SteelFrameDesignGroupIDs(v)).IsComposite).ToList()
        Dim clkS = Clock("CreateSections") : clkS.Start()
        ret = CreateEncasedSections(CompVars.Select(Function(v) Assigned(v)).Distinct().ToList())
        clkS.Stop()
        If ret <> 0 Then Return ret
        '2. composite column design procedure (the search set "No Design"; re-assigning the section does not reset it)
        For Each v In CompVars
            Dim G As String = Groups(SteelFrameDesignGroupIDs(v)).GroupName
            ret = SapModel.FrameObj.SetSection(G, CompositeSectionName(Assigned(v)), ETABSv1.eItemType.Group)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :FrameObj.SetSection " & G) : Return ret : End If
            ret = SapModel.FrameObj.SetDesignProcedure(G, COMPOSITE_COLUMN_DESIGN, ETABSv1.eItemType.Group)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :FrameObj.SetDesignProcedure " & G) : Return ret : End If
        Next
        Dim Proc As Integer
        SapModel.FrameObj.GetDesignProcedure(Groups(SteelFrameDesignGroupIDs(CompVars(0))).GroupObjectNames(0), Proc)
        Errorlogprint("Info: composite columns replaced by ETABS encased sections (design procedure " & Proc & ")")
        '3. analysis of the encased model; internal check on the same analysis
        ret = E3_Analysis()
        If ret <> 0 Then Return ret
        If AnalysisFailed Then : Errorlogprint("Warning: analysis of the encased model not finished, no ETABS composite check") : Return 0 : End If
        ret = G1_ConsPMM(False)
        If ret <> 0 Then Return ret
        '4. ETABS composite column design
        Dim CodeName As String = CompositeCodeName(FormInfo.CompositeCode)
        ret = SapModel.DesignCompositeColumn.SetCode(CodeName)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :DesignCompositeColumn.SetCode " & CodeName) : Return ret : End If
        For Each c In ComboNames.DesignSteelStrength
            ret = SapModel.DesignCompositeColumn.SetComboStrength(c, True)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :DesignCompositeColumn.SetComboStrength " & c) : Return ret : End If
        Next
        Dim clk = Clock("CompositeDesignETABS") : clk.Start()
        ret = SapModel.DesignCompositeColumn.StartDesign()
        clk.Stop()
        If (ret <> 0) Then : Errorlogprint("Problem occured on :DesignCompositeColumn.StartDesign") : Return ret : End If
        Dim Key As String = "Composite Column Summary - " & CodeName
        Dim Version, N As Integer, Fields() As String = Nothing, Data() As String = Nothing
        ret = SapModel.DatabaseTables.GetTableForDisplayArray(Key, Nothing, "", Version, Fields, N, Data)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :DatabaseTables.GetTableForDisplayArray " & Key) : Return ret : End If
        Dim iName As Integer = Array.IndexOf(Fields, "UniqueName"), iPMM As Integer = Array.IndexOf(Fields, "PMMRatio")
        Dim iV2 As Integer = Array.IndexOf(Fields, "VMajRatio"), iV3 As Integer = Array.IndexOf(Fields, "VMinRatio"), iMsg As Integer = Array.IndexOf(Fields, "Message")
        If iName < 0 OrElse iPMM < 0 Then : Errorlogprint("Unexpected fields in " & Key & ": " & String.Join(", ", Fields)) : Return -1 : End If
        Dim Num = Function(s As String) As Double
                      Dim x As Double
                      Return If(Double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, x), x, 0)
                  End Function
        'per group: largest PMM and shear ratio, design messages
        Dim PMM As New Dictionary(Of String, Double), Shear As New Dictionary(Of String, Double), Messages As New Dictionary(Of String, HashSet(Of String))
        For r = 0 To N - 1
            Dim F As Integer = 0
            If Not FrameIndex.TryGetValue(Data(r * Fields.Length + iName), F) Then Continue For
            Dim G As String = Frames(F).GroupName
            If G Is Nothing Then Continue For
            PMM(G) = Math.Max(If(PMM.ContainsKey(G), PMM(G), 0), Num(Data(r * Fields.Length + iPMM)))
            Dim V As Double = Math.Max(If(iV2 >= 0, Num(Data(r * Fields.Length + iV2)), 0), If(iV3 >= 0, Num(Data(r * Fields.Length + iV3)), 0))
            Shear(G) = Math.Max(If(Shear.ContainsKey(G), Shear(G), 0), V)
            If Not Messages.ContainsKey(G) Then Messages(G) = New HashSet(Of String)
            Dim Msg As String = If(iMsg >= 0, Data(r * Fields.Length + iMsg), "")
            If Not String.IsNullOrWhiteSpace(Msg) AndAlso Msg <> "No Message" Then Messages(G).Add(Msg)
        Next
        For v = 0 To SteelFrameDesignGroupIDs.Count - 1
            Dim ID As Integer = SteelFrameDesignGroupIDs(v)
            If Not Groups(ID).IsComposite Then Continue For
            Dim G As String = Groups(ID).GroupName
            If Not PMM.ContainsKey(G) Then
                Errorlogprint("Warning: no ETABS composite design result for group " & G)
                Continue For
            End If
            Dim Line As String = G & ": ETABS PMM " & PMM(G).ToString("F3", CultureInfo.InvariantCulture) & ", shear " & Shear(G).ToString("F3", CultureInfo.InvariantCulture) &
                                 " | internal strength " & Groups(ID).CompositeStrength.ToString("F3", CultureInfo.InvariantCulture) &
                                 ", detailing " & Groups(ID).CompositeDetailing.ToString("F3", CultureInfo.InvariantCulture) &
                                 If(Messages(G).Count > 0, " | " & String.Join("; ", Messages(G)), "")
            ETABS_print.ETABSCompositeCheck.Add(Line)
            ETABS_print.ETABSCompositeRatios.Add(Math.Max(PMM(G), Shear(G)))
            Errorlogprint("Info: ETABS composite check (" & CodeName & ") " & Line)
        Next
        Return 0
    End Function

    'ETABS 19: General section with transformed properties (steel material, weight/mass by modifiers)
    Private Function CreateGeneralSection(ByVal SecID As Integer) As Integer
        Dim Name As String = CompositeSectionName(SecID)
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

    Public Sub Errorlogprint(ByVal msg As String)
        Dim Dir As String = Nothing
        If Not String.IsNullOrEmpty(FormInfo.FileList.ETABSFile) Then Dir = Path.GetDirectoryName(FormInfo.FileList.ETABSFile)
        If String.IsNullOrEmpty(Dir) Then Dir = AppDomain.CurrentDomain.BaseDirectory
        Try
            'Info: / Warning: lines as they are, everything else is an error
            Dim Line As String = If(msg.StartsWith("Info:") OrElse msg.StartsWith("Warning:"), msg, "Error: " & msg)
            File.AppendAllText(Path.Combine(Dir, "ErrorLog.txt"), Date.Now.ToString("yyyy-MM-dd HH:mm:ss") & " " & Line & Environment.NewLine)
        Catch
            'logging must never stop the optimization
        End Try
    End Sub
End Class

Public Class ETABS_Print
    'Check Structure output (<output>.check.xml) and last constraint values
    Public Penalty As Double
    Public Cost As Double
    Public AnalysisFailed As Boolean
    Public GroupNames As New List(Of String)            'design variable groups, order of PMM_Ratios
    Public PMM_Ratios As New List(Of Double)            'design ratio per group (composite: max(strength, detailing))
    Public InterStoryDrifts As New List(Of List(Of Double))     'per story {X, Y} [mm]
    Public TopStoryDrifts As New List(Of Double)                '{X, Y} [mm]
    Public BeamToColumnGeometricRatio As New List(Of Double)
    Public ColumnToColumnGeometricRatio As New List(Of Double)
    Public CompositeRatios As New List(Of Double)
    'ETABS composite column design of the final / checked design (ETABS 20+): max(PMM, shear) per composite group
    Public ETABSCompositeRatios As New List(Of Double)
    Public ETABSCompositeCheck As New List(Of String)
End Class
