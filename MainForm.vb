Imports System.Xml
Imports System.IO
Imports System.Xml.Serialization
Public Class MainForm
    Public FormInfo As MiscellaneousStructures.FormInfo_
    Public SAP2000Class As ETABS_Class
    Public OptClass As OptimizationClass
    Public ID_mem As Integer
    Private Const MAX_STALL_LOOPS As Integer = 20
    Private ReadOnly AppTitle As String = "Steel Frame Optimization with Composite Columns (ETABS)"
    'progress display: time and analysis count since the ETABS model is ready (backup: since the restart)
    Private RunClock As Stopwatch
    Private IterAtStart As Integer

    Private Sub MainForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        If DriftCombos.SelectedIndex < 0 Then DriftCombos.SelectedIndex = 0
        If CompositeCodeBox.SelectedIndex < 0 Then CompositeCodeBox.SelectedIndex = CompositeCode_.AISC360_22
        If RepairModeBox.SelectedIndex < 0 Then RepairModeBox.SelectedIndex = MiscellaneousStructures.RepairMode_.Combined
        'unit costs: defaults of EncasedSections.xml (placeholders), edited by the user
        Dim Defaults As EncasedSettings_ = EncasedSettings_.LoadOrDefault()
        CostSteelBox.Text = Num(Defaults.SteelUnitCost)
        CostRebarBox.Text = Num(Defaults.RebarUnitCost)
        CostConcreteBox.Text = Num(Defaults.ConcreteUnitCost)
        CostFormworkBox.Text = Num(Defaults.FormworkUnitCost)
    End Sub

    'VB Rnd: Rnd(-1) followed by Randomize(seed) gives a repeatable sequence for the seed
    Private Sub SetRandomSeed(ByVal Seed As Integer)
        Rnd(-1)
        Randomize(Seed)
        ETABS_Class.SetSeed(Seed)
    End Sub
    Private Sub Start_Click(sender As Object, e As EventArgs) Handles start.Click
        Try
            RunAll()
        Catch ex As Exception
            'an unexpected exception must not leave ETABS and the working folder behind
            LogError("Unhandled exception: " & ex.ToString())
            CloseETABS(-1)
        End Try
    End Sub

    Private Sub RunAll()
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
            LogError("Initialisation failed (see the previous messages)")
            CloseETABS(ret)
            Exit Sub
        End If
        Me.Text = AppTitle & "  -  " & FormInfo.OptInfo.OptimizationMethod.ToString() & "  (seed " & FormInfo.Seed & ")"
        If SAP2000Class IsNot Nothing Then SAP2000Class.Errorlogprint("Info: run started, method " & FormInfo.OptInfo.OptimizationMethod.ToString() & ", seed " & FormInfo.Seed)
        'with the result cache a converged search may produce no new design: stop after MAX_STALL_LOOPS such loops
        Dim Stall As Integer = 0
        Do While OptClass.iter < FormInfo.OptInfo.MaxFuncEvaluation
            Dim IterBefore As Integer = OptClass.iter
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
            Stall = If(OptClass.iter = IterBefore, Stall + 1, 0)
            If Stall >= MAX_STALL_LOOPS Then
                LogError("Info: search converged, no new design in " & MAX_STALL_LOOPS & " loops (" & OptClass.iter & " analyses)")
                Exit Do
            End If
        Loop
        OptClass.Opt_Finalize()
        FinishTimeBox.Text = Date.Now.ToString("HH:mm:ss")
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

    'Form numbers with "." or "," as decimal separator, independent of the Windows culture
    '(with IsNumeric / CDbl a tr-TR Windows read "0.9" as 9 and "0.0636" as 636)
    Private Shared Function TryNum(ByVal text As String, ByRef value As Double) As Boolean
        If String.IsNullOrWhiteSpace(text) Then Return False
        Return Double.TryParse(text.Trim().Replace(","c, "."c), Globalization.NumberStyles.Float, Globalization.CultureInfo.InvariantCulture, value)
    End Function
    Private Shared Function IsValidNumber(ByVal text As String) As Boolean
        Dim x As Double
        Return TryNum(text, x)
    End Function
    Private Shared Function ToDbl(ByVal text As String) As Double
        Dim x As Double
        Return If(TryNum(text, x), x, 0)
    End Function
    Private Shared Function IsWholeNumber(ByVal text As String, ByVal Minimum As Double) As Boolean
        Dim x As Double
        Return TryNum(text, x) AndAlso x >= Minimum AndAlso x = Math.Floor(x) AndAlso x <= Integer.MaxValue
    End Function
    Private Shared Function Num(ByVal x As Double) As String
        Return x.ToString(Globalization.CultureInfo.InvariantCulture)
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
            If ToDbl(TS_LimitR.Text) <= 0 Then
                MsgBox("Top story drift limit (H / ratio) must be a positive number")
                Durdur = True
            End If
            If ToDbl(IS_LimitR.Text) <= 0 Then
                MsgBox("Inter story drift limit (h / ratio) must be a positive number")
                Durdur = True
            End If
            If String.IsNullOrWhiteSpace(Dcode_Steel.Text) Then
                MsgBox("Design Code Steel is not defined correctly")
                Durdur = True
            End If
            If CompositeColumns.Checked Then
                Dim CostBoxes() As TextBox = {CostSteelBox, CostRebarBox, CostConcreteBox, CostFormworkBox}
                If CostBoxes.Any(Function(b) Not IsValidNumber(b.Text) OrElse ToDbl(b.Text) < 0) OrElse CostBoxes.All(Function(b) ToDbl(b.Text) = 0) Then
                    MsgBox("Composite unit costs must be non-negative numbers, at least one of them positive")
                    Durdur = True
                End If
            End If
        End If
        '___________________________________________________________________________________________
        'Control Optimization Parameters
        If Not IsWholeNumber(MemSize.Text, 2) Then
            MsgBox("Memory size must be an integer of at least 2")
            Durdur = True
        End If
        If Not IsWholeNumber(SeedBox.Text, 0) Then
            MsgBox("Random seed must be a non-negative integer (0 = time based)")
            Durdur = True
        End If
        If Not IsWholeNumber(maxiter.Text, 1) Then
            MsgBox("Max. analyses must be a positive integer")
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
        FormInfo.CompositeCode = Math.Max(CompositeCodeBox.SelectedIndex, 0)
        FormInfo.RepairMode = Math.Max(RepairModeBox.SelectedIndex, 0)
        FormInfo.UseCache = ResultCache.Checked
        FormInfo.SkipUnusedCases = SkipCases.Checked
        FormInfo.Costs = New MiscellaneousStructures.UnitCosts_ With {.Steel = ToDbl(CostSteelBox.Text), .Rebar = ToDbl(CostRebarBox.Text),
                                                                      .Concrete = ToDbl(CostConcreteBox.Text), .Formwork = ToDbl(CostFormworkBox.Text)}
        FormInfo.AutoCombos = AutoCombos.Checked
        FormInfo.SkipCtoC = Not CtoC.Checked
        FormInfo.SkipBtoC = Not BtoC.Checked
        FormInfo.DriftComboMode = Math.Max(DriftCombos.SelectedIndex, 0)
        Dim Seed As Integer = CInt(ToDbl(SeedBox.Text))
        FormInfo.Seed = If(Seed > 0, Seed, Environment.TickCount And Integer.MaxValue)
        '_____________________________________________________________
        FormInfo.FrameInfo.TopStoryDriftR = ToDbl(TS_LimitR.Text)
        FormInfo.FrameInfo.InterStoryDriftR = ToDbl(IS_LimitR.Text)
        FormInfo.FrameInfo.SteelDesignCode = Dcode_Steel.Text
        '_____________________________________________________________
        FormInfo.OptInfo.MemorySize = CInt(ToDbl(MemSize.Text))
        FormInfo.OptInfo.MaxFuncEvaluation = CInt(ToDbl(maxiter.Text))
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
        CompositeCodeBox.SelectedIndex = FormInfo.CompositeCode
        RepairModeBox.SelectedIndex = FormInfo.RepairMode
        ResultCache.Checked = FormInfo.UseCache
        SkipCases.Checked = FormInfo.SkipUnusedCases
        If FormInfo.Costs.IsSet Then        'old backups: keep the file defaults shown at start
            CostSteelBox.Text = Num(FormInfo.Costs.Steel)
            CostRebarBox.Text = Num(FormInfo.Costs.Rebar)
            CostConcreteBox.Text = Num(FormInfo.Costs.Concrete)
            CostFormworkBox.Text = Num(FormInfo.Costs.Formwork)
        End If
        AutoCombos.Checked = FormInfo.AutoCombos
        CtoC.Checked = Not FormInfo.SkipCtoC
        BtoC.Checked = Not FormInfo.SkipBtoC
        DriftCombos.SelectedIndex = FormInfo.DriftComboMode
        SeedBox.Text = FormInfo.Seed
        '_____________________________________________________________
        TS_LimitR.Text = Num(FormInfo.FrameInfo.TopStoryDriftR)
        IS_LimitR.Text = Num(FormInfo.FrameInfo.InterStoryDriftR)
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
        PAR_Val.Text = Num(FormInfo.OptInfo.HarmonySearch.PAR)
        HMCR_val.Text = Num(FormInfo.OptInfo.HarmonySearch.HMCR)
        Mutation_Rate.Text = Num(FormInfo.OptInfo.BioGeography.MutationRate)
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
            ShowModelInfo()
        End If
        DateBox.Text = Date.Now.ToString("yyyy-MM-dd")
        RunClock = Stopwatch.StartNew()
        IterAtStart = If(FromBackUp, OptClass.iter, 0)
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
    'Model size on the Structural Properties tab
    Private Sub ShowModelInfo()
        NofJoint.Text = SAP2000Class.Points.Length.ToString()
        nofmember.Text = SAP2000Class.Frames.Length.ToString()
        nofgroup.Text = SAP2000Class.SteelFrameDesignGroupIDs.Count.ToString()
        nofsection1.Text = SAP2000Class.WSections.Count.ToString()
    End Sub

    Private Shared Function FormatSpan(ByVal t As TimeSpan) As String
        Return If(t.Days > 0, t.Days & "d ", "") & t.Hours.ToString("00") & ":" & t.Minutes.ToString("00") & ":" & t.Seconds.ToString("00")
    End Function

    Private Sub Write_form()
        Dim MaxIter As Double = Math.Max(FormInfo.OptInfo.MaxFuncEvaluation, 1)
        TextBox1.Text = OptClass.iter & " / " & FormInfo.OptInfo.MaxFuncEvaluation
        ProgressBar1.Value = CInt(Math.Min(Math.Max(100.0 * OptClass.iter / MaxIter, 0), 100))
        If Not Double.IsInfinity(OptClass.GlobalBest.PenalizedCost) Then BestCostBox.Text = OptClass.GlobalBest.CostValue.ToString("F2")
        If RunClock IsNot Nothing Then
            ElapsedBox.Text = FormatSpan(RunClock.Elapsed)
            Dim Done As Integer = OptClass.iter - IterAtStart
            If Done > 0 Then
                Dim PerAnalysis As Double = RunClock.Elapsed.TotalSeconds / Done
                AverageTimeBox.Text = PerAnalysis.ToString("F1")
                RemainingBox.Text = FormatSpan(TimeSpan.FromSeconds(PerAnalysis * Math.Max(MaxIter - OptClass.iter, 0)))
            End If
        End If
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
        ShowModelInfo()
        Dim Sect_ID() As Integer = Read_SectionID(ret)
        If ret <> 0 Then : LogError("Error occurend in Read_SectionID") : Exit Sub : End If
        ret = SAP2000Class.SetAndAnalyze(Sect_ID, False)
        If ret <> 0 Then : LogError("Error occurend in SetAndAnalyze") : Exit Sub : End If
        Dim Penalty As Double = -1
        SAP2000Class.Penalty(Penalty, Sect_ID, ret, applyRepair:=False)
        If ret <> 0 Then : LogError("Error occurend in Penalty") : Exit Sub : End If
        ret = SAP2000Class.VerifyCompositeWithETABS()      'ETABS composite column design -> ETABS_print (check.xml)
        If ret <> 0 Then : LogError("Error occurend in VerifyCompositeWithETABS") : Exit Sub : End If
        SAP2000Class.ETABS_print.Penalty = Penalty
        SAP2000Class.ETABS_print.Cost = SAP2000Class.CostStProfile(Sect_ID)
        SAP2000Class.ETABS_print.AnalysisFailed = SAP2000Class.AnalysisFailed
        SAP2000Class.Errorlogprint("Info: checked design: cost " & Num(SAP2000Class.ETABS_print.Cost) & ", penalty " & Num(Penalty) & If(SAP2000Class.AnalysisFailed, " (analysis not finished)", ""))
        Dim serializer As New XmlSerializer(GetType(ETABS_Print))
        Using writer As New StreamWriter(Path.ChangeExtension(OutputLoc.Text, ".check.xml"))
            serializer.Serialize(writer, SAP2000Class.ETABS_print)
        End Using
        SAP2000Class.Close(ret)
    End Sub
    'Sections of an output file, matched by group name ("<GroupName>: <SectionName> [composite info]")
    Private Function Read_SectionID(ByRef ret As Integer) As Integer()
        Dim Sect_ID(SAP2000Class.SteelFrameDesignGroupIDs.Count - 1) As Integer
        If Not File.Exists(OutputLoc.Text) Then : SAP2000Class.Errorlogprint("Output file to check not found: " & OutputLoc.Text) : ret = -1 : Return Sect_ID : End If
        Dim xmldoc As New XmlDocument()
        xmldoc.Load(OutputLoc.Text)
        Dim xmlnode As XmlNodeList = xmldoc.GetElementsByTagName("GlobalBestPrint")
        If xmlnode.Count = 0 Then : SAP2000Class.Errorlogprint("No GlobalBestPrint in " & OutputLoc.Text) : ret = -1 : Return Sect_ID : End If
        Dim ByGroup As New Dictionary(Of String, String)
        For Each item As XmlNode In xmlnode(0).ChildNodes
            Dim parts() As String = item.InnerText.Split(":".ToCharArray(), 2)
            If parts.Length = 2 Then ByGroup(parts(0).Trim()) = parts(1).Trim().Split(" "c)(0)
        Next
        For j = 0 To SAP2000Class.SteelFrameDesignGroupIDs.Count - 1
            Dim G As String = SAP2000Class.Groups(SAP2000Class.SteelFrameDesignGroupIDs(j)).GroupName
            Dim Sname As String = Nothing
            If Not ByGroup.TryGetValue(G, Sname) Then : SAP2000Class.Errorlogprint("Group " & G & " not found in " & OutputLoc.Text) : ret = -1 : Continue For : End If
            Sect_ID(j) = SAP2000Class.WSections.FindIndex(Function(c) c.SectionName = Sname)
            If Sect_ID(j) < 0 Then : SAP2000Class.Errorlogprint("Section not found in library: " & Sname) : ret = -1 : End If
        Next j
        Return Sect_ID
    End Function
End Class
