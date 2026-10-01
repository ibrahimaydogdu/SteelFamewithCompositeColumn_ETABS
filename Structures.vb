Imports System.Xml.Serialization


Public Structure Combinations_
    Public DesignSteelStrength As List(Of String)
    Public DesignSteelDeflection As List(Of String)
    Public AllCombos As List(Of String)
End Structure

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

End Class

' Frame, Point, Story and Group Structures
Public Class FramePointStoryGroupStructures_
    Public Structure Frame_
        Public FrameName As String
        Public FirstPointName As String
        Public SecondPointName As String
        Public FrameLength As Double
        Public FrameDirc As FrameDirc_
        Public GroupName As String
        Public LocalAxisAngle As Double
        Public FrameDesignProcedure As DesignProcedure_
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
        Other           '3D brace or zero length: no beam / column role
    End Enum

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
        Public GroupDesignProcedure As DesignProcedure_
        Public PMMRatio As Double
        Public DesignSecName As String
        Public DesignSecID As Integer
        Public IsComposite As Boolean       'encased composite column group (designed by CompositeColumn.vb)
        Public CompositeStrength As Double  'composite group: strength ratio (PMM / shear) only; PMMRatio also includes detailing
        Public CompositeDetailing As Double 'composite group: detailing ratio (As >= 1 % Ag, rho_sr >= 0.4 %)
    End Structure

    'Joint displacements of the output cases / combos (one entry per result)
    Public Structure LoadCaseDisp_
        Public U1 As List(Of Double)
        Public U2 As List(Of Double)
    End Structure

End Class

' Miscellaneous Structures
Public Class MiscellaneousStructures
    Public Structure GeoCons_
        Public CtoCList As List(Of String())    '{UpperColumnGroup, LowerColumnGroup}
        Public BtoCList As List(Of String())    '{ColumnGroup, BeamGroup, "Flange"|"Depth"}
    End Structure


    Public Structure FormInfo_
        Public FileList As FileList_
        Public FrameInfo As FrameInfo_
        Public OptInfo As OptimizationStructure_.OptInfo_
        Public TimerInfo As TimerInfo
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
        Public SkipCtoC As Boolean              'no column-to-column geometric constraint (form: "Column to Column" unchecked)
        Public SkipBtoC As Boolean              'no beam-to-column geometric constraint
        Public PDelta As Boolean                'P-Delta analysis in the working copy (old backups: model as it is)
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
        LateralCasesOnly = 2    'pure lateral load cases (wind / earthquake patterns only): service level, unfactored
    End Enum

    Public Structure FileList_
        Public ETABSFile As String
        Public OutputFile As String
    End Structure

    Public Structure TimerInfo
        Public StartTime As String
        Public startDate As Date
    End Structure
    Public Structure FrameInfo_
        Public TopStoryDriftR As Double
        Public InterStoryDriftR As Double
        Public SteelDesignCode As String
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
