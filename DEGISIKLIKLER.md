# Değişiklik Kaydı

Kompozit kolonlu uzay çelik çerçeve optimizasyon programı (VB.NET, ETABS 22 / ETABS 19 OAPI).
Orijinal kaynak dosyaların yedeği: `_yedek_asama1/`. Aşama 2 sonrası durum git'te (`7e6b41f`).

---

## 2026-10-03 — Aşama 17: Başka kullanıcılar için hazırlık, küçük iyileştirmeler

### Kullanıcının 300 analizlik ABC testinin incelemesi (525M, çelik modu)
- Koşu sorunsuz tamamlandı: 307 analiz, 2 saat 38 dakika.
- Üç ETABS yeniden başlatması oldu; bellek yaklaşık 1000 MB'tan 700 MB'a indi.
- Final tasarım tüm kontrolleri sağlıyor. Aramanın en iyisi, final analizi ve Excel'deki maliyet toplamı aynı: 6378,45 kN.
- Maliyet 8590 kN'den 6378 kN'ye düştü. Son iyileşme 256. analizde olduğu için arama henüz yakınsamamıştı.
- Gözlemler (hata değil):
  - İlk sınır tasarımı 8 dakika sürdü.
  - `ErrorLog.txt` bütün koşuları aynı dosyada biriktiriyor.

### İyileştirmeler
- **Koşu ayracı:** `ErrorLog.txt` dosyasında her koşu `Info: ======== new run <tarih>, program <sürüm>, model …, output … ========` satırıyla başlıyor.
- **Program sürümü** 2026.10.3.0 (`AssemblyInfo.vb`). Sürüm, exe özelliklerinde ve koşu ayracında görünüyor.
- **ABC:** koşu sonunda kâşif arı (terk edilen kaynak) sayısı günlüğe yazılıyor.
- **Ayar dosyası yoksa:** ayar dosyası ya da `SectionPropertyDataPath` yoksa, bulunan ETABS'in `Property Libraries` klasöründeki AISC16M (veya AISC14M) kullanılıyor. Önceden program "Section property file not found" diyerek duruyordu.
- **`EncasedSections.xml` exe'nin yanında yoksa:** günlüğe uyarı yazılıyor. Önceden ayarlar sessizce varsayılana dönüyordu.
- **Mesajlar:** günlük mesajları ayar dosyasını kullanıcının göreceği adla (`FrameSap2000.exe.config`) anıyor.

### Kullanım kılavuzu (başka kullanıcılar için)
- **Kurulum bölümü** yeniden yazıldı:
  - gereksinimler;
  - birlikte taşınması gereken dosyalar ve her dosya eksik olursa ne olacağı;
  - kurulum adımları: kısa klasör yolu, zip için *Engellemeyi kaldır*, SmartScreen uyarısı, kısayol;
  - ayar dosyasının düzenlenmesi ve örneği;
  - ETABS lisansı ve uzun koşularda uyku modu ile Windows Update.
- Terimler tablosu eklendi; `EncasedSections.xml` dosyasının nasıl düzenleneceği anlatıldı.
- ABC terk sınırı için kısa koşu önerisi eklendi.
- İlk sınır tasarımı süresi "2–10 dakika" olarak düzeltildi.
- Sorun tablosuna kurulum hataları eklendi: SmartScreen, eksik DLL, ETABS açılamıyor, eksik `EncasedSections.xml`.
- Derleme ve dağıtım paketi hazırlama, geliştiriciler için **Ek A**'ya taşındı.

### Testler
- **Dağıtım paketi:** *Release* derlemesindeki altı dosya ayrı bir klasöre kopyalandı.
  - Program bu klasörden açıldı; sürüm 2026.10.3.0 göründü.
  - `ETABSv1.dll` olmadan da form açılıyor; ETABS'e yalnızca Start'ta ihtiyaç var.
- **Eksik dosyalar:** ayar dosyası ve `EncasedSections.xml` olmadan formdan kompozit ABC koşusu yapıldı.
  - ETABS ve kütüphane kendiliğinden bulundu; eksik XML uyarısı ve koşu ayracı yazıldı.
  - Koşu ETABS doğrulaması ve Excel çıktısıyla tamamlandı.
- Test22 tüm testleri geçti; matematik testinde 15 yöntem hatasız çalıştı; MSBuild uyarısız.

---

## 2026-10-02 — Aşama 16: Paylaşıma hazırlık

- **README.md** (İngilizce; GitHub açılış sayfası) eklendi: özellikler, gereksinimler, derleme ve çalıştırma, belgeler, atıf ricası.
- **LICENSE** eklendi: MIT lisansı (ücretsiz; kullanım, değiştirme ve dağıtım serbest, telif notu korunmalı).
- **Kullanım kılavuzu** baştan düzenlendi ve ayrıntılandırıldı. Yeni bölümler:
  - programın akışı ve amaç fonksiyonu;
  - gereksinimler ve derleme;
  - adım adım ilk koşu;
  - model kontrol listesi;
  - Memory Update seçeneklerinin anlamı;
  - yöntem seçimi ile bellek, analiz sayısı ve süre önerileri;
  - koşuyu izleme, durdurma ve devam;
  - Excel sayfaları ve sonuçların yorumlanması;
  - Check Structure adımları;
  - genişletilmiş sorun tablosu.
- **Düzeltme:** kılavuzda kompozit eğilme kontrolü "plastik gerilme dağılımı" olarak yazıyordu. Varsayılan şekil değiştirme uyumu olarak düzeltildi.
- `Inputinfo.txt` kaldırıldı. Kodda kullanılmıyordu ve kişisel bir klasör yolu içeriyordu.
- **Temiz kopyadan derleme testi** (depo yeniden klonlandı, NuGet indirildi, MSBuild):
  - İlk denemede derleme **başarısız** oldu. `My Project\Resources.resx`, depoda olmayan `bin\Debug\Slab_data.xml` dosyasına başvuruyordu; bu, kullanılmayan ve eski bir projeden kalmış bir kaynaktı.
  - Kaynak kaldırıldıktan sonra temiz kopya hatasız ve uyarısız derlendi. Çıktı klasöründe exe, config, `EncasedSections.xml`, `ETABSv1.dll` ve `DocumentFormat.OpenXml.dll` var.

---

## 2026-10-02 — Aşama 15: Yeni optimizasyon yöntemleri ve Optimization sekmesi

### Mevcut yöntemler
Harmony Search, Biogeography-Based, Whale ve Dandelion vardı. Whale, Biogeography ve Dandelion dışında başka yöntem yoktu.

### Eklenen 11 yöntem
- Artificial Bee Colony, Ant Colony, Brain Storm, Crow Search, Firefly, Grasshopper, Teaching-Learning (TLBO-HS seçeneğiyle), Tree-Seed, Grey Wolf, Honey Badger, Aquila.
- Algoritmalar `OptimizationMethods.vb` dosyasında; bilgiler `MethodCatalog` içinde (açıklama, kaynak, parametreler, varsayılan değerler, sınırlar).

### Kaynak VB dosyalarının incelemesi (SteelStruc)
Algoritmalar literatüre göre yeniden yazıldı. Parametre adları ve varsayılan değerler kaynak dosyalardan alındı. Kaynaklardaki aşağıdaki hatalar taşınmadı:
- **ABC:**
  - İyileşen çözüm `colony` dizisine yazılmıyor; yalnızca uygunluk değeri değişiyor. Karşılaştırma rastgele bir k ile yapılıyor.
  - Sınırlar 22 gruplu tek bir probleme göre sabit.
  - Levy seçeneği **var**, ancak yalnızca kâşif arı aşamasında. Burada da kâşif arıda Levy seçeneği olarak uygulandı.
  - Levy adımı normal dağılım yerine düzgün dağılım kullanıyor ve tamsayı kayması hep negatif.
- **ACO:** tamamlanmamış: ana döngü çözüm üretmiyor ve sonsuza dek dönüyor. Ayrık kesit seçimi için feromon ve heuristik tabanlı standart ACO yazıldı (Camp & Bichon 2004).
- **BSO:**
  - Kümelerin en iyisi hiç kaydedilmiyor (`fit_values = 0`).
  - Çözüm yanlış satıra yazılıyor.
  - "k-means" merkezleri hiç güncellemiyor.
  - Levy seçeneği indeks taşmasıyla hata veriyor.
- **Crow:** uçuş uzunluğu tamsayı olarak tanımlı; Levy kutusu kullanılmıyor.
- **Firefly:**
  - Kabul kuralında konum ile uygunluk değeri birbirinden kopuyor.
  - Sınırlar ve ilk 5 ateş böceği 22 gruplu tek bir probleme sabit.
  - Kopya kontrolü yanlış indeks kullanıyor.
- **Grasshopper:**
  - Verilen dosya (`GOA\GOA_API`) aslında BBO içeriyor.
  - Gerçek GOA (`GOA_18.05`) kesit numaralarını **grup sayısıyla** sınırlıyor; mesafe dönüşümü `2 + rem(d, 2)` yerine `rem(d, 2)`.
- **TLBOHS:**
  - HS aşaması her zaman en kötü üyeyi kopyalıyor (`<` yerine `>` olmalı).
  - Öğretmen ve öğrenci formülleri standart dışı.
  - Döngü içinde yeniden sıralama var.
- **TSA:**
  - En iyi ağaç `best_param(n_d)` ile okunuyor (`best_param(k)` olmalı).
  - Global en iyi her ağaçta bellek en iyisiyle eziliyor.
  - "Best greedy" seçeneği kullanılmayan 0. satıra yazıyor.
- **Wolf:**
  - Standart Gri Kurt (GWO) değil, Wolf Colony türü bir algoritma: alfa/beta/delta yok.
  - Yeni konum uygunluk karşılaştırması yapılmadan kabul ediliyor.
  - GWO kullanıcının verdiği tanıma göre (Mirjalili 2014) yazıldı.
- **HBA, AO:** kullanıcının verdiği sözde koda göre yazıldı.
  - HBA'da koku şiddeti `I` 1 ile sınırlandı: av ile mesafe 0'a giderken ifade sınırsız büyüyor.
  - AO'da t ve T çevrim sayısı olarak alınıyor.

### Ortak kurallar
- Her yöntem mevcut değerlendirme altyapısını kullanıyor: onarım, önbellek, global en iyi, yedek, durdurma ve ETABS yeniden başlatma.
- Kabul kuralları:
  - Açgözlü: ABC, BSO, Crow, Firefly, TLBO, TSA, HBA, AO.
  - Arşivin en kötüsüyle karşılaştırma: ACO.
  - Doğrudan değiştirme: GOA ve GWO. GWO'da alfa, beta ve delta (bulunan en iyi üç tasarım) ayrıca saklanıyor.
- Formdaki *Memory Update* yalnızca HS, BBO, Whale ve Dandelion'da; *Levy Flight* yalnızca BBO, ABC, BSO ve Crow'da etkili. Diğer yöntemlerde bu seçenekler soluk görünüyor.
- Yöntem parametreleri `OptInfo.Params`, yöntem durumu `OptInfo.State` alanında; ikisi de yedeğe giriyor (ABC deneme sayaçları, ACO feromonu, GWO liderleri).
- Yöntem enum'una yalnızca sona değer eklendi; eski yedekler okunuyor.

### Optimization sekmesi
- **Solda:** *General* (yöntem en üstte, popülasyon boyutu, analiz sayısı, Memory Update, seçenekler) ve *Evaluation*.
- **Sağda:** *Method parameters*. Seçili yöntemin adı, açıklaması, kaynağı, kabul kuralı ve Levy etkisi ile yalnızca o yöntemin parametreleri görünüyor; parametrelerde ipucu ve sınır bilgisi var.
- Eski HS ve BBO kutuları kaldırıldı; parametreleri yeni kutuda.
- Değerler denetleniyor (sayı, sınırlar, tamsayı, TSA'da en az ≤ en çok). Yöntem değiştirilince girilen değerler kaybolmuyor.
- `Eval` içindeki `Application.DoEvents()` kaldırıldı; Aşama 13'ten beri koşu arka plan iş parçacığında çalışıyor.

### Testler
- **Matematik testi** (ETABS'siz, dişli treni problemi, 4 değişken, 3000 analiz, 3 tohum, Levy açık ve kapalı):
  - 15 yöntemin hepsi hatasız çalıştı; hiçbir değer sınır dışına çıkmadı.
  - Hepsi bilinen optimuma (2,7e-12) yaklaştı: en iyi sonuçlar 1e-12 ile 1e-7 arası.
  - ABC, ACO ve GWO durumu yedek XML'inden doğru geri okundu.
- **Formdan ETABS testi** (525M kopyası, 20 analiz):
  - ABC 7875 ve GWO 7387 ile final doğrulamasından geçti.
  - Bu aşamadan önce yazılmış bir HS yedeğinden yeni arayüzle devam edildi.
- **Test ortamı notu:** test klasörünün yolu 260 karakteri aşınca `DocumentFormat.OpenXml.dll` yüklenemiyor ve Excel yazılamıyor. Program hatası değil; kısa yolda Excel çıktısı doğrulandı.
- Test22 tüm testleri geçti; MSBuild uyarısız; form ekranları kontrol edildi.

---

## 2026-10-02 — Aşama 14: Yedek denetimleri, maliyet dökümü, Excel çıktısı

### Yedek denetimleri (bozuk ya da yanlış dosya)
- **Model kimliği:** yedek artık modelin kimliğini saklıyor (`Class_Backup.Model`, `ModelIdentity_`): model yolu, boyutu, tarihi, SHA-256 özeti, tasarım grubu adları ve kesit kütüphanesinin boyutu.
- **İçerik denetimi** (`CheckContent`): bellek boş olamaz; değişken sayısı ve kesit numaraları denetleniyor. Tutarsız yedekte `.bak` deneniyor. İkisi de kullanılamazsa mesaj iki dosyanın hatasını birlikte gösteriyor. `.bak` kullanılırsa ana yedeğin hatası günlüğe uyarı olarak yazılıyor.
- **Form denetimleri** (ETABS açılmadan önce):
  - Formdaki model yedeğin modelinden farklıysa hangi modelle devam edileceği soruluyor.
  - Model yedekten sonra değiştiyse (özet farklı) uyarı veriliyor; devam kullanıcıya bırakılıyor.
  - Her devamdan önce bir özet gösterilip onay isteniyor: model, kayıt zamanı, yöntem, tohum, analiz sayısı, en iyi maliyet.
- **ETABS sonrası kesin kontrol** (`CheckRestoredModel`): tasarım grupları ya da kesit kütüphanesi yedektekinden farklıysa koşu açık bir mesajla duruyor.
- Eski yedeklerde kimlik bilgisi yok; bu yedeklerde değişken sayısı ve kesit numarası denetimleri yine yapılıyor.
- Koşu hatayla biterse durum satırı "Failed (see ErrorLog.txt)" gösteriyor.

### Maliyet dökümü
- Final tasarım için grup başına döküm üretiliyor (`ETABS_Class.CostBreakdown`, `CostItem_`): kesit, tür (çelik / kompozit), üye sayısı, uzunluk, çelik ve donatı ağırlığı, beton hacmi, kalıp alanı, kalem maliyetleri, toplam ve toplam içindeki pay. Son satır toplam.
- Döküm `CostStProfile` ile aynı miktarları kullanıyor: toplam satırı değerlendirme maliyetine eşit.
- Sonuç XML'ine `CostBreakdown`, `FinalDesignPrint`, `FinalFails`, `Analyses` ve `FormInfo` eklendi. Check Structure çıktısına (`.check.xml`) da `CostBreakdown` eklendi.

### Excel çıktısı
- Yeni `ExcelExport.vb` modülü, projede zaten bulunan OpenXml SDK ile doğrudan `.xlsx` yazıyor; Excel kurulu olması gerekmiyor.
- **Sonuç kitabı** `<çıktı>.xlsx`: *Summary*, *Cost*, *Design*, *Constraints*, *ETABS composite* ve *History* sayfaları.
- **Check Structure kitabı** `<çıktı>.check.xlsx`: *Summary*, *Cost*, *Design ratios*, *Drifts* ve *ETABS composite* sayfaları.
- Dosya Excel'de açıksa `<ad>_<tarih>.xlsx` adıyla yazılıyor.
- Formda yeni **Excel** düğmesi: seçili çıktı dosyasından (ve varsa `.check.xml` dosyasından) kitapları yeniden oluşturuyor.

### Testler (525M kopyası, formdan)
- Stop ile kesilen koşunun yedeği beş senaryoda denendi:
  - Model özeti değiştirildi: uyarı çıktı, Hayır yanıtıyla iptal edildi.
  - Grup adı değiştirildi: ETABS açıldıktan sonra açık bir hata mesajıyla durdu.
  - Ana yedek bozuldu ve formda başka model seçildi: "başka model" sorusu ve özet onayı çıktı. `.bak`'tan devam edildi; final ve Excel ile "completed successfully" bitti.
  - Ana yedek ve `.bak` bozuldu: iki hatayı gösteren mesaj çıktı.
- *Cost* sayfasının toplamı (7945,4876310322088) final maliyetiyle birebir aynı.
- `res.xlsx` (6 sayfa) ve `res.check.xlsx` (5 sayfa) OpenXml doğrulamasından hatasız geçti. Excel düğmesi de çalıştı.
- Test22 tüm testleri geçti; MSBuild uyarısız.

---

## 2026-10-02 — Aşama 13: Form koşu sırasında güncelleniyor (arka plan iş parçacığı)

**Sorun (kullanıcı bildirimi):** koşu sırasında formda değişiklik görünmüyordu.
- Neden ETABS değildi. Koşu formun kendi iş parçacığında çalışıyordu ve her ETABS çağrısı (analiz 15–25 s, ilk sınır tasarımı dakikalar) formun yeniden çizilmesini engelliyordu.
- Windows yaklaşık 5 saniye sonra pencerenin donmuş bir görüntüsünü gösteriyordu.

**Çözüm:**
- **Arka plan iş parçacığı:**
  - Koşu (ETABS, arama, final) `RunWorker` içinde, arka plan STA iş parçacığında çalışıyor.
  - Form girdisi başlamadan önce okunuyor (`PrepareRun`); form güncellemeleri `UI(...)` / `SetPhase` ile yapılıyor.
  - ETABS sınıfının mesaj kutuları `ETABS_Class.MessageHandler` ile form iş parçacığında gösteriliyor.
- **Durum satırı:**
  - Formda o anki aşama ve aşamada geçen süre gösteriliyor; saniyede bir güncelleniyor. Örnek aşamalar: ETABS açılışı, model okuma, ilk sınır tasarımı, başlangıç belleği, arama (döngü / üye), yeniden başlatma, final ve ETABS kompozit tasarımı.
  - *Elapsed* alanı da saniyede bir güncelleniyor.
- **Start / Stop:**
  - Koşu sırasında düğme Stop'a dönüşüyor ve ayarlar kilitleniyor.
  - Stop onaydan sonra o anki değerlendirmenin sonunda durduruyor: ara yedek yazılıyor, ETABS kapatılıyor ve "Load BackUp ile devam" mesajı gösteriliyor.
  - Koşu sürerken pencere kapatılırsa onay isteniyor; koşu aynı şekilde durduruluyor ve program kapanıyor.
- **Testte bulunan hata ve düzeltmesi:**
  - Başlangıç belleği sırasında durdurulan bir koşunun yedeğinde bellek eksik ve HS vektörleri (`ParVec`) boştu; devamda `IndexOutOfRangeException` oluştu.
  - Artık devam sırasında eksik bellek tamamlanıyor ve algoritma vektörleri eksikse ya da boyu yanlışsa yeniden oluşturuluyor (`CompleteRestoredMemory`).
  - Yeni (yedeksiz) bir koşu, aynı çıktıya ait eski yedeği siliyor; önbellek zaten siliniyordu.

**Testler (formdan, 525M kopyası):**
- Form koşu boyunca güncellendi. 45 saniyede bir alınan ekran görüntülerinde durum satırı, analiz sayısı, en iyi maliyet ve süreler canlı değişti.
- 8. analizde Stop'a basıldı. Koşu 10. analizde durdu ve mesaj gösterildi; ardından düğme Start'a döndü ve ayarlar açıldı.
- Yarım kalmış yedekten devam eden koşu belleği ve vektörleri tamamladı, final ETABS doğrulamasıyla bitti (en iyi maliyet 7054,11).
- Test22 tüm testleri geçti; MSBuild uyarısız.

---

## 2026-10-02 — Aşama 12: E2K ile yeniden açma kaldırıldı

- Kullanıcı isteğiyle `RestartFormat = E2K` seçeneği ve ilgili kod kaldırıldı:
  - `App.config` anahtarı;
  - `.e2k` dışa aktarma ve açma;
  - sonuç ve model karşılaştırma fonksiyonları (`ResultFingerprint`, `ModelSignature`, `EncasedSectionCount`, `OpenE2K`).
- ETABS yeniden başlatması yalnızca kaydedilen `.EDB` modeliyle yapılıyor.
- Load BackUp iyileştirmeleri (Aşama 11) değişmedi.
- **Testler:**
  - Şeffaflık: yeniden başlatmadan önce ve sonra ceza, maliyet ve kısıtlar aynı (fark 1e-13).
  - Formdan koşu (*Restart ETABS every* = 10, 25 analiz): 3 yeniden başlatma; bellek 770–780 MB'tan yaklaşık 575 MB'a indi. En iyi maliyet 7519,18; koşu "completed successfully" ile bitti.
  - Test22 tüm testleri geçti; MSBuild uyarısız.

---

## 2026-10-02 — Aşama 11: Yedekten devam (Load BackUp) ve ETABS aç/kapa

### Load BackUp incelemesi
Bulunan eksikler:
- Yedek `BackUp.xml` göreli yolla, o anki klasöre yazılıyordu. Dosya seçme penceresi bu klasörü değiştirebildiği için yedek beklenmedik bir yerde kalabiliyordu.
- Yazma atomik değildi. Yazma sırasında elektrik kesilirse tek yedek bozuluyordu.
- Yedek yalnızca çevrim sonunda yazılıyordu. Bellek 100 iken bir çevrim saatler sürer; kesintide bütün çevrim kayboluyordu.
- Sonuç önbelleği kaydedilmiyordu. Devam eden koşu aynı tasarımları yeniden analiz ediyordu.
- Kesilen koşunun çalışma klasörü `%TEMP%` altında kalıyordu.

Düzeltmeler:
- Yedek çıktı dosyasının yanına yazılıyor: `<çıktı>.backup.xml`. Devam etmek için formda kesilen koşunun çıktı dosyası seçilir.
- Yazma önce `.tmp` dosyasına yapılıyor (`WriteThrough`), sonra eski yedeğin yerine geçiyor. Önceki yedek `.bak` olarak saklanıyor; son yedek okunamazsa `.bak` kullanılıyor.
- Yedek çevrim içinde de 10 dakikada bir yazılıyor (`ILoop - 1`, devamda çevrim tekrarlanır). Kayıt zamanı `SavedAt` yedekte tutuluyor.
- Sonuç önbelleği `<çıktı>.cache.txt` dosyasına satır satır ekleniyor. Devam eden koşu bu dosyayı okuyor, yeni koşu dosyayı siliyor.
- 2 günden uzun süre yazılmamış çalışma klasörleri bir sonraki koşuda siliniyor.

### ETABS aç/kapa (yeniden başlatma)
- Özellik kodda **yoktu**: ETABS koşu boyunca bir kez açılıyordu.
- Formda yeni alan: *Restart ETABS every N analyses* (varsayılan 100, 0 = kapalı). Değer `FormInfo.RestartEvery` alanında tutulur; eski yedeklerde 0 okunur.
- `RestartETABS` analizden önce şu adımları uygular: model kaydedilir, ETABS kapatılır (`ExitInstance`), yeni bir örnek açılır (`StartInstance`), kaydedilen model açılır, oturum ve tasarım ayarları yeniden uygulanır.
- `ExitInstance` programın kendi başlattığı ETABS sürecinin bitmesini bekler; süreç 60 saniye sonra hâlâ çalışıyorsa sonlandırılır. Kullanıcının ETABS'ine dokunulmaz.
- **E2K denemesi** (`File.ExportFile` / `OpenFile`, ETABS 22.6):
  - Gömülü kesitlerin (Concrete Encasement Rectangle) çelik profili ve donatısı `.e2k`'ya yazılmıyor. Final sonrası modelde U1 256 → 216 mm, T1 2,265 → 2,107 s oldu.
  - Arama modelinde (yalnızca General kesitler) de yerdeğiştirme toplamı %15 farklı çıktı.
  - Tasarım kombinasyonu sayısı 11 → 3'e düştü.
  - Bu nedenle varsayılan yöntem kaydedilen `.EDB`'yi açmak. `App.config` > `RestartFormat = E2K` seçeneği duruyor; her `.e2k` yeniden başlatması ek bir analizle kontrol ediliyor ve fark varsa EDB'ye dönülüyor.
- **Bulunan ETABS davranışı:** yeniden açılan modelde ETABS çelik dayanım kombinasyonu seçimini ilk tasarımdan önce siliyor. Bu durumda tasarım başka kombinasyonlarla yapılıyor ve çelik oranı 0,922 yerine 0,359 çıkıyordu. `G1_1_Design` artık her tasarımdan önce seçimi kontrol ediyor ve gerekirse yeniden yapıyor.

### Koruma adımı
- Bir üst kesit alan sırasına göre seçildiği için daha sığ bir kesit çıkabiliyordu: 30 analizlik koşuda W920X474 → W840X473 geçişi 0,085'lik geometrik ceza doğurdu.
- Artık daha büyük kesitler içinden komşu kolon ve kiriş bağlantılarıyla geometri kısıtlarını sağlayan ilk kesit seçiliyor (`GeometryFits`).

### Testler (525M kopyası, ETABS 22.6)
- **Şeffaflık:** aynı tasarım yeniden başlatmadan önce ve sonra analiz edildi.
  - EDB yönteminde ceza, maliyet ve tüm kısıt değerleri aynı. Fark 1e-13 düzeyinde; bu, yeniden başlatmasız kontrol analizinde de görülen çözücü gürültüsü.
  - E2K yönteminde fark yakalandı, uyarı yazıldı ve EDB'ye dönüldü.
- **Bellek:** 10 analizde 661 → 957 MB büyüme ölçüldü. Yeniden başlatma sonrası bellek yaklaşık 580 MB'a iniyor. Bir yeniden başlatma 40–47 saniye sürüyor.
- **Not:** aynı tohumla iki koşu birebir aynı sonucu vermiyor. P-Delta'lı analizlerdeki 1e-13'lük çözücü gürültüsü arama yolunu değiştirebiliyor; bu yeniden başlatmadan bağımsız.
- **Kesinti benzetimi:** formdan başlatılan koşu, ilk yedekten 60 saniye sonra ETABS ile birlikte zorla kapatıldı.
  - Load BackUp ile koşu 22. analizden devam etti; önbellekten 17 kayıt yüklendi.
  - Koşu 41 analizde final doğrulamasıyla bitti. En iyi maliyet 7519,18; aynı tohumla yapılan kesintisiz koşuyla aynı.
- **Diğer kontroller:** Test22 tüm testleri geçti; Visual Studio MSBuild uyarısız; form ekranları doğru.

---

## 2026-10-01 — Aşama 10: Açık kalan üç madde

### 1. İç çözücü ile ETABS farkı
- **Tanı** (W1100X499, 1300x600, 12D20; çerçeve 467, Comb3):
  - Eksenel terim neredeyse aynı: iç 0,275, ETABS 0,271. ETABS burkulma boyu için 0,8266·L (net boy) kullanıyor; iç çözücü tam boy kullandığından biraz güvenli tarafta.
  - Cm ve B1 aynı.
  - Fark zayıf eksen moment teriminde: iç (8/9)M2/Mc2 = 0,199, ETABS 0,217. İç çözücünün Mn2 değeri ETABS'inkinden yaklaşık %8–9 yüksek.
- **Çözüm 1, final ETABS koruması** (`Opt_Finalize`, `StepUpETABSFailures`):
  - ETABS kompozit tasarımında oranı 1'i aşan grup bir üst kesite (Ub içinde) çıkarılır.
  - Tasarım düzeltmesiz yeniden analiz edilir (maliyet, ceza, kısıtlar) ve ETABS ile yeniden doğrulanır.
  - En fazla 3 adım. Sonunda hâlâ aşan grup varsa uyarı yazılır.
  - `FinalCheck` / `FinalConstraints` korunan tasarımı gösterir; `GlobalBest` aramanın sonucu olarak kalır.
- **Çözüm 2, kalibrasyon katsayısı:** `App.config` > `CompositeStrengthFactor` (varsayılan 1,0). İç dayanım oranları bu katsayıyla çarpılır.
  - Final doğrulaması en büyük ETABS / iç oranını günlüğe yazar. Yalnızca iç oranı 0,3'ten büyük gruplar kullanılır.
  - Oran katsayıyı %2'den fazla aşarsa `Warning` satırı önerilen değeri verir.
- Yeni yardımcı `ReadNumber`: sayısal App.config ayarları tek yerden okunuyor.

### 2. Depremde servis ötelemesi büyütmesi
- `App.config` > `SeismicDriftAmplification` (varsayılan 1,0; ASCE 7: Cd/Ie, TBDY 2018: R/I).
- Yalnızca servis öteleme modunda uygulanır, yalnızca deprem durumlarının yerdeğiştirmelerine:
  - response spectrum durumları;
  - yükleri yalnızca Quake desenlerinden ya da ivmelerden oluşan doğrusal statik durumlar (`SRV_<deprem deseni>` dahil).
- Rüzgâr durumları büyütülmez.
- Günlük: katsayı 1 ise `Warning`, değilse büyütülen durumlar `Info` olarak yazılır.

### 3. Kozmetik maddeler
- **Adlar:**
  - `FrameLenght` → `FrameLength`, `GroupDesignPocedure` → `GroupDesignProcedure`, `Initilize*` → `Initialize*`, "occurend" → "occurred".
  - `SAP2000Class` → `ETABSModel`, `HideSAP2000` → `HideETABS`, `saplocation` → `ModelFileBox`, `loadSAP2000file` → `LoadModelButton`.
  - Bunlar yedeğe girmediği için eski yedekler etkilenmiyor. `HarmornySearch` ve `Lamda` yedeğe girdiği için değiştirilmedi. Kök ad alanı `FrameSap2000` de bilerek korundu.
- **Sihirli sayılar sabit oldu:**
  - `ANGLE_TOL` (0,01°): açı artık 180° moduna indirgeniyor; −90° ve 450° de döndürülmüş sayılıyor.
  - `BOUND_SHIFT_MULTIPLIER`, `PENALTY_EXPONENT`, `GAP_FAILED_RATIO`, `REBAR_DIAMETER_TOL`.
- **CompositeColumn.vb:**
  - Gömülü kesitte güçlü eksen kesmesi G2.1(b) Cv1 ile hesaplanıyor.
  - Fiber modelinde donatı alanı, çubuk çapının kapladığı şeritlerden düşülüyor; beton alanı artık fazla çıkmıyor.
  - `Build`, `MaxBarsPerFace < 2` olsa da kesit üretiyor.

### Final mesajı
- Final analizinde ceza varsa ya da ETABS kompozit kontrolü aşılıyorsa son satır artık `Warning: optimization completed, but the final design does not satisfy all checks` oluyor. Önceden her durumda "completed successfully" yazıyordu.
- Formdaki son mesaj kutusu da aynı durumda "API script completed successfully." yerine uyarı gösteriyor. *Check Structure* modunda ceza > 0, ETABS kompozit oranı > 1 ya da analiz tamamlanmamışsa uyarıda ceza ve en büyük ETABS oranı yazılıyor.
- **Formdan uçtan uca test** (525M kopyası, 30 analiz, varsayılan ayarlar): en iyi maliyet 7519. ETABS PMM oranları 0,34–0,86, iç oranlar 0,32–0,83. Önerilen `CompositeStrengthFactor` 1,11.
- **Visual Studio 18 MSBuild:** `.vbproj` hatasız ve uyarısız derleniyor. Çıktıda ETABS 22 `ETABSv1.dll`, `EncasedSections.xml` ve `FrameSap2000.exe.config` var.

### Testler (525M kopyası, ETABS 22.6)
- **Derleme:** temiz. **Test22:** tüm testler geçti. **Form ekranları:** doğru, maliyet varsayılanları 1 / 0,5 / 0,6 / 0,15.
- **30 analizlik koşu** (`CompositeStrengthFactor = 0,7`, servis öteleme modu):
  - ETABS PMM oranları 0,29–0,93 arasında; koruma adımı gerekmedi.
  - Kalibrasyon satırı katsayısız ETABS / iç oranını 1,077 verdi ve `CompositeStrengthFactor = 1,08` önerdi. İç çözücü ETABS'ten yaklaşık %8 düşük kalıyor; bu, tanıdaki Mn2 farkıyla tutarlı.
- **Zorlanmış koruma testi** (final = alt sınır kesitleri):
  - Her adımda aşan 10 grup bir üst kesite çıktı ve yeniden analiz edilip doğrulandı. Ceza 674 → 349 düştü.
  - 3 adımdan sonra uyarı yazıldı; `_best.EDB` ve sonuç XML'i üretildi.
- **Deprem testi** (EQX deseni eklenmiş kopya, `SeismicDriftAmplification = 5,5`): `seismic drift cases [EQX] amplified by 5.5` satırı yazıldı.
- **Not:** test klasörünün yolu çok uzunsa (.NET 260 karakter sınırı) `App.config` okunamıyor ve program açılışta duruyor. Kısa bir klasör kullanın.

---

## 2026-10-01 — Aşama 9: P-Delta, servis ötelemesi, birim maliyetler, inceleme Öncelik 3–5

### P-Delta (kullanıcı isteği)
- Formda *P-Delta analysis* seçeneği eklendi (varsayılan açık; `FormInfo.PDelta`, eski yedeklerde kapalı). Değişiklikler yalnızca çalışma kopyasında yapılıyor:
  - Nonlineer statik durumlar: `StaticNonlinear.SetGeometricNonlinearity(…, 1)` (P-Delta). 525M modelinde 3 dayanım durumu "None"dan "P-Delta"ya alındı.
  - Doğrusal durumlar: ön tanımlı P-Delta "Non-iterative Based on Mass". OAPI'de setter yok; `P-Delta Option Definition` tablosu kullanılıyor. Modelde başka bir yöntem tanımlıysa korunuyor.
- Analiz süresi değişmedi (yaklaşık 11 s). ETABS çıktısında `GEOMETRIC NONLINEARITY = P-DELTA` ve `~P-Delta` durumu görünüyor.
- İç kompozit kontroldeki B2 = 1 kabulü artık modelle tutarlı.

### Servis öteleme modu (kullanıcı isteği)
- "Lateral load cases (service, unfactored)" artık formda **varsayılan**.
- Modelde saf yatay yük durumu yoksa (525M), çalışma kopyasında her rüzgâr ve deprem deseni için `SRV_<desen>` doğrusal durumu oluşturuluyor (`EnsureServiceLateralCases`).
  - Yük katsayısı `App.config` > `ServiceLateralFactor` (varsayılan 1,0).
  - `~` ile başlayan iç desenler atlanıyor ve desen tipi okunamazsa o desen geçiliyor. Tanılama sırasında okunamayan desen tipi yüzünden yanlış bir `SRV_~LLRF` durumu oluşmuştu; bu yüzden eklendi.
- Bu modda `SkipUnusedCases` etkili oluyor: öteleme yalnızca SRV durumlarını kullanıyor.

### Birim maliyetler (kullanıcı isteği)
- Yer tutucular yerine dayanaklı varsayılanlar (çelik = 1): **donatı 0,5 /kN, beton 0,6 /m³, kalıp 0,15 /m²**.
- Varsayım: uygulanmış çelik 2,0 $/kg (204 $/kN), donatı 1,0 $/kg (102 $/kN), C30 beton 120 $/m³, kolon kalıbı 30 $/m².
- Varsayımlar kod yorumunda, `EncasedSections.xml` dosyasında, form notunda ve kılavuzda yazılı.

### Kod inceleme Öncelik 3: algoritmalar
- **HS perde ayarı:** her zaman 1 … bant genişliği (aralığın %1'i, en az 1) kadar kesit, aşağı veya yukarı. Önceden vakaların yaklaşık %40'ında 0 adım çıkıyordu.
- **Normal dağılım:** Levy uçuşu, Dandelion ve `Steplength`, [−3, 3] düzgün dağılım yerine tohumlu normal dağılım (`NormalRnd`, Box-Muller) kullanıyor.
- **Levy uçuşu:**
  - `RZD` reddi `|RZD| < 0,01` olarak düzeltildi; önceden tüm negatif değerleri reddediyordu.
  - Küçük rastgele yürüyüş −2 … +2 aralığında; önceden yalnızca −2 ve −1 çıkıyordu.
- **Dandelion:** aşamalar sürekli konumla yürüyor ve yalnızca sonda yuvarlanıyor. `MaxFuncEvaluation = 1` iken sıfıra bölme önlendi.

### Kod inceleme Öncelik 4: ETABS çağrıları
- Çelik tasarım sonuçları grup başına bir çağrı yerine tek `GetSummaryResults("All")` çağrısıyla okunuyor.
- Kompozit grubun kesiti `GetSection` API çağrısı yerine `Assigned()` içinden alınıyor.
- Analiz edilen durum listesi (`GetRunCaseFlag`) önbelleğe alınıyor ve yalnızca `SetRunCases` / `RestoreRunCases` ile yenileniyor.
- Uygun (ceza 0) sonuç, düzeltilmiş vektörün anahtarıyla da önbelleğe yazılıyor.
- Başlangıçta kat başına yapılan gereksiz `PointObj.GetNameListOnStory` çağrısı kaldırıldı.

### Kod inceleme Öncelik 5: temizlik
- Silinen ölü kod:
  - `encasedSections`, tekil `EnsureCompositeSection`, `CostSlab`, `CostStud`, `StructureWeight` alanı (yerel değişken oldu).
  - Hiç çalışmayan `AttachToInstance` dalı. Bu dal, kullanıcının açık ETABS'ini de kapatabilirdi.
  - `Close` içinde hesaplanıp kullanılmayan süre metinleri. Yerine günlükte `Info: run time …` satırı var.
  - Yer değiştirme listelerinden hiç okunmayan `LoadCaseName`, `U3`, `R1`, `R2`, `R3`.
  - `OptimizationClass.BackUp`, `FormInfo_.BackUp`, `FrameInfo_.CompositeBeamDesignCode`.
- `Structures.vb`'den silinen tipler: `MaterialStructures_`, `RectangularencasedISection_`, `Rebar_`, `Area_`, `LoadCaseForces_`, `Numbers_`, `OptimizationStructure_.TimerInfo`, `ParameterGeneral_`, `Combinations_.DesignComp*`, `Frame_.FrameSection/Frameforces`, `Group_.GroupSection/GroupEncasedSection`, `Story_.StoryPointNames`, yorum satırındaki SAP2000 blokları.
  - Silinen alanlar eski yedeklerde varsa XmlSerializer onları yok sayıyor. Yeniden adlandırma yapılmadı (`HarmornySearch`, `Lamda` korunuyor).
- Kullanılmayan `Imports System.Xml` (ETABSClass, OptimizationClass) ve `Imports System.Linq` kaldırıldı.
- Satır sonları bütün `.vb` dosyalarında tutarlı (CRLF).

### GUI testi (formun kendisi)
Test programı gerçek `MainForm`'u açtı, alanları doldurup **Start** düğmesine bastı (`PerformClick`). Arka plandaki bir yardımcı çıkan mesaj kutularının metnini kaydedip kapattı.
- **Ayarlar:** 525M, kompozit, bellek 6, 40 analiz, tohum 11. Diğerleri formun varsayılanları: servis öteleme, P-Delta, birleşik düzeltme, AISC 360-22, maliyetler 1 / 0,5 / 0,6 / 0,15.
- **Sonuç:** Formun akışı (Control → Init → döngü → Opt_Finalize → Close) hatasız çalıştı. 44 analiz, 16 dk 45 s.
  - Form: en iyi maliyet **7160,02**, ilerleme %100, model sayıları 234 düğüm / 525 eleman / 14 grup / 289 kesit.
  - Tek mesaj kutusu "API script completed successfully." oldu.
- **P-Delta:** 3 nonlineer duruma uygulandı, ön tanımlı P-Delta "Non-iterative Based on Mass". `SRV_WindX` ve `SRV_WindY` oluşturuldu; öteleme yalnızca bunlarla kontrol edildi.
- **Uyarı:** Bir aday tasarım P-Delta ile rüzgâr durumlarında yakınsamadı ve ceza aldı. Bu beklenen davranış; hafif kolonlu tasarım kararsız.
- **Final:** göreli öteleme 0,922, tepe ötelemesi 0,503, çelik oranı 0,711, kompozit dayanım 0,793, detay 0,963, kolon-kolon 1,000, kiriş-kolon 0,952.
- **ETABS kompozit doğrulaması:** 10 grubun hepsinde PMM < 1 (en büyük 0,905). İç çözücü ETABS'in %0,5–10 altında.
- **Temizlik:** `_best.EDB` ve sonuç XML'i yazıldı; arkada ETABS kalmadı.

---

## 2026-10-01 — Aşama 8: Kod inceleme düzeltmeleri (KOD_INCELEME_RAPORU, Öncelik 1 ve 2)

### Sonucu veya süreyi etkileyen hatalar
1. **Boşa yeniden analiz yok.**
   - `F2`, `F4` ve `G2` yalnızca bir değişken gerçekten değiştiğinde `True` döndürüyor. Adım 0'a yuvarlanırsa ya da değişken `Ub`'de kırpılırsa yeniden analiz yapılmıyor.
   - `CombinedRepair` de vektörün değişip değişmediğine bakıyor.
   - `SetAndAnalyze`, vektör son analizdekiyle (`LastAnalysed`) aynıysa analizi atlıyor (`Info: timing … SkippedAnalysis`).
   - Model başka yoldan değişirse (çalışan durumlar, gömülü kesit dönüşümü) `InvalidateAnalysis` çağrılıyor.
2. **Geometri düzeltmesi (E1) değişkenin `[Lb, Ub]` aralığında kalıyor.**
   - Uygun kesitler arasından mevcut kesite en yakını seçiliyor; eşit uzaklıkta tohumlu rastgele seçim yapılıyor.
   - Önce tüm kütüphaneden rastgele seçim yapılıyordu: kirişlere çoğu zaman çok hafif kesit geliyordu ve değer sınır dışına çıkıyordu.
3. `VerifyCompositeWithETABS` ve `CreateEncasedSections`, kompozit grup ya da kesit yokken artık çökmüyor.
4. **`Opt_Finalize`:** doğrulama ve kaydetme hataları ayrı tutuluyor. Biri başarısızsa "completed successfully" yazılmıyor ve `Close`, hata koduyla çağrılıyor.
5. `Lb ≤ Ub` (ve `Ub ≥ 0`) garanti ediliyor; çok büyük tasarım oranında oluşan `IndexOutOfRange` önlendi.
6. **f'c < 21 MPa artık 21 MPa'ya yükseltilmiyor** (güvensiz taraftı); gerçek değer kullanılıp uyarı yazılıyor. 69 MPa üst sınırı korunuyor.
7. Kiriş, kolon veya düzlem çapraz olmayan (3B çapraz, sıfır boylu) çubuklar `FrameDirc_.Other` olarak işaretleniyor; artık X kirişi sayılmıyor.
8. Başlangıç tasarımında otomatik kesit listeleri yalnızca **tasarım değişkeni olan gruplardaki** çubuklara atanıyor. Diğer çelik çubuklar modeldeki kesitini koruyor.
9. **Form sayıları Windows dil ayarından bağımsız okunuyor** (`TryNum`): "." ve "," ondalık ayırıcı olarak kabul ediliyor. Önceden tr-TR Windows'ta "0.9" → 9 okunuyordu.
   - Bellek ≥ 2 ve analiz sayısı ≥ 1 tam sayı olmalı.
   - Öteleme oranları > 0 olmalı.
10. **HS:** uyarlanabilir PAR/HMCR, belleğe gerçekten giren üyenin konumuna yazılıyor (`LastUpdatedID`).
11. Final değerlendirmesi `GlobalBest`'in bir kopyasıyla yapılıyor. Sonuç `FinalCheck` olarak XML'e yazılıyor; ceza > 0 ise uyarı veriliyor.
12. **Check Structure:** kesitler çıktı dosyasındaki **grup adlarıyla** eşleniyor. Grup bulunamazsa hata veriliyor; dosya yoksa da hata veriliyor.

### Form ve çıktılar
13. *Column to Column* / *Beam to Column* onay kutuları artık çalışıyor (`FormInfo.SkipCtoC` / `SkipBtoC`). Kısıt sayıları günlüğe yazılıyor. İşlevsiz *Discard warnings* kaldırıldı.
14. Hiçbir hesapta kullanılmayan *Disp. Limit (mm)* kaldırıldı (`FrameInfo_.DispLimit` de). Eski yedeklerde bu alan yok sayılır.
15. *Number of Joint / Members / Group / Section* kutuları modelden dolduruluyor ve salt okunur.
16. `.check.xml` dosyasına `Penalty`, `Cost`, `AnalysisFailed` ve `GroupNames` eklendi; günlükte `Info: checked design …` satırı yazılıyor.
17. **`ErrorLog.txt` biçimi:** "Error message:" öneki kaldırıldı. Satırlar `Info:`, `Warning:` veya `Error:` ile başlıyor.
18. Saatler 24 saat biçiminde (`HH:mm:ss`).
19. **`ETABS_Print`:**
    - `PMM_Ratios` artık tasarım değişkeni sırasında ve `GroupNames` ile eşleşiyor (önce çelik, sonra kompozit sırasıyla karışıktı).
    - `InterStoryDrift_Ratios` → `InterStoryDrifts`, `TopStoryDrift_Ratio` → `TopStoryDrifts` (değerler mm).
20. **İstisna koruması:** `Start_Click` içinde Try/Catch ve `ApplicationEvents.UnhandledException` eklendi. İstisnada hata günlüğe yazılıyor, ETABS kapatılıyor, geçici klasör siliniyor.

### Tam optimizasyon testi
**Ayarlar:** 525M, ETABS 22.6, form varsayılanları. Harmony Search (HMCR 0,9 Adaptive, PAR 0,6 Dynamic, greedy-worst, Clear Duplicates), kompozit AISC 360-22, birleşik düzeltme, önbellek, SkipUnusedCases, CtoC/BtoC açık, öteleme "All cases and combos". Bellek 10, **150 analiz**, tohum 2026.

| | |
|---|---|
| Süre | 38 dk. Başlangıç 128 s; değerlendirme 21 s (analiz 9,6 s, tasarım 1,9 s). |
| Hata / uyarı | **Yok** |
| Analizler | 151 analiz, 90 değerlendirme; 3 analiz atlandı (`SkippedAnalysis`) |
| Önbellek | İsabet yok: 14 değişken × ~289 kesitte HS bu bütçede tasarım tekrarlamadı |
| En iyi uygun maliyet | 16945 (2. analiz) → 8252 (35) → 8081 (43) → **7708,6** (79). Sonraki 70 analizde iyileşme yok. |
| Final (düzeltmesiz, tüm durumlarla) | ceza 0 |
| ETABS kompozit doğrulaması | 10 grubun hepsinde PMM < 1 (en büyük 0,708). İç dayanım oranı ETABS'in %2–9 altında. |
| Temizlik | `_best.EDB`, sonuç XML'i yazıldı; geçici klasör silindi; arkada ETABS kalmadı |

**Testte not edilen mantıksız noktalar ve düzeltmeler:**
- **Sonuç dosyasında hangi kısıtın belirleyici olduğu görünmüyordu.**
  - Kolonlar çok büyük (1300×600 içinde W1100X607), dayanım oranları ise yalnızca 0,25–0,71. Belirleyici büyük olasılıkla öteleme.
  - Düzeltme: final analizinin kısıt özeti (`ConstraintSummary`) günlüğe (`Info: final design, …`) ve sonuç XML'ine (`FinalConstraints`) yazılıyor. İçerik: göreli ve tepe ötelemesi / sınır, en büyük çelik oranı, kompozit dayanım ve detay oranı, geometrik oranlar.
- **Öteleme kontrolü katsayılı dayanım kombinasyonlarıyla yapılıyordu.** Hem "All cases and combos" hem "Lateral only" modu 1,2D + 1,6W gibi kombinasyonları kullanıyor. Öteleme sınırları normalde servis yükleri içindir; öteleme fazla tahmin ediliyor olabilir.
  - Yeni mod: **"Lateral load cases only (service)"** (`DriftComboMode_.LateralCasesOnly`). Yalnızca yükleri tümüyle rüzgâr/deprem desenlerinden oluşan doğrusal statik durumları ve response spectrum durumlarını kullanıyor.
  - 525M modelinde böyle bir durum yok; analizdeki durumların hepsi katsayılı NL kombinasyon durumları. Bu modu kullanmak için modele katsayısız rüzgâr durumları eklenmeli; yoksa program açık bir hata mesajıyla duruyor.
- **Doğrulama koşuları:**
  - Service modu 525M modelinde beklendiği gibi açık bir hatayla durdu.
  - 8 analizlik kısa koşuda final özeti doğru yazıldı. Değerler: göreli öteleme 0,654, tepe ötelemesi 0,472, çelik oranı 0,250, kompozit dayanım 0,565, kompozit detay 0,963, kolon-kolon 1,000, kiriş-kolon 0,993.
  - Bu kısa koşuda belirleyici olan öteleme değil; geometrik kısıtlar ve donatı oranı alt sınırı (ρsr ≥ %0,4).

---

## 2026-10-01 — Aşama 7: Farkın kaynağı, eğilme yöntemi, birim maliyetler formda

### İç çözücü ile ETABS arasındaki farkın kaynağı
ETABS tasarımdan sonra kendi hesapladığı değerleri `DesignCompositeColumn.AISC360_22.GetOverwrite` ile veriyor. Aynı kesit (W360X110, 550×450, 8Ø20) ve aynı kuvvetlerle karşılaştırma:

| Büyüklük | ETABS | İç çözücü |
|---|---|---|
| Cm (2 eksen) | 0,49152 / 0,34747 | aynı |
| B1, B2 | 1 | 1 |
| φPnt | 5310,6 | 5310,5 |
| Güçlü eksen eğilme oranı | 0,499 | 0,497 |
| φPn | 7992 kN (boy = 0,8266·L, net kolon boyu) | 7773 kN (L), %3 daha güvenli |
| **Zayıf eksen Mn2** | **≈ 550.000 kN·mm** | **657.000 kN·mm (PSDM), %19 fazla** |

- Farkın asıl kaynağı zayıf eksen eğilme dayanımı. PSDM'de I profilin tüm çeliği akmış kabul ediliyor; zayıf eksende bu doğru değil.
- Çözüm: gömülü kesitlerde Mn için **şekil değiştirme uyumu yöntemi** (AISC I1.2b, I3.3(c)) eklendi ve varsayılan yapıldı.
  - `CompositeSection.StrainCompatibilityMoment`: doğrusal şekil değiştirme, εcu = 0,003, Whitney bloğu 0,85·f'c·β1·c (ACI β1), çelik ve donatı elastik-tam plastik.
  - Ayar: `EncasedSections.xml` > `FlexureMethod` (`StrainCompatibility` / `PlasticStress`).
  - Aynı kesitte Mn3 = 1,040·10⁶ (ETABS 1,083·10⁶), Mn2 = 567.435 (ETABS ≈ 550.000).
- **Uçtan uca final doğrulaması (10 grup):** fark %1,8–14'e indi (önce %7–28). Oranın 1'e yakın olduğu grupta (10) fark %1,8. En büyük fark oranı düşük grup 5'te (0,539'a karşı 0,627).
  - İç çözücü hâlâ biraz güvensiz tarafta. Final ETABS kontrolü bu yüzden önemli.
- Not: Optimizasyon sonuçları Aşama 6'ya göre değişir. Gömülü kolonların eğilme dayanımı düştü ve sonuç daha güvenli tarafta. Eski davranış için `FlexureMethod = PlasticStress`.

### Modelle ilgili bulgu
- 525M modelindeki nonlineer yük durumlarında ETABS çıktısı **"TYPE OF GEOMETRIC NONLINEARITY = NONE"** gösteriyor, yani P-Delta yok.
- İç çözücü `B2 = 1` kabul ediyor; bu kabul P-Delta'lı analiz gerektirir (KULLANIM_KILAVUZU 2. bölüm).
- Bu modelde ya P-Delta açılmalı ya da `EncasedSections.xml` içinde `B2` gerçekçi bir değere ayarlanmalı.

### Birim maliyetler formda
- Structural Properties sekmesine **Composite Cost (relative unit prices)** grubu eklendi: Steel /kN, Rebar /kN, Concrete /m³, Formwork /m².
- Açılışta `EncasedSections.xml` değerleriyle doluyor. Değerler `FormInfo.Costs` (`UnitCosts_`) olarak yedeğe giriyor. Eski yedeklerde alan yok; o zaman XML değerleri kullanılıyor.
- Doğrulama: değerler negatif olamaz, en az biri sıfırdan büyük olmalı.
- Günlükte `Info: unit costs (form|EncasedSections.xml): …` satırı yazılıyor.
- Fiyat değerleri değiştirilmedi; kullanıcı formda düzeltecek (kılavuzda anlatıldı).

### Düzeltme
- Aşama 5'teki GUI düzenleme betiği `MainForm.Designer.vb` dosyasına çift CR'li (`\r\r\n`) satır sonu yazmıştı. VB derleyicisi kabul ettiği için fark edilmemişti; Visual Studio'da fazladan boş satır ve satır sonu uyarısı çıkarırdı. Düzeltildi.

---

## 2026-10-01 — Aşama 6: ETABS kompozit kolon tasarımıyla doğrulama

**Soru:** iç kompozit çözücü, ETABS'te kompozit kolon API'si olmadığı için yazılmıştı. ETABS 22'de böyle bir API var mı?

**Bulgular (ETABS 22.6):**
- `cDesignCompositeColumn` var: `SetCode` (`AISC 360-16` ve `AISC 360-22` kabul ediliyor), `StartDesign`, `GetSummaryResults`, tercih ve üzerine yazma (overwrite) tabloları.
- ETABS gömülü kesit tipini tanıyor (`eFramePropType.EncasedRectangle`), ama `PropFrame` sınıfında bu kesiti **oluşturan metot yok**. `SetRebarColumn` gömülü kesitte başarısız oluyor.
  - Kesit ve donatı `DatabaseTables` ile yazılabiliyor: `Conc Encasement Rectangle` ve `Concrete Column Reinforcing` tabloları. Doğrulandı.
- `GetSummaryResults` kaymış veri döndürüyor: çerçeve adı yerine kesit adı, PMM = 0. Gerçek sonuçlar `Composite Column Summary - AISC 360-22` tablosunda.
- **Süre:**
  - ETABS kompozit tasarımı 40 kolon için 36–37 s, tüm kolonlar için yaklaşık 190 s. İç çözücü 0,1 s.
  - Modelde 289 gömülü kesit tanımlıyken analiz 128 s sürdü (General section ile 10–12 s). 289 kesitin tablo içe aktarması yaklaşık 2 dakika.
- **Aynı kesit ve aynı kuvvetlerle karşılaştırma** (W360X110, 550×450, 8Ø20, AISC 360-22, 525M modelinde Story5'in 4 kolonu):

| Kolon | ETABS PMM | İç çözücü | Fark |
|---|---|---|---|
| C1 | 1,693 | 1,597 | −5,7 % |
| C3 | 1,304 | 1,285 | −1,5 % |
| C6 | 1,231 | 1,217 | −1,1 % |
| C7 | 1,713 | 1,605 | −6,3 % |

**Karar (kullanıcıyla):** karma yaklaşım.
- **Arama:** hızlı iç çözücü ve General section, önceki gibi.
- **Final değerlendirmesi ve Check Structure:** `VerifyCompositeWithETABS`.
  - En iyi tasarımın General kesitleri aynı adla gerçek gömülü kesitlere çevrilir, prosedür 13 yapılır, model yeniden analiz edilir.
  - Aynı analizde iç çözücü ve ETABS kompozit tasarımı (formdaki *Composite code*) çalışır.
  - Sonuçlar `ErrorLog.txt` dosyasına (`Info: ETABS composite check …`), sonuç XML'ine (`ETABSCompositeCheck`) ve Check Structure çıktısına (`ETABSCompositeRatios` / `ETABSCompositeCheck`) yazılır.
  - `_best.EDB` gerçek gömülü kesitleri ve ETABS tasarım sonuçlarını içerir.
- ETABS 19'da bu tablolar yok; doğrulama atlanır ve günlükte belirtilir.

**Diğer değişiklikler:**
- `Group_.CompositeStrength` / `CompositeDetailing`: iç çözücünün yalnızca dayanım oranı ve detay oranı ayrı tutuluyor (`PMMRatio` ikisinin büyüğü).
  - İlk karşılaştırmada "internal" değeri detay oranını da içerdiği için yanıltıcıydı; örneğin 0,955 ve 0,963 donatı oranı sınırından geliyordu.
- `EncasedSections.xml`: `TieDiameter` (10) ve `TieSpacing` (150) eklendi. ETABS donatısı iç çözücüyle aynı yerde: çubuk merkezi yüzeyden `RebarCover` uzaklıkta.
- Tablo yazma, mevcut kayıtları koruyacak şekilde yapıldı (`SetTable`).
  - İçe aktarma tüm tabloyu değiştiriyor; yalnızca yeni satır yazınca diğer 269 kesit silinmişti.
  - Kilitli modelde tablo düzenlemesi hatasız ama etkisiz kalıyor; önce kilit açılıyor.

**Test** (uçtan uca, 525M, final yolu zorlanarak):
- Arama: değerlendirme başına 24 s, değişmedi.
- Doğrulama: 9 kesit 1,3 s'de dönüştürüldü, prosedür 13, ETABS tasarımı 193 s.
- 10 grubun hepsinde **ETABS PMM, iç çözücünün dayanım oranından %7–28 yüksek**:

| Grup | ETABS PMM | İç çözücü (dayanım) |
|---|---|---|
| 5 | 0,627 | 0,491 |
| 6 | 0,819 | 0,762 |
| 7 | 0,670 | 0,581 |
| 8 | 0,853 | 0,746 |
| 9 | 0,575 | 0,513 |
| 10 | 0,910 | 0,820 |
| 11 | 0,233 | 0,204 |
| 12 | 0,454 | 0,422 |
| 13 | 0,246 | 0,203 |
| 14 | 0,358 | 0,329 |

- Bu tasarımlar büyük kesitli ve oranları düşük. Fark, oranı yüksek tek kesitli denemedekinden (%1–6) büyük.
- **Açık iş:** farkın kaynağı araştırılmalı (B1, Pn, Mn bileşenleri; ETABS tablosu `PRatio`, `MMajRatio`, `MMinRatio` veriyor).
  - Fark kapanana kadar iç çözücüyle "uygun" bulunan bir tasarım ETABS kontrolünde 1'i aşabilir.
  - Final kontrolünde bu durum günlükte görünür.

---

## 2026-10-01 — Aşama 5: Kod taraması, hızlandırma, algoritma düzeltmeleri, GUI

### Kritik hata
- `OptimizationClass.LogError` kendini sonsuz döngüde çağırıyordu ve programı `StackOverflowException` ile çökertiyordu.
  - `Opt_Finalize` başarı mesajını bu metotla yazdığı için **her başarılı koşunun sonunda** program çöküyordu. ETABS açık kalıyor, geçici klasör silinmiyordu. Hata durumlarında da aynısı oluyordu.
  - Düzeltme: mesaj `SAP2000Class.Errorlogprint` ile yazılıyor.

### Hızlandırma
Süre ölçümü ilk kez eklendi: `Clock`, `TimingReport` ve günlükte `Info: timing …` satırı. 525M modelinde mevcut durum (2 değerlendirme): değerlendirme başına **91 s**.

| İşlem | Ortalama | Değerlendirme başına |
|---|---|---|
| Analiz | 13,1 s | 6 |
| Çelik tasarımı | 8,2 s | 2–3 |
| `File.Save` | 1,8 s | 6 |
| Kesit atama | 1,7 s | 6 |
| Sonuç okuma, kompozit kontrol | < 0,2 s | — |

Sonucu değiştirmeyen hızlandırmalar (sıralı modda sonuçlar birebir aynı; değerlendirme başına **68 s**):
- `E3`'teki `File.Save` kaldırıldı. Model `WorkFile` üzerinden açıldığı için `RunAnalysis` dosya yolunu biliyor.
- Analiz ETABS süreci içinde çalıştırılıyor (`SetSolverOption_3`, process 1): yaklaşık %10 kazanç. Çözücü tipinin belirgin etkisi olmadı.
- `E2` yalnızca kesiti değişen grupları yeniden atıyor (`Assigned()`).
- **Sonuç önbelleği** (`UseCache`): aynı tasarım vektörü tekrar üretilirse ETABS çağrılmıyor.
  - Anahtar, düzeltme öncesi vektör. Final değerlendirmesi ve Check Structure önbellek kullanmaz.
  - Ana döngü, art arda 20 çevrimde yeni analiz yapılmazsa sonlanıyor; önbellekle döngünün sonsuza kadar dönmesi önleniyor.
- **Kullanılmayan yük durumlarının çözülmemesi** (`SkipUnusedCases`): kurallar PROGRAM_KURALLARI.md'de.
  - 525M modelinde kapatılacak durum yok: Modal, 3 nonlineer P-Delta kombinasyon durumu ve `~LLRF`.
  - `Opt_Finalize`, `_best.EDB` için tüm durumları geri açıyor.

Davranışı değiştiren hızlandırma:
- **Birleşik düzeltme modu** (`RepairMode = Combined`, formda varsayılan):
  - Öteleme (F2, F4) ve PMM (G2) düzeltme adımları tek analizin sonuçlarından birlikte hesaplanıyor; her değişken için en büyük adım alınıyor. Ardından tek bir yeniden analiz yapılıyor.
  - Değerlendirme başına en fazla 2 analiz ve 2 tasarım: **26 s**, başlangıca göre yaklaşık 3,5 kat hızlı.
  - Eski sıralı akış *Sequential (original)* olarak seçilebilir; eski yedeklerde varsayılan budur.

### Algoritma düzeltmeleri
- **Dandelion (iniş aşaması) ve Levy uçuşu (BBO)** `GlobalBest`'e yöneliyordu. `GlobalBest` yalnızca cezasız çözümle güncellendiği için uygun çözüm bulunana kadar değişkenleri 0'dı ve arama en küçük kesitlere itiliyordu.
  - Artık `Leader()` kullanılıyor: uygun çözüm varsa `GlobalBest`, yoksa belleğin en iyisi.
- **Whale:** lider `Memory(0)` yerine `Leader()`. Bellek yalnızca çevrim başında sıralandığı için `Memory(0)` güncel en iyi değildi.
- **`MemoryUpdate` (Worst):** en kötü maliyet her eleman için yeniden hesaplanıyordu; artık bir kez hesaplanıyor (sonuç aynı).
- "No feasible design" durumu günlüğe yazılıyor.
- `ETABS_Class.Quiet`: mesaj kutusu olmadan kapanış (toplu koşular, testler).

### GUI
- Pencere başlığı eklendi. "SAP2000 File" başlığı → "ETABS Model (*.EDB)". Bozuk karakterli eski varsayılan dosya yolları kaldırıldı.
- Ana sayfaya *Analyses* (yapılan / en fazla), *Best cost*, *Elapsed*, *Remaining* (tahmini) alanları ve ilerleme çubuğu eklendi. *Date* ve *Av. analysis (s)* alanları artık dolduruluyor (önceden hiç yazılmıyordu).
- *Structural Properties*: "Skip analysis cases not used by design / drift checks" seçeneği eklendi. "Random seed" etiketinin kutunun altında kalması ve hizalama sorunları düzeltildi.
- *Optimization*: yeni **Evaluation** grubu eklendi (*Repair mode*, *Reuse results of repeated designs*).
  - Yanlış etiketler düzeltildi: ikinci "Memory Update" → "Method", "Number of Members" → "Memory size", "Max. Iteration" → "Max. analyses".
- Pencerenin sağındaki boş alan kaldırıldı.
- Form her sekmenin ekran görüntüsüyle kontrol edildi.

### Testler (ETABS 22.6, 525M, kompozit 360-22, aynı tohum)
| Koşu | Değerlendirme 1 (maliyet / ceza) | Değerlendirme 2 | Süre / değerlendirme |
|---|---|---|---|
| Başlangıç (Aşama 4 kodu) | 7906,97 / 1,3548 | 7050,18 / 6,3586 | 91 s |
| Sıralı + hızlandırmalar | 7906,97 / 1,3548 | 7050,18 / 6,3586 | 68 s |
| Birleşik + hızlandırmalar | 6978,92 / 1,3713 | 9628,67 / 0,7427 | 26 s |

- Önbellek: tekrarlanan değerlendirme 0 s sürdü ve aynı sonucu verdi.
- **Uçtan uca optimizasyon** (test programı formdaki `Init` ve döngüyü izliyor; HS, bellek 4, birleşik mod):
  - 40 analizlik koşu 4 çevrim boyunca hatasız tamamlandı. Analiz başına ortalama 9,8 s, değerlendirme başına 24,6 s.
  - Bu bütçede uygun (cezasız) tasarım bulunamadı. "No feasible design" günlüğe yazıldı, program çökmeden kapandı.
  - Final yolunu denemek için ikinci bir kısa koşuda belleğin en iyisi zorla en iyi çözüm yapıldı (yalnızca testte).
    - Final analizi, `_best.EDB`, sonuç XML'i ve "completed successfully" satırı sorunsuz.
    - Çalışma klasörü silindi, arkada ETABS kalmadı.
  - Form, ekran görüntüleriyle kontrol edildi. Formdan başlatılan gerçek koşu elle denenmedi.

---

## 2026-10-01 — Aşama 4: Geçici çalışma klasörü

- Program artık seçilen modeli değiştirmiyor. Önceden `E3_Analysis` her analizde girdi dosyasının üzerine kaydediyordu.
- **Çalışma kopyası:**
  - Koşu başında model `%TEMP%\SteelFrameOpt\<model>_<yyyyMMdd_HHmmss>\` klasörüne kopyalanır. Klasör `App.config` > `WorkFolder` ile değiştirilebilir.
  - ETABS bu kopyayı açar; tüm kaydetme ve analizler orada yapılır.
- **Sonuç:** OneDrive klasöründe analiz dosyaları (`.Y0x`, `.K_x`, `.msh` …) oluşmaz.
- **Kalan çıktılar:** `ErrorLog.txt` ve `<model>_best.EDB` önceki gibi orijinal modelin klasörüne yazılır.
- **Temizlik:**
  - Çalışma klasörü yeni `ETABS_Class.Shutdown()` içinde, ETABS kapandıktan sonra silinir. `Close` bunu çağırır.
  - Silinemezse uyarı yazılır, koşu bozulmaz.
  - Program çökerse klasör `%TEMP%` altında kalır.
- **Test** (ETABS 22.6, kompozit mod, 1 değerlendirme):
  - Orijinal modelin MD5 değeri koşudan önce ve sonra aynı.
  - Model klasöründe analiz dosyası oluşmadı.
  - Çalışma klasörü oluşturuldu ve sonda silindi; arkada ETABS süreci kalmadı.
  - Sonuçlar Aşama 3 testiyle aynı: maliyet 7906,97, ceza 1,3548.

---

## 2026-10-01 — Aşama 3: ETABS 22 ve AISC 360-22

**Yaklaşım:** İki sürüm birlikte korunur.
- ETABS 22 varsayılandır; ETABS 19 ile derleme ve çalışma yolu açık kalır.
- Kompozit kontrol için formda *Composite code* (`AISC 360-16` / `AISC 360-22`) seçilir.
- 360-16 modu, Aşama 2 sonuçlarını birebir korur.

### ETABS 22 entegrasyonu
- **API:** ETABS 22 `ETABSv1.dll` (2.8, .NET Standard 2.0) .NET Framework 4.7.2 programından sorunsuz çalışıyor (ETABS 22.6.0 ile denendi).
  - `.vbproj`: `ETABSDir` özelliği ETABS 22 kuruluysa onu, değilse ETABS 19'u seçer.
  - `netstandard` ve `Microsoft.Win32.Registry.dll` (ETABS 22 klasöründen, net461 facade) referansları eklendi.
- **`App.config`:** ETABS 22 yolu ve `AISC16M.xml` (AISC14M'nin üst kümesi, aynı format).
  - Yol bulunamazsa kurulu en yeni ETABS kullanılır (`FindInstalledETABS`).
  - Kütüphane bulunamazsa aynı dosya adı o sürümün `Property Libraries` klasöründe aranır.
- **Günlük:** bağlanılan ETABS sürümü (`GetVersion`) ve gerçekten atanan çelik kodu (`GetCode`) `ErrorLog.txt` dosyasına yazılır.
- **Çelik kodu listesi:** `AISC 360-22` ve `AISC 360-16` eklendi; varsayılan `AISC 360-22`.
  - ETABS 22 ikisini de kabul ediyor (`SetCode` + `GetCode` ile doğrulandı). Geçersiz ad `ret = 1` döndürüyor.
  - ETABS 19 bu kodları kabul etmez.
- ETABS 22 API'sinde de Section Designer kesiti kurulamıyor (`cPropFrameSDShape` yalnızca `Get…` içeriyor). General section yaklaşımı aynen kullanılıyor.

### Kompozit çözücü: AISC 360-22 (`CompositeColumn.vb`)
Yeni `CompositeCode_` sürüm seçimi (`CompositeSection.Code`), formda *Composite code* ve `FormInfo.CompositeCode` (eski yedeklerde 0 = 360-16).

| Konu | 360-16 modu | 360-22 modu (rehber `AISC360_22_…md`) |
|---|---|---|
| Gömülü I kesit | — | **Fark yok** (Pno, C1, EI_eff, PSDM, kesme, detay). Çıktılar 360-16 ile birebir aynı. |
| Dolgulu kutu/boru kesme | Yalnız çelik (G4 / G5) | Çelik + beton katkısı 0,06·Kc·Ac·√f'c (I4-1). Kc, M/(V·d) oranından hesaplanır; kutu en fazla 10, boru en fazla 9. Boruda Av = 2As/π. |
| Kompakt olmayan / narin dolgulu kesit, basınç + eğilme | H1-1a/b | I5-1a/b, Tablo I5.1 (c_sr, c_p, c_m) |
| Burulma (dolgulu) | Kontrol yok | Tr > 0,2·Tc ise H3-6. Tc yalnızca çelik borudan (H3.1) alınır. |

**Rehber ile ilgili notlar** (360-22 metni elimizde yok; 360-16 metni `MakaleYayınlar/PDF` klasöründe):
- **I5-1a/b ve Tablo I5.1 360-16'da da vardır**, orada H1.1'e alternatif bir seçenektir. Rehber bunları 360-22 yeniliği olarak sunuyor.
  - 360-16 modunda önceki davranış (H1) korundu.
  - 360-22 modunda, rehberin "zorunlu" ifadesine göre I5 kullanılıyor.
- Tablo I5.1'deki sınırlar (c_m ≥ 1,0 / ≤ 1,67) rehberde yok; 360-16 metninden alındı. Üsler negatiftir (c_sr⁻⁰·⁴ vb.).
- Rehberdeki Kc enterpolasyonu ters yazılmış: 0,5'te 1 veriyor, 0,7'ye yaklaşırken 10'a çıkıyor, 0,7'de 1'e düşüyor. **Sürekli hale getirildi:** 0,5'te Kc_max, 0,7'de 1.
- **Doğrulanmalı:**
  - Beton kesme katkısında √f'c birimi **ksi** alındı. Rehberdeki psi yorumu yaklaşık 30 kat küçük değer verir. Örnek: 400×400×20 kutu, Kc = 1 → 108 kN (çelik 2815 kN).
  - Boruda Av = 2As/π (rehberden). G5 burkulma sınırı korundu.
  - Kutu çelik kesmesinde Cv korundu; rehberde yalnızca 0,6·Av·Fy yazıyor.
  - Malzeme sınırları (I1.3) iki sürümde de aynı alındı.
- Rehberin "360-16'da boru C2 = 0,90" ifadesi yanlış; iki sürümde de 0,95.
- ASD katsayıları eklenmedi; program LRFD çalışıyor.

### Bulunan ve düzeltilen hata
- **`FilledBox.DesignShear` güçlü eksende negatif kesme dayanımı veriyordu** (ör. 400×400×20 için −447 kN; doğrusu 2534 kN). Sebep VB'nin büyük/küçük harf duyarsızlığı:
  - `Dim h = If(…, H, B) - 3*t` satırında `H`, tanımlanmakta olan yerel `h`'yi (değeri 0) okuyordu.
  - Kutu kesit optimizasyona bağlı olmadığı için önceki sonuçlar etkilenmedi.
  - Tüm kaynakta aynı kalıp tarandı; başka örnek yok.

### Testler
- **Regresyon (360-16):** 12 W kesitinden üretilen gömülü kesitler, 4 kutu ve 3 boru kesit; her biri için 15 kuvvet durumu kullanıldı. Yeni kod, git'teki Aşama 2 koduyla birebir aynı çıktıyı verdi. Tek fark, yukarıdaki kutu kesme düzeltmesi.
- **Gömülü 360-22 ile 360-16:** tüm çıktılar birebir aynı.
- **360-22 el hesapları:** 24 kontrolün tamamı geçti.
  - Kutu kesme: Kc = 1 / 5,5 / 10 ve 0,5–0,7 sürekliliği.
  - Kutu ve boru Tc.
  - Kompakt kutuda H1 korunuyor.
  - Kompakt olmayan kutu: I5-1a/b; denge noktasında oran 1,0. Çekmede H1 kullanılıyor.
  - Boru: kesme, SRSS ile I5.
  - H3-6: Tr = 0,5·Tc dikkate alınıyor, 0,15·Tc ihmal ediliyor; 360-16'da kontrol yok.
- **Derleme:** proje ETABS 22 DLL'ine karşı hatasız derleniyor.
- **ETABS 22.6 uçtan uca** (`525M/525Member_steel.EDB` kopyası, konsol test programı, ETABS gizli):
  - **Kompozit mod, 360-22, çelik kodu AISC 360-22:**
    - Başlangıç 117 s: 289 W'lik otomatik listeler oluşturuldu, 14 değişken, 10 kompozit grup.
    - 2 değerlendirme hatasız tamamlandı (her biri yaklaşık 82 s); kompozit oranlar 0,64–0,96.
  - **Çelik mod, AISC 360-16:** 1 değerlendirme hatasız tamamlandı (82 s).
  - ETABS 19 modeli ETABS 22'de otomatik dönüştürüldü. Testlerden sonra arkada ETABS süreci kalmadı.
  - Form (GUI) akışı elle test edilmedi.

---

## 2026-10-01 — Aşama 2: Kompozit kolon çözücü ve ek özellikler

### Yeni: `CompositeColumn.vb` — AISC 360-16 Bölüm I kompozit kolon çözücü (LRFD)
`AISC360_16_Composite_Column_Rules.md` rehberine göre yazıldı. Eski `Class1.vb` (yarım kalmış Fortran çevirisi) kaldırıldı; yedekte duruyor.

| Sınıf | Kapsam |
|---|---|
| `CompositeSection` (temel sınıf) | EI_eff (I2-12/14), Pn (I2-2/3), çekme (I2-8), plastik gerilme dağılımı (PSDM), elastik ilk akma momenti, H1-1 etkileşimi, ETABS için dönüştürülmüş kesit özellikleri |
| `EncasedIShape` | Gömülü I kesit: Pno (I2-4), C1 (I2-13), PSDM ile Mn, kesme (I4.1(b)(a), G2.1/G6), detay sınırları (As ≥ %1 Ag, ρsr ≥ %0,4, en az 4 donatı) |
| `FilledBox` | Dolgulu kutu: sınıflandırma (Tablo I1.1a/b), kompakt/kompakt olmayan/narin Pno, Mn (I3.4b), kesme (G4) |
| `FilledPipe` | Dolgulu boru: sınıflandırma, Pno (C2 = 0,95), Mn, kesme (G5), etkileşimde momentlerin SRSS ile birleştirilmesi |
| `CompositeMemberCheck` | Cm, B1 büyütmesi (0,64·EI_eff ile Pe1), B2 (ayar dosyasından), eksenel yük + iki eksenli moment ve kesme oranı |
| `EncasedSettings_` | `EncasedSections.xml` okuma; her W kesitinden otomatik gömülü kesit üretme (beton ölçüsü + çevresel donatı) |

**Doğrulama** (bağımsız el hesaplarıyla, tümü geçti):
- Pno, EI_eff, Pn: tam eşleşme.
- Gömülü W10x45 (24×24 in): PSDM ile kapalı form AISC PNA denklemleri arasında %0,11 fark (Mp = 7584 kip-in).
- Çelik kutu, kompozit kutu ve boru plastik momentleri: ≤ %0,1 fark.
- Cm, B1 ve H1-1 uç durumları: tam eşleşme.

**Rehberden bilinçli olarak ayrılan noktalar** (AISC 360-16 metni esas alındı):
- C1 = 0,25 + 3(As + **Asr**)/Ag ≤ 0,7 (rehberde Asr yok).
- Dolgulu boru için C2 = **0,95** (rehberde 0,90).
- Boru kesme dayanımında Fcr = **min**(0,78E/(D/t)^1,5 ; 0,6Fy) (rehberde max yazıyor).
- Gömülü kesitlerde kesme dayanımı yalnızca çelik profille hesaplanır (I4.1(b)(a)).

### ETABS entegrasyonu (`ETABSClass.vb`)
- **Kompozit mod** (formda "Encased composite columns"): tüm üyeleri düşey olan çelik tasarım grupları gömülü kompozit kolon olur. Tasarım değişkeni yine W kesit indeksidir; beton ölçüsü ve donatı ondan türetilir.
- ETABS API'sinde Section Designer için kesit oluşturma metodu olmadığından her kompozit kesit **General section** olarak tanımlanır (`EC_<W adı>`):
  - Malzeme çeliktir; beton çeliğe dönüştürülür.
  - Eğilme rijitliği EI_eff, eksenel rijitlik elastik rijitliklerin toplamıdır.
  - Gerçek ağırlık ve kütle, özellik çarpanlarıyla (modifier) verilir.
- Kompozit gruplar ETABS çelik tasarımına **"No Design"** olarak işaretlenir. Kontrolleri dayanım kombinasyonlarının çubuk kuvvetleriyle programın kendi çözücüsü yapar.
- Kompozit grupların alt sınırı Lb = 0 alınır: beton sayesinde daha küçük W kesitleri de uygun olabilir. Üst sınır, çelik otomatik tasarımından gelir.
- Kompozit modda amaç fonksiyonu **göreli maliyettir**: çelik + donatı (kN) + beton (m³) + kalıp (m²). Birim fiyatlar `EncasedSections.xml` dosyasındadır. Çelik modda amaç, önceki gibi ağırlıktır (kN).

### Kullanıcı isteğiyle eklenenler
1. **Rastgele tohum (seed):** formdaki "Random seed" alanı; 0 girilirse saat bazlı tohum kullanılır.
   - `Rnd(-1): Randomize(seed)` ile her koşu farklı ve tekrarlanabilir.
   - Tohum pencere başlığına, `ErrorLog.txt` dosyasına ve sonuç XML'ine (`Seed`) yazılır.
   - Yedekten (backup) devam edilirken tohum `seed + ILoop` olarak alınır.
2. **Öteleme kombinasyonları:** "All cases and combos" ya da "Lateral (wind / earthquake) only".
   - Yatay kombinasyonlar, içindeki yük desenlerinin tipinden (Wind/Quake), ivme yüklerinden (UX/UY) veya response spectrum durumlarından otomatik bulunur. İç içe kombinasyonlar da taranır.
   - Not: ETABS analizi yine tüm durumları çözer; kazanç yalnızca sonuç okuma ve kontrol süresindedir.
3. **Otomatik yük kombinasyonları:** "Create default design combos if model has none" seçiliyse ve modelde çelik dayanım kombinasyonu yoksa `RespCombo.AddDesignDefaultCombos` ile yönetmeliğe göre varsayılan kombinasyonlar oluşturulur.
4. **Otomatik kesit listeleri:** `BeamSectionList` / `ColumnSectionList` modelde yoksa kütüphanedeki tüm W kesitleriyle oluşturulur (`PropFrame.SetAutoSelectSteel`). Liste adları `App.config` dosyasından değiştirilebilir.
5. Kullanıcı kılavuzu eklendi: `KULLANIM_KILAVUZU.md`.

### Test sırasında bulunan ve düzeltilen hatalar (525 çubuklu model, ETABS 19)
- **Modal durum ve ETABS iç durumu `~LLRF` öteleme kontrolüne giriyordu:** mod şekilleri yer değiştirme gibi okunuyordu (orijinal koddan kalma hata). Artık modal, burkulma ve `~` ile başlayan durumlar dışlanıyor.
- **Çözülemeyen bir analiz (kararsız veya yakınsamayan nonlineer durum) optimizasyonun tamamını durduruyordu.** Artık `Analyze.GetCaseStatus` ile algılanıyor; tasarım ceza = 10 alıyor ve çalışma devam ediyor.
- **ETABS 19 API'si `AISC 360-16` çelik kodunu kabul etmiyor** (`SetCode` başarısız oluyor). Çelik kirişler `AISC 360-10` ile tasarlanır; kompozit kolonlar 360-16 formülleriyle kontrol edilir.

### Test sonuçları (`525M/525Member_steel.EDB` kopyası üzerinde)
- **Çelik mod:** 14 değişken; eksik otomatik listeler oluşturuldu; sınırlar hesaplandı; 1 değerlendirme (6 analiz + tasarım) ≈ 65 s.
- **Kompozit mod + yalnızca yatay:** 10 kompozit kolon grubu; `EC_` kesitleri oluşturuldu; kompozit oranları hesaplandı; 3 değerlendirme hatasız tamamlandı.
- Form (GUI) akışı elle test edilmedi; derleme hatasız.

---

## 2026-10-01 — Aşama 1: Temel hataların düzeltilmesi ve düzenleme

### Programın çalışmasını engelleyen hatalar
- Proje derlenmiyordu: 128 hatanın tamamı `Class1.vb`'deydi. Dosya derlemeden çıkarıldı; Aşama 2'de yerine `CompositeColumn.vb` geldi.
- ETABS ve kesit kütüphanesi yolları ortam değişkeninden okunuyordu (tanımlı değildi). Artık önce `App.config`, yedek olarak ortam değişkeni okunuyor.
- CSI kütüphane formatı (`AISC14M.xml`, `<PROPERTY_FILE>`) okunamıyordu. Yeni okuyucu yazıldı: kültürden bağımsız sayı okuma, mm birim kontrolü.
- Modelde olmayan W kesitleri kütüphaneden otomatik içe aktarılıyor.
- Kolonlara otomatik liste atanırken `ItemType = Group` veriliyordu; `Objects` olmalıydı.

### Yanlış sonuç üreten hatalar
- Tasarım değişkeni indeksi ile grup indeksi karıştırılıyordu (geometri, öteleme, PMM, ceza ve maliyet hesaplarında). Eşleme artık `VarIndex` sözlüğüyle yapılıyor.
- Cezalar, düzeltme (repair) adımlarından önceki eski analiz sonuçlarıyla hesaplanıyordu. Artık tüm kısıtlar son analiz durumunda değerlendiriliyor.
- Sınır hesabında `Ub` ve `Lb` sıfırken düzeltme adımları çalışıyordu (tüm kolonlar en küçük kesite iniyordu). Bu çağrılar kaldırıldı.
- `BeamY` hesabında Y yerine X kirişleri aranıyordu.
- Öteleme çıktı listesi döngüden sonra temizlendiği için hep boş kalıyordu.
- Boş listede `.Max` çağrısı çökmeye yol açıyordu.
- Tepe ötelemesi düzeltmesi yalnızca X yönüne bakıyordu.
- "Check Structure" modu analiz yapmadan sonuç okuyordu.
- Sonuç dosyasında `Histories` hep boş yazılıyordu.
- En iyi model `.EDBbest.EDB` adıyla kaydediliyordu; artık `_best.EDB`.
- Final değerlendirmesi en iyi çözümü değiştirebiliyordu.

### Optimizasyon algoritmaları
- Dinamik HMCR hesabı her çalışmada tip hatasıyla çöküyordu; tüm maliyetler eşitken sıfıra bölme oluyordu.
- Adaptif PAR formülü `1/(1+…)` olarak düzeltildi (önceki formül hep 1'den büyük çıkıyordu).
- Rulet tekerleği seçimi dizi sınırını aşabiliyordu.
- Dandelion algoritmasında ortalama `N-1`'e bölünüyordu.
- Rastgele değişken üretimi üst sınırı seçemiyordu.
- Perde ayarı negatif yöne kayıyordu (`Floor` yerine `Round`).
- `ClearDuplicates` hiçbir tekrarı bulamıyordu ve formdaki onay kutusundan bağımsız, her zaman çalışıyordu.

### Hız ve düzen
- Düğüm yer değiştirmeleri tek API çağrısıyla okunuyor (grup `All`).
- İsimle aramalarda doğrusal arama yerine sözlük kullanılıyor.
- Tasarım kodu yalnızca bir kez atanıyor.
- `Stop` komutları kaldırıldı; hata kodları yukarıya iletiliyor.
- Hata günlüğüne zaman damgası eklendi.
- Dosya seçme penceresi ETABS'in `*.EDB` uzantısını arıyor.
