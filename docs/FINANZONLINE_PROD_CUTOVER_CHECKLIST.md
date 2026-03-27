# FinanzOnline PROD Cutover Readiness Checklist

Bu dokuman, FinanzOnline entegrasyonunun PROD moduna gecmeden once zorunlu kontrollerini tanimlar.

> Not: Bu checklist teknik readiness icindir. Tek basina yasal/fiskal uyum kaniti degildir.

## A) PROD Cutover Checklist

### 1) Configuration Readiness
- [ ] `FinanzOnline:Session`, `FinanzOnline:Registrierkassen`, `FinanzOnline:TransmissionQuery` endpoint/path/timeouts dogrulandi.
- [ ] `UseSimulation=false` ayarlari yalniz hedef ortamda aktif.
- [ ] `FinanzOnlineOutbox` retry/backoff/max-attempt degerleri onaylandi.
- [ ] Saat/NTP senkronizasyonu dogrulandi (token expiry ve retry scheduling icin).

### 2) Credential Handling
- [ ] Kimlik bilgileri sadece config secret kaynagindan geliyor (kod/test fixture icinde plaintext yok).
- [ ] Tenant/branch/register scope credential eslesmeleri test edildi.
- [ ] Credential rotasyonu proseduru belirlendi ve test edildi.

### 3) Session Login Verification
- [ ] TEST ortaminda login/logout basarili.
- [ ] Session expiry sonrasinda `GetValidSessionAsync` yeni session olusturuyor.
- [ ] Invalid credential durumda deterministik `CREDENTIALS_NOT_CONFIGURED` / auth hata kodu aliniyor.

### 4) TEST Submission Verification
- [ ] TEST modunda real submission en az bir desteklenen operasyon icin basarili.
- [ ] Outbox: `Pending -> Processing -> AwaitingProtocol/ProtocolSuccess` gecisi gozlemlendi.
- [ ] CorrelationId ile log + outbox + audit zinciri kurulabiliyor.

### 5) Protocol Query Verification
- [ ] `AwaitingProtocol` kayitlari query edilip final state'e geciyor.
- [ ] `ProtocolPayloadHash` ve `ProtocolSummary` alani doluyor.
- [ ] `NotFound/Unknown` case `ManualReviewRequired` olarak isaretleniyor.

### 6) Retry & Dead-Letter Verification
- [ ] Retryable hatada exponential backoff + jitter uygulaniyor.
- [ ] Max attempt asiminda `DeadLetter` terminal state'e geciliyor.
- [ ] Worker restart sonrasi stale `Processing` kayitlari recover ediliyor.

### 7) Idempotency Verification
- [ ] `IdempotencyKey` unique ihlalinde duplicate enqueue engelleniyor.
- [ ] `(TenantId, BranchId, MessageType, BusinessKey, PayloadHash, Mode)` unique kurali dogrulandi.
- [ ] Ayni business operasyonu tekrar tetiklendiginde ikinci external delivery olusmuyor.

### 8) Redaction / Logging Review
- [ ] Loglarda credential/token/secret yok.
- [ ] Yapilandirilmis loglar: `OutboxId`, `Status`, `FailureCategory`, `Attempt`, `CorrelationId`.
- [ ] Hata mesajlari operasyona uygun sekilde truncate ediliyor.

### 9) Audit Traceability
- [ ] Outbox kaydi, submission sonucu, protocol query sonucu korele edilebiliyor.
- [ ] Incident inceleme icin `CorrelationId`, `BusinessKey`, `TransmissionId` zinciri tam.

### 10) Enablement Guardrails
- [ ] PROD guard default deny (`AllowProdMode=false`) durumda.
- [ ] PROD acilisi icin explicit iki adim:
  - [ ] `AllowProdMode=true`
  - [ ] `RequireExplicitProdApproval=true` iken `ProdApprovalToken` dolu
- [ ] Degisiklik onayi ve rollback adimi runbook'a eklendi.

## B) Required Config / Env Changes

Ornek (degerler temsilidir):

```json
{
  "FinanzOnline": {
    "Session": {
      "UseSimulation": false,
      "BaseUrl": "https://...",
      "LoginPath": "/session/login",
      "LogoutPath": "/session/logout"
    },
    "Registrierkassen": {
      "UseSimulation": false,
      "EnableRealTestSubmission": true,
      "BaseUrl": "https://...",
      "SubmitPath": "/registrierkassen/submit"
    },
    "TransmissionQuery": {
      "UseSimulation": false,
      "EnableRealTestQuery": true,
      "BaseUrl": "https://...",
      "QueryPath": "/registrierkassen/query"
    },
    "CutoverGuard": {
      "AllowProdMode": false,
      "RequireExplicitProdApproval": true,
      "ProdApprovalToken": ""
    }
  },
  "FinanzOnlineOutbox": {
    "Enabled": true,
    "PollInterval": "00:00:10",
    "MaxAttempts": 8,
    "BaseDelaySeconds": 30,
    "BackoffCapSeconds": 3600,
    "JitterMaxSeconds": 15,
    "ProcessingTimeoutSeconds": 300
  }
}
```

## C) Required Manual Tests

1. Happy path TEST submission + protocol success.
2. Session expired simulation -> automatic refresh.
3. Auth failure (401/403) -> `AuthorizationFailure` + terminal state.
4. Temporary 503 -> `RetryableFailure` + backoff.
5. Unknown protocol status -> `ManualReviewRequired`.
6. Max attempt -> `DeadLetter`.
7. Duplicate enqueue attempt -> existing outbox reuse / unique constraint behavior.
8. Worker restart during `Processing` -> stale lease recovery.

## D) Suggested Rollout Stages

1. **Local simulation**
   - Tum pathlar simulation ile calisir.
2. **Integration environment**
   - Real transport endpoint smoke testleri (hala TEST mode).
3. **TEST mode with real service**
   - Outbox, query, retry, dead-letter senaryolari tam dogrulama.
4. **Controlled PROD pilot**
   - Az sayida register/tenant, zaman pencereli acilis, canli izleme.

## E) Feature-Flag / Guardrail Design

- PROD mode requesti `FinanzOnlineService` icinde cutover guard ile bloklanir.
- Default davranis: PROD kapali.
- PROD acilisi iki kosul ister:
  - `AllowProdMode=true`
  - `RequireExplicitProdApproval=true` ise `ProdApprovalToken` bos olmamali
- TEST/PROD arasi otomatik fallback yasak.
- Guard ihlalinde submission enqueue edilmez; hata deterministik kayitlanir.

---

## Incident Runbook (Concise)

### Retryable Incident
1. `RetryableFailure` kayitlarini listele (`CorrelationId`, `LastErrorCode`, `AttemptCount`).
2. Endpoint/network durumu dogrula.
3. Backoff penceresini bekle; gerekirse kontrollu manual retry tetikle.

### Permanent Failure
1. `PermanentFailure` veya `DeadLetter` kaydini ac.
2. `LastResponseJson` ve `ProtocolSummary` incele.
3. Veri/kurala dayali duzeltme gerekiyorsa yeni business operasyonu ile yeniden gonder.

### Credential Failure
1. `AuthorizationFailure`/`SessionFailure` kayitlarini filtrele.
2. Scoped credential konfigunu dogrula ve rotate et.
3. Session login testi yap, sonra sadece etkilenen kayitlari yeniden dene.

### Protocol Mismatch
1. `ManualReviewRequired` kaydinda `TransmissionId`, `ProtocolPayloadHash`, `ProtocolSummary` incele.
2. Uzak servis cevabini operator tarafinda dogrula.
3. Sonucu netlestirene kadar otomatik state gecisi yapma.

### Duplicate/Conflict Suspicion
1. `BusinessKey + PayloadHash + Mode` bazinda outbox kayitlarini kontrol et.
2. `IdempotencyKey` unique davranisini dogrula.
3. Ayni islem icin ikinci external submit varsa incident ac ve manuel reconciliation uygula.
