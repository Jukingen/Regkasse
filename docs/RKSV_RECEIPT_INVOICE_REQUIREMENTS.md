# RKSV — Fiş / Fatura Alanları ve Uygulama Durumu

> **Yasal uyarı:** Bu tablo Avusturya RKSV mevzuatının tam bir yorumu değildir; yalnızca **bu repoda** veri modeli, API ve yazdırma yollarında ne olduğunu belgeler. Uyumluluk iddiası veya hukuki kesinlik sağlamaz.

> **Dil:** Açıklamalar Türkçe; kullanıcı arayüzünde geçen sabit Almanca metinler (ör. şablon footer) kodda olduğu gibi alıntılanabilir.

---

## Veri kaynakları (özet)

| Kaynak | Dosya / tablo (seçme) |
|--------|------------------------|
| Fiş DTO | `backend/DTOs/ReceiptDTO.cs`, `ReceiptService.MapToDtoAsync` |
| Fiş entity | `backend/Models/Receipt.cs`, `ReceiptItem`, `ReceiptTaxLine` |
| POS faturası | `backend/Models/Invoice.cs` (`invoices`) |
| Ödeme | `backend/Models/PaymentDetails.cs` (TSE, `ReceiptNumber`, `TaxDetails` JSON) |
| TSE imza yükü | `backend/Tse/BelegdatenPayload.cs` |
| Beleg numarası | `backend/Services/ReceiptSequenceService.cs` → `AT-{RegisterNumber}-{yyyyMMdd}-{seq}` |
| Vergi oranları | `backend/Models/Product.cs` içindeki `TaxTypes` + `TaxType` enum |
| Şirket bilgisi | `CompanyProfileOptions` (`ReceiptService` / `PaymentService` içinde kullanım) |

---

## Alan bazlı inceleme

Aşağıda her satır için: **Amaç (iş/hukuk bağlamı — kesin hukuki değil)**, **Repo durumu**, **İlgili kod**, **Gösterim / yazdırma / export**, **Eksiklik riski**, **Önerilen teknik adım (kod yazılmadı)**.

### 1. Unternehmername / şirket unvanı

| | |
|--|--|
| **Amaç** | Belegde işletmenin tanımlanması (UID ile birlikte tipik RKSV seti). |
| **Durum** | **Uygulanıyor:** `ReceiptCompanyDTO.Name` ← `_companyProfile.CompanyName` (`ReceiptService.MapToDtoAsync`). `Invoice.CompanyName` POS faturasında set edilir (`PaymentService` içi invoice oluşturma). |
| **Kod** | `ReceiptService.cs`, `Invoice.cs`, `DTOs/ReceiptDTO.cs` |
| **Gösterim** | POS fiş şablonu / `receiptFormatter.ts` şirket bloğuna aktarılabilir (istemci). |
| **Risk** | Boş veya yanlış profil → müşteri belegi üzerinde hatalı unvan. |
| **Öneri** | `CompanyProfile` doğrulama ve ortam başına yapılandırma kontrolleri. |

### 2. Fortlaufende Belegnummer

| | |
|--|--|
| **Amaç** | Kasa bazında artan benzersiz beleg numarası. |
| **Durum** | **Uygulanıyor:** `ReceiptSequenceService` gün + kasa başına sıra; biçim `AT-{registerNumber}-{yyyyMMdd}-{n}`. `Receipt.ReceiptNumber`, `PaymentDetails.ReceiptNumber`, `Invoice.InvoiceNumber` ile hizalı kullanım. |
| **Kod** | `ReceiptSequenceService.cs`, `PaymentService.cs` (allocate), `Receipt.cs` |
| **Gösterim** | Evet (`ReceiptDTO.ReceiptNumber`). |
| **Risk** | Çakışma veya sıra drift’i (nadir yarış); DB unique index stratejisi repo içinde ayrı migrasyonlarla desteklenir. |
| **Öneri** | Üretim gözlemi: sequence ve `ReceiptNumber` unique ihlalleri için alarm. |

### 3. Datum der Belegausstellung

| | |
|--|--|
| **Amaç** | Belegin düzenlenme tarihi. |
| **Durum** | **Uygulanıyor:** `Receipt.IssuedAt` = ödeme `CreatedAt`; DTO’da `Date`. TSE yükünde `BelegdatenPayload.BelegDatum` (`DD.MM.YYYY` formatında doldurulur — payload sınıfı). |
| **Kod** | `ReceiptService`, `BelegdatenPayload.cs`, `ReceiptDTO.cs` |
| **Gösterim** | Evet (DTO `Date`). |
| **Risk** | Saat dilimi kayması; kod Viyana günü ve UTC dönüşümleri için `PostgreSqlUtcDateTime` kullanır (kapanış/raporlarda). |
| **Öneri** | Fiş görünümünde yerel saat dilimi netliği (IST/CEST) operasyon dokümantasyonu. |

### 4. Uhrzeit der Belegausstellung

| | |
|--|--|
| **Amaç** | Beleg düzenleme saati. |
| **Durum** | **Kısmen:** `ReceiptDTO.Date` tek `DateTime` taşır (tarih+saat); TSE payload’da `Uhrzeit` (`HH:mm:ss`). Yazdırma tarafında ayrı alanlar `receiptFormatter` içinde birleşik tarih-saatten türetilebilir. |
| **Kod** | `BelegdatenPayload.Uhrzeit`, `ReceiptDTO.Date` |
| **Gösterim** | İstemci biçimlendirmesine bağlı. |
| **Risk** | Sadece tarih gösteren şablonda saat eksikliği. |
| **Öneri** | Şablon gereksinimlerine göre açık saat satırı. |

### 5. Kassenidentifikationsnummer

| | |
|--|--|
| **Amaç** | RKSV Kassen-ID (yazıda genelde `CashRegisters.RegisterNumber`). |
| **Durum** | **Uygulanıyor:** `ReceiptDTO.KassenID` / `DisplayRegisterNumber` = `RegisterNumber`; `Invoice.KassenId` aynı metin. |
| **Kod** | `ReceiptService.MapToDtoAsync`, `Invoice.cs` |
| **Gösterim** | Evet. |
| **Risk** | GUID ile metin karıştırma; DTO yorumları register GUID değil `RegisterNumber` der. |
| **Öneri** | Eğitim: operatörler için Kassen-ID vs internal UUID ayrımı. |

### 6. Menge und handelsübliche Bezeichnung

| | |
|--|--|
| **Amaç** | Satır bazında miktar ve ticari ürün/hizmet adı. |
| **Durum** | **Uygulanıyor:** `ReceiptItem` / `ReceiptItemDTO` (`Name`, `Quantity`). Eski modifier yapısı için `+ ` önekli satırlar (`ReceiptService`). |
| **Kod** | `ReceiptService.cs`, `Models/ReceiptItem.cs` |
| **Gösterim** | Evet. |
| **Risk** | Legacy modifier satırlarında okunabilirlik. |
| **Öneri** | Phase 2 flat ürün modeline geçiş izleme (log’larda `Phase2.LegacyModifier` mesajı). |

### 7. Zahlungsbetrag (ödeme tutarı)

| | |
|--|--|
| **Amaç** | Müşterinin ödediği toplam. |
| **Durum** | **Uygulanıyor:** `Receipt.GrandTotal`, `ReceiptDTO.GrandTotal`, TSE payload `Betrag` (string formatında). |
| **Kod** | `ReceiptService`, `BelegdatenPayload` |
| **Gösterim** | Evet. |
| **Risk** | Nakit parça üstü (`Tendered`/`Change`) fiş DTO’sunda basitleştirilmiş varsayılanlar: `ReceiptService` `Payments` listesinde `Tendered = GrandTotal`, `Change = 0` atar — **nakit detayı üst düzeyde zenginleştirilmemiş olabilir**. |
| **Öneri** | Nakit işlemlerde gerçek `Tendered`/`Change` varsa ödeme kaynağından doldurma (ayrı geliştirme konusu). |

### 8. Vergi oranına göre tutar ayrımı (20%, 10%, 13%, 0%, 19%)

| Oran | **Durum** |
|------|------------|
| **20%** | **Uygulanıyor** — `TaxTypes.Standard` → `20.0m` (`Product.cs`). |
| **10%** | **Uygulanıyor** — `TaxTypes.Reduced` → `10.0m`. |
| **13%** | **Uygulanıyor** — `TaxTypes.Special` → `13.0m` (konaklama vb. sınıfı). |
| **0%** | **Uygulanıyor** — `TaxTypes.ZeroRate` / `TaxType.ZeroRate` (2026 reform notu enum’da). |
| **19%** | **Not found in current implementation** — `TaxTypes.GetTaxRate` içinde 19% dalı yok; Avusturya dışı veya geçiş oranı bu sabit tabloda **tanımlı değil**. |

**Kod:** `TaxType.cs`, `Product.cs` (`TaxTypes`), `ReceiptTaxLine` toplulaştırma `ReceiptService`.

**Gösterim:** `ReceiptDTO.TaxRates` (oran, net, vergi, brüt).

**Risk:** 19% gerektiren özel senaryo (ör. karmaşık AB B2B) bu tabloda yoksa yanlış sınıflandırma.

**Öneri:** Ürün vergisini yöneten iş kurallarına 19% eklenmesi gerekiyorsa `TaxTypes` ve validasyonların genişletilmesi (şu an yok).

### 9. Zahlungsart (ödeme yöntemi)

| | |
|--|--|
| **Amaç** | Ödemenin nasıl alındığının belgelenmesi. |
| **Durum** | **Uygulanıyor:** `Invoice.PaymentMethod` (`PaymentMethod` enum), fiş DTO’da `ReceiptPaymentDTO.Method` (`PaymentMethod.ToString()`). |
| **Kod** | `Invoice.cs`, `ReceiptService.MapToDtoAsync` |
| **Gösterim** | Evet (DTO). |
| **Risk** | Çoklu parçalı ödeme tek satırda özetlenir (liste tek eleman). |
| **Öneri** | Bölünmüş ödeme desteği gerekiyorsa veri modeli genişletmesi. |

### 10. RKSV makine okur kod / QR yükü

| | |
|--|--|
| **Amaç** | RKS-V QR metni (RKSV doğrulama için). |
| **Durum** | **Uygulanıyor:** `Receipt.QrCodePayload` ve `ReceiptSignatureDTO.QrData`; biçim `_R1-AT1_{register}_{ReceiptNumber}_{timestamp:sortable}_{total:0.00}_0.00_{certSerial}_{jws}`. |
| **Kod** | `ReceiptService.AddReceiptFromPaymentToContextAsync` |
| **Gösterim** | `receiptFormatter.ts` / `receiptPrinter.ts` QR üretimine girdi. |
| **Risk** | `0.00` sabit segmenti (payload içinde) iş kurallarına göre anlam taşır; değiştirmek imza/uyumluluk etkisi yaratır. |
| **Öneri** | RKSV spesifikasyonu ile segment anlamı için harici doğrulama (hukuk + üretici dokümanı). |

### 11. İmza / TSE / RKSV (JWS)

| | |
|--|--|
| **Amaç** | Belegin TSE ile imzalanması ve doğrulanabilirliği. |
| **Durum** | **Uygulanıyor:** `PaymentDetails.TseSignature` (compact JWS), `Receipt.SignatureValue`, ayrıştırılmış kolonlar (`SignatureFormat`, `JwsHeader`, `JwsPayload`, `JwsSignature` — `Receipt` entity). `Invoice.TseSignature` zorunlu. |
| **Kod** | `PaymentService`, `Receipt.cs`, `Invoice.cs`, `Tse` katmanı |
| **Gösterim** | `ReceiptSignatureDTO` (algoritma, seri no, zaman damgası, önceki imza, imza değeri). |
| **Risk** | TSE kapalıyken ödeme politikası (demo / zorunluluk bayrakları) ayrı iş kuralları; günlük kapanış TSE ister. |
| **Öneri** | Ortam bazlı TSE zorunluluğu runbook’u. |

### 12. Sertifika seri numarası

| | |
|--|--|
| **Amaç** | Hangi TSE sertifikası ile imzalandığının izi. |
| **Durum** | **Uygulanıyor:** `ITseService.GetTseCertificateInfoAsync` → `ReceiptSignatureDTO.SerialNumber` (`CertificateNumber`). |
| **Kod** | `ReceiptService.cs` |
| **Gösterim** | DTO’da evet. |
| **Risk** | Sertifika döngümünde eski seri no ile arşiv eşlemesi. |
| **Öneri** | Sertifika yenileme prosedürü dokümantasyonu. |

### 13. Önceki beleg imzası (zincir)

| | |
|--|--|
| **Amaç** | DEP / RKSV zincir bütünlüğü. |
| **Durum** | **Uygulanıyor:** `Receipt.PrevSignatureValue`; kaynak `PaymentDetails.PrevSignatureValueUsed` veya `SignatureChainState.LastSignature` / son fiş (`GetLastSignatureValueForCashRegisterAsync`). |
| **Kod** | `ReceiptService.cs` |
| **Gösterim** | `ReceiptSignatureDTO.PrevSignatureValue`. |
| **Risk** | Zincir kopukluğu → `IntegrityCheckService` ve fiscal export uyarıları ile ilişkili olabilir. |
| **Öneri** | `/rksv/integrity` ve `api/admin/integrity` periyodik kontrol. |

### 14. Umsatzzähler (şifreli sayaç) — ayrı alan

| | |
|--|--|
| **Amaç** | Bazı RKSV/TSE modellerinde şifreli ciro sayacı. |
| **Durum** | **Not found in current implementation** — açık bir `Umsatzzähler` veritabanı alanı veya DTO alanı **yok**; değer TSE üreticisine özgü olarak **JWS içinde** gömülü olabilir — bu **repo kanıtı dışı çıkarım**dır, kesin değildir. |
| **Kod** | — |
| **Gösterim** | Ayrı satır olarak **yok**. |
| **Risk** | Denetçi beklentisi ile UI sunumu arasında boşluk. |
| **Öneri** | Kullanılan TSE sağlayıcı dokümanına göre JWS decode runbook’u (yasal danışmanlık ile). |

### 15. DEP / denetim depo referansı

| | |
|--|--|
| **Amaç** | DEP satırı veya paket referansı. |
| **Durum** | **Kısmen:** QR yükü genelde DEP satırı **değildir** (`FinanzOnlineService` yorumu). `belegpruefung` için DEP deseni doğrulaması var. Fiscal export paketinde fiş + imza + zincir durumu bulunur (`IFiscalExportService`). Tek bir “DEP row id” alanı **Receipt üzerinde yok**. |
| **Kod** | `FinanzOnlineRkdbBelegpruefungValidator`, `FiscalExportController` |
| **Gösterim** | Doğrudan fişte DEP ref. **yok** (kanıtlanmış). |
| **Risk** | FinanzOnline TEST gönderiminde uygun `beleg` üretilememesi. |
| **Öneri** | Gerekirse üretici DEP export entegrasyonu (ayrı proje). |

### 16. Storno / Nullbeleg / Jahresbeleg özel işaretleri

| | |
|--|--|
| **Storno** | **Kısmen:** `PaymentDetails.IsStorno` → `ReceiptDTO.FiscalTraceKind = "Storno"` (`ReceiptService.MapToDtoAsync`). `Invoice` üzerinde `DocumentType`, `StornoReasonCode`, `StornoReasonText`, `CreditNote` enum değeri. Ayrı basılı “STORNO” watermark’ı **bu belgede doğrulanmadı** (şablon katmanı). |
| **Nullbeleg** | **Not found in current implementation** (özel beleg tipi akışı). |
| **Jahresbeleg** | **Not found in current implementation** (Jahres**beleg** ≠ Jahres**bericht** formal raporu; ayrı fiş tipi araması sonuç vermedi). |

**Kod:** `Invoice.cs`, `ReceiptDTO.cs`, `ReceiptService.cs`

**Risk:** İade (`Refund`) ile Storno ayrımı operasyonel olarak netleştirilmeli (`FiscalTraceKind` `"Refund"`).

**Öneri:** Kredi notası / Storno iş akışı için `InvoiceController` ve POS refund yollarının operasyon dokümantasyonu.

### 17. FinanzOnline doğrulama URL’si (metin olarak basım)

| | |
|--|--|
| **Amaç** | Vorgehensweise / doğrulama linki metni. |
| **Durum** | **Backend C# `ReceiptDTO` içinde `VerificationUrl` alanı yok** (grep: yok). Frontend tipi `frontend/types/ReceiptDTO.ts` içinde opsiyonel `verificationUrl` tanımlı; `receiptFormatter.ts` / `receiptPrinter.ts` varsa basar. API’nin doldurduğu kanıt **bulunamadı**. |
| **Kod** | `frontend/types/ReceiptDTO.ts`, `receiptFormatter.ts` |
| **Gösterim** | API doldurmazsa fişte **çıkmayabilir**. |
| **Risk** | Müşteri bilgilendirme eksikliği. |
| **Öneri** | Backend `ReceiptDTO` + `MapToDtoAsync` ile URL üretimi (ayrı iş). |

### 18. UID / Steuernummer (şirket)

| | |
|--|--|
| **Amaç** | Vergi numarası (ATU…). |
| **Durum** | **Uygulanıyor:** `ReceiptCompanyDTO.TaxNumber` — öncelik `receipt.Payment?.Steuernummer`, yoksa `_companyProfile.TaxNumber`. `Invoice.CompanyTaxNumber` zorunlu. |
| **Kod** | `ReceiptService.MapToDtoAsync`, `Invoice.cs` |
| **Gösterim** | Evet. |
| **Risk** | Ödeme satırında müşteri UID’si ile şirket UID’si karışıklığı; iş kuralı `Steuernummer` seçimine bağlı (`PaymentService`). |

---

## Yazdırma ve şablon

- **Fiziksel yazdırma:** `frontend/services/receiptPrinter.ts`, `receiptFormatter.ts`.
- **Şablon yönetimi (fiskal olmayan örnek):** `MultilingualReceiptController` — açıklamada “Non-fiscal … does not create Payment, Receipt, or TSE records” (`GenerateReceipt`).

---

## Export’ta fiş alanları

- **Fiscal export JSON:** `GET api/admin/fiscal-export` (`FiscalExportController`, `IFiscalExportService`) — paket içeriği fiş + imza + kapanış + zincir uyarıları (profil bazlı).
- **Legal export completeness:** Raporlar için kapı; doğrudan fiş alan listesi değil (`LegalExportCompletenessController`).

---

## Özet tablo — yüksek seviye

| Alan | Uygulama |
|------|-----------|
| Unternehmername | Var |
| Belegnummer | Var (`AT-…`) |
| Datum / Uhrzeit | Var (DTO `Date`; ayrı alan politikası istemci) |
| Kassen-ID | Var |
| Menge / Bezeichnung | Var |
| Zahlungsbetrag | Var |
| Steuer 20/10/13/0 | Var (sabit tablo) |
| Steuer 19% | **Yok** |
| Zahlungsart | Var (basit liste) |
| QR / RKS metni | Var |
| JWS / TSE | Var |
| Zertifikat SN | Var |
| Vorherige Signatur | Var |
| Umsatzzähler (ayrı) | **Yok** |
| DEP referansı (fiş satırı) | **Yok** (ayrı doğrulama / export katmanları var) |
| Nullbeleg / Start / Schluss / Jahresbeleg (fiş tipi) | **Yok** (Jahres**bericht** rapor ayrı) |
| RKSV verification URL (API) | **Backend’de yok** |

---

## Son kontrol listesi

### Eksik veya yüksek riskli uyumluluk alanları (teknik bakış)

1. **19% KDV** — ürün vergi tablosunda tanımlı değil (`TaxTypes`).
2. **Nullbeleg / Startbeleg / Schlussbeleg / Jahresbeleg (fiş)** — özel fiş türleri kodda yok.
3. **Umsatzzähler** — açık alan yok; TSE iç gömülü olabilir (belirsiz).
4. **FinanzOnline doğrulama URL** — backend `ReceiptDTO`’da yok; fişte basım API’ye bağlı değil.
5. **Formal raporların FinanzOnline yükü** — kod notlarında Non-DEP özet; klasik DEP dosyası üretimi bu belge kapsamında kanıtlanmadı.
6. **Nakit para üstü detayı** — `ReceiptService` ödeme listesi varsayılanları sadeleştirilmiş olabilir.

### Dokümantasyon boşlukları

- POS **reports** ekranı ile formal **Tagesbericht** ayrımı operatörlere netleştirilmeli.
- Aylık/yıllık **Tagesabschluss** sonrası FinanzOnline davranışı için kod tabanı ile operasyon runbook’u hizalanmalı.

### Önerilen sonraki geliştirme işleri (öncelik fikri — hukuki onay ayrı)

1. `VerificationUrl` için backend sözleşmesi ve POS şablonu doldurma.
2. Gerekliyse `TaxTypes` ve ürün doğrulamasına **19%** veya AB özel oranı eklenmesi (iş gereksinimi netleştikten sonra).
3. Nullbeleg / yıl başı fiş gibi RKSV özel beleg türleri için ayrı kullanıcı hikâyesi ve API tasarımı (şu an yok).
4. `ReceiptPaymentDTO` zenginleştirmesi (çoklu ödeme, nakit para üstü).
5. Aylık/yıllık kapanış için FinanzOnline gönderiminin günlük ile özdeş olup olmayacağının ürün kararı + kod hizalaması.
