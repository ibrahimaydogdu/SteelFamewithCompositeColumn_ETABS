# Değişiklik Kaydı

Kompozit kolonlu uzay çelik çerçeve optimizasyon programı (VB.NET, ETABS 19 OAPI).
Orijinal kaynak dosyaların yedeği: `_yedek_asama1/`

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
