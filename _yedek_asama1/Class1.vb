Imports System
Imports System.Collections.Generic
Imports System.Xml.Serialization
Imports DocumentFormat.OpenXml.Drawing.Charts
Imports ETABSv1
Imports FrameSap2000.MaterialStructures_

' Assuming these structures are defined in a separate file or module
' For example:
' Imports SectionStructures
' Imports MaterialStructures
' Imports FramePointStoryGroupStructures

' Placeholder for CompositeDesign module/class/namespace - UPDATED - Material properties removed
Module CompositeDesign
    Public Const pi As Double = Math.PI ' Define PI constant
End Module




Public Class FortranCodeTranslations
    Public SapModel As ETABSv1.cSapModel
    Sub encased_composite(ByRef ETABSClass As ETABS_Class)


        For Each Group In ETABSClass.Groups
            Dim EncasedSection As SectionStructures_.RectangularencasedISection_ = Group.GroupEncasedSection
            Dim Rebar As SectionStructures_.Rebar_ = EncasedSection.Rebar
            Dim currentSteelSection As SectionStructures_.STEEL_I_SECTION = ETABSClass.WSections.ToList().FirstOrDefault(Function(c) c.SectionName = EncasedSection.EmbaddedISection.SectionName)
            Dim h1 As Double = EncasedSection.Width ' Section width from structure
            Dim h2 As Double = EncasedSection.Dept ' Section depth from structure
            Dim n() As Integer
            ReDim n(Rebar.NumberR2Bars)
            n(0) = Rebar.NumberR3Bars : n(Rebar.NumberR2Bars) = Rebar.NumberR3Bars
            Dim fi As Double = Rebar.RebarSize
            If Rebar.NumberR3Bars > 2 Then
                For i = 1 To Rebar.NumberR2Bars - 2
                    n(i) = 2
                Next i
            End If

            'Material Properties from Structure
            Dim ConcreteMaterial As MaterialStructures_.ConcereteMaterial_ = EncasedSection.ConcreteMaterial
            Dim SteelMaterial As MaterialStructures_.SteelMaterial_ = EncasedSection.SteelMaterial
            Dim Fy As Double = SteelMaterial.YieldStrength
            Dim Fc As Double = ConcreteMaterial.CompressiveStrength
            Dim Fysr As Double = Rebar.MatPropLong ' Assuming Rebar_.MatPropLong holds rebar yield strength
            Dim Esr As Double = Rebar.MatPropLong ' Assuming Rebar_.MatPropLong holds rebar Young's modulus. If not, you might need to define RebarMaterial_ structure and access it.
            Dim Ec As Double = ConcreteMaterial.YoungsModulus
            Dim EL As Double = SteelMaterial.YoungsModulus

            'EncasedSection properties
            Dim As_steel As Double = currentSteelSection.Area
            Dim d As Double = currentSteelSection.Depth
            Dim tw As Double = currentSteelSection.WebThickness
            Dim bf As Double = currentSteelSection.FlangeLength
            Dim tf As Double = currentSteelSection.FlangeThickness
            Dim Ix As Double = currentSteelSection.Imajor
            Dim Iy As Double = currentSteelSection.Iminor
            Dim Zsx As Double = currentSteelSection.PlasticModulusMajor
            Dim Zsy As Double = currentSteelSection.PlasticModulusMinor

            'Rebar properties
            Dim Asr As Double = 0
            Dim Isrx As Double = 0
            Dim Isry As Double = 0
            Dim dh As Double = (h2 - EncasedSection.Rebar.Cover) / (Rebar.NumberR2Bars - 1) ' distance between rebars y dircection
            Dim db As Double = (h1 - EncasedSection.Rebar.Cover) / (Rebar.NumberR3Bars - 1) ' distance between rebars x dircection
            For i = 0 To Rebar.NumberR2Bars - 1
                Dim dy = i * dh - (h2 - EncasedSection.Rebar.Cover) / 2
                For j = 0 To n(i) - 1
                    Dim dx = j * dh - (h1 - EncasedSection.Rebar.Cover) / 2
                    Isrx += Math.PI * fi ^ 4 / 64.0 + Math.PI * fi ^ 2 * 0.25 * dy ^ 2
                    Asr += Math.PI * fi ^ 2 * 0.25
                    Isry += Math.PI * fi ^ 4 / 64.0 + Math.PI * fi ^ 2 * 0.25 * dx ^ 2
                Next j
            Next i
            Dim Icx As Double = h1 * h2 ^ 3 / 12.0 - Ix - Isrx
            Dim Icy As Double = h1 ^ 3 * h2 / 12.0 - Iy - Isry
            Dim Ac As Double = h1 * h2 - As_steel - Asr

            Dim GFrames As List(Of FramePointStoryGroupStructures_.Frame_) = ETABSClass.Frames.ToList().Where(Function(c) c.GroupName = Group.GroupName)

            For Each frame As FramePointStoryGroupStructures_.Frame_ In GFrames
                Dim L As Double = frame.FrameLenght
                Dim FrameForces As FramePointStoryGroupStructures_.LoadCaseForces_ = frame.Frameforces

                'get frame forces
                Dim ret As Integer = -1
                ret = SapModel.Results.FrameForce(frame.FrameName, eItemTypeElm.ObjectElm, FrameForces.NumberResults, FrameForces.Obj, FrameForces.ObjSta, FrameForces.Elm, FrameForces.ElmSta, FrameForces.LoadCase, FrameForces.StepType, FrameForces.StepNum, FrameForces.P, FrameForces.V2, FrameForces.V3, FrameForces.T, FrameForces.M2, FrameForces.M3)

                Dim uniqueLoadCases As List(Of String) = Nothing
                Try
                    uniqueLoadCases = FrameForces.LoadCase.Distinct().ToList()
                Catch ex As Exception
                    Console.WriteLine($"Error finding unique load cases for {frame.FrameName}: {ex.Message}")
                    Continue For
                End Try

                If uniqueLoadCases Is Nothing OrElse uniqueLoadCases.Count = 0 Then
                    Console.WriteLine($"Could not determine unique load cases for {frame.FrameName}.")
                    Continue For
                End If

                For Each currentLoadCase In uniqueLoadCases
                    ' Find indices corresponding to the current load case
                    Dim indicesForLoadCase = Enumerable.Range(0, FrameForces.NumberResults) _
                                                 .Where(Function(i) FrameForces.LoadCase(i) = currentLoadCase) _
                                                 .ToList() ' Using ToList might be slightly easier below
                    Dim PuI As Double = indicesForLoadCase.Select(Function(i) Math.Abs(FrameForces.P(i))).DefaultIfEmpty(0).Max()
                    Dim maxAbsV2 As Double = indicesForLoadCase.Select(Function(i) Math.Abs(FrameForces.V2(i))).DefaultIfEmpty(0).Max()
                    Dim maxAbsV3 As Double = indicesForLoadCase.Select(Function(i) Math.Abs(FrameForces.V3(i))).DefaultIfEmpty(0).Max()
                    Dim maxAbsT As Double = indicesForLoadCase.Select(Function(i) Math.Abs(FrameForces.T(i))).DefaultIfEmpty(0).Max()
                    Dim maxAbsM2 As Double = indicesForLoadCase.Select(Function(i) Math.Abs(FrameForces.M2(i))).DefaultIfEmpty(0).Max()
                    Dim maxAbsM3 As Double = indicesForLoadCase.Select(Function(i) Math.Abs(FrameForces.M3(i))).DefaultIfEmpty(0).Max()

                    'Compression Strenght
                    If PuI < 0 Then
                        compression_encased(Icx, Icy, Ix, Iy, Isrx, Isry, As_steel, Ac, Asr, Kx, Ky, L, Pn, Fy, Fc, Fysr, Esr, Ec, EL) ' Pass material properties
                        fiPn = 0.75 * Pn
                    Else
                        Ac = 0
                        Tension_Capacity(As_steel, Asr, Pn, Fy, Fysr)  ' Pass material properties
                        fiPn = 0.9 * Pn
                    End If
                Next currentLoadCase
            Next frame

            Next


        'Read and Calculate Material Properties
















        'Calculate Ultimate Moments and shear forces
        SGPU = 0
        SPE2Y = 0
        SPE2Z = 0
        For KJ = 1 To NumColumnsInstorey(Storey(i - 1) - 1) ' Adjusted index to 0-based, Storey and NumColumnsInstorey are still from Frame_Data placeholder - if still needed. Otherwise, replace with structure data if available.
            j = Storeyinfo(Storey(i - 1) - 1, KJ - 1) ' Adjusted index to 0-based, Storeyinfo is still from Frame_Data placeholder - if still needed. Otherwise, replace with structure data if available.
            SGPU = SGPU + Math.Abs(PU(j - 1, 1, IL - 1)) ' Adjusted index to 0-based, PU is still from Frame_Data placeholder - if still needed. Otherwise, replace with structure data if available.
            SPE2Y = SPE2Y + Math.PI ^ 2 * (EL * Ix + Ec * Icx) / (Kx * L) ^ 2
            SPE2Z = SPE2Z + Math.PI ^ 2 * (EL * Iy + Ec * Icy) / (Ky * L) ^ 2
        Next KJ
        PE1Z = Math.PI ^ 2 * (EL * Ix + Ec * Icx) / (Kx * L) ^ 2
        PE1Y = Math.PI ^ 2 * (EL * Iy + Ec * Icy) / (Ky * L) ^ 2
        CMZ = 0.6 - 0.4 * (MM1Z(1) / MM2Z(1)) ' Assuming index 1 as per fortran logic, MM1Z, MM2Z are still from Frame_Data placeholder - if still needed. Otherwise, replace with structure data if available.
        If MM1Z(1) = 0.0 And MM2Z(1) = 0.0 Then CMZ = 0.6
        B1Z = CMZ / (1 - Math.Abs(PuI) / PE1Z)
        If B1Z < 1.0 Then B1Z = 1.0
        B2Z = 1.0 / (1 - Math.Abs(SGPU) / SPE2Z)
        CMY = 0.6 - 0.4 * (MM1Y(1) / MM2Y(1)) ' Assuming index 1 as per fortran logic, MM1Y, MM2Y are still from Frame_Data placeholder - if still needed. Otherwise, replace with structure data if available.
        If MM1Y(1) = 0.0 And MM2Y(1) = 0.0 Then CMY = 0.6
        B1Y = CMY / (1 - Math.Abs(PuI) / PE1Y)
        If B1Y < 1 Then B1Y = 1
        B2Y = 1.0 / (1 - Math.Abs(SGPU) / SPE2Y)

        'Calculate nominal moments
        flexure_encased(h1, h2, d, tf, tw, bf, 0.05, Ac, Asr, As_steel, Zsx, Zsy, Mnx, Mny, Fy, Fc, Fysr) ' Pass material properties

        If Math.Abs(PuI) / fiPn >= 0.2 Then
            Ratio_PMM = Math.Abs(PuI) / fiPn + 8.0 / 9.0 * (Math.Abs(Mux) / (0.9 * Mnx) + Math.Abs(Muy) / (0.9 * Mny))
        Else
            Ratio_PMM = Math.Abs(PuI) / (2.0 * fiPn) + Math.Abs(Mux) / (0.9 * Mnx) + Math.Abs(Muy) / (0.9 * Mny)
        End If
        shear_encased(Vnx, Vny, bf, tf, d, tw, Fy) ' Pass Fy
        Ratio_V = Math.Max(Math.Abs(Vux) / (0.9 * Vnx), Math.Abs(Vuy) / (0.9 * Vny))
        Ratio_T = 0.0

    End Sub

    Sub flexure_encased(ByVal h1 As Double, ByVal h2 As Double, ByVal d As Double, ByVal tf As Double, ByVal tw As Double, ByVal bf As Double, ByVal c As Double, ByVal Ac As Double, ByVal Asr As Double, ByVal As_steel As Double, ByVal Zsx As Double, ByVal Zsy As Double, ByRef Mpmajor As Double, ByRef Mpminor As Double, ByVal Fy As Double, ByVal Fc As Double, ByVal Fyr As Double) ' Added material properties as arguments

        Dim Asrs As Double, Zsx_local As Double, Zsy_local As Double, Asr_local As Double, As_steel_local As Double, h1_local As Double, h2_local As Double, d_local As Double, tf_local As Double, tw_local As Double, bf_local As Double, c_local As Double, hn As Double, Zc As Double, Zr As Double, MD As Double, Zsn As Double, Zcn As Double


        Zsx_local = Zsx
        Zsy_local = Zsy
        Asr_local = Asr
        As_steel_local = As_steel
        h1_local = h1
        h2_local = h2
        d_local = d
        tf_local = tf
        tw_local = tw
        bf_local = bf
        c_local = c


        '----------------------------------------------------------------------------
        '!Major axis
        '----------------------------------------------------------------------------
        '!Zsx:full x axis plastic section modulus of a steel I-shape
        '!Asr:  area of continuous longitudinal reinforcing bars
        '!Zr:full x axis plastic section modulus of reinforcement
        '!c: Paspayı
        '!h2: kiriş yüksekliği
        '!h1: kiriş genişliği
        'Fyr = CompositeDesign.Fysr 'Fyr is now passed as argument
        Asrs = Asr_local / 3.0
        hn = 0.5 * (0.85 * Fc * (Ac + Asrs) - 2 * Fyr * Asrs) / (0.85 * Fc * (h1_local - tw_local) + 2 * Fy * tw_local)
        Zsn = tw_local * hn ^ 2
        If hn > (0.5 * d_local - tf_local) Then
            hn = 0.5 * (0.85 * Fc * (Ac + As_steel_local - d_local * bf_local + Asrs) - 2 * Fy * (As_steel_local - d_local * bf_local) - 2 * Fyr * Asrs) / (0.85 * Fc * (h1_local - bf_local) + 2 * Fy * tf_local)
            Zsn = Zsx_local - bf_local * (0.5 * d_local - hn) * (0.5 * d_local + hn)
        Else
            GoTo label1
        End If
        If (hn > (0.5 * d_local - tf_local) And hn < 0.5 * d_local) Then
            GoTo label1
        Else
            Asrs = 2 * Asr_local / 3.0
            hn = 0.5 * (0.85 * Fc * (Ac + As_steel_local + Asrs) - 2 * Fy * As_steel_local - 2 * Fyr * Asrs) / (0.85 * Fc * h1_local)
            Zsn = Zsx_local
        End If

label1:
        Zr = (Asr_local - Asrs) * (0.5 * h2_local - c_local)
        Zc = 0.25 * h1_local * h2_local ^ 2 - Zsx_local - Zr

        MD = Zsx_local * Fy + Zr * Fyr + 0.5 * Zc * (0.85 * Fc)
        Zcn = h1_local * hn ^ 2 - Zsx_local - Zsn
        Mpmajor = MD - Zsn * Fy - 0.5 * Zcn * (0.85 * Fc)
        '----------------------------------------------------------------------------
        '!Minor axis
        '----------------------------------------------------------------------------
        hn = 0.5 * (0.85 * Fc * (Ac + As_steel_local - 2 * tf_local * bf_local) - 2 * Fy * (As_steel_local - 2 * tf_local * bf_local)) / (4 * tf_local * Fy + 0.85 * Fc * (h2_local - 2 * tf_local)) '!h1 ve h2 nin yerleri ters unutma
        Zsn = Zsy_local - 2 * tf_local * (0.5 * bf_local + hn) * (0.5 * bf_local - hn)
        If (hn > 0.5 * tw_local And hn <= 0.5 * bf_local) Then GoTo label2
        hn = 0.5 * (0.85 * Fc * (Ac + As_steel_local) - 2 * Fy * As_steel_local) / (0.85 * Fc * h2_local)
        Zsn = Zsy_local

label2:
        Zr = Asr_local * (0.5 * h1_local - c_local)
        Zc = 0.25 * h2_local * h1_local ^ 2 - Zsy_local - Zr
        MD = Zsy_local * Fy + Zr * Fyr + 0.5 * Zc * (0.85 * Fc)
        Zcn = h2_local * hn ^ 2 - Zsn
        Mpminor = MD - Zsn * Fy - 0.5 * Zcn * (0.85 * Fc)

    End Sub

    Sub shear_encased(ByRef Vnmajor As Double, ByRef Vnminor As Double, ByVal bf As Double, ByVal tf As Double, ByVal d As Double, ByVal tw As Double, ByVal Fy As Double) ' Added Fy as argument

        Dim Aw As Double, Cv As Double, h As Double, htw As Double, bf_local As Double, tf_local As Double, d_local As Double, tw_local As Double, kv As Double, Es As Double

        'Es = EL ' EL is now passed to encased_composite and shear_encased doesn't need EL. If needed, pass EL as argument too.
        Es = 200000 ' Assuming Es = 200000 MPa for steel, if EL is not passed and used.
        bf_local = bf
        tf_local = tf
        d_local = d
        tw_local = tw
        '!Major axis
        h = d_local - 2 * tf_local
        htw = h / tw_local
        kv = 5
        If (htw < 1.1 * Math.Sqrt(kv * Es / Fy)) Then
            Cv = 1
        ElseIf (htw > 1.1 * Math.Sqrt(kv * Es / Fy) And htw <= 1.37 * Math.Sqrt(kv * Es / Fy)) Then
            Cv = 1.1 * Math.Sqrt(kv * Es / Fy) / htw
        ElseIf (htw > 1.37 * Math.Sqrt(kv * Es / Fy)) Then
            Cv = 1.51 * Es * kv / (htw ^ 2 * Fy)
        End If
        Aw = d_local * tw_local
        Vnmajor = 0.6 * Fy * Aw * Cv
        '!Manor axis
        htw = 0.5 * bf_local / tf_local
        kv = 1.2
        If (htw < 1.1 * Math.Sqrt(kv * Es / Fy)) Then
            Cv = 1
        ElseIf (htw > 1.1 * Math.Sqrt(kv * Es / Fy) And htw <= 1.37 * Math.Sqrt(kv * Es / Fy)) Then
            Cv = 1.1 * Math.Sqrt(kv * Es / Fy) / htw
        ElseIf (htw > 1.37 * Math.Sqrt(kv * Es / Fy)) Then
            Cv = 1.51 * Es * kv / (htw ^ 2 * Fy)
        End If
        Aw = 2 * bf_local * tf_local
        Vnminor = 0.6 * Fy * Aw * Cv

    End Sub

    Sub compression_encased(ByVal Icx As Double, ByVal Icy As Double, ByVal Isx As Double, ByVal Isy As Double, ByVal Isrx As Double, ByVal Isry As Double, ByVal As_steel As Double, ByVal Ac As Double, ByVal Asr As Double, ByVal Kx As Double, ByVal Ky As Double, ByVal L As Double, ByRef Pn As Double, ByVal Fy As Double, ByVal Fc As Double, ByVal Fysr As Double, ByVal Esr As Double, ByVal Ec As Double, ByVal EL As Double) ' Added material properties as arguments

        Dim C1 As Double, EIeff As Double, Pe As Double, Pno As Double, Pnx As Double, Pny As Double

        C1 = 0.1 + 2 * (As_steel / (As_steel + Ac))
        If (C1 > 0.3) Then C1 = 0.3
        Pno = Fy * As_steel + Fysr * Asr + 0.85 * Fc * Ac
        'Es = EL ' Es is now passed as argument
        '! Xdirection
        EIeff = EL * Isx + 0.5 * Esr * Isrx + C1 * Ec * Icx
        Pe = Math.PI ^ 2 * EIeff / (Kx * L ^ 2)
        If (Pno / Pe <= 2.25) Then
            Pnx = (0.658 ^ (Pno / Pe)) * Pno
        Else
            Pnx = 0.877 * Pe
        End If

        '! Y direction
        EIeff = EL * Isy + 0.5 * Esr * Isry + C1 * Ec * Icy
        Pe = Math.PI ^ 2 * EIeff / (Ky * L ^ 2)
        If (Pno / Pe <= 2.25) Then
            Pny = (0.658 ^ (Pno / Pe)) * Pno
        Else
            Pny = 0.877 * Pe
        End If

        Pn = Math.Min(Pnx, Pny)

    End Sub

    ' Placeholder for Tension_Capacity subroutine, if needed.
    ' You'll need to implement this based on the Fortran code if it exists and is necessary.
    Sub Tension_Capacity(ByVal As_steel As Double, ByVal Asr As Double, ByRef Pn As Double, ByVal Fy As Double, ByVal Fysr As Double) ' Added material properties

        Pn = (Fy * As_steel + Fysr * Asr) 'Example simple tension capacity
    End Sub


End Class