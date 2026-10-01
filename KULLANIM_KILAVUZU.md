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
| `ServiceLateralFactor` | Programın oluşturduğu `SRV_<desen>` servis durumlarının yük katsayısı (varsayılan 1,0) |
| `SeismicDriftAmplification` | Servis öteleme modunda deprem durumlarının yerdeğiştirme büyütmesi (varsayılan 1,0; ASCE 7: Cd/Ie, TBDY 2018: R/I) |
| `CompositeStrengthFactor` | İç kompozit dayanım oranlarının çarpanı (varsayılan 1,0). Final ETABS kontrolünün önerdiği değer girilebilir. |

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
- **Analiz:** Kompozit kolonlarda B2 = 1 kabul edilir (`EncasedSections.xml`), bu yüzden analizde P-Delta etkisi olmalıdır. Formdaki *P-Delta analysis* seçeneği (varsayılan açık) bunu çalışma kopyasında sağlar (bkz. 3. bölüm); kendi modelinizi değiştirmez.

## 3. Form
**Genel sekme**
- ETABS dosyası (`*.EDB`) ve çıktı dosyası (`*.xml`) seçilir.
- *Hide ETABS*: ETABS penceresini gizler.
- *Load BackUp File*: `BackUp.xml` dosyasından kaldığı yerden devam eder.
- *Check Structure Only*: çıktı XML'indeki en iyi kesitleri düzeltme yapmadan kontrol eder. Sonuç `<çıktı>.check.xml` dosyasına yazılır.
- Koşu sırasında gösterilenler:
  - *Analyses*: yapılan / en fazla analiz sayısı.
  - *Best cost*: en iyi uygun (cezasız) çözümün maliyeti.
  - *Elapsed*, *Remaining*: geçen ve tahmini kalan süre.
  - *Av. analysis (s)*: analiz başına ortalama süre.
  - İlerleme çubuğu.
  - Listeler: en iyi çözümün kesitleri ve iyileşme geçmişi.

**Structural Properties sekmesi**
- *Number of Joint / Members / Group / Section*: modelden okunan sayılar (salt okunur). Group, tasarım değişkeni olan grup sayısıdır.
- Öteleme sınırları (H/oran, pozitif sayı) ve çelik tasarım kodu (varsayılan `AISC 360-22`).
- *Column to Column*: üst kat kolonu alt kattakinden büyük olamaz. *Beam to Column*: kiriş flanşı kolona sığmalı. Kutu işaretli değilse o kısıt kullanılmaz.
- Sayı kutularında ondalık ayırıcı olarak "." veya "," kullanılabilir; Windows dil ayarından bağımsızdır.
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
    - **"Lateral load cases (service, unfactored)"** (varsayılan): yalnızca yükleri tümüyle rüzgâr/deprem desenlerinden oluşan doğrusal statik durumlar ve response spectrum durumları.
      - Modelde böyle bir durum yoksa program, çalışma kopyasında her rüzgâr ve deprem yük deseni için katsayısız bir doğrusal durum (`SRV_<desen>`) oluşturur ve günlüğe yazar.
      - Durumların yük katsayısı `App.config` > `ServiceLateralFactor` ile ayarlanır (varsayılan 1,0). Örneğin ASCE 7'deki servis rüzgârı için 0,6 ya da 0,7 kullanılabilir.
      - Deprem durumları (response spectrum ve yalnızca deprem desenli doğrusal durumlar) elastik ötelemeyi verir. Bunlar `App.config` > `SeismicDriftAmplification` ile çarpılır (ASCE 7: Cd/Ie, TBDY 2018: R/I). Varsayılan 1,0'dır ve bu durumda günlüğe uyarı yazılır. Rüzgâr durumları büyütülmez.
      - Katsayısız (servis) öteleme kontrolü içindir. Modelde böyle durumlar tanımlı olmalıdır.
    - **Dikkat:** İlk iki mod, katsayılı dayanım kombinasyonlarını da (ör. 1,2D + 1,6W) kullanır. Öteleme sınırları genellikle servis yükleri içindir; bu modlarda öteleme fazla tahmin edilip kolonlar gereğinden büyük çıkabilir.
    - Not: ETABS analizi yine tüm durumları çözer; kazanç sonuç okuma aşamasındadır.
  - *Random seed*: 0 girilirse her koşu farklı olur (saat bazlı). Pozitif bir sayı aynı koşuyu tekrarlar. Kullanılan tohum pencere başlığında, `ErrorLog.txt` dosyasında ve sonuç XML'inde yazar. Çoklu koşu için alanı 0 bırakıp programı tekrar çalıştırın.
  - *P-Delta analysis (nonlinear cases + preset P-Delta)* (varsayılan açık): çalışma kopyasında nonlineer statik durumlara P-Delta geometrik nonlineerliği atanır.
    - Doğrusal durumlar için ön tanımlı P-Delta "Non-iterative Based on Mass" olarak ayarlanır (modelde başka bir yöntem tanımlıysa o korunur).
    - Değişiklikler günlükte `Info: P-Delta: …` satırıyla listelenir. Eski yedeklerden devam edilirse bu seçenek kapalı okunur.
  - *Skip analysis cases not used by design / drift checks* (varsayılan açık): dayanım ve sehim kombinasyonlarında ve öteleme kontrolünde kullanılmayan yük durumları çözülmez.
    - Bu durumların ön koşulları, Modal durumlar ve ETABS iç durumları (`~…`) her zaman çözülür.
    - Kapatılan durumlar `ErrorLog.txt` dosyasında listelenir.
    - En iyi tasarım (`_best.EDB`) tüm durumlarla yeniden analiz edilir.

**Optimization sekmesi**
- **General Parameters:** bellek boyutu (*Memory size*), en fazla analiz sayısı (*Max. analyses*), bellek güncelleme türü, yöntem (HS, BBO, Whale, Dandelion).
- **Evaluation:**
  - *Repair mode*: kısıt ihlallerinin düzeltilme şekli.
    - **Combined (faster)**, varsayılan: öteleme ve PMM düzeltmeleri tek analizin sonuçlarından birlikte yapılır, ardından bir yeniden analiz. 525M modelinde değerlendirme başına yaklaşık 26 s.
    - **Sequential (original)**: önceki yöntem; her düzeltme adımından sonra ayrı analiz (en fazla 6 analiz, yaklaşık 68 s).
    - İki mod farklı sonuçlar verir. Karşılaştırma yapılacak koşularda aynı mod kullanılmalıdır.
  - *Reuse results of repeated designs* (varsayılan açık): daha önce değerlendirilmiş bir tasarım tekrar üretilirse ETABS çağrılmaz, saklanan sonuç kullanılır.
    - Bu değerlendirmeler analiz sayısına eklenmez.
    - Art arda 20 çevrimde yeni bir tasarım değerlendirilmezse arama yakınsamış kabul edilir ve koşu biter.
- **HS / BBO parametreleri.**

## 4. Kompozit kolon ayarları (`EncasedSections.xml`)
Her W kesiti için bir gömülü kesit üretilir:
- H = d + 2·`ConcreteCover`, B = bf + 2·`ConcreteCover`; `DimensionRounding` değerine yukarı yuvarlanır, en az `MinDimension`.
- Donatı çevreye dizilir. Yüz başına çubuk sayısı `MinBarsPerFace` değerinden başlar ve ρsr ≥ %0,4 olana kadar (en fazla `MaxBarsPerFace`) artırılır. Çap `RebarDiameter`, pas payı (çubuk merkezine) `RebarCover`.
- `K22`, `K33`: burkulma boyu katsayıları. `B2`: yanal ötelemeli çerçeve büyütme katsayısı.
- `SteelUnitCost` (kN), `RebarUnitCost` (kN), `ConcreteUnitCost` (m³), `FormworkUnitCost` (m²): amaç fonksiyonundaki **göreli** birim maliyetlerin varsayılan değerleri.
- `FlexureMethod`: gömülü kesitte eğilme dayanımı yöntemi.
  - `StrainCompatibility` (varsayılan, AISC I1.2b): ETABS kompozit kolon tasarımıyla uyumlu.
  - `PlasticStress` (I1.2a): önceki yöntem. Zayıf eksende yaklaşık %19 yüksek Mn veriyordu.
- `TieDiameter`, `TieSpacing`: ETABS ile doğrulamada gömülü kesitin etriyeleri.

### Birim maliyetler (kullanıcı paneli)
Birim maliyetler formda girilir: **Structural Properties** sekmesi → **Composite Cost (relative unit prices)** grubu.

| Alan | Birim | Varsayılan |
|---|---|---|
| *Steel* | kN çelik başına | 1 |
| *Rebar* | kN donatı başına | 0,5 |
| *Concrete* | m³ beton başına | 0,6 |
| *Formwork* | m² kalıp başına (kolon çevresi × boy) | 0,15 |

Varsayılanların dayandığı birim fiyat varsayımları:
- uygulanmış yapısal çelik 2,0 $/kg (≈ 204 $/kN),
- donatı 1,0 $/kg (≈ 102 $/kN),
- yerine konmuş C30 beton 120 $/m³,
- kolon kalıbı 30 $/m².

Oranlar çelik = 1 alınarak bu fiyatlara bölünerek bulundu.

- Form açılırken alanlar `EncasedSections.xml` dosyasındaki değerlerle dolar. Koşuda formdaki değerler kullanılır ve `ErrorLog.txt` dosyasına `Info: unit costs (form) …` satırıyla yazılır.
- Değerler göreli olmalıdır; birimleri ve oranları çalışmanızın fiyatlarından türetin. Örneğin çelik 1 alınırsa diğerleri çeliğe oranla verilir.
- Değerler negatif olamaz ve en az biri sıfırdan büyük olmalıdır; aksi hâlde form uyarı verir.
- Değerler yedeğe (`BackUp.xml`) kaydedilir. Eski yedeklerde bu alan yoktur; o durumda `EncasedSections.xml` değerleri kullanılır.
- Amaç fonksiyonu: çelik modunda yalnızca çelik ağırlığı (kN). Kompozit modda çelik·*Steel* + donatı·*Rebar* + beton hacmi·*Concrete* + kalıp alanı·*Formwork*.

> **Dikkat:** Varsayılanlar yukarıdaki örnek fiyatlardan türetilmiştir. Optimum çözüm bu oranlara doğrudan bağlıdır; çalışmanızın (ülke, yıl) fiyat oranlarını formda girin.

Kontrol edilenler (AISC 360-16 / 360-22, LRFD; gömülü kesitte iki sürüm aynıdır):
- Eksenel basınç / çekme (I2)
- Plastik gerilme dağılımıyla eğilme (I3.3)
- B1 büyütmeli H1-1 etkileşimi
- Kesme (çelik profil)
- Detay sınırları (As ≥ %1 Ag, ρsr ≥ %0,4)

Kolonun oranı, tüm üyelerde ve kombinasyonlarda bu kontrollerin en büyüğüdür.

**ETABS ile doğrulama (ETABS 20+):** arama sırasında kolonlar hızlı iç çözücüyle kontrol edilir. Koşu sonunda en iyi tasarım ETABS'in kendi kompozit kolon tasarımıyla doğrulanır; *Check Structure* modunda da aynı doğrulama yapılır.
- Kolonlar gerçek gömülü kesitlere (Concrete Encasement Rectangle + donatı) çevrilir, model yeniden analiz edilir ve ETABS kompozit tasarımı formdaki *Composite code* ile çalışır.
- Her grup için ETABS PMM ve kesme oranı, iç çözücünün dayanım ve detay oranlarıyla yan yana yazılır:
  - `ErrorLog.txt`: `Info: ETABS composite check …`
  - sonuç XML'i: `ETABSCompositeCheck`
  - `.check.xml`
- `_best.EDB` gömülü kesitleri ve ETABS tasarım sonuçlarını içerir.
- Bu adım tüm kolonlar için birkaç dakika sürer (525M: yaklaşık 3 dakika).
- **Fark:** 525M modelinde ETABS oranları iç çözücüden %0,5–14 yüksek çıktı. Fark çoğunlukla zayıf eksen moment kapasitesinden gelir.
- **ETABS koruması:** ETABS oranı 1'i aşan kompozit grup otomatik olarak bir üst kesite çıkarılır. Tasarım yeniden analiz edilip ETABS ile yeniden doğrulanır (en fazla 3 adım).
  - Günlükte `Info: ETABS guard …` satırları görünür. `FinalCheck` ve `_best.EDB` korunmuş tasarımı içerir.
  - Aşan grup üst sınırdaysa uyarı yazılır.
- **Kalibrasyon:** günlükteki `ETABS / internal composite strength ratio (max)` satırı önerilen `CompositeStrengthFactor` değerini verir. Bu değeri `App.config` dosyasına yazarsanız arama ETABS ile uyumlu, güvenli tarafta yürür.

Arama sırasında ETABS'te kesitler `EC_<W adı>` adında *General* kesit olarak görünür. Kesit notlarında beton ölçüsü ve donatı yazar. Rijitlikler dönüştürülmüş (EI_eff) değerlerdir, ağırlık gerçek değerdir. Bu kolonlar ETABS'te "No Design" olarak işaretlidir; tasarım sonuçları ETABS'te değil, programın çıktılarında yer alır.

## 5. Çıktılar
| Dosya | İçerik |
|---|---|
| Çıktı XML | En iyi çözüm, maliyet, geçmiş, tohum. `FinalCheck`: en iyi tasarımın düzeltmesiz ve tüm durumlarla final analizi (maliyet, ceza). `FinalConstraints`: belirleyici kısıtlar (oran / sınır). `ETABSCompositeCheck`: ETABS kompozit kolon kontrolü. Kompozit gruplar `W360X110 [EC 550x450 8D20]` biçiminde yazılır. |
| `<model>_best.EDB` | En iyi tasarımın ETABS modeli (orijinal modelin klasöründe) |
| Çalışma klasörü (`%TEMP%\SteelFrameOpt\…`) | Koşu süresince analiz dosyaları; koşu sonunda silinir |
| `ErrorLog.txt` (model klasörü) | Satırlar `Info:`, `Warning:` veya `Error:` ile başlar. Bilgi satırları (kullanılan kombinasyonlar, oluşturulan listeler, kompozit gruplar, malzemeler, çözülmeyen yük durumları), uyarılar (tamamlanamayan analizler vb.) ve hatalar. Koşu sonunda `Info: timing …` satırında analiz, tasarım ve kesit atama süreleri ile önbellek isabet sayısı yer alır. |
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
