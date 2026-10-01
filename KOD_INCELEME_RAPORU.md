# Kod İnceleme Raporu (2026-10-01)

Tüm kaynak dosyalar A'dan Z'ye incelendi: ETABSClass, OptimizationClass, MainForm (+Designer), Structures, CompositeColumn, ApplicationEvents.
- Derleyici uyarıları da tarandı. Kullanılmayan yerel değişken uyarısı yok; 28 örtük tür dönüşümü uyarısı var.
- **Henüz hiçbir madde düzeltilmedi.** Bu dosya düzeltilecek işlerin listesidir.
- ✔ = elle doğrulandı. Satır numaraları yaklaşıktır, dosyalar değiştikçe kayar.

## Öncelik 1: Sonucu veya ETABS süresini doğrudan etkileyen hatalar

| # | Dosya | Bulgu | Etki |
|---|---|---|---|
| 1 | ETABSClass `F2`/`F4`/`G2`/`CombinedRepair` | Düzeltme adımı 0'a yuvarlanınca ya da değişken zaten `Ub`'deyken vektör değişmiyor; yine de yeniden analiz yapılıyor | Her boşa analiz yaklaşık 10 s + tasarım 4 s |
| 2 | ETABSClass `E1_Modifier_Geometric` | Geometri düzeltmesi kesiti tüm kütüphaneden seçiyor; `[Lb, Ub]` sınırını yok sayıyor ve kirişe rastgele (çoğu zaman çok hafif) kesit atıyor | Arama bozuluyor; sınır dışı değer belleğe yazılıyor |
| 3 ✔ | ETABSClass `VerifyCompositeWithETABS` | Kompozit grup yoksa `CompVars(0)` ve `Missing.First()` satırlarında çöküyor | Final adımında çökme; ETABS açık kalır |
| 4 ✔ | OptimizationClass `Opt_Finalize` | Doğrulama hatası `ret`, `File.Save` sonucu tarafından eziliyor | Hata kayboluyor, yine "completed successfully" yazılıyor |
| 5 | ETABSClass `Initilize_UBLB` | Ceza çok büyükse `Lb > Ub` (hatta `> N-1`) olabiliyor | `IndexOutOfRange` |
| 6 | ETABSClass `InitilizeCompositeMaterials` | f'c < 21 MPa ise 21 MPa'ya yükseltiliyor (I1.3 alt sınır) | Güvensiz taraf; hata verilmeli |
| 7 | ETABSClass `InitilizeFrames` | Sınıflandırılamayan (3B çapraz) çubuklar `X` kirişi sayılıyor | Kirişlere otomatik liste atanıyor, yanlış kiriş-kolon kısıtı |
| 8 | ETABSClass `Initilize_UBLB` | Hiçbir gruba ait olmayan çelik çubuklara otomatik liste atanıyor; arama boyunca o kesitte kalıyorlar | Kullanıcının verdiği kesit kayboluyor |
| 9 | MainForm (`IsValidNumber`/`CDbl`) | Sayılar Windows'un dil ayarına göre okunuyor. tr-TR'de "0.9" → 9, "0.0636" → 636 (bu makine en-US) | Türkçe Windows'ta yanlış parametreler |
| 10 | OptimizationClass HS | `ParVec`/`HMCRVec`, belleğin gerçekten güncellenen konumuna değil `Imem` konumuna yazılıyor | Uyarlanabilir PAR/HMCR yanlış |
| 11 | OptimizationClass `Opt_Finalize` | Düzeltmesiz final değerlendirmesi `GlobalBest`'in maliyet ve cezasını eziyor | Sonuç XML'inde cezalı bir "Global Best" görünebilir |
| 12 | MainForm `Read_SectionID` | Check Structure kesitleri grup adına göre değil **sırayla** eşliyor | Grup sırası farklı bir modelde yanlış kesit |

## Öncelik 2: Kullanıcıyı yanıltan form ve çıktılar

| # | Bulgu |
|---|---|
| 13 ✔ | `Column to Column`, `Beam to Column`, `Discard warnings` onay kutuları hiç okunmuyor; kısıtlar her zaman açık |
| 14 ✔ | `Disp. Limit (mm)` zorunlu ama hiçbir hesapta kullanılmıyor |
| 15 | `Number of Joint/Members/Group/Section` kutuları hiç doldurulmuyor |
| 16 | Check Structure çıktısında ceza ve maliyet yok (`.check.xml`) |
| 17 | `Errorlogprint` her satıra "Error message:" yazıyor (Info ve Warning satırlarına da) |
| 18 | Saat biçimi `hh:mm:ss` (12 saatlik, AM/PM yok); `HH:mm:ss` olmalı |
| 19 | `ETABS_Print.InterStoryDrift_Ratios` / `TopStoryDrift_Ratio` oran değil, mm tutuyor. `PMM_Ratios` sırası grup sırasıyla uyuşmuyor |
| 20 | Yakalanmayan istisnada ETABS ve geçici klasör ortada kalıyor (Try/Finally ve `UnhandledException` yok) |

## Öncelik 3: Algoritma kusurları

| # | Bulgu |
|---|---|
| 21 | HS perde ayarı ±0,01·(Ub−Lb). N≈280'de vakaların yaklaşık %40'ında 0 adım çıkıyor |
| 22 | Levy uçuşu aşağı yönlü kayık (`int1` yalnızca −2 veya −1). `RZD < 0.01` negatif değerlerin hepsini reddediyor. Normal dağılım yerine düzgün dağılım kullanılıyor |
| 23 | Dandelion: `rndn < 1.5` olasılığı %75 (orijinalde ≈%93). Ara değerler Integer'a yuvarlanıyor |

## Öncelik 4: Hız (ETABS çağrılarını azaltan)

| # | Bulgu |
|---|---|
| 24 | Ceza 0 olan sonuç, onarılmış vektörün anahtarıyla da önbelleğe yazılabilir |
| 25 | `G1_ConsPMM` grup başına `GetSummaryResults` çağırıyor; tek çağrı yeterli |
| 26 | `CompositeSectionID` her değerlendirmede `FrameObj.GetSection` çağırıyor; `Assigned()` bu bilgiyi zaten tutuyor |
| 27 | `E3` her analizde `GetRunCaseFlag` çağırıyor; değer yalnızca `SetRunCases` ile değişir |
| 28 | `SkipUnusedCases`, varsayılan öteleme modunda (All cases and combos) neredeyse hiç durum kapatamıyor |

## Öncelik 5: Ölü kod ve temizlik

- **Kullanılmayan öğeler:**
  - `encasedSections`, `EnsureCompositeSection` (tekil), `CostSlab`, `CostStud`.
  - `AttachToInstance` dalı.
  - `Close` içindeki `TotalTime`/`avtime` (hesaplanıp kullanılmıyor).
  - `StoryPointNames` (kat başına boşa API çağrısı), `InterStoryDriftX/Y`.
  - `PointDisp.LoadCaseName/U3/R1/R2/R3`.
  - `Imports System.Xml` (2 dosya), `OptimizationClass.BackUp`.
- **Structures.vb'de kullanılmayan tipler** (yedeğe girmez, silinebilir): `MaterialStructures_`, `RectangularencasedISection_`, `Rebar_`, `Area_`, `LoadCaseForces_`, `Numbers_`, `OptimizationStructure_.TimerInfo`, `ParameterGeneral_`, `Combinations_.DesignComp*`, yorum satırındaki SAP2000 blokları.
- **Yedeğe giren ama kullanılmayan alanlar:** `CompositeBeamDesignCode`, `DispLimit`. Silmek eski yedekleri bozmaz, yalnızca değer kaybolur. Yeniden adlandırmak bozar.
- **Adlandırma:**
  - SAP2000 kalıntıları: `SAP2000Class`, `HideSAP2000`, `saplocation`.
  - Yazım hataları: `FrameLenght`, `GroupDesignPocedure`, `Initilize*`, "occurend".
  - `HarmornySearch` ve `Lamda` yedeğe girdiği için **değiştirilmemeli**.
- **Tekrar eden tanımlar:**
  - Tasarım prosedürü sabitleri iki yerde (`NO_DESIGN = 7` ve `DesignProcedure_`).
  - Düzeltme adım formülleri iki yerde (`F2`/`F4`/`G2` ve `RepairSteps`).
  - `repair` koşulu iki yerde.
- **Sihirli sayılar:**
  - `COORD_TOL` (mm) açı toleransı olarak kullanılıyor; −90° ve 450° yakalanmıyor.
  - 0.05 (sınır kaydırma), `^3` (ceza), 2 (boşluk ≤ 0).
- **CompositeColumn.vb:**
  - Güçlü eksen kesmede Cv2 formülü kullanılıyor (Cv1 olmalı); haddelenmiş W'de nadiren etkili.
  - `Build`, `MaxBarsPerFace < 2` ise `Nothing` döndürüyor.
  - Fiber modelinde beton alanı biraz fazla çıkıyor.
