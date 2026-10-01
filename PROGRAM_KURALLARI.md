# PROGRAM YAPISI VE KODLAMA KURALLARI

Bu dosya, programı geliştirecek kişiler ve AI kod asistanları içindir. Koda dokunmadan önce okunmalıdır.
Değişiklik geçmişi: `DEGISIKLIKLER.md`. Kullanım: `KULLANIM_KILAVUZU.md`. Kompozit formüller: `AISC360_16_Composite_Column_Rules.md`, `AISC360_22_Composite_Column_Rules.md`.

## 1. Ortam
- VB.NET, .NET Framework 4.7.2, WinForms; eski tip `.vbproj`. `Option Explicit On`, `Option Strict Off`, `Option Infer On`.
- ETABS OAPI (`ETABSv1.dll`): **ETABS 22** (varsayılan) ve ETABS 19 desteklenir.
  - `.vbproj` içindeki `ETABSDir` özelliği ETABS 22 kuruluysa onu, değilse ETABS 19'u seçer (`/p:ETABSDir=...` ile değiştirilebilir).
  - ETABS 22 API'si .NET Standard 2.0'dır. `netstandard` referansı ve ETABS klasöründeki `Microsoft.Win32.Registry.dll` gerekir (net461 facade; exe yanına kopyalanır).
  - Program, derlendiği `ETABSv1.dll` ile aynı sürümdeki ETABS'e bağlanmalıdır. v22 DLL'i ile ETABS 19 denenmedi.
- Yollar `App.config` > `appSettings` içinde tanımlıdır. Ortam değişkeni yalnızca yedek olarak okunur.
  - `ETABSProgramPath` bulunamazsa kurulu en yeni ETABS kullanılır.
  - Kütüphane dosyası bulunamazsa aynı dosya adı o ETABS'in `Property Libraries` klasöründe aranır. İki durumda da günlüğe uyarı yazılır.
- ETABS 22'de kaydedilen model ETABS 19'da açılmaz.
- **Birimler:** program ETABS'i `kN_mm_C` birimine alır. Tüm hesaplar kN–mm'dir (gerilme kN/mm²; 1 MPa = 0,001). Kesit kütüphanesi mm olmalıdır.
- VB büyük/küçük harf duyarsızdır: aynı kapsamda `F` ve `f`, `MB` ve `Mb` **aynı değişkendir**.

## 2. Dosyalar ve sorumluluklar
| Dosya | Sorumluluk |
|---|---|
| `MainForm.vb` / `.Designer.vb` | Form girdisi → `FormInfo_`, doğrulama (`Control`), tohum, ana döngü, yedek okuma, Check Structure |
| `OptimizationClass.vb` | Metasezgiseller (HS, BBO, Whale, Dandelion), bellek güncelleme, global en iyi, yedek ve sonuç XML'i. **ETABS'e doğrudan erişmez.** |
| `ETABSClass.vb` (`ETABS_Class`) | ETABS ile ilgili her şey: modeli okuma, sınırlar, kesit atama, analiz, kısıtlar, ceza, maliyet, kompozit entegrasyonu |
| `CompositeColumn.vb` | Saf AISC 360-16 / 360-22 kompozit hesabı (`CompositeCode_`). **ETABS'e bağımlı değildir**, bağımsız test edilebilir. |
| `Structures.vb` | Veri yapıları (Structure / Enum). Yedek XML'e girdiği için alan adları değiştirilmemelidir. |
| `EncasedSections.xml` | Kompozit kolon ayarları ve birim maliyetler (`EncasedSettings_`) |

## 3. Temel veri modeli
- **Tasarım değişkeni** `Member_.DesignVariables(v)` = `WSections` listesindeki indeks. `WSections` alana (A) göre artan sıralıdır ve yalnızca `DESIGNATION = W` kesitlerini içerir.
- `v` (değişken indeksi) ≠ grup indeksi:
  - Değişkenden gruba: `SteelFrameDesignGroupIDs(v)`.
  - Grup adından değişkene: `VarIndex(GroupName)`.
  - `Sect_Ind(...)` dizisine **asla grup indeksiyle erişilmez**.
- Değişken olan gruplar: tüm üyeleri aynı tasarım prosedürüne sahip ve prosedürü *Steel Frame Design* olan çerçeve grupları.
- Kompozit grup (`Group_.IsComposite`): kompozit modda, tüm üyeleri düşey (Z) olan değişken gruplar.
- Arama sözlükleri (`PointIndex`, `FrameIndex`, `GroupIndex`, `VarIndex`) başlangıçta kurulur. Döngü içinde `ToList().FindIndex` gibi doğrusal aramalar kullanılmaz.

## 4. Değerlendirme akışı (değiştirirken korunmalı)
```
Evaluate(Member, applyRepair)
 └ SetAndAnalyze(Sect_Ind, geometri düzeltmesi)   E1 geometri → E2 kesit ata → E3 kaydet + analiz + durum kontrolü
 └ AnalysisFailed ise → Penalty = 10 (program durmaz)
 └ Penalty
     F_Evaluate_Drift  : F1 göreli öteleme → (düzeltme varsa F2 + yeniden analiz) → F3 tepe ötelemesi → (F4 + yeniden analiz)
     G_Evaluate_PMM    : G1 çelik tasarım + G1_2 kompozit kontrol → (G2 + yeniden analiz + F + G1)
     H geometrik       : kolon-kolon ve kiriş-kolon oranları
     ceza = tüm kısıtlar, SON analiz durumu üzerinden
 └ Cost = CostStProfile (çelik modda ağırlık, kompozit modda göreli maliyet)
 └ PenalizedCost = Cost · (1 + Penalty)^3
```
Kurallar:
- Her yeniden analizden sonra, cezada kullanılan kısıtlar **yeniden hesaplanmalıdır**. Eski sonuçla ceza hesaplanmaz.
- Düzeltme (repair) adımları yalnızca `applyRepair = True` ve Check Structure kapalıyken çalışır. Final değerlendirmesi ve Check Structure düzeltme yapmaz.
- Düzeltme adımları `Sect_Ind` dizisini **yerinde** değiştirir; bu değişiklik bireye geri yazılır (Lamarck yaklaşımı). Adımlar değişkeni `[Lb, Ub]` aralığında tutar (`StepVariable`).
- API çağrıları `ret` döndürür; `ret <> 0` ise `Errorlogprint` ile kaydedilip yukarıya iletilir. `Stop`, `MsgBox` veya yakalanmamış istisna kullanılmaz; `MsgBox` yalnızca `Close` ve formda kullanılır.
- Tasarımın kötü olmasından kaynaklanan durumlar (analizin tamamlanmaması, tasarım sonucu olmaması) **hata değil, ceza** olarak ele alınır (`AnalysisFailed`).
- Sonuç okumadan önce çıktı seçimi yapılmalıdır:
  - Öteleme: `SelectOutputCases`.
  - Kompozit kuvvetler: `SelectOutput(dayanım kombinasyonları)`.
  - Modal, burkulma ve `~` ile başlayan durumlar yer değiştirme olarak okunmaz.

## 5. Kompozit kolon kuralları
- Formüller `CompositeColumn.vb` içindedir; ETABS kodu yalnızca kuvvetleri okuyup `CompositeMemberCheck.Ratio` fonksiyonunu çağırır.
- Yerel eksenler ETABS ile aynıdır: 2 ekseni derinlik (I kesitin gövdesi) yönündedir. M3 / V2 güçlü eksene, M2 / V3 zayıf eksene aittir. ETABS'te basınç eksenel kuvveti negatiftir.
- **Kesitin oluşturulması:**
  - ETABS 19 ve 22 API'si Section Designer ile kesit oluşturamaz (v22'de `cPropFrameSDShape` yalnızca `Get…` metotları içerir). Kompozit kesitler `SetGeneral` ile tanımlanır (`EC_<W adı>`, çelik malzeme).
  - Rijitlik: EI_eff / Es. Ağırlık ve kütle `SetModifiers` ile verilir (indeks 6 ve 7).
  - Her kesit bir koşuda bir kez oluşturulur (`CreatedSections`).
- Kompozit gruplar kesit atamasından sonra `SetDesignProcedure(…, 7)` ile ETABS çelik tasarımından çıkarılır. Yeniden açılışta `EC_` önekli kesitler yine değişken kabul edilir.
- Yeni bir kesit tipi (FilledBox, FilledPipe) optimizasyona bağlanacaksa:
  - Ayrı bir katalog ve değişken uzayı gerekir.
  - `Encased(SecID)`, `CostStProfile`, `DescribeVariable` ve `H_Evaluate_GeometricPenalty` genelleştirilmelidir.
- **Yönetmelik sürümü:** `CompositeSection.Code` (`AISC360_16` = 0, `AISC360_22` = 1).
  - Akış: formdaki *Composite code* → `FormInfo.CompositeCode` → `ETABS_Class.Encased()` kesite atar.
  - Sürüme bağlı kod yalnızca üç yerdedir:
    - `ConcreteShear` (I4-1, Kc)
    - `BalancePoint` + `InteractionRatio` (I5-1a/b)
    - `DesignTorsion` + `CompositeMemberCheck.Ratio` (H3-6)
  - Yeni bir fark eklenirse aynı yerlere `If Code = CompositeCode_.AISC360_22` ile eklenir. İki sürümün ortak formülleri çoğaltılmaz.
  - Gömülü kesitlerde iki sürüm **aynı sonucu** verir (testle doğrulandı). 360-16 modu önceki sürümün sonuçlarını birebir korumalıdır.
  - Eski yedeklerde bu alan yoktur; değer 0 = 360-16 olarak okunur.
- Rehber (`AISC360_16_…md`, `AISC360_22_…md`) ile AISC metni çelişirse yönetmelik esas alınır ve fark `DEGISIKLIKLER.md` dosyasına yazılır.
  - 360-22 metni elde değildir. Rehberden alınıp doğrulanamayan noktalar DEGISIKLIKLER.md'de "doğrulanmalı" olarak işaretlidir.
- Formüllerde değişiklik yapıldıktan sonra doğrulama testi tekrarlanmalıdır: gömülü W10x45 PSDM ile kapalı form arasındaki fark %1'in altında kalmalıdır (bkz. DEGISIKLIKLER.md).
- Regresyon testi: aynı kesit ve kuvvet setinde 360-16 modu, git'teki önceki `CompositeColumn.vb` ile birebir aynı çıktıyı vermelidir. Fark yalnızca bilinçli düzeltmelerden kaynaklanabilir.

## 6. Rastgelelik
- Tüm rastgele sayılar VB `Rnd()` (algoritmalar) ve `ETABS_Class.Rng` (geometri düzeltmesi) üzerinden üretilir. Her ikisi de `MainForm.SetRandomSeed` ile tohumlanır.
- Yeni kodda `New Random()` oluşturulmaz; aynı tohum aynı sonucu vermelidir.

## 7. Dosya ve çıktı kuralları
- **Çalışma kopyası:** girdi modeli hiçbir zaman değiştirilmez.
  - `InitilizeETABS` modeli `WorkFolder` klasörüne kopyalar (`App.config`; boşsa `%TEMP%\SteelFrameOpt`). Klasör adı `<model>_<yyyyMMdd_HHmmss>` biçimindedir. ETABS bu kopyayı (`WorkFile`) açar.
  - `E3_Analysis` her analizde **`WorkFile`** üzerine kaydeder. Model dosyasına kaydetme yalnızca `WorkFile` ve `_best.EDB` için yapılır.
  - Çalışma klasörü `Shutdown` içinde (`Close` çağırır), ETABS kapandıktan sonra silinir. Silinemezse uyarı yazılır, koşu bozulmaz.
  - ETABS'i kapatan her yol `Close` veya `Shutdown` üzerinden geçmelidir; aksi halde geçici klasör kalır.
- Çıktılar:
  - `ErrorLog.txt` (model klasöründe; `Info` / `Warning` / hata satırları)
  - `BackUp.xml` (çalışma klasöründe)
  - sonuç XML'i
  - `<model>_best.EDB` (girdi modelinin klasöründe)
  - Check Structure çıktısı: `<çıktı>.check.xml`
- `GlobalBestPrint` biçimi `"Grup: <W adı> [ek bilgi]"` şeklindedir. `Read_SectionID` ilk kelimeyi W adı olarak okur; biçim bozulmamalıdır.
- `Structures.vb` içindeki alanlar XML serileştirmesine girer. Alan silmek veya yeniden adlandırmak eski yedekleri bozar; yeni alan eklemek güvenlidir.

## 8. Derleme ve test (Visual Studio olmadan)
- Derleme kontrolü: Roslyn `dotnet "<sdk>/Roslyn/bincore/vbc.dll"`; referanslar .NETFramework v4.7.2 reference assemblies ve `ETABSv1.dll`. `dotnet msbuild` bu projede resx üretemez.
- ETABS testleri modelin **kopyası** üzerinde yapılır. Test sonunda `ApplicationExit` çağrılmalı ve arkada kalan `ETABS.exe` olmamalıdır.
- Çelik kodu:
  - ETABS 19 API'sinde `AISC 360-16` yoktur; `AISC 360-10` kullanılır.
  - ETABS 22, `AISC 360-22` ve `AISC 360-16` kodlarını kabul eder.
  - v22'de `SetCode` bazı adları normalleştirir (ör. `Eurocode 3-2005` → `EN 1993-1-1:2005`). Atanan kod `GetCode` ile okunup günlüğe yazılır. Geçersiz ad `ret = 1` döndürür.
- `vbc` derlemesinde referans yolları boşluk içerir; yanıt dosyasında (`@args.rsp`) tırnak içinde yazılmalıdır.
- Test kodundaki `Module` üye adları (`F`, `Mat`, `W` …) büyük/küçük harf duyarsızlığı yüzünden kaynak koddaki adlarla çakışabilir.
