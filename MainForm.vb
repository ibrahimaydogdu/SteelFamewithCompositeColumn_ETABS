Imports System.Xml
Imports System.IO
Imports System.Xml.Serialization
Public Class MainForm
    Public FormInfo As MiscellaneousStructures.FormInfo_
    Public SAP2000Class As ETABS_Class
    Public OptClass As OptimizationClass
    Public ID_mem As Integer

    Private Sub MainForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        If DriftCombos.SelectedIndex < 0 Then DriftCombos.SelectedIndex = 0
    End Sub

    'VB Rnd: Rnd(-1) followed by Randomize(seed) gives a repeatable sequence for the seed
    Private Sub SetRandomSeed(ByVal Seed As Integer)
        Rnd(-1)
        Randomize(Seed)
        ETABS_Class.SetSeed(Seed)
    End Sub
    Private Sub Start_Click(sender As Object, e As EventArgs) Handles start.Click
        Dim ret As Integer = 0
        If CheckStructure.Checked = True Then
            Check_Structure(ret)
            If ret <> 0 Then
                LogError("Error occurend in Check Structure")
                CloseETABS(ret)
            End If
            Exit Sub
        End If
        Init(ret)
        If ret <> 0 Then
            LogError("Init")
            CloseETABS(ret)
            Exit Sub
        End If
        Me.Text = FormInfo.OptInfo.OptimizationMethod.ToString() & "  (seed " & FormInfo.Seed & ")"
        If SAP2000Class IsNot Nothing Then SAP2000Class.Errorlogprint("Info: run started, method " & FormInfo.OptInfo.OptimizationMethod.ToString() & ", seed " & FormInfo.Seed)
        Do While OptClass.iter < FormInfo.OptInfo.MaxFuncEvaluation
            OptClass.ILoop += 1
            OptClass.Memory = OptClass.Memory.OrderBy(Function(c) c.PenalizedCost).ToList()
            For Imem = 0 To FormInfo.OptInfo.MemorySize - 1
                ID_mem = Imem
                Opt_Main(ret)
                If ret <> 0 Then
                    LogError("Error occurend in Opt_Main")
                    CloseETABS(ret)
                    Exit Sub
                End If
            Next Imem

            If FormInfo.OptInfo.ClearDuplicates Then
                OptClass.ClearDuplicates(ret)
                If ret <> 0 Then
                    LogError("Error occurend in Clear Duplicates")
                    CloseETABS(ret)
                    Exit Sub
                End If
            End If
            OptClass.Backup_Write()
        Loop
        OptClass.Opt_Finalize()
        FinishTimeBox.Text = TimeOfDay.ToString("hh:mm:ss")
    End Sub
    Private Sub Opt_Main(ByRef ret As Integer)
        OptClass.Main(ID_mem, ret)
        Write_form()
    End Sub

    'ETABS class may not exist yet (validation error, math test mode)
    Private Sub LogError(ByVal msg As String)
        If SAP2000Class IsNot Nothing Then
            SAP2000Class.Errorlogprint(msg)
        Else
            MsgBox(msg)
        End If
    End Sub
    Private Sub CloseETABS(ByVal ret As Integer)
        If SAP2000Class IsNot Nothing Then SAP2000Class.Close(ret)
    End Sub

    Private Sub LoadSAP2000file_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles loadSAP2000file.Click
        Using openFileDialog1 As New Windows.Forms.OpenFileDialog With {
            .Filter = "ETABS File|*.EDB",
            .Title = "Select ETABS file"
        }
            If openFileDialog1.ShowDialog() = DialogResult.OK Then saplocation.Text = openFileDialog1.FileName
        End Using
    End Sub
    Private Sub Loadoutput_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles loadoutput.Click
        Using saveFileDialog1 As New Windows.Forms.SaveFileDialog With {
            .Filter = "xml Files|*.xml",
            .Title = "Output Dosyasını Giriniz"
        }
            If saveFileDialog1.ShowDialog() = DialogResult.OK Then OutputLoc.Text = saveFileDialog1.FileName
        End Using
    End Sub

    Private Shared Function IsValidNumber(ByVal text As String) As Boolean
        Return Not String.IsNullOrWhiteSpace(text) AndAlso IsNumeric(text)
    End Function
    Private Shared Function ToDbl(ByVal text As String) As Double
        Return If(IsValidNumber(text), CDbl(text), 0)
    End Function

    'Returns True if the form input is not valid
    Private Function Control() As Boolean
        Dim Durdur As Boolean = False
        '_______________________________________________________________________________________________
        'Control Input Output file locations
        If My.Computer.FileSystem.FileExists(saplocation.Text) = False And TestwithMath.Checked = False Then
            MessageBox.Show("ETABS File Not Found: " & saplocation.Text)
            Durdur = True
        End If
        Dim OutDir As String = Nothing
        Try
            OutDir = Path.GetDirectoryName(OutputLoc.Text)
        Catch
        End Try
        If String.IsNullOrEmpty(OutDir) OrElse My.Computer.FileSystem.DirectoryExists(OutDir) = False Then
            MessageBox.Show("Output File Not Found: " & OutputLoc.Text)
            Durdur = True
        End If
        '______________________________________________________________________________________________
        'Control Frame Parameters
        If TestwithMath.Checked = False Then
            If Not IsValidNumber(displimit.Text) Then
                MsgBox("Displacement limit is not defined correctly")
                Durdur = True
            End If
            If Not IsValidNumber(TS_LimitR.Text) Then
                MsgBox("Top Story limit is not defined correctly")
                Durdur = True
            End If
            If Not IsValidNumber(IS_LimitR.Text) Then
                MsgBox("Inter Story limit is not defined correctly")
                Durdur = True
            End If
            If String.IsNullOrWhiteSpace(Dcode_Steel.Text) Then
                MsgBox("Design Code Steel is not defined correctly")
                Durdur = True
            End If
        End If
        '___________________________________________________________________________________________
        'Control Optimization Parameters
        If Not IsValidNumber(MemSize.Text) Then
            MsgBox("Optimization Info Memory size is not defined correctly")
            Durdur = True
        End If
        If Not IsValidNumber(SeedBox.Text) OrElse CDbl(SeedBox.Text) < 0 OrElse CDbl(SeedBox.Text) <> Math.Floor(CDbl(SeedBox.Text)) Then
            MsgBox("Random seed must be a non-negative integer (0 = time based)")
            Durdur = True
        End If
        If Not IsValidNumber(maxiter.Text) Then
            MsgBox("Optimization Info MaxIteration is not defined correctly")
            Durdur = True
        End If
        If Opt_method.SelectedIndex = 0 Then 'Harmony Search
            If Not IsValidNumber(PAR_Val.Text) Then
                MsgBox("Harmony Search Info the PAR is not defined correctly")
                Durdur = True
            End If
            If Not IsValidNumber(HMCR_val.Text) Then
                MsgBox("Harmony Search Info the HMCR is is not defined correctly")
                Durdur = True
            End If
        ElseIf Opt_method.SelectedIndex = 1 Then 'Biogeoraphy-Based
            If Not IsValidNumber(Mutation_Rate.Text) Then
                MsgBox("Biogeography-based Info the Mutation rate is is not defined correctly")
                Durdur = True
            End If
        End If
        Return Durdur
    End Function
    Private Sub FormInfo_Read()
        FormInfo = New MiscellaneousStructures.FormInfo_
        FormInfo.FileList.ETABSFile = saplocation.Text
        FormInfo.FileList.OutputFile = OutputLoc.Text
        FormInfo.BackUp = BackUp.Checked
        FormInfo.HideETABS = HideSAP2000.Checked
        FormInfo.CheckStructure = CheckStructure.Checked
        FormInfo.CompositeColumns = CompositeColumns.Checked
        FormInfo.AutoCombos = AutoCombos.Checked
        FormInfo.DriftComboMode = Math.Max(DriftCombos.SelectedIndex, 0)
        Dim Seed As Integer = CInt(ToDbl(SeedBox.Text))
        FormInfo.Seed = If(Seed > 0, Seed, Environment.TickCount And Integer.MaxValue)
        '_____________________________________________________________
        FormInfo.FrameInfo.DispLimit = ToDbl(displimit.Text)
        FormInfo.FrameInfo.TopStoryDriftR = ToDbl(TS_LimitR.Text)
        FormInfo.FrameInfo.InterStoryDriftR = ToDbl(IS_LimitR.Text)
        FormInfo.FrameInfo.SteelDesignCode = Dcode_Steel.Text
        '_____________________________________________________________
        FormInfo.OptInfo.MemorySize = CInt(MemSize.Text)
        FormInfo.OptInfo.MaxFuncEvaluation = CInt(maxiter.Text)
        FormInfo.OptInfo.MemoryUpdateType = MemoryUpdate.SelectedIndex
        FormInfo.OptInfo.OptimizationMethod = Opt_method.SelectedIndex
        FormInfo.OptInfo.LevyFlight = Levy_Flight.Checked
        FormInfo.OptInfo.TestWithMath = TestwithMath.Checked
        FormInfo.OptInfo.ClearDuplicates = Clear_Duplicates.Checked
        FormInfo.OptInfo.HarmonySearch.PARChangeType = PAR_Type.SelectedIndex
        FormInfo.OptInfo.HarmonySearch.HMCRChangeType = HMCR_Type.SelectedIndex
        FormInfo.OptInfo.HarmonySearch.PAR = ToDbl(PAR_Val.Text)
        FormInfo.OptInfo.HarmonySearch.HMCR = ToDbl(HMCR_val.Text)
        FormInfo.OptInfo.BioGeography.MutationRate = ToDbl(Mutation_Rate.Text)
    End Sub
    Private Sub FormInfo_Write()
        saplocation.Text = FormInfo.FileList.ETABSFile
        OutputLoc.Text = FormInfo.FileList.OutputFile
        BackUp.Checked = True
        HideSAP2000.Checked = FormInfo.HideETABS
        CheckStructure.Checked = FormInfo.CheckStructure
        CompositeColumns.Checked = FormInfo.CompositeColumns
        AutoCombos.Checked = FormInfo.AutoCombos
        DriftCombos.SelectedIndex = FormInfo.DriftComboMode
        SeedBox.Text = FormInfo.Seed
        '_____________________________________________________________
        displimit.Text = FormInfo.FrameInfo.DispLimit
        TS_LimitR.Text = FormInfo.FrameInfo.TopStoryDriftR
        IS_LimitR.Text = FormInfo.FrameInfo.InterStoryDriftR
        Dcode_Steel.Text = FormInfo.FrameInfo.SteelDesignCode
        '_____________________________________________________________
        MemSize.Text = FormInfo.OptInfo.MemorySize
        maxiter.Text = FormInfo.OptInfo.MaxFuncEvaluation
        MemoryUpdate.SelectedIndex = FormInfo.OptInfo.MemoryUpdateType
        Opt_method.SelectedIndex = FormInfo.OptInfo.OptimizationMethod
        Levy_Flight.Checked = FormInfo.OptInfo.LevyFlight
        TestwithMath.Checked = FormInfo.OptInfo.TestWithMath
        Clear_Duplicates.Checked = FormInfo.OptInfo.ClearDuplicates
        PAR_Type.SelectedIndex = FormInfo.OptInfo.HarmonySearch.PARChangeType
        HMCR_Type.SelectedIndex = FormInfo.OptInfo.HarmonySearch.HMCRChangeType
        PAR_Val.Text = FormInfo.OptInfo.HarmonySearch.PAR
        HMCR_val.Text = FormInfo.OptInfo.HarmonySearch.HMCR
        Mutation_Rate.Text = FormInfo.OptInfo.BioGeography.MutationRate
    End Sub
    Private Sub Backup_Read()
        Dim Results As Class_Backup
        Dim serializer As New XmlSerializer(GetType(Class_Backup))
        Using reader As New StreamReader("BackUp.xml")
            Results = CType(serializer.Deserialize(reader), Class_Backup)
        End Using
        OptClass.Memory = Results.Memory
        FormInfo = Results.FormInfo
        OptClass.GlobalBest = Results.GlobalBest
        OptClass.GlobalBestPrint = Results.GlobalBestPrint
        OptClass.BestValue = Results.BestValue
        OptClass.Histories = Results.Histories
        OptClass.iter = Results.iter
        OptClass.ILoop = Results.ILoop
        Write_form()
    End Sub

    Private Sub Init(ByRef ret As Integer)
        OptClass = New OptimizationClass()
        Dim FromBackUp As Boolean = BackUp.Checked
        If FromBackUp Then
            If Not File.Exists("BackUp.xml") Then : MsgBox("BackUp.xml not found") : ret = -1 : Exit Sub : End If
            Backup_Read()
            FormInfo_Write()
            If Control() = True Then : ret = -1 : Exit Sub : End If
        Else
            If Control() = True Then : ret = -1 : Exit Sub : End If
            FormInfo_Read()
        End If

        'backup: the generator state cannot be restored, continue with a seed derived from the loop number
        SetRandomSeed(If(FromBackUp, FormInfo.Seed + OptClass.ILoop, FormInfo.Seed))
        StartTimeBox.Text = FormInfo.TimerInfo.StartTime
        OptClass.FormInfo = FormInfo
        OptClass.FileList = FormInfo.FileList
        OptClass.BackUp = FormInfo.BackUp
        If TestwithMath.Checked = True Then
            OptClass.Math_Init()
        Else
            SAP2000Class = New ETABS_Class(FormInfo, ret)
            If ret <> 0 Then : LogError("Error occurend in ETABS_Class") : Exit Sub : End If
            OptClass.SAP2000Class = SAP2000Class
            OptClass.Ub = SAP2000Class.Ub
            OptClass.Lb = SAP2000Class.Lb
            StartTimeBox.Text = SAP2000Class.FormInfo.TimerInfo.StartTime
        End If
        If FromBackUp Then Exit Sub

        OptClass.Memory = New List(Of OptimizationStructure_.Member_)
        OptClass.BestValue = Double.PositiveInfinity
        OptClass.GlobalBest.PenalizedCost = Double.PositiveInfinity
        OptClass.Histories = New List(Of OptimizationStructure_.History_)
        ReDim OptClass.GlobalBest.DesignVariables(OptClass.Ub.Count - 1)
        OptClass.iter = 0
        For i = 0 To FormInfo.OptInfo.MemorySize - 1
            Dim Member As New OptimizationStructure_.Member_
            OptClass.RandomGenerate(Member, 0, ret)
            If ret <> 0 Then : LogError("Error occurend in RandomGenerate") : Exit Sub : End If
            OptClass.Memory.Add(Member)
            Write_form()
        Next i
        OptClass.ILoop = 0
        If FormInfo.OptInfo.OptimizationMethod = OptimizationStructure_.OptMethod_.HarmornySearch Then OptClass.Init_HarmonySearch()
        If FormInfo.OptInfo.OptimizationMethod = OptimizationStructure_.OptMethod_.BioGBasedO Then OptClass.Init_BioGeographyBased()
    End Sub
    Private Sub Write_form()
        TextBox1.Text = OptClass.iter
        If OptClass.GlobalBest.PenalizedCost <> Double.PositiveInfinity AndAlso OptClass.GlobalBestPrint IsNot Nothing Then
            ListBox1.Items.Clear()
            For Each it In OptClass.GlobalBestPrint
                ListBox1.Items.Add(it)
            Next
        End If
        ListBox2.Items.Clear()
        ListBox2.Items.Add("History")
        ListBox2.Items.Add("Iter   Loop   Cost")
        For Each h In OptClass.Histories
            ListBox2.Items.Add(CStr(h.Iter) & " " & CStr(h.ILoop) & " " & CStr(h.Cost))
        Next
        Me.Refresh()
    End Sub

    'Checks the sections of an optimization output file (GlobalBestPrint) without modifying them
    Private Sub Check_Structure(ByRef ret As Integer)
        If Control() = True Then : ret = -1 : Exit Sub : End If
        FormInfo_Read()
        SetRandomSeed(FormInfo.Seed)
        SAP2000Class = New ETABS_Class(FormInfo, ret)
        If ret <> 0 Then : LogError("Error occurend in ETABS_Class") : Exit Sub : End If
        Dim Sect_ID() As Integer = Read_SectionID(ret)
        If ret <> 0 Then : LogError("Error occurend in Read_SectionID") : Exit Sub : End If
        ret = SAP2000Class.SetAndAnalyze(Sect_ID, False)
        If ret <> 0 Then : LogError("Error occurend in SetAndAnalyze") : Exit Sub : End If
        Dim Penalty As Double = -1
        SAP2000Class.Penalty(Penalty, Sect_ID, ret, applyRepair:=False)
        If ret <> 0 Then : LogError("Error occurend in Penalty") : Exit Sub : End If
        SAP2000Class.CostStProfile(Sect_ID)
        Dim serializer As New XmlSerializer(GetType(ETABS_Print))
        Using writer As New StreamWriter(Path.ChangeExtension(OutputLoc.Text, ".check.xml"))
            serializer.Serialize(writer, SAP2000Class.ETABS_print)
        End Using
        SAP2000Class.Close(ret)
    End Sub
    Private Function Read_SectionID(ByRef ret As Integer) As Integer()
        Dim xmldoc As New XmlDocument()
        xmldoc.Load(OutputLoc.Text)
        Dim xmlnode As XmlNodeList = xmldoc.GetElementsByTagName("GlobalBestPrint")
        Dim Sect_ID(SAP2000Class.SteelFrameDesignGroupIDs.Count - 1) As Integer
        If xmlnode.Count = 0 Then : ret = -1 : Return Sect_ID : End If
        'items: "Global Best: ..", header, then "<GroupName>: <SectionName> [composite info]" for each design group
        For j = 0 To SAP2000Class.SteelFrameDesignGroupIDs.Count - 1
            Dim textT As String = xmlnode(0).ChildNodes.Item(j + 2).InnerText
            Dim Sname As String = textT.Split(":".ToCharArray(), 2)(1).Trim().Split(" "c)(0)
            Sect_ID(j) = SAP2000Class.WSections.FindIndex(Function(c) c.SectionName = Sname)
            If Sect_ID(j) < 0 Then : SAP2000Class.Errorlogprint("Section not found in library: " & Sname) : ret = -1 : End If
        Next j
        Return Sect_ID
    End Function
End Class
