Imports System.IO
Imports System.Xml
Imports System.Xml.Serialization

Public Class OptimizationClass
    Public Memory As List(Of OptimizationStructure_.Member_)
    Public Ub() As Integer
    Public Lb() As Integer
    Public GlobalBest As OptimizationStructure_.Member_
    Public GlobalBestPrint As List(Of String)
    Public BestValue As Double
    Public Histories As List(Of OptimizationStructure_.History_)
    Public FormInfo As MiscellaneousStructures.FormInfo_
    Public FileList As MiscellaneousStructures.FileList_
    Public BackUp As Boolean
    Public iter As Integer
    Public ILoop As Integer
    Public Update As Boolean
    Public SAP2000Class As ETABS_Class
    Public ETABSCompositeCheck As List(Of String)      'ETABS composite column design of the final design

    Public Sub Init_HarmonySearch()
        ReDim FormInfo.OptInfo.HarmonySearch.ParVec(Memory.Count - 1)
        ReDim FormInfo.OptInfo.HarmonySearch.HMCRVec(Memory.Count - 1)
        For i = 0 To Memory.Count - 1
            FormInfo.OptInfo.HarmonySearch.ParVec(i) = FormInfo.OptInfo.HarmonySearch.PAR
            FormInfo.OptInfo.HarmonySearch.HMCRVec(i) = FormInfo.OptInfo.HarmonySearch.HMCR
        Next i
    End Sub
    Public Sub Init_BioGeographyBased()
        ReDim FormInfo.OptInfo.BioGeography.Mu(Memory.Count - 1)
        ReDim FormInfo.OptInfo.BioGeography.Lamda(Memory.Count - 1)
        For i = 0 To Memory.Count - 1
            FormInfo.OptInfo.BioGeography.Mu(i) = (Memory.Count - i) / (Memory.Count)
            FormInfo.OptInfo.BioGeography.Lamda(i) = 1.0 - FormInfo.OptInfo.BioGeography.Mu(i)
        Next
    End Sub
    Public Sub Main(ByRef Imem As Integer, ByRef ret As Integer)
        If FormInfo.OptInfo.OptimizationMethod = OptimizationStructure_.OptMethod_.HarmornySearch Then Main_HarmonySearch(Imem, ret)
        If FormInfo.OptInfo.OptimizationMethod = OptimizationStructure_.OptMethod_.BioGBasedO Then Main_BioGeographyBased(Imem, ret)
        If FormInfo.OptInfo.OptimizationMethod = OptimizationStructure_.OptMethod_.WhaleOpt Then Main_Whale(Imem, ret)
        If FormInfo.OptInfo.OptimizationMethod = OptimizationStructure_.OptMethod_.DandelionOpt Then Main_Dandelion(Imem, ret)
    End Sub
    Private Sub Main_HarmonySearch(ByRef Imem As Integer, ByRef ret As Integer)
        Dim PAR As Double
        If FormInfo.OptInfo.HarmonySearch.PARChangeType = OptimizationStructure_.PAR_HMCR_ChangeType_.IStatic Then
            PAR = FormInfo.OptInfo.HarmonySearch.PAR
        ElseIf FormInfo.OptInfo.HarmonySearch.PARChangeType = OptimizationStructure_.PAR_HMCR_ChangeType_.Dynamic Then
            Dim ParMax As Double = 0.99
            Dim ParMin As Double = 0.01
            PAR = ParMin + (ParMax - ParMin) * CostSpread(FormInfo.OptInfo.HarmonySearch.PAR)
        ElseIf FormInfo.OptInfo.HarmonySearch.PARChangeType = OptimizationStructure_.PAR_HMCR_ChangeType_.Adaptive Then
            PAR = 1 / (1 + (1 - FormInfo.OptInfo.HarmonySearch.ParVec.Average) / FormInfo.OptInfo.HarmonySearch.ParVec.Average * Math.E ^ (-0.5 * (3 - Rnd() * 6)))
        End If
        Dim HMCR As Double
        If FormInfo.OptInfo.HarmonySearch.HMCRChangeType = OptimizationStructure_.PAR_HMCR_ChangeType_.IStatic Then
            HMCR = FormInfo.OptInfo.HarmonySearch.HMCR
        ElseIf FormInfo.OptInfo.HarmonySearch.HMCRChangeType = OptimizationStructure_.PAR_HMCR_ChangeType_.Dynamic Then
            Dim HMCRMax As Double = 0.99
            Dim HMCRMin As Double = 0.01
            HMCR = HMCRMin + (HMCRMax - HMCRMin) * CostSpread(FormInfo.OptInfo.HarmonySearch.HMCR)
        ElseIf FormInfo.OptInfo.HarmonySearch.HMCRChangeType = OptimizationStructure_.PAR_HMCR_ChangeType_.Adaptive Then
            HMCR = 1 / (1 + (1 - FormInfo.OptInfo.HarmonySearch.HMCRVec.Average) / FormInfo.OptInfo.HarmonySearch.HMCRVec.Average * Math.E ^ (-0.5 * (3 - Rnd() * 6)))
        End If
        Dim Member = New OptimizationStructure_.Member_
        ReDim Member.DesignVariables(Ub.Count - 1)
        For i = 0 To Ub.Count - 1
            If Rnd() > HMCR Then
                Member.DesignVariables(i) = RandomVariable(i)
            Else
                Dim ID As Integer = Math.Floor(Rnd() * Memory.Count)
                If Rnd() > PAR Then
                    Member.DesignVariables(i) = Memory(ID).DesignVariables(i)
                Else
                    Member.DesignVariables(i) = Memory(ID).DesignVariables(i) + CInt(Math.Round((Ub(i) - Lb(i)) * 0.02 * (Rnd() - 0.5)))
                End If
            End If
        Next
        Eval(Member, Imem, ret)
        If ret <> 0 Then : LogError("Problem occured in :Eval") : Exit Sub : End If
        If Update = True Then
            FormInfo.OptInfo.HarmonySearch.ParVec(Imem) = PAR
            FormInfo.OptInfo.HarmonySearch.HMCRVec(Imem) = HMCR
        End If
    End Sub
    Private Sub Main_BioGeographyBased(ByRef Imem As Integer, ByRef ret As Integer)
        Dim Member As New OptimizationStructure_.Member_
        ReDim Member.DesignVariables(Ub.Count - 1)
        For idv = 0 To Ub.Count - 1
            If Rnd() < FormInfo.OptInfo.BioGeography.Lamda(Imem) Then
                Dim SelectMem As Integer = Roulette_wheel(FormInfo.OptInfo.BioGeography.Mu)
                Member.DesignVariables(idv) = Memory(SelectMem).DesignVariables(idv)
            Else
                Member.DesignVariables(idv) = Memory(Imem).DesignVariables(idv)
            End If
            If Rnd() < FormInfo.OptInfo.BioGeography.MutationRate Then
                If FormInfo.OptInfo.LevyFlight = True Then
                    Member.DesignVariables(idv) = LevyFlight(Memory(Imem), idv)
                Else
                    Member.DesignVariables(idv) = RandomVariable(idv)
                End If
            End If
        Next idv
        Eval(Member, Imem, ret)
        If ret <> 0 Then : LogError("Problem occured in :Eval") : Exit Sub : End If
    End Sub
    'Best known design: the global best once a feasible design exists, otherwise the best member of the memory
    '(GlobalBest holds only feasible designs; before that its variables are all 0)
    Private Function Leader() As OptimizationStructure_.Member_
        If GlobalBest.DesignVariables IsNot Nothing AndAlso Not Double.IsInfinity(GlobalBest.PenalizedCost) Then Return GlobalBest
        Dim Best As OptimizationStructure_.Member_ = Memory(0)
        For Each M In Memory
            If M.PenalizedCost < Best.PenalizedCost Then Best = M
        Next
        Return Best
    End Function

    Private Sub Main_Whale(ByRef Imem As Integer, ByRef ret As Integer)
        Dim a1 As Double = 2.0 - CDbl(iter) * ((2.0) / CDbl(FormInfo.OptInfo.MaxFuncEvaluation)) ' % a is a parameter that drops linearly from  2 to 0
        Dim a2 As Double = -1.0 + CDbl(iter) * ((-1.0) / CDbl(FormInfo.OptInfo.MaxFuncEvaluation)) 'a2 is a parameter that drops linearly from  -1 to -2
        Dim Member As New OptimizationStructure_.Member_
        ReDim Member.DesignVariables(Ub.Count - 1)
        Dim Lead() As Integer = Leader().DesignVariables
        For idv = 0 To Ub.Count - 1
            'Compute parameters C and A
            Dim A As Double = 2.0 * a1 * CDbl(Rnd()) - a1
            Dim C As Double = 2 * Rnd()
            'Spiral position update
            Dim b As Double = 1.0
            Dim l1 As Double = (a2 - 1.0) * Rnd() + 1.0
            If Rnd() < 0.5 Then '!50% chance to choose between shrinking containment mechanism and spiral model
                If Math.Abs(A) >= 1 Then
                    Dim X_rand As OptimizationStructure_.Member_ = Memory(Math.Floor(Memory.Count * Rnd()))
                    Dim D_X_rand As Double = Math.Abs(C * X_rand.DesignVariables(idv) - Memory(Imem).DesignVariables(idv))
                    Member.DesignVariables(idv) = Math.Floor(X_rand.DesignVariables(idv) - A * D_X_rand)
                Else
                    Dim D_Leader As Double = Math.Abs(C * Lead(idv) - Memory(Imem).DesignVariables(idv))
                    Member.DesignVariables(idv) = Math.Floor(Lead(idv) - A * D_Leader)
                End If
            Else
                Dim Distance2Leader As Double = Math.Abs(Lead(idv) - Memory(Imem).DesignVariables(idv))
                Member.DesignVariables(idv) = Math.Floor(Distance2Leader * Math.Exp(b * l1) * Math.Cos(l1 * 2 * Math.PI) + Lead(idv))
            End If
        Next idv
        Eval(Member, Imem, ret)
        If ret <> 0 Then : LogError("Problem occured in :Eval") : Exit Sub : End If
    End Sub

    Private Sub Main_Dandelion(ByRef Imem As Integer, ByRef ret As Integer)
        Dim Member As New OptimizationStructure_.Member_
        ReDim Member.DesignVariables(Ub.Count - 1)
        'Rising stage
        Dim alpha As Double = Rnd() * ((1 / FormInfo.OptInfo.MaxFuncEvaluation ^ 2) * iter ^ 2 - 2 / FormInfo.OptInfo.MaxFuncEvaluation * iter + 1) ' eq.(8) in this paper
        Dim a As Double = -1 / (FormInfo.OptInfo.MaxFuncEvaluation ^ 2 - 2 * FormInfo.OptInfo.MaxFuncEvaluation + 1)
        Dim b As Double = -2 * a
        Dim c As Double = 1 - a - b
        Dim k As Double = 1 - Rnd() * (c + a * iter ^ 2 + b * iter) ' eq.(11) In this paper
        For idv = 0 To Ub.Count - 1
            Dim rndn As Double = -3 + 6 * Rnd()
            If rndn < 1.5 Then
                Dim lamb As Double = Math.Max(Math.Abs(-3 + Rnd() * 6), 0.000001)
                Dim theta As Double = (2 * Rnd() - 1) * Math.PI
                Dim row As Double = 1 / Math.Exp(theta)
                Dim vx As Double = row * Math.Cos(theta)
                Dim vy As Double = row * Math.Sin(theta)
                Dim newv As Double = Rnd() * (Ub(idv) - Lb(idv)) + Lb(idv)
                Member.DesignVariables(idv) = Memory(Imem).DesignVariables(idv) + alpha * vx * vy * Lognpdf(lamb, 0, 1) * (newv - Memory(Imem).DesignVariables(idv)) ' eq.(5) in this paper
            Else
                Member.DesignVariables(idv) = Memory(Imem).DesignVariables(idv) * k
            End If
        Next idv
        UBLBCheck(Member)

        'Decline stage
        For idv = 0 To Ub.Count - 1
            Dim dandelions_mean As Double = 0
            For i = 0 To Memory.Count - 1
                dandelions_mean += Memory(i).DesignVariables(idv)
            Next i
            dandelions_mean /= Memory.Count
            Dim beta As Double = -3 + Rnd() * 6
            Member.DesignVariables(idv) = Member.DesignVariables(idv) - beta * alpha * (dandelions_mean - beta * alpha * Member.DesignVariables(idv)) ' eq.(13) In this paper
        Next idv
        UBLBCheck(Member)

        'Landing stage
        Dim Elite() As Integer = Leader().DesignVariables
        For idv = 0 To Ub.Count - 1
            Member.DesignVariables(idv) = Math.Floor(Elite(idv) + Steplength(1.5) * alpha * (Elite(idv) - Member.DesignVariables(idv) * (2 * iter / FormInfo.OptInfo.MaxFuncEvaluation))) ' eq.(15) In this paper
        Next idv
        UBLBCheck(Member)

        ' Calculated all dandelion seeds' fitness values
        Eval(Member, Imem, ret)
        If ret <> 0 Then : LogError("Problem occured in :Eval") : Exit Sub : End If


    End Sub
    Public Sub RandomGenerate(ByRef Member As OptimizationStructure_.Member_, ByVal i As Integer, ByRef ret As Integer)
        Member = New OptimizationStructure_.Member_
        ReDim Member.DesignVariables(Ub.Count - 1)
        For j = 0 To Ub.Count - 1
            Member.DesignVariables(j) = RandomVariable(j)
        Next

        If FormInfo.OptInfo.TestWithMath = True Then
            Call Math_Evaluate(Member)
        Else
            SAP2000Class.Evaluate(Member, iter, ret)
            If ret <> 0 Then : LogError("Problem occured in :Evaluate") : Exit Sub : End If
        End If
        GlobalBestCheck(Member, i)
    End Sub
    Private Function Roulette_wheel(ByVal x() As Double) As Integer
        Dim RandomNum As Double = x.Sum * Rnd()
        Dim Select_N As Double = x(0)
        Dim SelectIndex As Integer = 0
        Do While RandomNum > Select_N And SelectIndex < x.Length - 1
            SelectIndex += 1
            Select_N += x(SelectIndex)
        Loop
        Return SelectIndex
    End Function
    Private Function LevyFlight(ByVal Member As OptimizationStructure_.Member_, ByVal idv As Integer) As Integer

        'Levy flights
        'Levy exponent And coefficient
        'For details, see equation (2.21), Page 16 (chapter 2) of the book
        'X.S.Yang, Nature - Inspired Metaheuristic Algorithms, 2nd Edition, Luniver Press, (2010).
        Dim id As Integer
        Dim Beta As Double = 1.5
        Dim Gamma1 As Double = 1.329340388179137
        Dim Gamma2 = 0.906402477055477
        Dim Sigma As Double = ((Gamma1 * Math.Sin(Math.PI * Beta / 2)) / (Gamma2 * Beta * 2 ^ ((Beta - 1) / 2))) ^ (1 / Beta)
        'This Is a simple way of implementing Levy flights
        'For standard random walks, use step=1;

        Dim URN As Double = (-3 + 6 * Rnd()) * Sigma
        Dim RZD As Double = -3 + 6 * Rnd()
        Do While RZD < 0.01
            RZD = -3 + 6 * Rnd()
        Loop
        'Levy flights by Mantegna's algorithm	
        Dim STEPLevy As Double = URN / (Math.Abs(RZD)) ^ (1 + Beta)
        'In the Next equation, the difference factor (s-best) means that 
        'when the solution Is the best solution, it remains unchanged.     
        Dim Best() As Integer = Leader().DesignVariables
        Dim STSZ As Double = 0.01 * STEPLevy * (Member.DesignVariables(idv) - Best(idv))
        Dim RKD As Double = -3 + 6 * Rnd()
        'Here the factor 0.01 comes from the fact that L/100 should the typical
        'step Size of walks/flights where L Is the typical lenghtscale; 
        'otherwise, Levy flights may become too aggresive/efficient, 
        'which makes New solutions (even) jump out side of the design domain (And thus wasting evaluations).
        'Now the actual random walks Or flights
        Dim int1 As Integer = 0
        Dim RVD As Double = Rnd()
        If (RVD <= 0.4) Then int1 = Math.Floor(-2 + 4 * RVD)
        id = Best(idv) + Math.Floor(STSZ * RKD) + int1
        Return id
    End Function

    Private Function Steplength(ByVal beta As Double) As Double
        ' beta Is set to 1.5 in this paper
        Dim num As Double = (1 + beta) * Math.Sin(Math.PI * beta / 2)
        Dim den As Double = 1.6168504121556959 'Gamma((1 + beta) / 2) * beta * 2 ^ ((beta - 1) / 2)
        Dim sigma_u As Double = (num / den) ^ (1 / beta)
        Dim u As Double = (-3 + 6 * Rnd()) * sigma_u 'Random('Normal',0,sigma_u,n,m);
        Dim v As Double = -3 + 6 * Rnd() ' Random('Normal',0,1,n,m);
        Return u / (Math.Abs(v) ^ (1 / beta)) * 0.1

    End Function
    Private Function Lognpdf(ByVal x As Double, ByVal mu As Double, ByVal sigma As Double) As Double
        Return 1 / (x * sigma * Math.Sqrt(2 * Math.PI)) * Math.Exp(-(Math.Log(x) - mu) ^ 2 / (2 * sigma ^ 2))
    End Function

    Private Sub GlobalBestCheck(ByVal MemberG As OptimizationStructure_.Member_, ByVal Iloop As Integer)
        If MemberG.PenalizedCost < BestValue Then
            Dim History As OptimizationStructure_.History_
            History.Iter = iter
            History.Penalty = MemberG.Penalty
            History.ILoop = Iloop
            History.Cost = MemberG.PenalizedCost
            Histories.Add(History)
            BestValue = MemberG.PenalizedCost
        End If
        If MemberG.PenalizedCost < GlobalBest.PenalizedCost And MemberG.Penalty = 0 Then
            GlobalBest = MemberG
            GlobalBest.DesignVariables = CType(MemberG.DesignVariables.Clone(), Integer())
            GlobalBestPrint = New List(Of String) From {
                "Global Best: " & CStr(GlobalBest.CostValue),
                "Group Name   Variable Name"
            }
            If FormInfo.OptInfo.TestWithMath = True Then
                For i = 0 To Ub.Count - 1
                    GlobalBestPrint.Add("Variable " & CStr(i) & ": " & CStr(MemberG.DesignVariables(i)))
                Next
            Else


                For i = 0 To SAP2000Class.SteelFrameDesignGroupIDs.Count - 1
                    Dim isec As Integer = SAP2000Class.SteelFrameDesignGroupIDs(i)
                    GlobalBestPrint.Add(SAP2000Class.Groups(isec).GroupName & ": " & SAP2000Class.DescribeVariable(i, GlobalBest.DesignVariables(i)))
                Next
            End If
        End If
    End Sub
    Public Sub ClearDuplicates(ByRef ret As Integer)
        'Members are equal if their design variables are equal (Distinct() compared array references)
        Memory = Memory.GroupBy(Function(c) String.Join(",", c.DesignVariables)).Select(Function(g) g.First()).ToList()
        For i = Memory.Count To FormInfo.OptInfo.MemorySize - 1
            Dim Member As New OptimizationStructure_.Member_
            RandomGenerate(Member, ILoop, ret)
            If ret <> 0 Then : LogError("Problem occured in :RandomGenerate") : Exit Sub : End If
            Memory.Add(Member)
        Next i
    End Sub
    'Uniform random integer in [Lb, Ub] (upper bound included)
    Private Function RandomVariable(ByVal i As Integer) As Integer
        Return Math.Min(Lb(i) + CInt(Int((Ub(i) - Lb(i) + 1) * Rnd())), Ub(i))
    End Function
    '(Max - Average) / (Max - Min) of the penalized costs, fallback if all costs are equal
    Private Function CostSpread(ByVal Fallback As Double) As Double
        Dim Costs As List(Of Double) = Memory.Select(Function(c) c.PenalizedCost).Where(Function(c) Not Double.IsInfinity(c)).ToList()
        If Costs.Count = 0 OrElse Costs.Max() = Costs.Min() Then Return Fallback
        Return (Costs.Max() - Costs.Average()) / (Costs.Max() - Costs.Min())
    End Function
    Public Sub LogError(ByVal msg As String)
        If SAP2000Class IsNot Nothing Then SAP2000Class.Errorlogprint(msg)
    End Sub
    Private Sub UBLBCheck(ByRef Member As OptimizationStructure_.Member_)
        For i = 0 To Ub.Count - 1
            If Member.DesignVariables(i) > Ub(i) Then Member.DesignVariables(i) = Ub(i)
            If Member.DesignVariables(i) < Lb(i) Then Member.DesignVariables(i) = Lb(i)
        Next i
    End Sub
    Private Sub Eval(ByRef Member As OptimizationStructure_.Member_, ByRef Imem As Integer, ByRef ret As Integer)
        Call UBLBCheck(Member)
        If FormInfo.OptInfo.TestWithMath = True Then
            Call Math_Evaluate(Member)
        Else
            SAP2000Class.Evaluate(Member, iter, ret)
            If ret <> 0 Then : LogError("Problem occured in :Evaluate") : Exit Sub : End If
        End If
        Application.DoEvents()
        GlobalBestCheck(Member, ILoop)
        Dim CId = Imem
        Update = True
        MemoryUpdate(CId, Member, Update)
    End Sub
    Private Sub Math_Evaluate(ByRef Member As OptimizationStructure_.Member_)
        Dim Sect_Ind() As Integer = Member.DesignVariables
        Member.CostValue = (1 / 6.931 - (Sect_Ind(2) * Sect_Ind(1)) / (Sect_Ind(3) * Sect_Ind(0))) ^ 2
        Member.Penalty = 0
        Member.PenalizedCost = Member.CostValue
        iter += 1
    End Sub
    Public Sub Math_Init()
        Ub = {60, 60, 60, 60}
        Lb = {12, 12, 12, 12}
    End Sub
    Private Sub MemoryUpdate(ByRef ID As Integer, ByRef Member As OptimizationStructure_.Member_, ByRef Update As Boolean)
        If FormInfo.OptInfo.MemoryUpdateType = OptimizationStructure_.MemoryUpdateType_.NoGreedyRandom Or FormInfo.OptInfo.MemoryUpdateType = OptimizationStructure_.MemoryUpdateType_.GreedyRandom Then
            ID = Math.Floor(Rnd() * Memory.Count)
        ElseIf FormInfo.OptInfo.MemoryUpdateType = OptimizationStructure_.MemoryUpdateType_.NoGreedyWorst Or FormInfo.OptInfo.MemoryUpdateType = OptimizationStructure_.MemoryUpdateType_.GreedyWorst Then
            Dim Worst As Double = Memory.Max(Function(c) c.PenalizedCost)
            ID = Memory.FindIndex(Function(c) c.PenalizedCost = Worst)
        End If
        If FormInfo.OptInfo.MemoryUpdateType = OptimizationStructure_.MemoryUpdateType_.NoGreedyCurrent Or FormInfo.OptInfo.MemoryUpdateType = OptimizationStructure_.MemoryUpdateType_.NoGreedyRandom _
            Or FormInfo.OptInfo.MemoryUpdateType = OptimizationStructure_.MemoryUpdateType_.NoGreedyWorst Then Memory(ID) = Member
        If FormInfo.OptInfo.MemoryUpdateType = OptimizationStructure_.MemoryUpdateType_.GreedyCurrent Or FormInfo.OptInfo.MemoryUpdateType = OptimizationStructure_.MemoryUpdateType_.GreedyRandom _
            Or FormInfo.OptInfo.MemoryUpdateType = OptimizationStructure_.MemoryUpdateType_.GreedyWorst Then
            If Memory(ID).PenalizedCost > Member.PenalizedCost Then
                Memory(ID) = Member
            Else
                Update = False
            End If
        End If
    End Sub
    Public Sub Opt_Finalize()
        Dim ret As Integer = 0
        If GlobalBest.PenalizedCost = Double.PositiveInfinity Then
            LogError("Warning: no feasible design found")
            If SAP2000Class Is Nothing OrElse Not SAP2000Class.Quiet Then MsgBox("No feasible design was found.")
        ElseIf FormInfo.OptInfo.TestWithMath = True Then
            Math_Evaluate(GlobalBest)
        Else
            'Re-analyse the best design as it is (no repair) with all analysis cases, save as <file>_best.EDB
            ret = SAP2000Class.RestoreRunCases()
            If ret = 0 Then SAP2000Class.Evaluate(GlobalBest, iter, ret, applyRepair:=False)
            If ret <> 0 Then
                LogError("Problem occured in :Evaluate")
            Else
                'composite columns of the best design: ETABS composite column design (ETABS 20+)
                ret = SAP2000Class.VerifyCompositeWithETABS()
                If ret <> 0 Then LogError("Problem occured in :VerifyCompositeWithETABS")
                ETABSCompositeCheck = SAP2000Class.ETABS_print.ETABSCompositeCheck
                Dim f As String = FormInfo.FileList.ETABSFile
                ret = SAP2000Class.SapModel.File.Save(Path.Combine(Path.GetDirectoryName(f), Path.GetFileNameWithoutExtension(f) & "_best.EDB"))
                If (ret <> 0) Then LogError("Problem occured in :File.Save")
            End If
        End If
        If GlobalBest.PenalizedCost <> Double.PositiveInfinity Then
            Yazdir_Final()
            If ret = 0 Then LogError("Optimization Process has been completed successfully")
        End If
        If SAP2000Class IsNot Nothing Then SAP2000Class.Close(ret)
    End Sub

    Public Sub Backup_Write()
        Dim Results = New Class_Backup() With {
            .Memory = Memory,
            .FormInfo = FormInfo,
            .GlobalBest = GlobalBest,
            .GlobalBestPrint = GlobalBestPrint,
            .BestValue = BestValue,
            .Histories = Histories,
            .iter = iter,
            .ILoop = ILoop
        }
        Dim serializer As New XmlSerializer(GetType(Class_Backup))
        Using writer As New StreamWriter("BackUp.xml")
            serializer.Serialize(writer, Results)
        End Using
    End Sub
    Private Sub Yazdir_Final()
        Dim OptResults = New ClassFinal() With {
            .GlobalBest = GlobalBest,
            .Histories = Histories,
            .BestValue = BestValue,
            .GlobalBestPrint = GlobalBestPrint,
            .Seed = FormInfo.Seed,
            .ETABSCompositeCheck = ETABSCompositeCheck
        }
        Dim serializer As New XmlSerializer(GetType(ClassFinal))
        Using writer As New StreamWriter(FileList.OutputFile)
            serializer.Serialize(writer, OptResults)
        End Using
    End Sub
End Class
Public Class ClassFinal
    Public Seed As Integer
    Public GlobalBest As OptimizationStructure_.Member_
    Public BestValue As Double
    Public Histories As List(Of OptimizationStructure_.History_)
    Public GlobalBestPrint As List(Of String)
    'per composite group: "Group: ETABS PMM .., shear .. | internal .." (ETABS 20+)
    Public ETABSCompositeCheck As List(Of String)
End Class
Public Class Class_Backup
    Public Memory As List(Of OptimizationStructure_.Member_)
    Public FormInfo As MiscellaneousStructures.FormInfo_
    Public GlobalBest As OptimizationStructure_.Member_
    Public BestValue As Double
    Public Histories As List(Of OptimizationStructure_.History_)
    Public iter As Integer
    Public ILoop As Integer
    Public GlobalBestPrint As List(Of String)
End Class


