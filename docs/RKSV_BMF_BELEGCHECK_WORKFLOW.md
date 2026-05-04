# BMF Belegcheck — Manuel İş Akışı ve Sistem Özeti

> **Yasal uyarı:** Bu belge **operasyonel rehberlik** içindir; **hukuki danışmanlık değildir**. Avusturya RKSV, FinanzOnline ve BMF süreçleri için bağlayıcı bilgi ve son tarihler için yetkili merciler, resmi kılavuzlar ve uzman görüşü esas alınmalıdır.

> **Dil:** Açıklamalar **Türkçe**; kullanıcı arayüzünde ve resmi süreçte yerleşik **Almanca** terimler (ör. **Startbeleg**, **Jahresbeleg**, **FinanzOnline**, **BMF Belegcheck**) bu metinde de **Almanca** bırakılmıştır.

---

## 1. BMF Belegcheck nedir?

**BMF Belegcheck**, Federal Maliye Bakanlığı (**BMF**) çevresindeki kayıtlı kasa (**Registrierkasse**) dünyasında, bir **Beleg** (fiş) üzerindeki **RKSV-QR kodunun** okunup doğrulanmasına yönelik süreç ve araçlar bütünü olarak düşünülebilir. Pratikte operatörler genelde:

- Mobil cihazda **BMF Belegcheck** uygulaması ile QR tarar,
- Gerekirse **FinanzOnline** tarafında kimlik doğrulama / kod ile oturum açar,
- Uygulamanın verdiği **doğrulama sonucunu** (geçerli / geçersiz ve gerekçe) işletme kayıtlarına veya arşive not düşer.

Bu doküman, özellikle **Startbeleg** ve **Jahresbeleg** için hem **manuel** (uygulama + QR) hem de **sistem içi** (admin paneli, kuyruk, durum alanları) izlenecek yolu özetler; diğer **Sonderbelege** için ise **manuel doğrulama** ile **otomatik FinanzOnline gönderiminin** ayrımını netleştirir.

---

## 2. Hangi fişler ilgilidir?

### 2.1. Manuel BMF Belegcheck (QR) — tüm ilgili Sonderbelege

Aşağıdaki RKSV **Sonderbelege** için fiş üzerindeki **RKSV-QR**, resmi **BMF Belegcheck** uygulaması ile taranıp sonuç kaydedilebilir; bu yol **kod tarafında zorunlu kılınmaz**, operasyonel tercihtir.

| Fiş türü | Bu repoda otomatik FinanzOnline “RKSV submission” izi (`RksvSpecialReceiptFinanzOnlineSubmission` / fiş detayı kartı) | Manuel Belegcheck (QR) |
|----------|---------------------------------------------------------------------------------------------------------------------|-------------------------|
| **Nullbeleg** | Yok — yalnızca manuel doğrulama (uygulama / operasyon notu). | Evet (QR okunabilir ise). |
| **Monatsbeleg** | Yok — yalnızca manuel doğrulama. | Evet. |
| **Startbeleg** | Var — oluşturma sonrası outbox + submission satırı; worker hattı. | Evet (önerilen operasyonel tamamlayıcı). |
| **Jahresbeleg** | Var — oluşturma sonrası outbox + submission satırı; worker hattı. | Evet. |
| **Schlussbeleg** | Yok — `RksvSpecialReceiptService` bu tür için RKSV özel fiş outbox kuyruğunu tetiklemez; **otomatik RKSV webservice gönderimi yoktur**. | Evet (QR ile). |

Özet: **FinanzOnline üzerinden izlenen otomatik RKSV özel fiş gönderimi** yalnızca **Startbeleg** ve **Jahresbeleg** oluşturma yollarında tanımlıdır; **Nullbeleg** ve **Monatsbeleg** için sistem bu anlamda yalnızca manuel doğrulamaya uygundur; **Schlussbeleg** otomatik bu hatla gönderilmez.

### 2.2. Fatura mutabakatı (legacy) ile RKSV submission farkı

| Kavram | Ne günceller / ne izler? | Tipik admin girişi |
|--------|---------------------------|-------------------|
| **Fatura / ödeme satırı FinanzOnline mutabakatı (legacy)** | `PaymentService.RetryFinanzOnlineSubmitAsync` → `FinanzOnlineService.SubmitInvoiceAsync` ile **fatura** gönderimi; `PaymentDetails` üzerindeki **FinanzOnlineStatus** vb. mutabakat alanları (`FinanzOnlineReconciliationController`, `POST api/admin/finanzonline-reconciliation/retry/{paymentId}`). | FinanzOnline-Abgleich **(Legacy)** kuyruğu / retry. |
| **RKSV özel fiş FinanzOnline submission izi** | **Startbeleg** / **Jahresbeleg** için `rksv_special_receipt_finanz_online_submissions` ve ilgili **FinanzOnline outbox** mesaj türleri (`RksvStartbelegSubmission`, `RksvJahresbelegSubmission`); fiş DTO’sunda `RksvFinanzOnlineSubmission`. | FinanzOnline **outbox** worker işleyişi; **Beleg** detayındaki RKSV FinanzOnline kartı. |

**Legacy admin retry** (`.../finanzonline-reconciliation/retry/{paymentId}`) **fatura** gönderim yolunu yeniler; **Startbeleg/Jahresbeleg** için fiş detayında gösterilen **RKSV submission** durumunu bu endpoint **güncellemez** (farklı veri modeli ve işlem hattı).

---

## 3. Manuel iş akışı (operatör)

Aşağıdaki sıra tipik bir **manuel BMF Belegcheck** hazırlığıdır; ortam ve yetkilere göre küçük farklar olabilir.

### 3.1. Fişi oluşturma (**create receipt**)

1. İlgili **Sonderbeleg** türü için yetkili kullanıcı, **admin** tarafında RKSV **Sonderbeleg** akışını tamamlar (**Startbeleg** / **Jahresbeleg** / diğerleri — bkz. §2.1).
2. Admin tarafında özet: **RKSV Sonderbelege** ekranı (`frontend-admin` içinde RKSV menüsü altı) veya **Belegliste** üzerinden ilgili fişe gidilir. FinanzOnline **RKSV submission** özet kartı yalnızca **Startbeleg** ve **Jahresbeleg** detaylarında anlamlıdır.
3. Fiş oluşturulduğunda sunucuda **Receipt** / ödeme kaydı ve TSE imzası üretimi backend kurallarına göre tamamlanır (detay: `docs/RKSV_RECEIPT_INVOICE_REQUIREMENTS.md`, `docs/RKSV_CASH_REGISTER_OPERATIONS.md`).

### 3.2. Fişi yazdırma (**print receipt**)

1. Fişin fiziksel veya PDF kopyası, QR kodun **okunabilir** kalması için yazdırılır veya dışa aktarılır.
2. QR bozulmuş, kesilmiş veya kontrast yetersizse mobil uygulama okuyamaz (bkz. [Bölüm 7](#7-sorun-giderme-troubleshooting)).

### 3.3. QR ile **BMF Belegcheck** uygulamasında tarama

1. Resmi **BMF Belegcheck** mobil uygulaması açılır.
2. Fiş üzerindeki **RKSV-QR** kod taranır.
3. Uygulama, kodun format ve imza zinciri açısından özet sonuç gösterir.

### 3.4. Gerekirse **FinanzOnline** kodu ile kimlik doğrulama

1. Bazı senaryolarda uygulama, işletmenin **FinanzOnline** oturumu veya tek kullanımlık doğrulama kodu ister.
2. Kod, güvenli kanaldan (yetkili kişi) girilir; **parola / client secret** gibi sırlar bu dokümanda ve operasyon notlarında **yazılmamalı**, ekran görüntüsü paylaşılmamalıdır.

### 3.5. Sonucu doğrulama (**verify result**)

1. Uygulama çıktısı (ör. geçerli / uyarı / hata kodu) kaydedilir.
2. İşletme içi kalite kontrol: fiş numarası, tarih ve kasa (**Kassen-ID**) ile uygulama sonucunun eşleştiği teyit edilir.

### 3.6. Arşivleme (**archive receipt / result**)

1. Yazdırılmış fiş veya PDF, uygulama ekran görüntüsü (kurumsal politika uygunsa) veya resmi çıktı, **denetim süresi** boyunca erişilebilir arşivde saklanır.
2. Sadece kişisel veri minimizasyonu ve DSGVO / iç politika kurallarına uygunluk gözetilir.

---

## 4. Sistem içi iş akışı (bu repo)

Aşağıdaki tablo, geliştirici / sistem yöneticisi için **kod ve arayüz** üzerinden izlenecek yerleri özetler; hukuki “yeterlilik” iddiası taşımaz.

| Adım | Ne oluyor? | Nerede izlenir / görünür? |
|------|------------|---------------------------|
| Fiş oluşturma | **Startbeleg** / **Jahresbeleg** üretimi backend’de özel fiş servisleri ve ödeme/fiş kayıtları ile yapılır. | Backend: `RksvSpecialReceiptsController`, `RksvSpecialReceiptService` (özet). Admin: **RKSV Sonderbelege** sayfası, fiş oluşturma butonları. |
| QR görünürlüğü | Fiş DTO’sunda imza bloğu içinde QR verisi taşınır; yazdırma POS/admin akışına bağlıdır. | Admin **Beleg** detayı: `ReceiptDTO` / detay kartında QR alanı; şablon ve yazıcı yolları POS tarafında. |
| Durum izleme (RKSV özel fiş FinanzOnline hattı) | Yalnızca **Startbeleg** ve **Jahresbeleg** için gönderim yaşam döngüsü `rksv_special_receipt_finanz_online_submissions` + outbox (`RksvSpecialReceiptFinanzOnlineOutboxHandler`). **Nullbeleg**, **Monatsbeleg**, **Schlussbeleg** bu tabloda izlenmez. | Admin **RKSV Sonderbelege** tablosunda (yalnızca izlenen türler için) durum sütunu; **Beleg** detayında RKSV FinanzOnline kartı. |
| Hata görünümü | Son hata kodu / mesajı ve son deneme zamanı DTO’ya yansıtılır (ham credential yok). | Aynı detay kartı ve loglar (teknik loglar İngilizce politika ile; operatör metni Almanca ekranlarda). |

Admin içi **QR format kontrolü** (sunucuya parse isteği) için ayrıca **Belegcheck** benzeri bir doğrulama sayfası (`/rksv/belegcheck` vb.) kullanılabilir; bu, BMF uygulamasının yerine geçmez — **ek doğrulama** veya destek amaçlıdır.

---

## 5. Webservice (otomatik gönderim) iş akışı

**FinanzOnline** webservice entegrasyonu ve outbox mimarisi açıksa (şirket / TSE ayarları ve worker işleyişi):

- Sistem, uygun mesajları **kuyruğa alır** ve işler (**queue → submit**),
- Başarılı protokol adımlarında durum **Submitted** / **Verified** gibi değerlerle güncellenebilir,
- Geçici ağ veya oturum hatalarında outbox **worker** yeniden deneme politikası uygulanır (RKSV özel fiş mesajları dahil). **Legacy ödeme satırı reconciliation** ekranındaki retry ise **fatura** gönderimine aittir; bkz. §2.2.

Entegrasyon **kapalı** veya iş kuralı gereği manuel onay bekleniyorsa:

- İzleme satırında **ManualVerificationRequired** (manuel doğrulama gerekli) anlamına gelebilen durumlar görülebilir; operatör **BMF Belegcheck** veya resmi süreçle sonucu tamamlar ve arşiv notunu günceller.

> **Not:** “Webservice açık/kapalı” tam eşlemenin her ortamda aynı olduğu garanti edilmez; üretimde `FinanzOnline` yapılandırması, outbox ekranı ve ilgili loglar birlikte okunmalıdır.

---

## 6. Son tarihler (**deadlines**)

- **Jahresbeleg** için BMF / FinanzOnline tarafında **yasal son tarih** ve resmi formülasyon, yalnızca **yetkili kaynaklardan** teyit edilmelidir; bu belge bağlayıcı takvim vermez.
- **Operasyonel not (hukuki tavsiye değil):** birçok işletme pratiğinde yıllık fişin **Belegcheck** / bildirim tarafı **takip eden yılın 15 Şubat’ına** kadar tamamlanmış olması hedeflenir. Bu ifade **işletme içi planlama** içindir; mevzuat metninin yerine geçmez.

---

## 7. Sorun giderme (**troubleshooting**)

| Belirti | Olası neden | Önerilen kontrol |
|---------|-------------|------------------|
| **QR missing** (QR yok) | Yazdırma kırpması, şablon hatası veya imza bloğunun fişe düşmemesi. | POS/admin fiş önizlemesi; **Beleg** detayında QR alanı; yazıcı DPI / kesim alanları. |
| **Invalid QR** (geçersiz QR) | Bozuk payload, yanlış kasa sırası, imza zinciri uyumsuzluğu. | **BMF Belegcheck** sonuç kodu; admin **Belegcheck** doğrulama sayfası (varsa) ile format kontrolü; TSE zincir logları (teknik). |
| **TSE unavailable** | İmzalama anında donanım veya servis kesintisi. | TSE bağlantı uyarıları (POS), backend TSE hata logları; fiş oluşturma öncesi kasa **Bereitschaft** kontrolleri. |
| **FinanzOnline credentials missing** | API kullanıcısı / sertifika / şirket ayarı eksik. | Şirket FinanzOnline ayarları; yetkili kullanıcı; **credential** değerleri operasyon dokümanına **yazılmamalı**. |
| **Failed submission** | Ağ, oturum, doğrulama veya servis reddi (RKSV özel fiş outbox hattı veya fatura hattı). | **Startbeleg/Jahresbeleg:** fiş detayı + FinanzOnline **outbox** (RKSV mesaj türü). **Legacy retry** fatura alanlarını etkiler; RKSV submission satırını güncellemez (§2.2). |

---

## İlgili dokümanlar

- `docs/RKSV_RECEIPT_INVOICE_REQUIREMENTS.md` — Fiş alanları ve uygulama durumu.
- `docs/RKSV_CASH_REGISTER_OPERATIONS.md` — Kasa ve RKSV operasyonları (admin yolları, izinler).
- `docs/RKSV_OFFICIAL_SOURCES.md` — BMF / FinanzOnline / RIS bağlantıları (dış kaynak).

---

*Belge sonu — operasyonel rehber; hukuki danışmanlık değildir.*
