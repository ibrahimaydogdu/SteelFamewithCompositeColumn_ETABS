Imports System.Xml.Serialization


Public Structure Combinations_
    Public DesignSteelStrength As List(Of String)
    Public DesignSteelDeflection As List(Of String)
    Public DesignCompStrength As List(Of String)
    Public DesignCompDeflection As List(Of String)
    Public AllCombos As List(Of String)
End Structure


Public Class MaterialStructures_
    ' Material Structures
    Public Structure SteelMaterial_
        Public Name As String
        Public YoungsModulus As Double
        Public YieldStrength As Double
        Public UltimateStrength As Double
        Public PoissonsRatio As Double
        Public Density As Double
    End Structure
    ' Concrete Material Structures  
    Public Structure ConcereteMaterial_
        Public Name As String
        Public YoungsModulus As Double
        Public CompressiveStrength As Double
        Public TensileStrength As Double
        Public PoissonsRatio As Double
        Public Density As Double
    End Structure
    ' Steel Material Structures 
    Public Structure RebarMaterial_
        Public Name As String
        Public YoungsModulus As Double
        Public YieldStrength As Double
        Public UltimateStrength As Double
        Public PoissonsRatio As Double
        Public Density As Double
    End Structure

End Class


' Section Structures
Public Class SectionStructures_
    <XmlRoot("STEEL_I_SECTION")>
    Public Structure STEEL_I_SECTION
        <XmlElement("SectionName")>
        Public Property SectionName As String

        <XmlElement("Designation")>
        Public Property Designation As String

        <XmlElement("Depth")>
        Public Property Depth As Double

        <XmlElement("FlangeLength")>
        Public Property FlangeLength As Double

        <XmlElement("FlangeThickness")>
        Public Property FlangeThickness As Double

        <XmlElement("WebThickness")>
        Public Property WebThickness As Double

        <XmlElement("KDES")>
        Public Property KDES As Double

        <XmlElement("Area")>
        Public Property Area As Double

        <XmlElement("Imajor")>
        Public Property Imajor As Double

        <XmlElement("PlasticModulusMajor")>
        Public Property PlasticModulusMajor As Double

        <XmlElement("ShearAreaMajor")>
        Public Property ShearAreaMajor As Double

        <XmlElement("Iminor")>
        Public Property Iminor As Double

        <XmlElement("PlasticModulusMinor")>
        Public Property PlasticModulusMinor As Double

        <XmlElement("ShearAreaMinor")>
        Public Property ShearAreaMinor As Double

        <XmlElement("TorsionalConstant")>
        Public Property TorsionalConstant As Double

        <XmlElement("SectionModulusMajorPos")>
        Public Property SectionModulusMajorPos As Double

        <XmlElement("SectionModulusMajorNeg")>
        Public Property SectionModulusMajorNeg As Double

        <XmlElement("SectionModulusMinorPos")>
        Public Property SectionModulusMinorPos As Double

        <XmlElement("SectionModulusMinorNeg")>
        Public Property SectionModulusMinorNeg As Double

        <XmlElement("RadiusofGyrationMajor")>
        Public Property RadiusofGyrationMajor As Double

        <XmlElement("RadiusofGyrationMinor")>
        Public Property RadiusofGyrationMinor As Double
    End Structure

    Public Structure RectangularencasedISection_
        Public SteelMaterial As MaterialStructures_.SteelMaterial_
        Public ConcreteMaterial As MaterialStructures_.ConcereteMaterial_
        Public EmbaddedISection As STEEL_I_SECTION
        Public Dept As Double
        Public Width As Double
        Public Rebar As Rebar_
    End Structure

    Public Structure Rebar_
        Dim RebarName As String
        Dim MatPropLong As String
        Dim MatPropConfine As String
        Dim Pattern As Integer
        Dim ConfineType As Integer
        Dim Cover As Double
        Dim NumberCBars As Integer 'This item applies to a circular rebar configuration, Pattern = 2. It is the total number of longitudinal reinforcing bars in the column.
        Dim NumberR3Bars As Integer 'This item applies to a rectangular rebar configuration, Pattern = 1. It is the number of longitudinal bars (including the corner bar) on each face 
        Dim NumberR2Bars As Integer 'This item applies to a rectangular rebar configuration, Pattern = 1. It is the number of longitudinal bars (including the corner bar) on each face 
        Dim RebarSize As Double 'The rebar name for the longitudinal rebar in the column.
        Dim TieSize As String 'The rebar name for the confinement rebar in the column.
        Dim TieSpacingLongit As Double 'The longitudinal spacing of the confinement bars (ties). [L]
        Dim Number2DirTieBars As Integer 'This item applies to a rectangular reinforcing configuration, Pattern = 1. It is the number of confinement bars (tie legs) running in the local 2-axis direction of the column.
        Dim Number3DirTieBars As Integer 'This item applies to a rectangular reinforcing configuration, Pattern = 1. It is the number of confinement bars (tie legs) running in the local 3-axis direction of the column.
        Dim ToBeDesigned As Boolean 'If this item is True, the column longitudinal rebar is to be designed; otherwise it is to be checked.
    End Structure

End Class

' Frame, Point, Story and Group Structures
Public Class FramePointStoryGroupStructures_
    Public Structure Frame_
        Public FrameName As String
        Public FirstPointName As String
        Public SecondPointName As String
        Public FrameLenght As Double
        Public FrameDirc As FrameDirc_
        Public GroupName As String
        Public FrameSection As SectionStructures_.STEEL_I_SECTION
        Public LocalAxisAngle As Double
        Public FrameDesignProcedure As DesignProcedure_
        Public Frameforces As LoadCaseForces_
    End Structure

    Public Structure Point_
        Public PointName As String
        Public Xcoord As Double
        Public YCoord As Double
        Public Zcoord As Double
        Public PointDisp As LoadCaseDisp_
    End Structure

    Public Structure Story_
        Public StoryName As String
        Public StoryPointNames() As String
        Public StoryFrames() As Frame_
        Public StoryLevel As Double
        Public InterStoryDriftLimit As Double
        Public InterStoryDriftX As Double
        Public InterStoryDriftY As Double
        Public InterStoryDPenaltyX As Double
        Public InterStoryDPenaltyY As Double
    End Structure
    Public Enum FrameDirc_
        X = 0
        Y
        Z
        DiagonalXZ
        DiagonalYZ
        DiagonalXY
    End Enum
    Public Structure Area_
        Public AreaName As String
        Public GroupName As String
        Public Area As Double
    End Structure

    Public Enum ObjectType_
        Point = 1
        Frame
        Cable
        Tendon
        Area
        Solid
        Link
    End Enum

    Public Enum DesignProcedure_
        Programdetermined = 0
        SteelFrameDesign = 1
        ConcreteFrameDesign = 2
        CompositeBeamDesign = 3
        SteelJoistDesign = 4
        NoDesign = 7
        CompositeColumnDesign = 13
    End Enum
    Public Structure Group_
        Public GroupName As String
        Public GroupObjectNames() As String
        Public GroupObjectTypes() As ObjectType_
        Public GroupLength As Double
        Public GroupSection As SectionStructures_.STEEL_I_SECTION
        Public GroupEncasedSection As SectionStructures_.RectangularencasedISection_
        Public GroupDesignPocedure As DesignProcedure_
        Public PMMRatio As Double
        Public DesignSecName As String
        Public DesignSecID As Integer
        Public IsComposite As Boolean       'encased composite column group (designed by CompositeColumn.vb)
        Public CompositeStrength As Double  'composite group: strength ratio (PMM / shear) only; PMMRatio also includes detailing
        Public CompositeDetailing As Double 'composite group: detailing ratio (As >= 1 % Ag, rho_sr >= 0.4 %)
    End Structure

    Public Structure LoadCaseDisp_
        Public LoadCaseName As List(Of String)
        Public U1 As List(Of Double)
        Public U2 As List(Of Double)
        Public U3 As List(Of Double)
        Public R1 As List(Of Double)
        Public R2 As List(Of Double)
        Public R3 As List(Of Double)
    End Structure

    Public Structure LoadCaseForces_
        Public NumberResults As Integer
        Public Obj() As String
        Public ObjSta() As Double
        Public Elm() As String
        Public ElmSta() As Double
        Public LoadCase() As String
        Public StepType() As String
        Public StepNum() As Double
        Public P() As Double
        Public V2() As Double
        Public V3() As Double
        Public T() As Double
        Public M2() As Double
        Public M3() As Double
    End Structure


End Class



' Miscellaneous Structures
Public Class MiscellaneousStructures
    Public Structure GeoCons_
        Public CtoCList As List(Of String())    '{UpperColumnGroup, LowerColumnGroup}
        Public BtoCList As List(Of String())    '{ColumnGroup, BeamGroup, "Flange"|"Depth"}
    End Structure
    Public Structure Numbers_
        Public NumberofMembers As Integer
        Public NumberofPoints As Integer
        Public NumberofGroups As Integer
        Public NumberofStories As Integer
        Public NumberofSteelFrameDesignGroups As Integer
        Public NumberofCompositeBeamDesignGroups As Integer
    End Structure


    Public Structure FormInfo_
        Public FileList As FileList_
        Public FrameInfo As FrameInfo_
        Public OptInfo As OptimizationStructure_.OptInfo_
        Public TimerInfo As TimerInfo
        Public BackUp As Boolean
        Public HideETABS As Boolean
        Public CheckStructure As Boolean
        Public CompositeColumns As Boolean  'column groups are designed as encased composite columns
        Public AutoCombos As Boolean        'create default design combos if the model has none
        Public DriftComboMode As DriftComboMode_
        Public Seed As Integer              'random seed of the run
        Public CompositeCode As CompositeCode_  'edition of the composite column check (old backups: 360-16)
        Public RepairMode As RepairMode_        'old backups: sequential (one re-analysis per repair step)
        Public UseCache As Boolean              'reuse the result of a design vector evaluated before
        Public SkipUnusedCases As Boolean       'do not run analysis cases that no design/drift check uses
        Public Costs As UnitCosts_              'relative unit costs of the composite objective (all 0 = EncasedSections.xml)
    End Structure

    'Relative unit costs (composite mode): steel and rebar per kN, concrete per m³, formwork per m²
    Public Structure UnitCosts_
        Public Steel As Double
        Public Rebar As Double
        Public Concrete As Double
        Public Formwork As Double
        Public ReadOnly Property IsSet As Boolean
            Get
                Return Steel > 0 OrElse Rebar > 0 OrElse Concrete > 0 OrElse Formwork > 0
            End Get
        End Property
    End Structure

    Public Enum RepairMode_
        Sequential = 0      'drift (F2) -> re-analysis -> top drift (F4) -> re-analysis -> PMM (G2) -> re-analysis
        Combined = 1        'all repair steps from one analysis, one re-analysis
    End Enum

    Public Enum DriftComboMode_
        AllCasesAndCombos = 0
        LateralOnly = 1
    End Enum

    Public Structure FileList_
        Public ETABSFile As String
        Public OutputFile As String
    End Structure

    Public Structure TimerInfo
        Public StartTime As String
        Public FinishTime As String
        Public TotalTime As String
        Public startDate As Date
    End Structure
    Public Structure FrameInfo_
        Public DispLimit As Double
        Public TopStoryDriftR As Double
        Public InterStoryDriftR As Double
        Public SteelDesignCode As String
        Public CompositeBeamDesignCode As String
    End Structure
End Class




Public Class OptimizationStructure_
    Public Structure OptInfo_
        Public MaxFuncEvaluation As Double
        Public MemorySize As Integer
        Public MemoryUpdateType As MemoryUpdateType_
        Public ClearDuplicates As Boolean
        Public HarmonySearch As HarmonySearch_
        Public BioGeography As BioGeography_
        Public OptimizationMethod As OptMethod_
        Public LevyFlight As Boolean
        Public TestWithMath As Boolean
    End Structure
    Public Structure HarmonySearch_
        Dim PAR As Double
        Dim HMCR As Double
        Dim PARChangeType As PAR_HMCR_ChangeType_
        Dim HMCRChangeType As PAR_HMCR_ChangeType_
        Dim ParVec() As Double
        Dim HMCRVec() As Double
    End Structure
    Public Structure BioGeography_
        Dim MutationRate As Double
        Dim Mu() As Double
        Dim Lamda() As Double
    End Structure
    Public Structure TimerInfo
        Public StartTime As String
        Public FinishTime As String
        Public TotalTime As String
        Public startDate As Date
    End Structure
    Public Structure ParameterGeneral_
        Public MaxFuncEvaluation As Double
        Public MemorySize As Integer
        Public MemoryUpdateType As MemoryUpdateType_
        Public ClearDuplicates As Boolean
        Public MethodName As OptMethod_
    End Structure
    Public Enum OptMethod_
        HarmornySearch = 0
        BioGBasedO = 1
        WhaleOpt = 2
        DandelionOpt = 3
    End Enum
    Public Enum MemoryUpdateType_
        NoGreedyCurrent = 0
        NoGreedyRandom = 1
        NoGreedyWorst = 2
        GreedyCurrent = 3
        GreedyRandom = 4
        GreedyWorst = 5
    End Enum
    Public Enum PAR_HMCR_ChangeType_
        IStatic = 0
        Dynamic = 1
        Adaptive = 2
    End Enum
    Public Structure Member_
        Public DesignVariables() As Integer
        Public CostValue As Double
        Public Penalty As Double
        Public PenalizedCost As Double
    End Structure
    Public Structure History_
        Public Iter As Integer
        Public ILoop As Integer
        Public Cost As Double
        Public Penalty As Double
    End Structure
End Class



'Public Enum eFramePropType_
'    I = 1
'    Channel = 2
'    T = 3
'    Angle = 4
'    DblAngle = 5
'    Box = 6
'    Pipe = 7
'    Rectangular = 8
'    Circle = 9
'    General = 10
'    DbChannel = 11
'    Auto = 12
'    SD = 13
'    Variable = 14
'    Joist = 15
'    Bridge = 16
'    Cold_C = 17
'    Cold_2C = 18
'    Cold_Z = 19
'    Cold_L = 20
'    Cold_2L = 21
'    Cold_Hat = 22
'    BuiltupICoverplate = 23
'    PCCGirderI = 24
'    PCCGirderU = 25
'    BuiltupIHybrid = 26
'    BuiltupUHybrid = 27
'    Concrete_L = 28
'    FilledTube = 29
'    FilledPipe = 30
'    EncasedRectangle = 31
'    EncasedCircle = 32
'    BucklingRestrainedBrace = 33
'    CoreBrace_BRB = 34
'    ConcreteTee = 35
'    ConcreteBox = 36
'    ConcretePipe = 37
'    ConcreteCross = 38
'    SteelPlate = 39
'    SteelRod = 40
'    PCCGirderSuperT = 41
'    Cold_Box = 42
'    Cold_I = 43
'    Cold_Pipe = 44
'    Cold_T = 45
'    Trapezoidal = 46
'End Enum
'Public Class Writing
'    Public Structure Filenames
'        Public SAP2000fileName As String
'        Public OutputFileName As String
'    End Structure

'    Public Sub OutputWriting(ByRef Path As String)
'        Dim doc As New XmlDocument()
'        Dim root As XmlNode = doc.DocumentElement
'        Dim Filenames As New Writing.Filenames
'        'Create a new node.
'        Dim elem As XmlElement = doc.CreateElement("SAP2000fileName")
'        elem.InnerText = Filenames.SAP2000fileName
'        'Add the node to the document.
'        root.AppendChild(elem)

'        'Create a new node.
'        elem = doc.CreateElement("OutputFileName")
'        elem.InnerText = Filenames.OutputFileName
'        'Add the node to the document.
'        root.AppendChild(elem)
'    End Sub
'End Class