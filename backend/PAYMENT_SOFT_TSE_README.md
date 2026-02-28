# Soft TSE & QR Code - Payment API

## Özet

Demo modda TSE cihazı olmadan RKSV akışını taklit eden "Soft TSE" ile imza üretimi ve QR Code payload oluşturma. Tek config kaynağı: `TseMode`.

---

## Config – Tek Kaynak: TseMode

```json
{
  "Tse": {
    "TseMode": "Device"
  }
}
```

| Değer   | Açıklama |
|---------|----------|
| `Off`   | TSE kapalı. tseRequired yok sayılır, sadece NON_FISCAL QR üretilir. |
| `Demo`  | Cihaz yoksa Soft TSE kullanılır. Signature chain DB'den (KassenId) devam eder. |
| `Device`| Gerçek TSE cihazı zorunlu. Cihaz yoksa ödeme başarısız. |

**Eski ayarlar kaldırıldı:** `DemoTseEnabled`, `MockEnabled` — sadece `TseMode` kullanılır.

---

## Demo / Prod Davranış Matrisi

| TseMode | tseRequired | Cihaz | Davranış | QR Format |
|---------|-------------|-------|----------|-----------|
| Off     | true/false  | -     | TSE yok sayılır, imza üretilmez | NON_FISCAL_DEMO_… |
| Off     | -           | -     | FinanzOnline atlanır | - |
| Demo    | false       | -     | İmza üretilmez | NON_FISCAL_DEMO_… |
| Demo    | true        | Yok   | Soft TSE ile imza | RKSV (_R1-AT1_…) |
| Demo    | true        | Var   | Gerçek cihaz kullanılır | RKSV |
| Device  | false       | -     | İmza üretilmez | NON_FISCAL_DEMO_… |
| Device  | true        | Yok   | Hata: cihaz gerekli | - |
| Device  | true        | Var   | Gerçek cihaz ile imza | RKSV |

---

## RKSV Uyumluluğu

| QR Tipi       | Format              | RKSV Uyumlu? |
|---------------|---------------------|--------------|
| Fiskal        | `_R1-AT1_…`         | Evet (gerçek TSE veya Soft TSE imzalı) |
| Non-fiscal    | `NON_FISCAL_DEMO_…` | Hayır – fiskal değildir |

**tseRequired=false** veya **TseMode=Off** durumunda QR payload açıkça `NON_FISCAL_DEMO_` ile başlar. UI bu prefix ile fiskal olmadığını anlayabilir; fiskal olarak işlemez.

---

## Değişen Dosyalar

| Dosya | Değişiklik |
|-------|------------|
| `Models/TseOptions.cs` | TseMode Off\|Demo\|Device, DemoTseEnabled/MockEnabled kaldırıldı |
| `appsettings.json` | `Tse`: TseMode only |
| `appsettings.Development.json` | `Tse`: TseMode=Demo |
| `Services/PaymentService.cs` | TseMode mantığı, effectiveTseRequired, NON_FISCAL_DEMO QR |
| `Controllers/PaymentController.cs` | Safe response: tse (provider, isDemoFiscal, qrPayload, receiptNumber), tseSignature yok; signature-debug: compactJws |
| `Controllers/TestController.cs` | Aynı safe tse response |

---

## Response Güvenliği

**Varsayılan ödeme response'unda** (POST /api/Payment, quick-payment):

- `tse`: sadece `provider`, `isDemoFiscal`, `qrPayload`, `receiptNumber`
- `tseSignature` (JWS) **dönmez**
- `payment` objesinde `tseSignature` **yok** (sanitize edilir)

**Admin / debug endpoint'lerinde** JWS:

- `GET /api/Payment/{id}/signature-debug` [Administrator] → `{ steps, compactJws }`
- `POST /api/Payment/verify-signature` [Administrator] → `{ valid, steps, summary }`

---

## Örnek Response (Create Payment)

```json
{
  "success": true,
  "paymentId": "c53521eb-0053-435a-b04e-54602578f62a",
  "message": "Payment created successfully",
  "payment": {
    "id": "c53521eb-0053-435a-b04e-54602578f62a",
    "totalAmount": 10.00,
    "receiptNumber": "AT-KASSE-001-20260228-011cfa48"
  },
  "tse": {
    "provider": "Demo",
    "isDemoFiscal": true,
    "qrPayload": "_R1-AT1_KASSE-001_AT-KASSE-001-20260228-011cfa48_2026-02-28T17:52:48_10.00_0.00_SW-TEST-abc12345_eyJhbGci...",
    "receiptNumber": "AT-KASSE-001-20260228-011cfa48"
  }
}
```

Non-fiscal örnek (tseRequired=false):

```json
"tse": {
  "provider": "None",
  "isDemoFiscal": true,
  "qrPayload": "NON_FISCAL_DEMO_AT-KASSE-001-20260228-011cfa48_2026-02-28T17:52:48_10.00",
  "receiptNumber": "AT-KASSE-001-20260228-011cfa48"
}
```

---

## QR Payload Formatları

- **RKSV (TSE imzalı):** `_R1-AT1_{KassenId}_{ReceiptNumber}_{Timestamp}_{TotalAmount}_0.00_{CertSerial}_{CompactJws}`
- **Non-fiscal:** `NON_FISCAL_DEMO_{ReceiptNumber}_{Timestamp}_{TotalAmount}` — fiskal değildir, UI fiskal işlem yapmamalı.

---

## Test Adımları

### 1. Backend başlat (Development: TseMode=Demo)

```bash
cd backend
dotnet run
```

### 2. Quick-payment (tseRequired=true) – Soft TSE + QR

```bash
curl -X POST "http://localhost:5183/api/Test/quick-payment?tseRequired=true"
```

Beklenen: `tse.qrPayload` `_R1-AT1_` ile başlar, `tse.provider=Demo`, `tse.receiptNumber` dolu, `tseSignature` yok.

### 3. Quick-payment (tseRequired=false) – NON_FISCAL_DEMO

```bash
curl -X POST "http://localhost:5183/api/Test/quick-payment?tseRequired=false"
```

Beklenen: `tse.qrPayload` `NON_FISCAL_DEMO_` ile başlar, `tse.provider=None`.

### 4. Signature debug (Admin) – JWS al

```bash
curl -X GET "http://localhost:5183/api/Payment/{paymentId}/signature-debug" \
  -H "Authorization: Bearer ADMIN_JWT"
```

Beklenen: `data: { steps: [...], compactJws: "eyJ..." }`

---

## Signature Chain (Demo Mod)

Demo modda `prevSignatureValueUsed` aynı `KassenId` için DB'den alınır (`PaymentDetails` ve `Receipts` tabloları). Zincir doğru devam eder.
