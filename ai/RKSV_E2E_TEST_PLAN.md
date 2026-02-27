# RKSV Checklist 1–5 E2E Test Plan

**Tarih:** 2025-02-25  
**Kapsam:** Signature verification (VerifyDiagnostic) test matrisi, fixture’lar, API assertion’ları  
**API Endpoints:** `POST /api/Payment/verify-signature`, `GET /api/Payment/{id}/signature-debug`

---

## 1. Test Matrisi Özeti

| # | Senaryo | Beklenen | Önce Çalışan Step |
|---|---------|----------|-------------------|
| 1 | Normal (geçerli JWS) | PASS | Tümü PASS |
| 2 | CMC mismatch (farklı anahtar) | FAIL | Step 4 (Signature verify) |
| 3 | Corrupted payload | FAIL | Step 2 veya 3 veya 4 |
| 4 | DER/raw format hatası | FAIL | Step 4 |
| 5 | Base64URL padding hatası | FAIL | Step 5 |

---

## 2. Test Detayları

### 2.1 Normal → PASS

**Amaç:** Geçerli COMPACT JWS ile tüm checklist adımlarının PASS olması.

**Input fixture:**
```json
{
  "compactJws": "<SoftwareTseKeyProvider ile Sign() ile üretilen geçerli JWS>"
}
```

**Örnek Belegdaten:**
```json
{
  "kassenId": "KASSE-001",
  "belegNr": "AT-KASSE001-20250225-12345678",
  "belegDatum": "25.02.2025",
  "uhrzeit": "14:30:00",
  "betrag": "123.45",
  "prevSignatureValue": "",
  "taxDetails": "{}"
}
```

**Beklenen step sonuçları:**
| StepId | Name | Status | Evidence (örnek) |
|--------|------|--------|------------------|
| 1 | CMC match | PASS | "Software mode: key provider used (no CMC)" veya "Certificate serial X matches signing key" |
| 2 | JWS format | PASS | "header.payload.signature valid" |
| 3 | Hash | PASS | "SHA-256(N chars)" |
| 4 | Signature verify | PASS | "ES256 verification succeeded" |
| 5 | Base64URL padding | PASS | "No padding in any part" |

**Beklenen API response:**
```json
{
  "success": true,
  "message": "Verify completed",
  "data": {
    "valid": true,
    "steps": [ /* 5 adım, hepsi status: "PASS" */ ],
    "summary": "All checklist items PASS"
  },
  "timestamp": "<ISO8601>"
}
```

---

### 2.2 CMC Mismatch → FAIL

**Amaç:** JWS, A anahtarı ile imzalanmış; doğrulama B anahtarı ile yapılıyor → Step 4 FAIL.

**Input fixture:**
- KeyProvider A ile `Sign(payload)` → `compactJws`
- Pipeline KeyProvider B ile `VerifyDiagnostic(compactJws)` çağrılıyor (farklı key pair)

**Not:** Software modda Step 1 (CMC) genelde PASS; CMC FAIL sadece sertifika tabanlı provider’da tetiklenir. Bu senaryoda **Step 4 FAIL** beklenir.

**Beklenen step sonuçları:**
| StepId | Name | Status | Evidence |
|--------|------|--------|----------|
| 1 | CMC match | PASS | Software mode |
| 2 | JWS format | PASS | "header.payload.signature valid" |
| 3 | Hash | PASS | "SHA-256(N chars)" |
| 4 | Signature verify | **FAIL** | "ES256 verification failed" |
| 5 | Base64URL padding | PASS | "No padding in any part" |

**Beklenen API response:**
```json
{
  "success": true,
  "message": "Verify completed",
  "data": {
    "valid": false,
    "steps": [ /* Step 4 status: "FAIL" */ ],
    "summary": "Failed: 1 step(s)"
  },
  "timestamp": "<ISO8601>"
}
```

---

### 2.3 Corrupted Payload → FAIL

**Amaç:** Payload kısmı bozulmuş JWS → Step 2 veya 3 veya 4 FAIL.

**Input fixture:**
- Geçerli JWS: `header.payload.signature`
- Bozulmuş: `header.CORRUPTED_PAYLOAD.signature` (Base64URL decode edilemez)
  - veya `header.<geçersiz-base64>.signature`

**Alternatif:** Payload’ı geçerli Base64URL ile değiştir (örn. farklı bir payload) → Step 4 FAIL (imza eşleşmez).

**Tercih edilen fixture (Step 2 FAIL):**
```
header = eyJhbGciOiJFUzI1NiIsInR5cCI6IkpXVCJ9  (standart ES256 header)
payload = CORRUPTED_PAYLOAD_NOT_BASE64
signature = <geçerli JWS'ten alınan 3. parça>
```

**Beklenen step sonuçları:**
| StepId | Name | Status | Evidence |
|--------|------|--------|----------|
| 1 | CMC match | PASS | - |
| 2 | JWS format | **FAIL** | "Invalid Base64URL in one or more parts" veya "Part contains padding" |
| 3 | Hash | FAIL | "Invalid JWS or padding" |
| 4 | Signature verify | FAIL | - |
| 5 | Base64URL padding | PASS veya FAIL | payload'a bağlı |

**Beklenen API response:**
```json
{
  "success": true,
  "message": "Verify completed",
  "data": {
    "valid": false,
    "steps": [ /* en az bir FAIL */ ],
    "summary": "Failed: N step(s)"
  },
  "timestamp": "<ISO8601>"
}
```

---

### 2.4 DER/Raw Format Hatası → FAIL

**Amaç:** İmza parçası geçersiz format (64 byte raw veya DER değil) → Step 4 FAIL.

**Input fixture:**
- `header.payload.<base64-encoded-invalid-signature>`
- Invalid signature: Base64URL-decode sonrası anlamsız/hatalı byte dizisi (örn. rastgele 32 byte, veya bozuk DER)

**Örnek:** Geçerli JWS’ten header ve payload alınır; signature yerine `AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA` (40x'A' → 30 byte) konur.

**Beklenen step sonuçları:**
| StepId | Name | Status | Evidence |
|--------|------|--------|----------|
| 1 | CMC match | PASS | - |
| 2 | JWS format | PASS | - |
| 3 | Hash | PASS | - |
| 4 | Signature verify | **FAIL** | Exception message veya "ES256 verification failed" |
| 5 | Base64URL padding | PASS | - |

**Beklenen API response:**
```json
{
  "success": true,
  "message": "Verify completed",
  "data": {
    "valid": false,
    "steps": [ /* Step 4 status: "FAIL" */ ],
    "summary": "Failed: 1 step(s)"
  },
  "timestamp": "<ISO8601>"
}
```

---

### 2.5 Base64URL Padding Hatası → FAIL

**Amaç:** Herhangi bir parçada `=` padding → Step 5 FAIL.

**Input fixture:**
- Geçerli JWS üret
- Signature parçasına `==` ekle: `header.payload.<signature>==`

**Örnek:**
```
eyJhbGciOiJFUzI1NiIsInR5cCI6IkpXVCJ9.eyJrYXNzZW5JZCI6IktBU1NFLTAwMSJ9.ABC123...==
```

**Beklenen step sonuçları:**
| StepId | Name | Status | Evidence |
|--------|------|--------|----------|
| 1 | CMC match | PASS | - |
| 2 | JWS format | **FAIL** | "Part contains padding (see Base64URL step)" |
| 3 | Hash | FAIL | "Invalid JWS or padding" |
| 4 | Signature verify | FAIL | - |
| 5 | Base64URL padding | **FAIL** | "Padding '=' found in part" |

**Not:** Mevcut sıra: Step 2 kontrolü `p.Contains('=')` yapıyor → Step 2 FAIL de dönebilir. Step 5 açıkça padding kontrolü yapıyor. En az biri FAIL olmalı.

**Beklenen API response:**
```json
{
  "success": true,
  "message": "Verify completed",
  "data": {
    "valid": false,
    "steps": [ /* Step 5 ve muhtemelen Step 2 FAIL */ ],
    "summary": "Failed: N step(s)"
  },
  "timestamp": "<ISO8601>"
}
```

---

## 3. Ek Senaryolar (Opsiyonel)

| Senaryo | Input | Beklenen |
|---------|-------|----------|
| Empty input | `{"compactJws": ""}` | Step 1 PASS, 2–5 FAIL |
| Two parts only | `{"compactJws": "a.b"}` | Step 2 FAIL ("Expected 3 parts, got 2") |
| Null/missing compactJws | `{}` | 400 Validation failed |
| Payment not found | `GET /api/Payment/{guid}/signature-debug` | 404 |
| Payment without TseSignature | `GET /api/Payment/{id}/signature-debug` | 200, tüm steps FAIL, "No signature on payment" |

---

## 4. CI Stratejisi

### Smoke (Her Commit/PR)
- **Test:** Sadece **Normal → PASS**
- **Süre:** ~5 sn
- **Amaç:** Temel akışın çalıştığından emin olmak

### Nightly (Günlük / Haftalık)
- **Test:** Tam matris (5 senaryo + opsiyonel)
- **Süre:** ~30 sn
- **Amaç:** Tüm hata senaryolarının doğru handle edildiğini doğrulamak

---

## 5. Test Class İsimlendirme Önerisi

| Tip | Öneri | Örnek |
|-----|-------|--------|
| Unit (Pipeline) | `SignaturePipeline{Scenario}Tests` | `SignaturePipelineCmcMismatchTests` |
| Integration (API) | `RksvVerifySignature{Scenario}E2ETests` | `RksvVerifySignatureNormalE2ETests` |
| Toptan E2E | `RksvChecklistE2ETests` | Tüm matris tek class |
| Smoke | `RksvSmokeTests` | Sadece Normal → PASS |

**Önerilen yapı:**
```
backend/KasseAPI_Final.Tests/
├── SignaturePipelineTests.cs           (mevcut, unit)
├── RksvChecklistE2ETests.cs            (tam matris, POST verify-signature)
├── RksvSignatureDebugE2ETests.cs       (GET signature-debug, payment-based)
└── RksvSmokeTests.cs                   (smoke: normal PASS)
```

**Metod isimlendirme:**
- `VerifySignature_ValidJws_AllStepsPass`
- `VerifySignature_CmcMismatch_Step4Fails`
- `VerifySignature_CorruptedPayload_Step2OrLaterFails`
- `VerifySignature_DerRawFormatError_Step4Fails`
- `VerifySignature_Base64UrlPadding_Step5Fails`
- `SignatureDebug_ValidPayment_ReturnsAllPass`
- `SignatureDebug_EmptySignature_ReturnsAllFail`

---

## 6. Fixture Yardımcıları

```csharp
// Örnek helper
public static class RksvTestFixtures
{
    public static string CreateValidCompactJws(BelegdatenPayload payload, SignaturePipeline pipeline)
    {
        return pipeline.Sign(payload, "e2e-fixture");
    }

    public static string CreateCorruptedPayloadJws(string validJws)
    {
        var parts = validJws.Split('.');
        return $"{parts[0]}.CORRUPTED_PAYLOAD.{parts[2]}";
    }

    public static string CreatePaddedSignatureJws(string validJws)
    {
        var parts = validJws.Split('.');
        return $"{parts[0]}.{parts[1]}.{parts[2]}==";
    }

    public static string CreateInvalidSignatureJws(string validJws)
    {
        var parts = validJws.Split('.');
        var fakeSig = Convert.ToBase64String(new byte[32]).Replace("+", "-").Replace("/", "_").TrimEnd('=');
        return $"{parts[0]}.{parts[1]}.{fakeSig}";
    }
}
```

---

## 7. Assertion Örnekleri

```csharp
// Normal PASS
Assert.True(response.Data.Valid);
Assert.Equal(5, response.Data.Steps.Count);
Assert.All(response.Data.Steps, s => Assert.Equal("PASS", s.Status));

// CMC mismatch
Assert.False(response.Data.Valid);
var step4 = response.Data.Steps.First(s => s.StepId == 4);
Assert.Equal("FAIL", step4.Status);
Assert.Contains("ES256 verification failed", step4.Evidence ?? "");

// Base64URL padding
var step5 = response.Data.Steps.First(s => s.StepId == 5);
Assert.Equal("FAIL", step5.Status);
```
