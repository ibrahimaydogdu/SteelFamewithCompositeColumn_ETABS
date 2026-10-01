# KULLANIM KILAVUZU

Kompozit kolonlu uzay çelik çerçevelerin optimum tasarımı (ETABS 22; ETABS 19 ile de çalışır).

## 1. Kurulum
1. ETABS 22 (veya ETABS 19) kurulu ve lisanslı olmalıdır.
   - Proje derlenirken ETABS 22 kuruluysa onun API'si (`ETABSv1.dll`), değilse ETABS 19'unki kullanılır. Program, derlendiği sürümdeki ETABS'e bağlanacak şekilde ayarlanmalıdır.
   - ETABS 22'de kaydedilen model ETABS 19'da açılmaz. ETABS 19 modelleri ETABS 22'de açılınca otomatik dönüştürülür.
2. `App.config` (derlenmiş hâli `FrameSap2000.exe.config`) içindeki ayarlar:

| Anahtar | Açıklama |
|---|---|
| `ETABSProgramPath` | `ETABS.exe` dosyasının yolu (varsayılan ETABS 22). Yol bulunamazsa kurulu en yeni ETABS kullanılır ve uyarı yazılır. |
| `SectionPropertyDataPath` | Kesit kütüphanesi. CSI formatında ve birimi **mm** olmalıdır (ETABS 22: `AISC16M.xml`, ETABS 19: `AISC14M.xml`). |
| `BeamAutoSelectList`, `ColumnAutoSelectList` | Başlangıç tasarımında kullanılan otomatik kesit listelerinin adları |
| `WorkFolder` | Modelin çalışma kopyalarının klasörü. Boş bırakılırsa `%TEMP%\SteelFrameOpt` kullanılır. |

3. Kompozit mod kullanılacaksa `EncasedSections.xml` dosyası exe ile aynı klasörde bulunmalıdır. Proje derlenince otomatik kopyalanır.

## 2. ETABS modelinin hazırlanması
Program seçilen modeli **değiştirmez**:
- Koşu başında model geçici bir çalışma klasörüne kopyalanır (`%TEMP%\SteelFrameOpt\<model>_<tarih_saat>`) ve tüm analizler orada yapılır.
- Analiz dosyaları OneDrive gibi senkronize klasörleri doldurmaz.
- Çalışma klasörü koşu sonunda silinir. Yolu `ErrorLog.txt` dosyasında `Info: working copy …` satırında yazar.
- En iyi tasarım yine orijinal modelin yanına `<model>_best.EDB` adıyla kaydedilir.

- **Gruplar:** Aynı kesiti alacak çubukları bir grupta toplayın. Her çubuk yalnızca bir gruba ("All" dışında) ait olmalıdır. Grup içindeki çubukların tasarım prosedürü aynı olmalıdır.
- **Tasarım değişkenleri:** Tasarım prosedürü *Steel Frame Design* olan gruplar değişken olur. Diğer gruplar (beton, No Design vb.) sabit kalır.
- **Kolonlar:** Kompozit modda, tüm üyeleri düşey olan çelik gruplar gömülü kompozit kolon olarak tasarlanır. Kolon ve kiriş gruplarını ayrı tutun.
- **Malzemeler:** `A992Fy50` çeliği modelde tanımlı olmalıdır. Kompozit modda `EncasedSections.xml` içinde adı verilen beton (ör. `4000Psi`) ve donatı (ör. `A615Gr60`) malzemeleri de gereklidir.
- **Yükler:** Yük desenlerinin tipleri doğru olmalıdır (Dead, Live, **Wind**, **Quake**). Otomatik kombinasyonlar ve yatay yük tespiti bu tiplere göre yapılır.
- **Yük kombinasyonları:**
  - Modelde çelik tasarımı için işaretli *dayanım* kombinasyonları varsa bunlar kullanılır.
  - Yoksa ve formda "Create default design combos" seçiliyse, program ETABS'in yönetmeliğe göre varsayılan kombinasyonlarını oluşturur (`DStlS…`, `DStlD…`).
  - Kombinasyon yoksa ve bu seçenek kapalıysa program hata vererek durur.
- **Otomatik kesit listeleri:**
  - Başlangıç sınırları için bütün çelik kirişlere `BeamSectionList`, kolonlara `ColumnSectionList` atanır ve ETABS tasarımı yapılır.
  - Listeler modelde yoksa kütüphanedeki **tüm W kesitleriyle otomatik oluşturulur**; ilk tasarım bu durumda uzun sürebilir.
  - Daha kısa bir liste kullanmak için listeyi ETABS'te kendiniz tanımlayın (Define > Section Properties > Frame Sections > Auto Select List). Ardından adını `App.config` dosyasına yazın. Örneğin 525M modelinde hazır bulunan `A-LatBm` / `A-LatCol` listeleri kullanılabilir.
- **Analiz:** Kompozit kolonlarda B2 = 1 kabul edilir (`EncasedSections.xml`). Bu nedenle analizde P-Delta etkisi olmalıdır (nonlineer statik ya da P-Delta seçeneği açık).

## 3. Form
**Genel sekme**
- ETABS dosyası (`*.EDB`) ve çıktı dosyası (`*.xml`) seçilir.
- *Hide ETABS*: ETABS penceresini gizler.
- *Load BackUp File*: `BackUp.xml` dosyasından kaldığı yerden devam eder.
- *Check Structure Only*: çıktı XML'indeki en iyi kesitleri düzeltme yapmadan kontrol eder. Sonuç `<çıktı>.check.xml` dosyasına yazılır.

**Structural Properties sekmesi**
- Öteleme sınırları (H/oran) ve çelik tasarım kodu (varsayılan `AISC 360-22`).
  - ETABS 22: `AISC 360-22` ve `AISC 360-16` kullanılabilir.
  - ETABS 19 API'si bu iki kodu kabul etmez; `AISC 360-10` seçin.
  - Atanan kod `ErrorLog.txt` dosyasına `Info: steel design code …` satırıyla yazılır.
- **Analysis / Composite Options** grubu:
  - *Encased composite columns*: kolon gruplarını gömülü kompozit kolon olarak tasarlar.
  - *Composite code*: kompozit kolon kontrolünün yönetmelik sürümü, `AISC 360-16` veya `AISC 360-22` (varsayılan). Çelik tasarım kodundan bağımsızdır.
    - Gömülü kolonlarda iki sürümün formülleri aynıdır; sonuç değişmez.
    - Farklar dolgulu kutu/boru kesitlerdedir (beton kesme katkısı, narin kesit etkileşimi, burulma). Bu kesitler henüz optimizasyona bağlı değildir.
    - Eski bir `BackUp.xml` dosyasından devam edilirse `AISC 360-16` kullanılır.
  - *Create default design combos if model has none*: kombinasyon yoksa otomatik oluşturur.
  - *Drift check combos*: öteleme kontrolünde kullanılacak sonuçlar.
    - "All cases and combos": modal, burkulma ve iç durumlar dışındaki tüm durum ve kombinasyonlar.
    - "Lateral (wind / earthquake) only": yalnızca rüzgâr veya deprem içeren kombinasyonlar; bunlar yoksa bu tür yük durumları.
    - Not: ETABS analizi yine tüm durumları çözer; kazanç sonuç okuma aşamasındadır.
  - *Random seed*: 0 girilirse her koşu farklı olur (saat bazlı). Pozitif bir sayı aynı koşuyu tekrarlar. Kullanılan tohum pencere başlığında, `ErrorLog.txt` dosyasında ve sonuç XML'inde yazar. Çoklu koşu için alanı 0 bırakıp programı tekrar çalıştırın.

**Optimization sekmesi:** yöntem (HS, BBO, Whale, Dandelion), bellek boyutu, en fazla analiz sayısı ve yöntem parametreleri.

## 4. Kompozit kolon ayarları (`EncasedSections.xml`)
Her W kesiti için bir gömülü kesit üretilir:
- H = d + 2·`ConcreteCover`, B = bf + 2·`ConcreteCover`; `DimensionRounding` değerine yukarı yuvarlanır, en az `MinDimension`.
- Donatı çevreye dizilir. Yüz başına çubuk sayısı `MinBarsPerFace` değerinden başlar ve ρsr ≥ %0,4 olana kadar (en fazla `MaxBarsPerFace`) artırılır. Çap `RebarDiameter`, pas payı (çubuk merkezine) `RebarCover`.
- `K22`, `K33`: burkulma boyu katsayıları. `B2`: yanal ötelemeli çerçeve büyütme katsayısı.
- `SteelUnitCost` (kN), `RebarUnitCost` (kN), `ConcreteUnitCost` (m³), `FormworkUnitCost` (m²): amaç fonksiyonundaki **göreli** birim maliyetlerdir.

> **Dikkat:** Varsayılan birim maliyetler yalnızca yer tutucudur. Çalışmanıza uygun fiyat oranlarını girin; optimum çözüm bu oranlara doğrudan bağlıdır.

Kontrol edilenler (AISC 360-16 / 360-22, LRFD; gömülü kesitte iki sürüm aynıdır):
- Eksenel basınç / çekme (I2)
- Plastik gerilme dağılımıyla eğilme (I3.3)
- B1 büyütmeli H1-1 etkileşimi
- Kesme (çelik profil)
- Detay sınırları (As ≥ %1 Ag, ρsr ≥ %0,4)

Kolonun oranı, tüm üyelerde ve kombinasyonlarda bu kontrollerin en büyüğüdür.

ETABS'te kesitler `EC_<W adı>` adında *General* kesit olarak görünür. Kesit notlarında beton ölçüsü ve donatı yazar. Rijitlikler dönüştürülmüş (EI_eff) değerlerdir, ağırlık gerçek değerdir. Bu kolonlar ETABS'te "No Design" olarak işaretlidir; tasarım sonuçları ETABS'te değil, programın çıktılarında yer alır.

## 5. Çıktılar
| Dosya | İçerik |
|---|---|
| Çıktı XML | En iyi çözüm, maliyet, geçmiş, tohum. Kompozit gruplar `W360X110 [EC 550x450 8D20]` biçiminde yazılır. |
| `<model>_best.EDB` | En iyi tasarımın ETABS modeli (orijinal modelin klasöründe) |
| Çalışma klasörü (`%TEMP%\SteelFrameOpt\…`) | Koşu süresince analiz dosyaları; koşu sonunda silinir |
| `ErrorLog.txt` (model klasörü) | Bilgi satırları (kullanılan kombinasyonlar, oluşturulan listeler, kompozit gruplar, malzemeler), uyarılar (tamamlanamayan analizler vb.) ve hatalar |
| `BackUp.xml` (program klasörü) | Her çevrimde güncellenen yedek |

## 6. Sık karşılaşılan durumlar
| Mesaj (`ErrorLog.txt`) | Neden / çözüm |
|---|---|
| `Info: ETABS 22.x.x (…)` | Bağlanılan ETABS sürümü ve yolu. |
| `ETABS program not found` / `Section property file not found` | `App.config` yollarını düzeltin. |
| `Warning: 'ETABSProgramPath' not found …, using …` | Ayardaki yol bulunamadı; kurulu en yeni ETABS kullanıldı. Ayarı düzeltin. |
| `DesignSteel.SetCode, code not available` | Formda ETABS API'sinin desteklediği bir kod seçin (ETABS 22: AISC 360-22; ETABS 19: AISC 360-10). |
| `No strength design combination` | Kombinasyon tanımlayın veya otomatik kombinasyon seçeneğini açın. |
| `No lateral (wind/earthquake) combination` | Yük desenlerinin tipini Wind/Quake yapın veya öteleme modunu "All" seçin. |
| `Warning: analysis not finished …` | Aday tasarım kararsız; ceza alır ve arama devam eder. Hata değildir. |
| `PropMaterial.GetOConcrete …` | `EncasedSections.xml` içindeki malzeme adı modelde yok. |
| `Design section … is not a W section` | Otomatik listede kütüphane dışı kesit var; o grup için orta kesit kullanılır. |
