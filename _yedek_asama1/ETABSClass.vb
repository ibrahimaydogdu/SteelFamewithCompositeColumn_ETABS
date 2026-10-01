Imports System.Xml
Imports System.IO
Imports System.Xml.Serialization
Imports System.Linq
Imports System.Security.Cryptography.X509Certificates
Imports DocumentFormat.OpenXml.Wordprocessing
Imports DocumentFormat.OpenXml.Drawing.Charts
Imports System.Configuration


Public Class ETABS_Class
    Private Const PMM_LOG_MULTIPLIER As Double = 0.5
    Private Const UPPER_BOUND_MULTIPLIER As Double = 0.23
    Private Const LOWER_BOUND_MULTIPLIER As Double = 0.23
    Private Const PMM_RATIO_OFFSET As Double = 0.01

    Public Frames() As FramePointStoryGroupStructures_.Frame_
    Public Points() As FramePointStoryGroupStructures_.Point_
    Public Stories() As FramePointStoryGroupStructures_.Story_
    Public Groups() As FramePointStoryGroupStructures_.Group_
    Public SteelFrameDesignGroupIDs As List(Of Integer)
    Public SapModel As ETABSv1.cSapModel
    Public ETABSObject As ETABSv1.cOAPI = Nothing
    Public WSections As IEnumerable(Of SectionStructures_.STEEL_I_SECTION)
    Public encasedSections As List(Of SectionStructures_.RectangularencasedISection_)
    Public FormInfo As MiscellaneousStructures.FormInfo_
    Public GeoCons As MiscellaneousStructures.GeoCons_
    Public ComboNames As Combinations_
    'Public ret As Integer
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


    Public Sub New(ByRef FormInfo_ As MiscellaneousStructures.FormInfo_, ByRef ret As Integer)
        ETABS_print = New ETABS_Print
        FormInfo = FormInfo_
        ret = Initilize()
    End Sub
    Public Sub Close(ret As Integer)
        FormInfo.TimerInfo.FinishTime = TimeOfDay.ToString("hh:mm:ss")   'Finish Time @ textbox 11
        Dim endDate As Date = Date.Now
        Dim timeSpan As TimeSpan = endDate.Subtract(FormInfo.TimerInfo.startDate)
        Dim timedmin As Integer = timeSpan.Minutes * 60
        Dim timedsec As Integer = timeSpan.Seconds
        Dim avtime As Double = 0 ' Initialize avtime to 0
        'avtime = Math.Round(((timedmin + timedsec) / iter), 2)
        FormInfo.TimerInfo.TotalTime = timeSpan.Days & "D:" & timeSpan.Hours & "H:" & timeSpan.Minutes & "M:" & timeSpan.Seconds & "S," & "Ave=" & avtime & "sec"

        ' Check if ETABSObject is not null before calling ApplicationExit
        'Close SAP2000
        ETABSObject?.ApplicationExit(False)

        'Clean up variables
        SapModel = Nothing
        ETABSObject = Nothing

        'Check ret value 
        If ret = 0 Then
            MsgBox("API script completed successfully.")
        Else
            MsgBox("API script FAILED to complete.")
        End If
    End Sub

    Protected Overrides Sub Finalize()
        Console.WriteLine("An Instance of class destroyed")
    End Sub

    Private Function Initilize() As Integer
        Dim ret As Integer
        ret = InitilizeETABS()
        If (ret <> 0) Then : Errorlogprint("Problem occured on Function: InitilizeSAP2000") : Return ret : End If
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
        '_____________________________________________________
        'Upper Lower boundary Def
        If FormInfo.CheckStructure = False Then ret = Initilize_UBLB()
        If (ret <> 0) Then : Errorlogprint("Problem occured on Function: Initilize_UBLB") : Return ret : End If
        '_____________________________________________________
        'Start Timer
        FormInfo.TimerInfo.startDate = Date.Now
        FormInfo.TimerInfo.StartTime = TimeOfDay.ToString("hh:mm:ss")        'Start Time @ textbox 10
        Return ret
    End Function

    Private Function InitilizeETABS() As Integer
        Dim ret As Integer
        Dim SapFileName As String = FormInfo.FileList.ETABSFile
        'set the following flag to true to attach to a running instance of the program 
        'otherwise a new instance of the program will be started Steel
        Dim AttachToInstance As Boolean = False

        ' Read paths from configuration
        Dim ProgramPath As String = Environment.GetEnvironmentVariable("ETABSProgramPath")
        SectionPropertyData = Environment.GetEnvironmentVariable("SectionPropertyDataPath")

        If AttachToInstance Then
            'attach to a running instance of ETABS 
            Try
                'get the active ETABS object
                ETABSObject = DirectCast(System.Runtime.InteropServices.Marshal.GetActiveObject("CSI.ETABS.API.ETABSObject"), ETABSv1.cOAPI)
            Catch ex As Exception
                Errorlogprint("No running instance of the program found or failed to attach.")
                ret = -1 : Return ret
            End Try
        Else
            'create a new instance of ETABS 
            Try
                'create API helper object 
                Dim myHelper As ETABSv1.cHelper
                myHelper = New ETABSv1.Helper

                'create ETABS object
                ETABSObject = myHelper.CreateObject(ProgramPath)
            Catch ex As Exception
                Errorlogprint("Cannot start a new instance of the program.")
                ret = -1 : Return ret
            End Try

            ' Check if ETABSObject is null
            If ETABSObject Is Nothing Then
                Errorlogprint("Failed to create ETABS object.")
                ret = -1 : Return ret
            End If

            'start ETABS application
            ret = ETABSObject.ApplicationStart()
            If (ret <> 0) Then : Errorlogprint("Problem occured on :ApplicationStart") : Return ret : Exit Function : End If
        End If

        'Get a reference to cSapModel to access all OAPI classes and functions 
        SapModel = ETABSObject.SapModel

        If FormInfo.HideETABS = True Then ret = ETABSObject.Hide                       'hide application
        If (ret <> 0) Then : Errorlogprint("Problem occured on :Hide model") : Return ret : Exit Function : End If

        ret = SapModel.File.OpenFile(SapFileName)     'open an existing file
        If (ret <> 0) Then : Errorlogprint("Problem occured on :OpenFile") : Return ret : Exit Function : End If

        Dim islocked As Boolean
        islocked = SapModel.GetModelIsLocked       'check if model is locked
        If islocked = True Then                    'Unlock model
            ret = SapModel.SetModelIsLocked(False)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :Unlock model") : Return ret : Exit Function : End If
        End If
        '_____________________________________________________
        'set present units to KN-m
        ret = SapModel.SetPresentUnits(ETABSv1.eUnits.kN_mm_C)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :SetPresentUnits") : Return ret : Exit Function : End If
        'assign material property weight per unit volume
        Dim m As Double
        ret = SapModel.PropMaterial.GetWeightAndMass("A992Fy50", A992Fy50Weight, m)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :PropMaterial.GetWeightAndMass") : Return ret : Exit Function : End If
        Return ret
    End Function

    Private Function InitilizePoints() As Integer
        Dim ret As Integer
        '_____________________________________________________
        'get Point object data
        Dim PNumber As Integer
        Dim PNames() As String = Nothing
        '_____________________________________________________
        'get point object names
        ret = SapModel.PointObj.GetNameList(PNumber, PNames)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :PointObj.GetNamelist") : Return ret : Exit Function : End If ':stop
        ReDim Points(PNumber - 1)
        ' Initialize each element in the Points array
        For i = 0 To PNumber - 1
            Points(i) = New FramePointStoryGroupStructures_.Point_ With {
                .PointName = PNames(i)
            }
            '_____________________________________________________
            'get point coordinates
            ret = SapModel.PointObj.GetCoordCartesian(Points(i).PointName, Points(i).Xcoord, Points(i).YCoord, Points(i).Zcoord)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :GetCoordCartesian") : Return ret : Exit Function : End If
        Next i
        Return ret
    End Function

    Private Function InitilizeFrames() As Integer
        Dim ret As Integer
        Dim FNumber As Integer
        Dim FNames() As String = Nothing
        '_____________________________________________________
        'get frame object names
        ret = SapModel.FrameObj.GetNameList(FNumber, FNames)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :GetNameList") : Return ret : Exit Function : End If
        ReDim Frames(FNumber - 1)
        For i = 0 To FNumber - 1
            Frames(i) = New FramePointStoryGroupStructures_.Frame_ With {
                .FrameName = FNames(i)
            }
            '_____________________________________________________
            'get names of points
            Dim FirstPoint As String = Nothing
            Dim SecondPoint As String = Nothing
            ret = SapModel.FrameObj.GetPoints(Frames(i).FrameName, FirstPoint, SecondPoint)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :FrameObj.GetPoints") : Return ret : Exit Function : End If

            ' Find indices of the points
            Dim ind1 As Integer = Points.ToList().FindIndex(Function(c) c.PointName = FirstPoint)
            Dim ind2 As Integer = Points.ToList().FindIndex(Function(c) c.PointName = SecondPoint)

            ' Check if indices are valid
            If ind1 = -1 Or ind2 = -1 Then
                Errorlogprint("Point not found for frame: " & Frames(i).FrameName)
                Return -1
            End If

            Frames(i).FirstPointName = Points(ind1).PointName
            Frames(i).SecondPointName = Points(ind2).PointName
            Dim err As Double = 0.0000001
            If Math.Abs(Points(ind1).Xcoord - Points(ind2).Xcoord) > err And Math.Abs(Points(ind1).YCoord - Points(ind2).YCoord) < err And Math.Abs(Points(ind1).Zcoord - Points(ind2).Zcoord) < err Then
                Frames(i).FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.X
            ElseIf Math.Abs(Points(ind1).Xcoord - Points(ind2).Xcoord) < err And Math.Abs(Points(ind1).YCoord - Points(ind2).YCoord) > err And Math.Abs(Points(ind1).Zcoord - Points(ind2).Zcoord) < err Then
                Frames(i).FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Y
            ElseIf Math.Abs(Points(ind1).Xcoord - Points(ind2).Xcoord) < err And Math.Abs(Points(ind1).YCoord - Points(ind2).YCoord) < err And Math.Abs(Points(ind1).Zcoord - Points(ind2).Zcoord) > err Then
                Frames(i).FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Z
            ElseIf Math.Abs(Points(ind1).Xcoord - Points(ind2).Xcoord) > err And Math.Abs(Points(ind1).YCoord - Points(ind2).YCoord) < err And Math.Abs(Points(ind1).Zcoord - Points(ind2).Zcoord) > err Then
                Frames(i).FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.DiagonalXZ
            ElseIf Math.Abs(Points(ind1).Xcoord - Points(ind2).Xcoord) < err And Math.Abs(Points(ind1).YCoord - Points(ind2).YCoord) > err And Math.Abs(Points(ind1).Zcoord - Points(ind2).Zcoord) > err Then
                Frames(i).FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.DiagonalYZ
            ElseIf Math.Abs(Points(ind1).Xcoord - Points(ind2).Xcoord) > err And Math.Abs(Points(ind1).YCoord - Points(ind2).YCoord) > err And Math.Abs(Points(ind1).Zcoord - Points(ind2).Zcoord) < err Then
                Frames(i).FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.DiagonalXY
            Else
                Errorlogprint("Check coordinates of the member: " & Frames(i).FrameName)
            End If
            Frames(i).FrameLenght = Math.Sqrt((Points(ind2).Xcoord - Points(ind1).Xcoord) ^ 2 + (Points(ind2).YCoord - Points(ind1).YCoord) ^ 2 + (Points(ind2).Zcoord - Points(ind1).Zcoord) ^ 2)
            '_____________________________________________________
            'get frame local axis angle
            Dim Advanced As Boolean = False
            ret = SapModel.FrameObj.GetLocalAxes(Frames(i).FrameName, Frames(i).LocalAxisAngle, Advanced)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :FrameObj.GetLocalAxes") : Return ret : Exit Function : End If
            '_____________________________________________________
            'get frame object groups
            Dim NumberGroups As Integer
            Dim Groups() As String = Nothing
            ret = SapModel.FrameObj.GetGroupAssign(Frames(i).FrameName, NumberGroups, Groups)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :GetGroupAssign") : Return ret : Exit Function : End If
            If NumberGroups = 1 Then
                Errorlogprint("No group definition, Check group of frame ID:" & Frames(i).FrameName)
            End If
            If NumberGroups > 2 Then
                Errorlogprint("More group definition than 1, Check group of frame ID:" & Frames(i).FrameName)
            End If
            For j = 0 To NumberGroups - 1
                If Groups(j) <> "All" Then
                    Frames(i).GroupName = Groups(j)
                End If
            Next j
            '_____________________________________________________
            'Get Frame design procedure
            ret = SapModel.FrameObj.GetDesignProcedure(Frames(i).FrameName, Frames(i).FrameDesignProcedure)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :GetDesignProcedure") : Return ret : Exit Function : End If
        Next i
        Return ret
    End Function
    Private Function InitilizeStories() As Integer
        Dim ret As Integer
        Dim SNumber As Integer
        Dim SNames() As String = Nothing
        Dim PNumber As Integer
        Dim PNames() As String = Nothing
        Dim FNumber As Integer
        Dim FNames() As String = Nothing
        '_____________________________________________________
        'get story names
        ret = SapModel.Story.GetNameList(SNumber, SNames)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :Story.GetNameList") : Return ret : Exit Function : End If
        ReDim Stories(SNumber - 1)
        For i = 0 To SNumber - 1
            Stories(i).StoryName = SNames(i)
            '_____________________________________________________
            'get point object names
            ret = SapModel.PointObj.GetNameListOnStory(Stories(i).StoryName, PNumber, PNames)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :PointObj.GetNameListonStory") : Return ret : Exit Function : End If
            ReDim Stories(i).StoryPointNames(PNumber - 1)

            For j = 0 To PNumber - 1
                Stories(i).StoryPointNames(j) = PNames(j)
            Next j
            Dim StoryName As String = Stories(i).StoryPointNames(0)
            Dim PointId As Integer = Points.ToList().FindIndex(Function(c) c.PointName = StoryName)
            Stories(i).StoryLevel = Points(PointId).Zcoord
            '_____________________________________________________
            'get frame object names on each story
            ret = SapModel.FrameObj.GetNameListOnStory(Stories(i).StoryName, FNumber, FNames)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :FrameObj.GetNameListonStory") : Return ret : Exit Function : End If
            ReDim Stories(i).StoryFrames(FNumber - 1)
            For j = 0 To FNumber - 1
                For k = 0 To Frames.Length - 1
                    If Frames(k).FrameName = FNames(j) Then
                        Stories(i).StoryFrames(j) = Frames(k)
                        Exit For
                    End If
                Next k
            Next j
            '_____________________________________________________
            'Calculate Inter-Story Drift Limits on each story
            Stories(i).InterStoryDriftLimit = Stories(i).StoryFrames.ToList().Where(Function(c) c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Z)(0).FrameLenght / FormInfo.FrameInfo.InterStoryDriftR
        Next i
        StructureHeight = Stories.ToList().Select(Function(c) c.StoryLevel).Max()
        TopDriftLimit = StructureHeight / FormInfo.FrameInfo.TopStoryDriftR
        Return ret
    End Function
    Private Function InitilizeGroups() As Integer
        Dim ret As Integer
        Dim FNumber As Integer
        Dim FNames() As String = Nothing
        Dim GNumber As Integer
        Dim GNames() As String = Nothing
        '_____________________________________________________
        'get Group names
        ret = SapModel.GroupDef.GetNameList(GNumber, GNames)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :GroupDef.GetNameList") : Return ret : Exit Function : End If
        ' Remove "All" from the list
        GNames = GNames.Where(Function(item) item <> "All").ToArray()
        GNumber -= 1
        ReDim Groups(GNumber - 1)

        SteelFrameDesignGroupIDs = New List(Of Integer)
        For i = 0 To GNumber - 1

            Groups(i).GroupName = GNames(i)
            Dim ObjectType() As Integer = Nothing
            '_____________________________________________________
            'get group assignments
            ret = SapModel.GroupDef.GetAssignments(Groups(i).GroupName, FNumber, ObjectType, FNames)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :GroupDef.GetAssignments") : Return ret : Exit Function : End If
            If FNumber = 0 Then : Errorlogprint("check group members of group:" & GNames(i)) : End If
            ReDim Groups(i).GroupObjectNames(FNumber - 1)
            ReDim Groups(i).GroupObjectTypes(FNumber - 1)

            Groups(i).GroupLength = 0
            For j = 0 To FNumber - 1
                Groups(i).GroupObjectNames(j) = FNames(j)
                Groups(i).GroupObjectTypes(j) = ObjectType(j)
            Next j
            For j = 1 To FNumber - 1
                If Groups(i).GroupObjectTypes(j) <> Groups(i).GroupObjectTypes(j - 1) Then
                    Errorlogprint("Different types of object please check group " & Groups(i).GroupName)
                    ret = -1
                    Return ret : Exit Function
                End If
            Next j

            For j = 0 To FNumber - 1
                If Groups(i).GroupObjectTypes(0) = FramePointStoryGroupStructures_.ObjectType_.Frame Then
                    Dim FName As String = FNames(j)
                    Dim GFrames As IEnumerable(Of FramePointStoryGroupStructures_.Frame_) = Frames.ToList().Where(Function(c) c.FrameName = FName)
                    Groups(i).GroupLength += GFrames.Sum(Function(c) c.FrameLenght)
                    If Groups(i).GroupDesignPocedure <> GFrames(0).FrameDesignProcedure And j > 0 Then
                        Errorlogprint("Check design procedure of member " & GFrames(0).FrameName & " of group: " & Groups(i).GroupName)
                        ret = -1
                        Return ret : Exit Function
                    End If
                    Groups(i).GroupDesignPocedure = GFrames(0).FrameDesignProcedure
                End If
            Next j

            If Groups(i).GroupObjectTypes(0) = FramePointStoryGroupStructures_.ObjectType_.Frame Then
                If Groups(i).GroupDesignPocedure = FramePointStoryGroupStructures_.DesignProcedure_.SteelFrameDesign Then
                    SteelFrameDesignGroupIDs.Add(i)
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
        If (ret <> 0) Then : Errorlogprint("Problem occured on :RespCombo.GetNameList") : Return ret : Exit Function : End If
        ComboNames.AllCombos = MyName.ToList()

        ret = SapModel.DesignSteel.GetComboStrength(NumberNames, MyName)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :DesignSteel.GetComboStrength") : Return ret : Exit Function : End If
        ComboNames.DesignSteelStrength = MyName.ToList()
        ret = SapModel.DesignSteel.GetComboDeflection(NumberNames, MyName)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :DesignSteel.GetComboDeflection") : Return ret : Exit Function : End If
        ComboNames.DesignSteelDeflection = MyName.ToList()

        '______________________________________________________
        'set combo selected for output
        For i = 0 To ComboNames.AllCombos.Count - 1
            ret = SapModel.Results.Setup.SetComboSelectedForOutput(ComboNames.AllCombos(i))
            If (ret <> 0) Then : Errorlogprint("Problem occured on :SetComboSelectedForOutput") : Return ret : Exit Function : End If
        Next i
        Return ret
    End Function

    Private Function InitilizeSections() As Integer
        Dim ret As Integer
        ret = InitilizeSections_ReadXML()
        If (ret <> 0) Then : Errorlogprint("Problem occured on :InitilizeSections_ReadXML") : Return ret : Exit Function : End If
        ret = Initilize_ReadXml_Encased()
        If ret <> 0 Then : Errorlogprint("Problem occured on: Initilize_ReadXml_Encased") : Return ret : Exit Function : End If
        Return ret
    End Function

    Private Function InitilizeSections_ReadXML() As Integer
        Try
            ' Define the XML serializer for the SectionStructures_.Steel_I_Section array
            Dim serializer As New XmlSerializer(GetType(SectionStructures_.STEEL_I_SECTION()))

            ' Read and deserialize the XML file
            Using fs As New FileStream(SectionPropertyData, FileMode.Open, FileAccess.Read)
                Dim sections As SectionStructures_.STEEL_I_SECTION() = CType(serializer.Deserialize(fs), SectionStructures_.STEEL_I_SECTION())

                ' Validate the deserialized data
                If sections Is Nothing OrElse sections.Length = 0 Then
                    Throw New InvalidDataException("The XML file does not contain valid section data.")
                End If

                ' Filter and order the sections
                WSections = sections.Where(Function(c) c.Designation = "W").OrderBy(Function(c) c.Area).ToList()

                ' Validate the filtered sections
                If Not WSections.Any() Then
                    Throw New InvalidDataException("No valid 'W' sections found in the XML file.")
                End If
            End Using

            Return 0 ' Indicate success
        Catch ex As Exception
            Errorlogprint("Problem occurred while reading and processing the XML file: " & ex.Message)
            Return -1 ' Indicate failure
        End Try
    End Function

    Public Function Initilize_ReadXml_Encased() As Integer
        Dim serializer As New XmlSerializer(GetType(List(Of SectionStructures_.RectangularencasedISection_)))
        Dim filePath As String = "EncasedSections.xml"

        Using reader As New StreamReader(filePath)
            encasedSections = CType(serializer.Deserialize(reader), List(Of SectionStructures_.RectangularencasedISection_))
        End Using
        ' Validate the deserialized data
        If encasedSections Is Nothing Then
            Throw New InvalidDataException("The XML file does not contain valid section data.")
        End If
        Return 0
    End Function
    Private Function InitilizeGeometricCons() As Integer
        Dim ret As Integer
        Dim StoryList As List(Of FramePointStoryGroupStructures_.Story_) = Stories.ToList().OrderByDescending(Function(c) c.StoryLevel).ToList()
        GeoCons.CtoCList = New HashSet(Of List(Of String))
        For i = 0 To StoryList.Count - 2
            Dim Upcolums As List(Of FramePointStoryGroupStructures_.Frame_) = StoryList(i).StoryFrames.Where(Function(c) c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Z).ToList()
            Dim DownColumns As List(Of FramePointStoryGroupStructures_.Frame_) = StoryList(i + 1).StoryFrames.Where(Function(c) c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Z).ToList()
            For j = 0 To Upcolums.Count - 1
                Dim upcolumn As FramePointStoryGroupStructures_.Frame_ = Upcolums(j)
                Dim downcolumn As FramePointStoryGroupStructures_.Frame_ = DownColumns(DownColumns.FindIndex(Function(c) c.FirstPointName = upcolumn.FirstPointName _
                                           Or c.SecondPointName = upcolumn.FirstPointName Or c.FirstPointName = upcolumn.SecondPointName Or c.SecondPointName = upcolumn.SecondPointName))
                If upcolumn.GroupName <> downcolumn.GroupName Then
                    Dim clist As New List(Of String) From {upcolumn.GroupName, downcolumn.GroupName}

                    Dim same As Boolean = False
                    For k = 0 To GeoCons.CtoCList.Count - 1
                        If GeoCons.CtoCList(k)(0) = clist(0) And GeoCons.CtoCList(k)(1) = clist(1) Then
                            same = True
                        End If
                    Next k
                    If same = False Then
                        GeoCons.CtoCList.Add(clist)
                    End If
                End If
            Next j
        Next i
        'GeoCons.CtoCList = GeoCons.CtoCList.Distinct().ToList()
        GeoCons.BtoCList = New HashSet(Of List(Of String))
        For i = 0 To StoryList.Count - 1
            Dim Level As Double = StoryList(i).StoryLevel
            Dim Columns As List(Of FramePointStoryGroupStructures_.Frame_) = StoryList(i).StoryFrames.Where(Function(c) c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Z _
                                                                                And c.FrameDesignProcedure = FramePointStoryGroupStructures_.DesignProcedure_.SteelFrameDesign).ToList()
            Dim BeamsX As List(Of FramePointStoryGroupStructures_.Frame_) = StoryList(i).StoryFrames.Where(Function(c) c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.X _
                                                                                And c.FrameDesignProcedure = FramePointStoryGroupStructures_.DesignProcedure_.SteelFrameDesign).ToList()
            Dim BeamsY As List(Of FramePointStoryGroupStructures_.Frame_) = StoryList(i).StoryFrames.Where(Function(c) c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Y _
                                                                                And c.FrameDesignProcedure = FramePointStoryGroupStructures_.DesignProcedure_.SteelFrameDesign).ToList()
            For Each column In Columns
                Dim point As FramePointStoryGroupStructures_.Point_ = Points.ToList().Where(Function(c) (c.PointName = column.FirstPointName Or c.PointName = column.SecondPointName) And c.Zcoord = Level)(0)
                Dim BeamX As FramePointStoryGroupStructures_.Frame_ = BeamsX(BeamsX.FindIndex(Function(c) _
                c.FirstPointName = point.PointName Or c.SecondPointName = point.PointName))
                If BeamX.FrameName <> Nothing Then
                    Dim ConnectionType As String = "Flange"
                    If column.LocalAxisAngle = 90 Or column.LocalAxisAngle = 270 Then
                        ConnectionType = "Depth"
                    End If
                    Dim Blist As New List(Of String) From {column.GroupName, BeamX.GroupName, ConnectionType}
                    Dim same As Boolean = False
                    For k = 0 To GeoCons.BtoCList.Count - 1
                        If GeoCons.BtoCList(k)(0) = Blist(0) And
                            GeoCons.BtoCList(k)(1) = Blist(1) And GeoCons.BtoCList(k)(2) = Blist(2) Then
                            same = True
                        End If
                    Next k
                    If same = False Then
                        GeoCons.BtoCList.Add(Blist)
                    End If
                End If
                Dim BeamY As FramePointStoryGroupStructures_.Frame_ = BeamsX(BeamsX.FindIndex(Function(c) _
                c.FirstPointName = point.PointName Or c.SecondPointName = point.PointName))
                If BeamY.FrameName <> Nothing Then
                    Dim ConnectionType As String = "Depth"
                    If column.LocalAxisAngle = 90 Or column.LocalAxisAngle = 270 Then
                        ConnectionType = "Flange"
                    End If
                    Dim Blist As New List(Of String) From {
                        column.GroupName,
                        BeamY.GroupName,
                        ConnectionType
                    }
                    Dim same As Boolean = False
                    For k = 0 To GeoCons.BtoCList.Count - 1
                        If GeoCons.BtoCList(k)(0) = Blist(0) And
                            GeoCons.BtoCList(k)(1) = Blist(1) And GeoCons.BtoCList(k)(2) = Blist(2) Then
                            same = True
                        End If
                    Next k
                    If same = False Then
                        GeoCons.BtoCList.Add(Blist)
                    End If
                End If
            Next
        Next i
        Return ret
    End Function
    Public Function Initilize_UBLB() As Integer
        Dim ret As Integer = 0
        '_______________________________________________________________________________________________
        'Assign Auto Steel Beam
        Dim SteelBeams As IEnumerable(Of FramePointStoryGroupStructures_.Frame_) = Frames.ToList().Where(Function(c) c.FrameDesignProcedure = FramePointStoryGroupStructures_.DesignProcedure_.SteelFrameDesign _
                                            And (c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.X Or c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Y))
        For Each SteelBeam In SteelBeams
            ret = SapModel.FrameObj.SetSection(SteelBeam.FrameName, "BeamSectionList", 0)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :FrameObj.SetSection") : Return ret : End If
        Next
        '_______________________________________________________________________________________________
        'Assign Auto Steel Column
        Dim SteelColumns As IEnumerable(Of FramePointStoryGroupStructures_.Frame_) = Frames.ToList().Where(Function(c) c.FrameDesignProcedure = FramePointStoryGroupStructures_.DesignProcedure_.SteelFrameDesign _
                                    And c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Z)
        For Each SteelColumn In SteelColumns
            ret = SapModel.FrameObj.SetSection(SteelColumn.FrameName, "ColumnSectionList", 1)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :FrameObj.SetSection") : Return ret : End If
        Next
        Call E3_Analysis(ret)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :E3_Analysis") : Return ret : End If

        ret = G1_ConsPMM()
        If (ret <> 0) Then : Errorlogprint("Problem occured on :G1_ConsPMM") : Return ret : End If

        Dim NofDesignVariables As Integer = SteelFrameDesignGroupIDs.Count
        ReDim Ub(NofDesignVariables - 1)
        ReDim Lb(NofDesignVariables - 1)
        Dim Sect_ind(NofDesignVariables - 1) As Integer
        For i = 0 To SteelFrameDesignGroupIDs.Count - 1
            Dim isec As Integer = SteelFrameDesignGroupIDs(i)
            Sect_ind(i) = Groups(isec).DesignSecID
        Next i

        ret = F1_ConsInterStoryDrift()
        If (ret <> 0) Then : Errorlogprint("Problem occured on :F1_ConsInterStoryDrift") : Return ret : End If

        Call F2_Modifier_InterStoryDrift(Sect_ind, ret)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :F2_Modifier_InterStoryDrift") : Return ret : End If

        ret = F3_ConsTopStoryDrift()
        If (ret <> 0) Then : Errorlogprint("Problem occured on :F3_ConsTopStoryDrift") : Return ret : End If

        Call F4_Modifier_TopStoryDrift(Sect_ind, ret)
        For i = 0 To SteelFrameDesignGroupIDs.Count - 1
            Dim isec As Integer = SteelFrameDesignGroupIDs(i)
            Ub(i) = Groups(isec).DesignSecID + CInt(Math.Log(Groups(isec).PMMRatio + PMM_RATIO_OFFSET) * 0.05 * (WSections.Count - 1) + UPPER_BOUND_MULTIPLIER * (WSections.Count - 1))
            Lb(i) = Groups(isec).DesignSecID + CInt(Math.Log(Groups(isec).PMMRatio + PMM_RATIO_OFFSET) * 0.05 * (WSections.Count - 1) - LOWER_BOUND_MULTIPLIER * (WSections.Count - 1))
            If Ub(i) > WSections.Count - 1 Then Ub(i) = WSections.Count - 1
            If Lb(i) < 0 Then Lb(i) = 0
        Next i
        Return ret
    End Function

    Public Sub Evaluate(ByRef Member As OptimizationStructure_.Member_, ByRef iter_ As Integer, ByRef ret As Integer)


        Dim Sect_Ind() As Integer = Member.DesignVariables
        Iter = iter_

        Call E_Set_Analysis(Sect_Ind, ret)
        If ret <> 0 Then : Errorlogprint("Problem occured on :E_Set_Analysis") : Exit Sub : End If

        Call Penalty(Member.Penalty, Sect_Ind, ret)
        If ret <> 0 Then : Errorlogprint("Problem occured on :Penalty") : Exit Sub : End If
        Member.CostValue = CostStProfile(Sect_Ind)
        Member.PenalizedCost = Member.CostValue * (1 + Member.Penalty) ^ 3
        iter_ = Iter
    End Sub

    Private Sub E_Set_Analysis(ByRef Sect_Ind() As Integer, ByRef ret As Integer)
        Call E1_Modifier_Geometric(Sect_Ind)
        Call E2_SetSection(Sect_Ind)
        Call E3_Analysis(ret)
    End Sub
    Private Sub E1_Modifier_Geometric(ByRef Sect_Ind() As Integer)
        Dim rand As New Random()

        For i = 0 To GeoCons.CtoCList.Count - 1
            Dim GrNameUp As String = GeoCons.CtoCList(i)(0)
            Dim GrNameDown As String = GeoCons.CtoCList(i)(1)
            Dim UpGroupId As Integer = Groups.ToList().FindIndex(Function(c) c.GroupName = GrNameUp)
            Dim DownGroupId As Integer = Groups.ToList().FindIndex(Function(c) c.GroupName = GrNameDown)
            If UpGroupId = -1 Or DownGroupId = -1 Then Continue For ' Skip if group not found

            Dim UpArea As Double = WSections(Sect_Ind(UpGroupId)).Area
            Dim DownArea As Double = WSections(Sect_Ind(DownGroupId)).Area
            Dim UpDepth As Double = WSections(Sect_Ind(UpGroupId)).Depth
            Dim DownDepth As Double = WSections(Sect_Ind(DownGroupId)).Depth
            Dim DownDownDepths As New List(Of Double)
            Dim DownDownAreas As New List(Of Double)

            For j = 0 To GeoCons.CtoCList.Count - 1
                If GrNameDown = GeoCons.CtoCList(j)(0) Then
                    Dim DownDownGroupName As String = GeoCons.CtoCList(j)(1)
                    Dim DownDownGroupID As Integer = Groups.ToList().FindIndex(Function(c) c.GroupName = DownDownGroupName)
                    If DownDownGroupID <> -1 Then
                        DownDownDepths.Add(WSections(Sect_Ind(DownDownGroupID)).Depth)
                        DownDownAreas.Add(WSections(Sect_Ind(DownDownGroupID)).Area)
                    End If
                End If
            Next j

            Dim DownDownDepth As Double = If(DownDownDepths.Any(), DownDownDepths.Min(), UpDepth)
            Dim DownDownArea As Double = If(DownDownAreas.Any(), DownDownAreas.Min(), UpArea)

            If UpArea > DownArea Or UpDepth > DownDepth Then
                Dim PosSections As IEnumerable(Of SectionStructures_.STEEL_I_SECTION) = WSections.ToList().Where(Function(c) (c.Area >= UpArea And c.Depth >= UpDepth) _
                                                                     And (c.Area <= DownDownArea And c.Depth <= DownDownDepth))
                If PosSections.Any() Then
                    Dim randi As Integer = rand.Next(PosSections.Count())
                    Dim section As SectionStructures_.STEEL_I_SECTION = PosSections.ElementAt(randi)
                    Dim sectionid As Integer = WSections.ToList().FindIndex(Function(c) c.SectionName = section.SectionName)
                    Sect_Ind(DownGroupId) = sectionid
                End If
            End If
        Next i

        For i = 0 To GeoCons.BtoCList.Count - 1
            Dim GrNameCol As String = GeoCons.BtoCList(i)(0)
            Dim GrNameBeam As String = GeoCons.BtoCList(i)(1)
            Dim ConType As String = GeoCons.BtoCList(i)(2)
            Dim ColGroupID As Integer = Groups.ToList().FindIndex(Function(c) c.GroupName = GrNameCol)
            Dim BeamGroupID As Integer = Groups.ToList().FindIndex(Function(c) c.GroupName = GrNameBeam)
            If ColGroupID = -1 Or BeamGroupID = -1 Then Continue For ' Skip if group not found

            Dim BeamFlange As Double = WSections(Sect_Ind(BeamGroupID)).FlangeLength
            Dim Gap As Double = If(ConType = "Depth", WSections(Sect_Ind(ColGroupID)).Depth - 2 * WSections(Sect_Ind(ColGroupID)).FlangeThickness, WSections(Sect_Ind(ColGroupID)).FlangeLength)

            If BeamFlange > Gap Then
                Dim PosSections As IEnumerable(Of SectionStructures_.STEEL_I_SECTION) = WSections.ToList().Where(Function(c) c.FlangeLength <= Gap)
                If PosSections.Any() Then
                    Dim randi As Integer = rand.Next(PosSections.Count())
                    Dim section As SectionStructures_.STEEL_I_SECTION = PosSections.ElementAt(randi)
                    Sect_Ind(BeamGroupID) = WSections.ToList().FindIndex(Function(c) c.SectionName = section.SectionName)
                    If Sect_Ind(BeamGroupID) = -1 Then Sect_Ind(BeamGroupID) = WSections.ToList().FindIndex(Function(c) c.SectionName = WSections.ToList().OrderBy(Function(d) d.FlangeLength)(0).SectionName)
                End If
            End If
        Next i
    End Sub
    Public Sub E2_SetSection(ByRef Sect_Ind() As Integer)
        Dim ret As Integer
        Call E1_Modifier_Geometric(Sect_Ind)
        '_______________________________________________________________________________________________
        'check if model is locked
        If SapModel.GetModelIsLocked = True Then                'Unlock model
            ret = SapModel.SetModelIsLocked(False)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :Unlock model") : Stop : Exit Sub : End If
        End If
        '_______________________________________________________________________________________________
        'set frame steel section property
        For i = 0 To SteelFrameDesignGroupIDs.Count - 1
            Dim id As Integer = i
            Dim isec As Integer = SteelFrameDesignGroupIDs(id)
            ret = SapModel.FrameObj.SetSection(Groups(isec).GroupName, WSections(Sect_Ind(id)).SectionName, ETABSv1.eItemType.Group)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :FrameObj.SetSection") : Stop : Exit Sub : End If
        Next i
        Iter += 1
    End Sub
    Public Sub E3_Analysis(ByRef ret As Integer)
        '_______________________________________________________________________________________________
        'run model
        ret = SapModel.File.Save(FormInfo.FileList.ETABSFile)
        If (ret <> 0) Then : Errorlogprint("Problem occured in :File.Save") : Exit Sub : End If
        ret = SapModel.Analyze.RunAnalysis
        If (ret <> 0) Then : Errorlogprint("Problem occured on :RunAnalysis") : Exit Sub : End If
    End Sub

    Private Function G1_1_Design() As Integer
        Dim ret As Integer
        '_______________________________________________________________________________________________
        'start Steel design
        If SteelFrameDesignGroupIDs.Count > 0 Then
            ret = SapModel.DesignSteel.SetCode(FormInfo.FrameInfo.SteelDesignCode)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :DesignSteel.SetCode") : Return ret : End If
            ret = SapModel.DesignSteel.StartDesign
            If (ret <> 0) Then : Errorlogprint("Problem occured on :DesignSteel.StartDesign") : Return ret : End If
        End If
        Return ret
    End Function




    Public Sub Penalty(ByRef Penalty As Double, ByRef Sect_Ind() As Integer, ByRef ret As Integer)
        Penalty = 0
        Call F_Evaluate_Drift(Sect_Ind, ret)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :F_Evaluate_Drift") : Exit Sub : End If
        Dim interstorydriftpenalty As Double = Math.Max(Stories.Max(Function(c) c.InterStoryDPenaltyX), Stories.Max(Function(c) c.InterStoryDPenaltyY))
        Dim TopStoryDriftPenalty As Double = Math.Max(If(TopDriftX > TopDriftLimit, TopDriftX / TopDriftLimit - 1, 0), If(TopDriftY > TopDriftLimit, TopDriftY / TopDriftLimit - 1, 0))

        Call G_Evaluate_PMM(Sect_Ind, ret)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :G_Evaluate_PMM") : Exit Sub : End If
        Dim Ratios As IEnumerable(Of Double) = Groups.ToList().Select(Function(c) c.PMMRatio)
        If Ratios.Any() AndAlso Ratios.Max() > 1 Then Penalty += Ratios.Max() - 1

        Call H_Evaluate_GeometricPenalty(Sect_Ind)
        If (ret <> 0) Then : Errorlogprint("Problem occured on :H_Evaluate_GeometricPenalty") : Exit Sub : End If
        Dim GeometricPenalty As Double = Math.Max(ETABS_print.ColumnToColumnGeometricRatio.Max - 1, 0) + Math.Max(ETABS_print.BeamToColumnGeometricRatio.Max - 1, 0)
        Penalty += interstorydriftpenalty + TopStoryDriftPenalty + GeometricPenalty
    End Sub

    Private Sub F_Evaluate_Drift(ByRef Sect_Ind() As Integer, ByRef ret As Integer)

        ret = F1_ConsInterStoryDrift()
        If (ret <> 0) Then : Errorlogprint("Problem occured on :F1_ConsInterStoryDrift") : Exit Sub : End If

        If FormInfo.CheckStructure = False Then
            F2_Modifier_InterStoryDrift(Sect_Ind, ret)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :F2_Modifier_InterStoryDrift") : Exit Sub : End If
        End If

        ret = F3_ConsTopStoryDrift()
        If (ret <> 0) Then : Errorlogprint("Problem occured on :F3_ConsTopStoryDrift") : Exit Sub : End If

        If FormInfo.CheckStructure = False Then
            F4_Modifier_TopStoryDrift(Sect_Ind, ret)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :F4_Modifier_TopStoryDrift") : Exit Sub : End If
        End If


    End Sub
    Private Function F1_ConsInterStoryDrift() As Integer
        Dim ret As Integer = 0
        ret = F1_1_UpdateJointDisp()
        If (ret <> 0) Then : Errorlogprint("Problem occured on :F1_1_UpdateJointDisp") : Return ret : End If
        Try
            For i = 0 To Stories.Count - 1
                Dim DriftX As New List(Of Double)
                Dim DriftY As New List(Of Double)
                For Each Frame In Stories(i).StoryFrames
                    Dim SecondPoint As FramePointStoryGroupStructures_.Point_ = Points.ToList().Where(Function(c) c.PointName = Frame.SecondPointName)(0)
                    Dim FirstPoint As FramePointStoryGroupStructures_.Point_ = Points.ToList().Where(Function(c) c.PointName = Frame.FirstPointName)(0)

                    For j = 0 To FirstPoint.PointDisp.U1.Count - 1
                        DriftX.Add(Math.Abs(FirstPoint.PointDisp.U1(j) - SecondPoint.PointDisp.U1(j)))
                        DriftY.Add(Math.Abs(FirstPoint.PointDisp.U2(j) - SecondPoint.PointDisp.U2(j)))
                    Next j
                Next
                Stories(i).InterStoryDriftX = DriftX.Max
                Stories(i).InterStoryDriftY = DriftY.Max
                If DriftX.Max > Stories(i).InterStoryDriftLimit Then
                    Stories(i).InterStoryDPenaltyX = DriftX.Max / Stories(i).InterStoryDriftLimit - 1
                Else
                    Stories(i).InterStoryDPenaltyX = 0
                End If
                If DriftY.Max > Stories(i).InterStoryDriftLimit Then
                    Stories(i).InterStoryDPenaltyY = DriftY.Max / Stories(i).InterStoryDriftLimit - 1
                Else
                    Stories(i).InterStoryDPenaltyY = 0
                End If
                Dim DriftList As New List(Of Double)({Stories(i).InterStoryDriftX, Stories(i).InterStoryDriftY})
                ETABS_print.InterStoryDrift_Ratios.Add(DriftList)
            Next i
            If ETABS_print.InterStoryDrift_Ratios Is Nothing Then
                ETABS_print.InterStoryDrift_Ratios = New List(Of List(Of Double))
            End If
            ETABS_print.InterStoryDrift_Ratios.Clear()
        Catch ex As Exception
            Errorlogprint("Problem occured on :F1_ConsInterStoryDrift" & ex.Message)
            ret = -1
        End Try
        Return ret
    End Function
    Private Function F1_1_UpdateJointDisp() As Integer
        Dim ret As Integer
        Dim NumberNames As Integer
        Dim MyName() As String = Nothing
        'get load case names
        ret = SapModel.LoadCases.GetNameList(NumberNames, MyName)
        If (ret <> 0) Then : Errorlogprint("SapModel.LoadCases.GetNameList") : Return ret : End If
        For i = 0 To NumberNames - 1
            ret = SapModel.Results.Setup.SetCaseSelectedForOutput(MyName(i))
        Next i

        For i = 0 To Points.Count - 1
            Dim NumberResults As Integer
            Dim ItemType As ETABSv1.eItemType = ETABSv1.eItemType.Objects
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

            ' Initialize PointDisp
            Points(i).PointDisp = New FramePointStoryGroupStructures_.LoadCaseDisp_

            '_________________________________________
            'get joint displacements
            ret = SapModel.Results.JointDispl(Points(i).PointName, ItemType, NumberResults, Obj, Elm, LoadCase, StepType, StepNum, U1, U2, U3, R1, R2, R3)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :Results.JointDispl") : Return ret : End If
            Points(i).PointDisp.LoadCaseName = LoadCase.ToList()
            Points(i).PointDisp.U1 = U1.ToList()
            Points(i).PointDisp.U2 = U2.ToList()
            Points(i).PointDisp.U3 = U3.ToList()
            Points(i).PointDisp.R1 = R1.ToList()
            Points(i).PointDisp.R2 = R2.ToList()
            Points(i).PointDisp.R3 = R3.ToList()
        Next i

        Return ret
    End Function
    Private Sub F2_Modifier_InterStoryDrift(ByRef Sect_Ind() As Integer, ByRef ret As Integer)
        Dim maxIterations As Integer = 1 ' Set a maximum number of iterations to prevent infinite loop
        Dim currentIteration As Integer = 0
        ret = 0
        Do
            Dim irun As Boolean = False
            For i = 0 To Stories.Count - 1
                If Math.Max(Stories(i).InterStoryDPenaltyX, Stories(i).InterStoryDPenaltyY) > 0 Then
                    irun = True
                    Dim Ratio As Double = Math.Max(Stories(i).InterStoryDPenaltyX, Stories(i).InterStoryDPenaltyY) + 1
                    Dim StoryColumnGroups As IEnumerable(Of String) = Stories(i).StoryFrames.ToList().Where(Function(c) c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Z).Select(Function(c) c.GroupName).Distinct()
                    For Each GroupName In StoryColumnGroups
                        Dim ID As Integer = Groups.ToList().FindIndex(Function(c) c.GroupName = GroupName)
                        Sect_Ind(ID) = Sect_Ind(ID) + CInt(0.5 * Math.Log(Ratio) * WSections.Count)
                        If Ub(ID) < Sect_Ind(ID) Then Sect_Ind(ID) = Ub(ID)
                        If Lb(ID) > Sect_Ind(ID) Then Sect_Ind(ID) = Lb(ID)
                    Next
                End If
            Next i

            If Not irun Then Exit Do
            Call E_Set_Analysis(Sect_Ind, ret)
            If ret <> 0 Then : Errorlogprint("Problem occured on :E_Set_Analysis") : Exit Sub : End If

            ret = F1_1_UpdateJointDisp()
            If ret <> 0 Then : Errorlogprint("Problem occured on :F1_1_UpdateJointDisp") : Exit Sub : End If

            currentIteration += 1
        Loop While currentIteration < maxIterations
    End Sub
    Private Function F3_ConsTopStoryDrift() As Integer
        Dim ret As Integer = 0
        Try
            If ETABS_print.TopStoryDrift_Ratio Is Nothing Then
                ETABS_print.TopStoryDrift_Ratio = New List(Of Double)
            End If
            ETABS_print.TopStoryDrift_Ratio.Clear()

            Dim TopPoints As IEnumerable(Of FramePointStoryGroupStructures_.Point_) = Points.ToList().Where(Function(c) c.Zcoord = StructureHeight)
            If Not TopPoints.Any() Then
                Throw New InvalidOperationException("No top points found at the structure height.")
            End If

            Dim U1Top(TopPoints.Count - 1) As Double
            Dim U2Top(TopPoints.Count - 1) As Double
            For i = 0 To TopPoints.Count - 1
                U1Top(i) = Math.Max(Math.Abs(TopPoints(i).PointDisp.U1.Max()), Math.Abs(TopPoints(i).PointDisp.U1.Min()))
                U2Top(i) = Math.Max(Math.Abs(TopPoints(i).PointDisp.U2.Max()), Math.Abs(TopPoints(i).PointDisp.U2.Min()))
            Next
            TopDriftX = U1Top.Max()
            TopDriftY = U2Top.Max()
            ETABS_print.TopStoryDrift_Ratio.Add(TopDriftX)
            ETABS_print.TopStoryDrift_Ratio.Add(TopDriftY)
        Catch ex As Exception
            Errorlogprint("Error in F3_ConsTopStoryDrift: " & ex.Message)
            ret = -1
        End Try
        Return ret
    End Function
    Private Sub F4_Modifier_TopStoryDrift(ByRef Sect_Ind() As Integer, ByRef ret As Integer)
        Dim Ratio As Double = TopDriftX / TopDriftLimit
        If Ratio > 1 Then
            For i = 0 To Stories.Count - 1
                Dim StoryColumnGroups As IEnumerable(Of String) = Stories(i).StoryFrames.ToList().Where(Function(c) c.FrameDirc = FramePointStoryGroupStructures_.FrameDirc_.Z).Select(Function(c) c.GroupName).Distinct()
                For Each GroupName In StoryColumnGroups
                    Dim ID As Integer = Groups.ToList().FindIndex(Function(c) c.GroupName = GroupName)
                    Sect_Ind(ID) = Sect_Ind(ID) + CInt(0.5 * Math.Log(Ratio) * WSections.Count)
                    If Ub(ID) < Sect_Ind(ID) Then Sect_Ind(ID) = Ub(ID)
                    If Lb(ID) > Sect_Ind(ID) Then Sect_Ind(ID) = Lb(ID)
                Next
            Next
            Call E_Set_Analysis(Sect_Ind, ret)
            If ret <> 0 Then : Errorlogprint("Problem occured on :E_Set_Analysis") : Exit Sub : End If

            ret = F1_1_UpdateJointDisp()
            If ret <> 0 Then : Errorlogprint("Problem occured on :F1_1_UpdateJointDisp") : Exit Sub : End If

        End If
    End Sub

    Private Sub G_Evaluate_PMM(ByRef Sect_Ind() As Integer, ByRef ret As Integer)

        ret = G1_ConsPMM()
        If ret <> 0 Then : Errorlogprint("Problem occured on :G1_ConsPMM") : Exit Sub : End If

        If FormInfo.CheckStructure = False Then
            G2_Modifier_PMM(Sect_Ind, ret)
            If (ret <> 0) Then : Errorlogprint("Problem occured on :G2_Modifier_PMM") : Exit Sub : End If
        End If

    End Sub

    Private Function G1_ConsPMM()
        Dim ret As Integer
        ret = G1_1_Design()
        If (ret <> 0) Then : Errorlogprint("Problem occured on :G1_1_Design") : Return ret : End If
        Try
            If ETABS_print.PMM_Ratios Is Nothing Then
                ETABS_print.PMM_Ratios = New List(Of Double)
            End If
            ETABS_print.PMM_Ratios.Clear()

            For i = 0 To SteelFrameDesignGroupIDs.Count - 1
                Dim ID As Integer = SteelFrameDesignGroupIDs(i)
                Dim NumberItems As Integer
                Dim FrameName As String() = Nothing
                Dim Ratio As Double() = Nothing
                Dim RatioType As Integer() = Nothing
                Dim Location As Double() = Nothing
                Dim ComboName As String() = Nothing
                Dim ErrorSummary As String() = Nothing
                Dim WarningSummary As String() = Nothing
                Dim ItemType As ETABSv1.eItemType = ETABSv1.eItemType.Group
                ret = SapModel.DesignSteel.GetSummaryResults(Groups(ID).GroupName, NumberItems, FrameName, Ratio, RatioType, Location, ComboName, ErrorSummary, WarningSummary, ItemType)
                If (ret <> 0) Then : Errorlogprint("Problem occured on :DesignSteel.GetSummaryResults") : Return ret : End If
                Groups(ID).PMMRatio = Ratio.Max()
                Dim ErrorMember = ErrorSummary.ToList().Where(Function(c) c <> "")
                If ErrorMember.Count > 0 Then Groups(ID).PMMRatio += 1 + ErrorMember.Count / NumberItems

                ETABS_print.PMM_Ratios.Add(Groups(ID).PMMRatio)
                Dim DSectionAreas As New List(Of Double)
                Dim PropName(NumberItems - 1) As String
                For j = 0 To NumberItems - 1
                    ret = SapModel.DesignSteel.GetDesignSection(FrameName(j), PropName(j))
                    If (ret <> 0) Then : Errorlogprint("Problem occured on :DesignSteel.GetDesignSection") : Return ret : End If
                    Dim PName As String = PropName(j)
                    Dim SectID As Integer = WSections.ToList().FindIndex(Function(c) c.SectionName = PName)
                    DSectionAreas.Add(WSections(SectID).Area)
                Next j
                Dim AreaMax = DSectionAreas.Max()
                Dim IDmax As Integer = DSectionAreas.FindIndex(Function(c) c = AreaMax)
                Groups(ID).DesignSecName = PropName(IDmax)
                Groups(ID).DesignSecID = WSections.ToList().FindIndex(Function(c) c.SectionName = PropName(IDmax))
            Next i
        Catch ex As Exception
            Errorlogprint("Problem occured on :G1_ConsPMM" & ex.Message)
            ret = -1
        End Try
        Return ret
    End Function


    Private Sub G2_Modifier_PMM(ByRef Sect_Ind() As Integer, ByRef ret As Integer)
        Dim maxIterations As Integer = 1 ' Set a maximum number of iterations to prevent infinite loop
        Dim currentIteration As Integer = 0

        Do
            For i = 0 To SteelFrameDesignGroupIDs.Count - 1
                Dim ID As Integer = SteelFrameDesignGroupIDs(i)
                If Groups(ID).PMMRatio > 1 Then Sect_Ind(ID) = Sect_Ind(ID) + CInt(PMM_LOG_MULTIPLIER * Math.Log(Groups(ID).PMMRatio) * WSections.Count) 'özel formül
                If Ub(ID) < Sect_Ind(ID) Then Sect_Ind(ID) = Ub(ID)
                If Lb(ID) > Sect_Ind(ID) Then Sect_Ind(ID) = Lb(ID)
            Next i

            Dim Ratios As IEnumerable(Of Double) = Groups.ToList().Select(Function(c) c.PMMRatio)
            If Ratios.Max() <= 1 Then Exit Do
            Call E_Set_Analysis(Sect_Ind, ret)
            If ret <> 0 Then : Errorlogprint("Problem occured on :E_Set_Analysis") : Exit Sub : End If

            Call F_Evaluate_Drift(Sect_Ind, ret)
            If ret <> 0 Then : Errorlogprint("Problem occured on :F_Evaluate_Drift") : Exit Sub : End If

            ret = G1_ConsPMM()
            If ret <> 0 Then : Errorlogprint("Problem occured on :G1_ConsPMM") : Exit Sub : End If
            currentIteration += 1
        Loop While currentIteration < maxIterations
    End Sub



    Public Sub H_Evaluate_GeometricPenalty(ByRef Sect_Ind() As Integer)
        If GeoCons.CtoCList.Count > 0 Then
            Dim CtoCPenalty(GeoCons.CtoCList.Count - 1) As Double
            If ETABS_print.ColumnToColumnGeometricRatio Is Nothing Then
                ETABS_print.ColumnToColumnGeometricRatio = New List(Of Double)
            End If
            ETABS_print.ColumnToColumnGeometricRatio.Clear()

            For i = 0 To GeoCons.CtoCList.Count - 1
                Dim GrNameUp As String = GeoCons.CtoCList(i)(0)
                Dim GrNameDown As String = GeoCons.CtoCList(i)(1)
                Dim UpGroupId As Integer = Groups.ToList().FindIndex(Function(c) c.GroupName = GrNameUp)
                Dim DownGroupId As Integer = Groups.ToList().FindIndex(Function(c) c.GroupName = GrNameDown)
                Dim UpArea As Double = WSections(Sect_Ind(UpGroupId)).Area
                Dim DownArea As Double = WSections(Sect_Ind(DownGroupId)).Area
                Dim UpDepth As Double = WSections(Sect_Ind(UpGroupId)).Depth
                Dim DownDepth As Double = WSections(Sect_Ind(DownGroupId)).Depth
                If UpArea > DownArea Or UpDepth > DownDepth Then CtoCPenalty(i) = Math.Max(UpArea / DownArea - 1, UpDepth / DownDepth - 1)
                ETABS_print.ColumnToColumnGeometricRatio.Add(CtoCPenalty(i) + 1)
            Next i

        End If
        If GeoCons.BtoCList.Count > 1 Then
            Dim BtoCPenalty(GeoCons.BtoCList.Count - 1) As Double
            If ETABS_print.BeamToColumnGeometricRatio Is Nothing Then
                ETABS_print.BeamToColumnGeometricRatio = New List(Of Double)
            End If
            ETABS_print.BeamToColumnGeometricRatio.Clear()

            For i = 0 To GeoCons.BtoCList.Count - 1
                Dim GrNameCol As String = GeoCons.BtoCList(i)(0)
                Dim GrNameBeam As String = GeoCons.BtoCList(i)(1)
                Dim ConType As String = GeoCons.BtoCList(i)(2)
                Dim ColGroupID As Integer = Groups.ToList().FindIndex(Function(c) c.GroupName = GrNameCol)
                Dim BeamGroupID As Integer = Groups.ToList().FindIndex(Function(c) c.GroupName = GrNameBeam)
                Dim BeamFlange As Double = WSections(Sect_Ind(BeamGroupID)).FlangeLength
                Dim Gap As Double = -1
                If ConType = "Depth" Then
                    Gap = WSections(Sect_Ind(ColGroupID)).Depth - 2 * WSections(Sect_Ind(ColGroupID)).FlangeThickness
                ElseIf ConType = "Flange" Then
                    Gap = WSections(Sect_Ind(ColGroupID)).FlangeLength
                End If
                If BeamFlange > Gap Then BtoCPenalty(i) = BeamFlange / Gap - 1
                ETABS_print.BeamToColumnGeometricRatio.Add(BeamFlange / Gap)
            Next i
        End If
    End Sub



    Public Function CostStProfile(ByRef Sect_Ind() As Integer) As Double
        StructureWeight = 0
        For i = 0 To Groups.Count - 1
            StructureWeight += Groups(i).GroupLength * WSections(Sect_Ind(i)).Area * A992Fy50Weight
        Next
        Return StructureWeight
    End Function

    Public Function CostSlab() As Double
        Return 0
    End Function

    Public Function CostStud() As Double
        Return 0
    End Function


    Public Function Errorlogprint(ByVal msg As String) As Object
        Dim sw As New StreamWriter(Path.GetDirectoryName(FormInfo.FileList.ETABSFile) & "\ErrorLog.txt", True)
        ' Write the error message to the file
        sw.WriteLine("Error message: " & msg)

        ' Close the StreamWriter object
        sw.Close()
        Return Nothing
    End Function
End Class

Public Class ETABS_Print
    Public PMM_Ratios As New List(Of Double)
    Public InterStoryDrift_Ratios As New List(Of List(Of Double))
    Public TopStoryDrift_Ratio As New List(Of Double)
    Public BeamToColumnGeometricRatio As New List(Of Double)
    Public ColumnToColumnGeometricRatio As New List(Of Double)
End Class
