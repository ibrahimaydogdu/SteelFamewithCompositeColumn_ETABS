Imports System.Xml
Imports System.IO
Imports System.Xml.Serialization
Public Class MainForm
    Dim t1, t2 As System.Threading.Thread
    Public FormInfo As MiscellaneousStructures.FormInfo_
    Public SAP2000Class As ETABS_Class
    Public OptClass As OptimizationClass
    Public ID_mem As Integer
    Private Sub Start_Click(sender As Object, e As EventArgs) Handles start.Click
        Dim ret As Integer = 0
        If CheckStructure.Checked = True Then
            Check_Structure(ret)
            If ret <> 0 Then
                SAP2000Class.Errorlogprint("Error occurend in Check Structure")
                SAP2000Class.Close(ret)
                Exit Sub
            End If
            Exit Sub
        End If
        Init(ret)
        If ret <> 0 Then
            SAP2000Class.Errorlogprint("Init")
            SAP2000Class.Close(ret)
            Exit Sub
        End If
        Me.Text = FormInfo.OptInfo.OptimizationMethod
            't1 = New System.Threading.Thread(AddressOf Opt_Main)
            Do While OptClass.iter < FormInfo.OptInfo.MaxFuncEvaluation
                OptClass.ILoop += 1
            Dim Memory As List(Of OptimizationStructure_.Member_) = OptClass.Memory.OrderBy(Function(c) c.PenalizedCost).ToList()
            OptClass.Memory = Memory
            For Imem = 0 To FormInfo.OptInfo.MemorySize - 1
                ID_mem = Imem
                't1.Start()
                Opt_Main(ret)
                If ret <> 0 Then
                    SAP2000Class.Errorlogprint("Error occurend in Opt_Main")
                    SAP2000Class.Close(ret)
                    Exit Sub
                End If
            Next Imem

            OptClass.ClearDuplicates(ret)
            If ret <> 0 Then
                SAP2000Class.Errorlogprint("Error occurend in Clear Duplicates")
                SAP2000Class.Close(ret)
                Exit Sub
            End If
            OptClass.Backup_Write()
            Loop
            OptClass.Opt_Finalize()


    End Sub
    Private Sub Opt_Main(ByRef ret As Integer)
        OptClass.Main(ID_mem, ret)
        Write_form()
    End Sub
    Private Sub LoadSAP2000file_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles loadSAP2000file.Click
        Dim openFileDialog1 As New Windows.Forms.OpenFileDialog With {
            .Filter = "SAP2000 File|*.SDB",
            .Title = "Select SAP2000 file"
        }
        openFileDialog1.ShowDialog()
        saplocation.Text = openFileDialog1.FileName.ToString
    End Sub
    Private Sub Loadoutput_Click(ByVal sender As System.Object, ByVal e As System.EventArgs) Handles loadoutput.Click
        Dim saveFileDialog1 As New Windows.Forms.SaveFileDialog With {
            .Filter = "xml Files|*.xml",
            .Title = "Output Dosyasını Giriniz"
        }
        saveFileDialog1.ShowDialog()
        OutputLoc.Text = saveFileDialog1.FileName.ToString
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs)
        Dim openFileDialog As New Windows.Forms.OpenFileDialog()
        Dim loadloc As String
        Try
            openFileDialog.Filter = "Text Files|*.txt"
            openFileDialog.Title = "Yükleme Dosyasını Seçiniz"
            openFileDialog.ShowDialog()
            loadloc = openFileDialog.FileName.ToString
        Finally
            If openFileDialog Is Nothing Then
            Else
                openFileDialog.Dispose()
            End If
        End Try
        Dim objreader As New System.IO.StreamReader(loadloc)
        saplocation.Text = objreader.ReadLine
        OutputLoc.Text = objreader.ReadLine
    End Sub
    Private Function Control() As Boolean
        Dim Durdur As Boolean = False
        '___________________________________________________________________________________________
        'File info Parameters
        '_______________________________________________________________________________________________
        'Control Input Output file locations
        If My.Computer.FileSystem.FileExists(saplocation.Text) = False And TestwithMath.Checked = False Then
            MessageBox.Show("SAP2000 File Not Found: " & saplocation.Text)
            Durdur = True
        End If
        If My.Computer.FileSystem.DirectoryExists(Path.GetDirectoryName(OutputLoc.Text)) = False Then
            MessageBox.Show("Output File Not Found: " & OutputLoc.Text)
            Durdur = True
        End If
        '______________________________________________________________________________________________
        'Control Frame Parameters
        If TestwithMath.Checked = False Then
            If Not IsNumeric(displimit.Text) Or displimit.Text = vbEmpty Then
                MsgBox("Displacement limit is not defined correctly")
                Durdur = True
            End If
            If Not IsNumeric(TS_LimitR.Text) Or TS_LimitR.Text = vbEmpty Then
                MsgBox("Top Story limit is not defined correctly")
                Durdur = True
            End If
            If Not IsNumeric(IS_LimitR.Text) Or IS_LimitR.Text = vbEmpty Then
                MsgBox("Inter Story limit is not defined correctly")
                Durdur = True
            End If
            If Dcode_Steel.Text = Nothing Then
                MsgBox("G1_1_Design Code Steel is not defined correctly")
                Durdur = True
            End If
        End If
        '___________________________________________________________________________________________
        'Control Optimization Parameters
        If Not IsNumeric(MemSize.Text) Or MemSize.Text = vbEmpty Then
            MsgBox("Optimization Info Memory size is not defined correctly")
            Durdur = True
        End If
        If Not IsNumeric(maxiter.Text) Or maxiter.Text = vbEmpty Then
            MsgBox("Optimization Info MaxIteration is not defined correctly")
            Durdur = True
        End If
        If Opt_method.SelectedIndex = 0 Then 'Harmony Search
            If Not IsNumeric(PAR_Val.Text) Or PAR_Val.Text = vbEmpty Then
                MsgBox("Harmony Search Info the PAR is not defined correctly")
                Durdur = True
            End If
            If Not IsNumeric(HMCR_val.Text) Or HMCR_val.Text = vbEmpty Then
                MsgBox("Harmony Search Info the HMCR is is not defined correctly")
                Durdur = True
            End If
        ElseIf Opt_method.SelectedIndex = 1 Then 'Biogeoraphy-Based
            If Not IsNumeric(Mutation_Rate.Text) Or Mutation_Rate.Text = vbEmpty Then
                MsgBox("Biogeography-based Info the Mutation rate is is not defined correctly")
                Durdur = True
            End If
        End If
        'If Not IsNumeric(PrFemale.Text) Or PrFemale.Text = vbEmpty Then : MsgBox("Probability of female movement direction is not defined correctly") : durdur = True : Exit Sub : End If
        'If Not IsNumeric(tole.Text) Or tole.Text = vbEmpty Then : MsgBox("Tolerance is not defined correctly") : durdur = True : Exit Sub : End If
        Return Durdur
    End Function
    Private Sub FormInfo_Read()
        FormInfo = New MiscellaneousStructures.FormInfo_
        FormInfo.FileList.ETABSFile = saplocation.Text
        FormInfo.FileList.OutputFile = OutputLoc.Text
        FormInfo.BackUp = BackUp.Checked
        FormInfo.HideETABS = HideSAP2000.Checked
        FormInfo.CheckStructure = CheckStructure.Checked
        '_____________________________________________________________
        FormInfo.FrameInfo.DispLimit = CDbl(displimit.Text)
        FormInfo.FrameInfo.TopStoryDriftR = CDbl(TS_LimitR.Text)
        FormInfo.FrameInfo.InterStoryDriftR = CDbl(IS_LimitR.Text)
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
        FormInfo.OptInfo.HarmonySearch.PAR = CDbl(PAR_Val.Text)
        FormInfo.OptInfo.HarmonySearch.HMCR = CDbl(HMCR_val.Text)
        FormInfo.OptInfo.BioGeography.MutationRate = CDbl(Mutation_Rate.Text)
    End Sub
    Private Sub FormInfo_Write()
        saplocation.Text = FormInfo.FileList.ETABSFile
        OutputLoc.Text = FormInfo.FileList.OutputFile
        BackUp.Checked = True
        HideSAP2000.Checked = FormInfo.HideETABS
        CheckStructure.Checked = FormInfo.CheckStructure
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
        Dim Results = New Class_Backup()
        Dim serializer As New XmlSerializer(GetType(Class_Backup))
        Using reader As New StreamReader("BackUp.xml")
            Results = serializer.Deserialize(reader)
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
        If BackUp.Checked = True Then
            Backup_Read()
            FormInfo_Write()
            If Control() = True Then Exit Sub
            StartTimeBox.Text = FormInfo.TimerInfo.StartTime
            OptClass.FormInfo = FormInfo
            OptClass.FileList = FormInfo.FileList
            OptClass.BackUp = FormInfo.BackUp
            If TestwithMath.Checked = True Then
                OptClass.Math_Init()
            Else
                SAP2000Class = New ETABS_Class(FormInfo, ret)
                If ret <> 0 Then
                    SAP2000Class.Errorlogprint("Error occurend in ETABS_Class")
                    Exit Sub
                End If
                OptClass.SAP2000Class = SAP2000Class
                OptClass.Ub = SAP2000Class.Ub
                OptClass.Lb = SAP2000Class.Lb
            End If
        Else
            FormInfo_Read()
            If Control() = True Then Exit Sub
            StartTimeBox.Text = FormInfo.TimerInfo.StartTime
            OptClass.FormInfo = FormInfo
            OptClass.FileList = FormInfo.FileList
            OptClass.BackUp = FormInfo.BackUp
            If TestwithMath.Checked = True Then
                OptClass.Math_Init()
            Else
                SAP2000Class = New ETABS_Class(FormInfo, ret)
                If ret <> 0 Then
                    SAP2000Class.Errorlogprint("Error occurend in ETABS_Class")
                    Exit Sub
                End If
                OptClass.SAP2000Class = SAP2000Class
                OptClass.Ub = SAP2000Class.Ub
                OptClass.Lb = SAP2000Class.Lb
            End If
            OptClass.Memory = New List(Of OptimizationStructure_.Member_)
            OptClass.BestValue = Double.PositiveInfinity
            OptClass.GlobalBest.PenalizedCost = Double.PositiveInfinity
            OptClass.Histories = New List(Of OptimizationStructure_.History_)
            ReDim OptClass.GlobalBest.DesignVariables(OptClass.Ub.Count - 1)
            OptClass.iter = 0
            For i = 0 To FormInfo.OptInfo.MemorySize - 1
                Dim Member As New OptimizationStructure_.Member_
                OptClass.RandomGenerate(Member, 0, ret)
                If ret <> 0 Then
                    SAP2000Class.Errorlogprint("Error occurend in RandomGenerate")
                    Exit Sub
                End If
                OptClass.Memory.Add(Member)
                Write_form()
            Next i
            OptClass.ILoop = 0
            If FormInfo.OptInfo.OptimizationMethod = OptimizationStructure_.OptMethod_.HarmornySearch Then OptClass.Init_HarmonySearch()
            If FormInfo.OptInfo.OptimizationMethod = OptimizationStructure_.OptMethod_.BioGBasedO Then OptClass.Init_BioGeographyBased()
        End If

    End Sub
    Private Sub Write_form()
        TextBox1.Text = OptClass.iter
        If OptClass.GlobalBest.PenalizedCost <> Double.PositiveInfinity Then
            ListBox1.Items.Clear()
            For Each it In OptClass.GlobalBestPrint
                ListBox1.Items.Add(it)
            Next

        End If
        ListBox2.Items.Clear()
        ListBox2.Items.Add("History")
        ListBox2.Items.Add("Iter   Loop   Cost")
        For i = 0 To OptClass.Histories.Count - 1
            ListBox2.Items.Add(CStr(OptClass.Histories(i).Iter) & " " & CStr(OptClass.Histories(i).ILoop) _
                                   & " " & CStr(OptClass.Histories(i).Cost))
        Next
        'System.Threading.Thread.Sleep(100)
        Me.Refresh()
    End Sub
    Private Sub Check_Structure(ByRef ret As Integer)
        If Control() = True Then Exit Sub
        FormInfo_Read()
        SAP2000Class = New ETABS_Class(FormInfo, ret)
        If ret <> 0 Then : SAP2000Class.Errorlogprint("Error occurend in ETABS_Class") : Exit Sub : End If
        Dim Sect_ID() As Integer = Read_SectionID()
        Dim Penalty As Double = -1
        SAP2000Class.E2_SetSection(Sect_ID)
        SAP2000Class.Penalty(Penalty, Sect_ID, ret)
        If ret <> 0 Then : SAP2000Class.Errorlogprint("Error occurend in Penalty") : Exit Sub : End If
        SAP2000Class.CostStProfile(Sect_ID)
        Dim OptResults = New ETABS_Print() With {
            .PMM_Ratios = SAP2000Class.ETABS_print.PMM_Ratios,
            .InterStoryDrift_Ratios = SAP2000Class.ETABS_print.InterStoryDrift_Ratios,
            .TopStoryDrift_Ratio = SAP2000Class.ETABS_print.TopStoryDrift_Ratio,
            .ColumnToColumnGeometricRatio = SAP2000Class.ETABS_print.ColumnToColumnGeometricRatio,
            .BeamToColumnGeometricRatio = SAP2000Class.ETABS_print.BeamToColumnGeometricRatio
}
        Dim serializer As New XmlSerializer(GetType(ETABS_Print))
        Using writer As New StreamWriter(OutputLoc.Text & "xml")
            serializer.Serialize(writer, OptResults)
        End Using
        SAP2000Class.Close(ret)
    End Sub
    Private Function Read_SectionID()
        Dim fs As New FileStream(OutputLoc.Text, FileMode.Open, FileAccess.Read)
        'Dim xmldoc As New XmlDataDocument()
        Dim xmldoc As New XmlDocument()
        Dim xmlnode As XmlNodeList
        xmldoc.Load(fs)
        xmlnode = xmldoc.GetElementsByTagName("GlobalBestPrint")
        Dim Sect_ID() As Integer
        ReDim Sect_ID(SAP2000Class.SteelFrameDesignGroupIDs.Count - 1)
        For i = 0 To xmlnode.Count - 1
            For j = 0 To SAP2000Class.SteelFrameDesignGroupIDs.Count - 1
                Dim textT As String = xmlnode(i).ChildNodes.Item(j + 2).InnerText
                Dim Sname As String = textT.Split(":".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)(1).Replace(" ", "")
                Sect_ID(j) = SAP2000Class.WSections.ToList().FindIndex(Function(c) c.SectionName = Sname)
            Next j
        Next i
            Return Sect_ID
    End Function
End Class





