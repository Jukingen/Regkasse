# RKSV İmza Checklist Uyum Analizi ve Konsolidasyon Planı

**Tarih:** 2025-02-25  
**Kapsam:** Checklist 1–5 maddeleri, referans dosyalar (Models + Services)  
**Politika:** Yeni paralel servis ailesi açılmadan mevcut TseService/ReceiptService konsolidasyonu

---

## 1) Checklist Maddesi → Mevcut Kod Eksikliği Haritası

### Checklist 1: CMC / Sertifika–Anahtar Eşleşmesi

| Dosya | Satır | Problem | Checklist Referansı |
|-------|-------|---------|---------------------|
| `TseService.cs` | 265–276 | `GetTseCertificateInfoAsync` sertifika bilgisini simüle ediyor; CMC parse, public key extraction, imza anahtarı eşleşmesi yok | 1 |
| `TseService.cs` | 149–158 | `CreateInvoiceSignatureAsync` TSE cihazından sertifika/anahtar almıyor; sadece string concatenation | 1 |
| `TseController.cs` | 257–267 | `GenerateTseSignature` gerçek TSE cihazına bağlanmıyor; CMC/sertifika kullanımı yok | 1 |
| `TseSignature.cs` | 34–35 | `TseDeviceId`, `CertificateNumber` var ama gerçek CMC/sertifika doğrulamasına bağlanmıyor | 1 |
| `ReceiptService.cs` | 238, 244 | `DEMO-CERT`, `DEMO-123456` sabit; gerçek sertifika zinciri yok | 1 |

---

### Checklist 2: JWS header.payload Canonicalization

| Dosya | Satır | Problem | Checklist Referansı |
|-------|-------|---------|---------------------|
| `TseService.cs` | 141, 146 | İmza `MOCK_...` veya `TSE_...` formatında; JWS `header.payload.signature` yapısı yok | 2 |
| `TseController.cs` | 264–266 | `GenerateTseSignature` JWS üretmiyor | 2 |
| `ReceiptService.cs` | 228–248 | `GenerateRKSVSignature` JWS formatı kullanmıyor; sadece hash + QR string | 2 |
| `TseSignature.cs` | 15–16 | `Signature` alanı serbest string; JWS compact/compact-serialization desteği yok | 2 |
| `PaymentDetails.cs` | 71–72 | `TseSignature` JWS değil, rastgele string | 2 |
| `Invoice.cs` | 76–77 | `TseSignature` JWS değil | 2 |

---

### Checklist 3: SHA-256 Hash (Doğru Input Üzerinde)

| Dosya | Satır | Problem | Checklist Referansı |
|-------|-------|---------|---------------------|
| `ReceiptService.cs` | 231–235 | `dataString` formatı RKSV Belegdaten canonical formatıyla uyuşmayabilir; `ReceiptNumber|CreatedAt|TotalAmount` yetersiz | 3 |
| `ReceiptService.cs` | 233 | SHA-256 kullanılıyor ancak input: vergi satırları, KassenId, önceki imza zinciri eksik | 3 |
| `TseService.cs` | 141–220 | Hiç SHA-256 kullanılmıyor; sadece string concat | 3 |
| `TseController.cs` | 257–267 | Hash yok | 3 |

---

### Checklist 4: ES256 İmza (DER vs Raw R||S Yönetimi)

| Dosya | Satır | Problem | Checklist Referansı |
|-------|-------|---------|---------------------|
| `TseService.cs` | 135–164 | Gerçek ES256/ECDSA imza üretilmiyor; `TSE_...` veya `MOCK_...` string dönüyor | 4 |
| `ReceiptService.cs` | 233–235 | Sadece SHA-256 hash; hash üzerinde ES256 imza yok | 4 |
| `ReceiptService.cs` | 242 | `Algorithm = "ES256 (Demo)"` etiket var ama imza ES256 değil | 4 |
| `PaymentService.cs` | 1053 | `Algorithm = "ES256"` sadece DTO'da; gerçek imza ES256 değil | 4 |
| `ReceiptDTO.cs` | 81 | `Algorithm = "ES256"` varsayılan; DER vs raw R||S dönüşümü yok | 4 |

---

### Checklist 5: Base64URL No-Padding Format

| Dosya | Satır | Problem | Checklist Referansı |
|-------|-------|---------|---------------------|
| `ReceiptService.cs` | 235 | `Convert.ToBase64String(hashBytes)` → standart Base64, padding var; Base64URL no-padding değil | 5 |
| `TseService.cs` | 141, 146 | İmza Base64URL formatında değil; plain text | 5 |
| `TseController.cs` | 264–266 | İmza Base64URL değil | 5 |
| Tüm modeller | — | JWS compact format `base64url(header).base64url(payload).base64url(signature)` gereksinimi karşılanmıyor | 5 |

---

## 2) Mock / Simülasyon Üreten Noktalar

| Dosya | Satır | İfade | Açıklama |
|-------|-------|-------|----------|
| `TseService.cs` | 22 | `IsMockEnabled` | Config ile mock modu açılıyor |
| `TseService.cs` | 28–45 | `GetTseStatusAsync` | `MOCK_DEVICE`, `MOCK_SERIAL_123` dönüyor |
| `TseService.cs` | 139–143 | `CreateInvoiceSignatureAsync` | `MOCK_{cashRegisterId}_{invoiceNumber}_{totalAmount}_{timestamp}` |
| `TseService.cs` | 146 | `CreateInvoiceSignatureAsync` | `TSE_{...}` simüle imza |
| `TseService.cs` | 170–220 | Daily/Monthly/Yearly Closing | `TSE_DAILY_...`, `TSE_MONTHLY_...`, `TSE_YEARLY_...` |
| `TseService.cs` | 266–275 | `GetTseCertificateInfoAsync` | `CERT_{device.SerialNumber}` simüle sertifika |
| `TseService.cs` | 332–347 | `GetDeviceStatusAsync` | `MOCK_DEVICE`, `MOCK_SERIAL_123` |
| `TseController.cs` | 78, 89 | `SimulateTseConnection` | Bağlantı simülasyonu |
| `TseController.cs` | 257–267 | `GenerateTseSignature` | `TSE-{Serial}-{timestamp}-{Guid}` |
| `ReceiptService.cs` | 113 | `PrevSignatureValue = "DEMO-PREV-SIG"` | Sabit demo önceki imza |
| `ReceiptService.cs` | 170, 174 | `MapToDTO` | `Algorithm = "ES256"`, `SerialNumber = "DEMO-CERT-123"` |
| `ReceiptService.cs` | 228–248 | `GenerateRKSVSignature` | `DEMO-CERT`, `DEMO-123456`, SHA256-only "imza" |
| `ReceiptService.cs` | 238 | `qrPayload` | `_R1-AT1_..._DEMO-CERT_{signatureValue}` |
| `PaymentService.cs` | 1054–1055 | `GetReceiptDataAsync` | `SerialNumber = "DEMO-SERIAL-123"` |
| `InvoiceController.cs` | 1010–1011 | `GenerateInvoiceNumber` | `Random().Next(1000,9999)` → fatura numarası için random |

---

## 3) Refactor Planı

### Aşama 1: Cleanup (Mock/Simülasyon Temizliği ve Ortak Nokta Toplama)

| Adım | Dosya | Eylem | Kaldırılacak Eski Kod |
|------|-------|-------|------------------------|
| 1.1 | `TseService.cs` | Mock branch’leri ayrı `TseMockProvider` veya config guard ile sınırla; production path’te mock imza üretimini kes | Satır 139–143 (MOCK_), 28–45, 332–347 (MOCK_DEVICE) |
| 1.2 | `ReceiptService.cs` | `GenerateRKSVSignature` içindeki demo sabitleri (`DEMO-PREV-SIG`, `DEMO-CERT`, `DEMO-123456`) kaldır veya ITseService’ten gelen gerçek değerlerle değiştir | Satır 113, 238, 244 |
| 1.3 | `TseController.cs` | `GenerateTseSignature` simülasyonunu ITseService.CreateInvoiceSignatureAsync’e yönlendir; controller’da doğrudan imza üretme | Satır 257–267 |
| 1.4 | `PaymentService.cs` | `GetReceiptDataAsync` içindeki `SerialNumber = "DEMO-SERIAL-123"` → TseService/Certificate’ten al | Satır 1054–1055 |
| 1.5 | `InvoiceController.cs` | `GenerateInvoiceNumber` random kısmını receipt numbering policy’ye uygun sequence ile değiştir (07_DO_NOT_TOUCH) | Satır 1010–1011 |

---

### Aşama 2: Gerçek SignaturePipeline Entegrasyonu

| Adım | Dosya | Eylem | Entegre Edilecek Yeni Mantık |
|------|-------|-------|------------------------------|
| 2.1 | `TseService.cs` | Tek bir `CreateSignatureAsync(Belegdaten payload)` metodu tanımla; içinde: (1) canonical Belegdaten oluşturma, (2) SHA-256 hash, (3) ES256 imza, (4) JWS compact serialization | Checklist 2, 3, 4, 5 |
| 2.2 | `TseService.cs` | CMC/sertifika yükleme ve public key ile imza anahtarı eşleşmesi; `GetTseCertificateInfoAsync` gerçek TSE cihazından veri alacak şekilde güncelle | Checklist 1 |
| 2.3 | `ReceiptService.cs` | `GenerateRKSVSignature` → ITseService.CreateSignatureAsync’e çağrı yapacak; kendi hash/imza üretmeyi bırakacak | Mevcut 228–248 yerine TseService delegasyonu |
| 2.4 | `TseSignature.cs`, `PaymentDetails.cs`, `Receipt.cs`, `Invoice.cs` | `TseSignature` / `SignatureValue` alanları JWS compact string tutacak; mevcut alan adları korunabilir, format değişir | — |
| 2.5 | Ortak utility | Base64URL no-padding helper; SHA-256 + ES256 (raw R||S → JWS uyumlu format) | Yeni helper sınıfı; mevcut TseService içinde veya `TseCryptoHelper` (PR’da hangi eski kod kaldırıldı belirtilecek) |

**PR kuralı:** Yeni sınıf/helper eklenirse hangi eski fonksiyonun (dosya + satır) kaldırıldığı PR açıklamasında belirtilecek.

---

### Aşama 3: Verify + Logging + Test

| Adım | Dosya | Eylem |
|------|-------|-------|
| 3.1 | `TseService.cs` | `ValidateTseSignatureAsync` → JWS parse, Base64URL decode, ES256 verify, sertifika chain kontrolü |
| 3.2 | `AuditLogService.cs` | TSE imza üretimi/doğrulama başarı/başarısızlık audit log’a eklenmeli (mevcut `LogPaymentOperationAsync` tseSignature parametresi kullanılıyor, doğrulama sonucu da loglanmalı) |
| 3.3 | Test | Unit: canonical input, SHA-256, Base64URL, JWS roundtrip; Integration: TSE cihazı olmadan fiskaly/Epson SDK mock ile pipeline test |

---

## 4) Özet Tablo: Checklist → Eksiklik Özeti

| Checklist | Eksik | Kritik Dosyalar |
|-----------|-------|-----------------|
| 1) CMC / sertifika-anahtar | CMC parse, anahtar eşleşmesi yok | TseService, TseController |
| 2) JWS canonicalization | JWS yapısı hiç yok | TseService, ReceiptService, modeller |
| 3) SHA-256 doğru input | Input formatı RKSV’ye uygun değil; TseService hash yapmıyor | ReceiptService, TseService |
| 4) ES256 (DER/raw) | Gerçek ES256 imza yok, sadece string/hash | TseService, ReceiptService |
| 5) Base64URL no-padding | `Convert.ToBase64String` kullanılıyor | ReceiptService |

---

## 5) Do-Not-Touch Uyumu

- `07_DO_NOT_TOUCH.md` ve `05_SECURITY_COMPLIANCE.md` ile uyum: TSE imza akışına dokunulacak; değişiklikler açık scope ile yapılacak.
- Receipt numbering, daily closing, FinanzOnline mapping bu plan kapsamında değiştirilmedi.
- Audit trail: Aşama 3’te doğrulama log’ları eklenecek.
