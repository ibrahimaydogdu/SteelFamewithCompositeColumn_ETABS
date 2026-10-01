# Değişiklik Kaydı

Kompozit kolonlu uzay çelik çerçeve optimizasyon programı (VB.NET, ETABS 22 / ETABS 19 OAPI).
Orijinal kaynak dosyaların yedeği: `_yedek_asama1/`. Aşama 2 sonrası durum git'te (`7e6b41f`).

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
