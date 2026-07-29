# RKSV Uyumluluk Değerlendirme Raporu

**Tarih:** 2026-07-29 (ilk değerlendirme)  
**Son güncelleme:** 2026-07-29 — P0–P2 kod kapanışı + final validation checklist  
**Kapsam:** `backend/`, `frontend/`, `frontend-admin/`, `docs/` (kod + yapılandırma + dokümantasyon kanıtı)  
**Yöntem:** Gereksinim → uygulama eşlemesi (Adım 1–4 analizlerinin birleşimi)

> **Önemli uyarı:** Bu rapor **yazılım kanıtına** dayanır. BMF/FinanzOnline resmi kabulü, TSE donanım onayı veya yasal “RKSV sertifikası” iddiası değildir. Kaynak: `ai/05_SECURITY_COMPLIANCE.md`.

**İlgili hub dokümanlar:** [`RKSV_ACTION_PLAN.md`](RKSV_ACTION_PLAN.md) · [`RKSV_FINAL_VALIDATION_CHECKLIST.md`](RKSV_FINAL_VALIDATION_CHECKLIST.md) (sign-off) · `docs/RKSV_COMPLIANCE.md` · `docs/DEP_EXPORT_DEVELOPMENT.md` · `docs/RKSV_CASH_REGISTER_OPERATIONS.md` · `docs/FINANZONLINE_PROD_CUTOVER_CHECKLIST.md` · `docs/RKSV_OFFICIAL_SOURCES.md` · `AGENTS.md` § Fiscal Rules.

---

## 1. Özet Tablo

| # | Gereksinim | Durum | Tamamlanma |
|---|------------|--------|------------|
| 1 | **Signaturerstellungseinheit (SCU / TSE)** — her mali işlemin elektronik imzası | ✅ Tam* | Önceden (çekirdek) |
| 2 | **Datenerfassungsprotokoll (DEP)** — BMF Signaturjournal dışa aktarımı | ✅ Tam* | F1–F5 + P2-1…P2-3 (2026-07-29) |
| 3 | **Beleg (fiş)** — yasal geçerli müşteri fişi + QR/machine code | ✅ Tam* | Önceden (çekirdek) |
| 4 | **FinanzOnline** — kasa kaydı + Sonderbeleg gönderimi | ✅ Kod hazır† | P0-1, P1-1, P1-2 (2026-07-29) |
| 5a | **Sürekli:** Signaturkarte / sertifika periyodik yenileme | 🟡 Kısmen‡ | Mevcut `TseCertificateService`; P1-4 açık |
| 5b | **Sürekli:** Arıza / sistem değişikliği → FinanzOnline bildirimi | ✅ Kod hazır† | P0-3 (2026-07-29) |
| 5c | **Sürekli:** Mayıs 2027 Signaturkarte değişim zorunluluğu | ✅ Kod + FA† | P1-3 (2026-07-29) |

\* “Tam” = çekirdek yazılım yüzeyi karşılanıyor; üretim yapılandırması (gerçek SCU, Soft TSE kapalı, Prüftool/crypto eşleşmesi) operatör sorumluluğundadır.  
† Kod yüzeyi tamam; **BMF TEST/PROD E2E / live cutover kanıtı** Ops+Compliance sign-off’una bağlı — bkz. [`RKSV_FINAL_VALIDATION_CHECKLIST.md`](RKSV_FINAL_VALIDATION_CHECKLIST.md).  
‡ Yenileme API + uyarı var; vendor runbook / fleet i18n (P1-4) henüz kapanmadı.

| Alt konu (derin analiz) | Durum | Tamamlanma |
|-------------------------|--------|------------|
| DEP BMF `Belege-Gruppe` şeması | ✅ Tam | F1–F5 |
| DEP normal + özel + daily closing kapsamı | ✅ Tam (varsayılan bayraklar) | F1–F5 |
| DEP thumbprint / leaf / CA zinciri | ✅ Tam (leaf hard-fail; CA boş → uyarı) | P2-3 leaf (2026-07-29) |
| DEP Prüftool (`verify-rksv-dep-export.ps1`) | ✅ Tam (fixture + [CI](../.github/workflows/dep-prueftool.yml)) | P2-1 (2026-07-29) |
| Pre-F5 legacy JWS uyarı | ✅ Tam (envelope + FA + history) | P2-2 (2026-07-29) |
| FON outbox Startbeleg / Jahresbeleg | ✅ Tam (SOAP Real + Fake; prod Fake yasak) | P0-1 (2026-07-29) |
| FON outbox Monatsbeleg | ✅ **NotRequired** ([karar](MONATSBELEG_FINANZONLINE_DECISION.md)) | P1-1 (2026-07-29) |
| FON Sonderbeleg gerçek SOAP | ✅ Kod hazır (BMF E2E Ops) | P0-1 (2026-07-29) |
| FON outbox Mode ambient | ✅ Tam | P1-2 (2026-07-29) |
| FON outbox retry / hata yönetimi | ✅ Tam | Önceden |
| FA FON / TSE / Ausfall / 2027 UI | ✅ Tam | P0–P1 (2026-07-29) |
| TSE Production config lock | ✅ Tam | P0-2 (2026-07-29) |
| Ausfallmeldung kod yüzeyi | ✅ Tam | P0-3 (2026-07-29) |

---

## 2. Detaylı Bulgular

### 2.1 Signaturerstellungseinheit (SCU) — ✅ Tam*

Her mali işlem, yapılandırılmış TSE/SCU üzerinden ES256 compact JWS ile imzalanır.

| Katman | Dosya / sınıf | Not |
|--------|----------------|-----|
| Ödeme kapısı | `backend/Services/PaymentService.cs` | `effectiveTseRequired` iken imza yoksa rollback |
| İmza servisi | `backend/Services/TseService.cs` — `CreateInvoiceSignatureAsync` | Belegdaten → pipeline → zincir |
| Pipeline | `backend/Tse/SignaturePipeline.cs` | JWS header `{"alg":"ES256"}`, §9 machine code; `IsF5CompliantJws` |
| SCU (fiskaly) | `backend/Tse/FiskalyTseKeyProvider.cs`, `FiskalyHttpClient`, `FiskalyOptions.SignatureCreationUnitId` | Private key export edilmez |
| Soft / Fake | `SoftwareTseKeyProvider`, `FakeTseProvider` | Dev/demo; **Production’da P0-2 kilidi** |
| Offline limit | `TseOptions.MaxOfflineTransactionsPerCashRegister` (50); POS `frontend/constants/offlineConfig.ts` | %80 uyarısı (40) |
| Prod kilidi | `TseProductionOptionsValidator`, `/health/tse/mode`, FA banner | [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) |

**Testler:** `PaymentReceiptSignatureIntegrationTests`, `SignaturePipelineTests`, `FiskalyTseKeyProviderTests`, `TseServiceSignatureChainPostgreSqlTests`.

---

### 2.2 Datenerfassungsprotokoll (DEP) — ✅ Tam*

BMF Signaturjournal (`Belege-Gruppe`) formatında dışa aktarım, yapısal doğrulama, history/archive/compliance, Prüftool CI ve P2 sertleştirmeleri mevcuttur (F1–F5 + P2-1…P2-3).

#### Şema

```text
RksvDepExportRootDto
  └─ "Belege-Gruppe"[]
       ├─ "Signaturzertifikat"   (leaf DER Base64 — boş emit yasak, P2-3)
       ├─ "Zertifizierungsstellen"[]  (issuer CA DER Base64)
       └─ "Belege-kompakt"[]     (compact JWS strings)
```

| Bileşen | Yol |
|---------|-----|
| Servis | `backend/Services/RksvDepExportService.cs` |
| DTO / envelope | `RksvDepExportDtos`, `RksvDepExportEnvelopeDto` (`legacyJwsCount`, …) |
| API | `AdminRksvDepExportController` — `GET /api/admin/rksv/dep-export` |
| CA zinciri | `TseCertificateChainBuilder`, `ITseKeyProvider.GetCertificateChainAsync` |
| FA | `/admin/rksv/dep-export`, compliance/history |
| CI | `.github/workflows/dep-prueftool.yml` |
| Docs | `docs/DEP_EXPORT_DEVELOPMENT.md`, `docs/DEP_EXPORT_COMPLETION.md` |

#### Kapsam (veri kaynakları)

| Kaynak | Tür | Filtre |
|--------|-----|--------|
| `payment_details` | Normal (`RksvSpecialReceiptKind == null`) | `CreatedAt` |
| `payment_details` | Nullbeleg / Startbeleg / Monatsbeleg / Jahresbeleg / Schlussbeleg | `CreatedAt` |
| `DailyClosings` | Tagesabschluss (+ monthly closing satırları) | `ClosingDate` |

Varsayılan: `includeSpecialReceipts=true`, `includeDailyClosings=true`. Sıra: `IssuedAt` → `SequenceNumber`. İmzasız / geçersiz JWS satırlar bilinçli dışlanır. Max dönem: **366 gün**.

#### İmza zinciri ve sertifika

- Gruplama: `certificate_thumbprint` (yoksa aktif TSE cert).
- Leaf boş → `RksvDepExportCertificateMissingException` / HTTP 500 `RKSV_DEP_EXPORT_MISSING_CERTIFICATE` (P2-3).
- Pre-F5 JSON payload JWS → `legacyJwsCount` + FA uyarı (P2-2); otomatik re-sign **yok**.

#### Prüftool

- Script: `scripts/verify-rksv-dep-export.ps1` + `ensure-bmf-prueftool.ps1`.
- Fixture: `backend/Tests/fixtures/prueftool/` — PASS.
- CI: JDK 17 + fixture smoke + `Category=DepPrueftool` seeded export.

**Testler:** `RksvDepExportServiceTests`, `DepExportValidationServiceTests`, `RksvDepPrueftoolFixtureTests`, `FiskalyDepExportPrueftoolTests`.

---

### 2.3 Beleg (fiş) — ✅ Tam*

Ödeme → TSE imza → Receipt/QR → POS yazdırma zinciri kuruludur.

| Katman | Dosya / sınıf |
|--------|----------------|
| Model | `backend/Models/Receipt.cs` |
| Servis | `backend/Services/ReceiptService.cs`, `ReceiptSequenceService` |
| QR / §9 | `RksvReceiptQrPayloadBuilder`, `RksvMachineCodeBuilder`, `BelegdatenPayloadBuilder` |
| POS | `frontend/components/ReceiptPrint.tsx`, `frontend/services/receiptPrinter.ts` |

**Test / docs:** `ReceiptServiceGenerateTests`, `RksvReceiptQrPayloadBuilderTests`; `docs/RKSV_RECEIPT_INVOICE_REQUIREMENTS.md`.

---

### 2.4 FinanzOnline — ✅ Kod hazır†

> Not: Entegrasyon `backend/Services/FinanzOnlineIntegration/` altındadır.

#### Kasa / SCU kayıt

SOAP + simülasyon: `FinanzOnlineRegistrierkassenInfrastructure`, `SoapFinanzOnlineRegistrierkassenTransport`, `SimulatedFinanzOnlineAdapters`.

#### Sonderbeleg üretimi

`RksvSpecialReceiptService` + FA `/rksv/sonderbelege`. Docs: `docs/RKSV_CASH_REGISTER_OPERATIONS.md` §4.

#### Outbox kapsamı

| Tür | Outbox + FO submission satırı |
|-----|-------------------------------|
| Startbeleg | ✅ `RksvStartbelegSubmission` |
| Jahresbeleg | ✅ `RksvJahresbelegSubmission` |
| Monatsbeleg | ✅ **NotRequired** — [`MONATSBELEG_FINANZONLINE_DECISION.md`](MONATSBELEG_FINANZONLINE_DECISION.md) |
| Nullbeleg / Schlussbeleg | ❌ (manuel Belegcheck isteğe bağlı) |

Enqueue Mode: ambient `FinanzOnline:Mode` (`FinanzOnlineModeResolver.ResolveOutboxMode`) — P1-2.

#### Üretim vs Fake / Real

| ClientKind | Davranış |
|------------|----------|
| `Fake` | Ağ yok; Production’da yasak |
| `Real` | BMF/rkdb SOAP via `IFinanzOnlineSubmissionService` + beleg mapper |
| `Enabled=false` | Skip → `RKS_SUBMISSION_DISABLED` |
| Monatsbeleg | `RKS_MONATSBELEG_NOT_REQUIRED` |

#### Retry / Admin UI — ✅

Outbox retry + FA `/rksv/finanz-online-*`, Sonderbelege FO kartları (Start/Jahres tracked; Monatsbeleg NotRequired notu).

---

### 2.5 Sürekli yükümlülükler

#### 5a. Signaturkarte / sertifika yenileme — 🟡 Kısmen‡

| Yetenek | Kanıt |
|---------|--------|
| Lifecycle | `TseCertLifecycleStatus` |
| Uyarı penceresi | `TseOptions.CertificateExpiringSoonDays` (default 30) |
| Periyodik tarama | `TseFailoverBackgroundService` → `ProcessExpiryWarningsAsync` |
| Activity | `TseCertificateExpiringSoon` / `Expired` / `Renewed` / `RenewalScheduled` |
| API / FA | `AdminTseManagementController`, `/admin/tse-management` |

**Açık (P1-4):** fiskaly kart yenileme runbook; fleet “X gün içinde dolacak” özeti; `TseCertificate*` i18n güçlendirme.

#### 5b. Arıza bildirimi (Ausfall) — ✅ Kod hazır†

`rksv_ausfall_episodes`, rkdb XML, failover hooks, outbox, FA `/admin/tse/ausfall`. Detay: [`AUSFALL_BENACHRICHTIGUNG_PLAN.md`](AUSFALL_BENACHRICHTIGUNG_PLAN.md). BMF E2E Ops sign-off’ta.

#### 5c. Mayıs 2027 Signaturkarte deadline — ✅ Kod + FA†

Config + reminder + FA `/admin/tse/signaturkarte-program`. Detay: [`MAI_2027_SIGNATURKARTE_PLAN.md`](MAI_2027_SIGNATURKARTE_PLAN.md).

---

## 3. Riskler ve Eksiklikler (güncel)

| Risk | Etki | Şiddet | Durum |
|------|------|--------|--------|
| Soft TSE / `TseMode=Off` üretimde | İmzasız mali işlem | Yüksek | ✅ Kod kilidi (P0-2); Ops prod config doğrulamalı |
| FON Sonderbeleg Fake / iskelet | Sahte Verified | Yüksek | ✅ Real SOAP kod (P0-1); BMF E2E açık |
| Monatsbeleg ayrı FON outbox yok | Yanlış operatör beklentisi | Düşük | ✅ NotRequired + FA (P1-1) |
| Enqueue `Mode=TEST` sabit | Yanlış ortam etiketi | Orta | ✅ Ambient Mode (P1-2) |
| FON Ausfallmeldung yok | Yasal bildirim kaçırma | Yüksek | ✅ Kod (P0-3); BMF E2E açık |
| Mayıs 2027 takibi yok | Deadline kaçırma | Yüksek | ✅ Program + FA (P1-3) |
| Pre-F5 / legacy JWS | Prüftool beleg fail | Orta | ✅ Uyarı (P2-2); re-sign yok |
| Boş `Signaturzertifikat` | Geçersiz DEP | Orta | ✅ Hard-fail (P2-3) |
| Demo Prüftool skip | False confidence | Düşük | ✅ CI hard-fail (P2-1) |
| Signaturkarte runbook / i18n (P1-4) | Operatör yenileme kaçırabilir | Düşük–Orta | ⬜ Açık |
| BMF TEST/PROD cutover kanıtı | Resmi “üretim hazır” iddiası | Yüksek (gate) | ⬜ Ops/Compliance |

**İş etkisi özeti:** P0–P2 **yazılım yüzeyi** büyük ölçüde kapanmıştır. Kalan asıl engel **operasyonel/BMF kanıtı** (TEST/PROD cutover, canlı Ausfall, prod Soft TSE yokluğu doğrulaması) ve **P1-4** runbook/i18n’dir.

---

## 4. Aksiyon Önerileri (durum)

### P0 — Üretim kesici — ✅ Kod complete

1. Sonderbeleg SOAP — ✅ P0-1  
2. TSE Production kilidi — ✅ P0-2  
3. Ausfallmeldung — ✅ P0-3  

### P1 — Uyumluluk — ✅ / ⬜

4. Monatsbeleg FON — ✅ NotRequired (P1-1)  
5. Enqueue Mode — ✅ (P1-2)  
6. Mayıs 2027 — ✅ (P1-3)  
7. Signaturkarte runbook + FA fleet/i18n — ⬜ **P1-4 açık**

### P2 — Kalite — ✅ / ⬜

8. DEP Prüftool CI — ✅ (P2-1)  
9. Legacy JWS uyarı — ✅ (P2-2)  
10. Boş Signaturzertifikat hard-fail — ✅ (P2-3)  
11. Cutover sonrası doküman senkronu — 🟡 Bu güncelleme + [`RKSV_FINAL_VALIDATION_CHECKLIST.md`](RKSV_FINAL_VALIDATION_CHECKLIST.md) (P2-4 kısmen; live cutover sonrası yeniden onay)

---

## 5. Sonuç

### Karar: **Koşullu GO (yazılım) / NO-GO (tam üretim iddiası)**

| Perspektif | Karar | Gerekçe |
|------------|--------|---------|
| **Yazılım / platform yüzeyi (P0–P2 kod)** | **GO — koşullu** | SCU/Beleg/DEP, FON SOAP istemcisi, Ausfall kodu, TSE prod kilidi, Mayıs 2027 programı, DEP CI + legacy/cert sertleştirmeleri kodda mevcut |
| **Tam RKSV üretim / Betriebsprüfung iddiası** | **NO-GO** | BMF TEST (ve PROD) Start/Jahres E2E kanıtı, canlı Ausfall kanıtı, prod Soft TSE yokluğu ve cutover checklist imzası henüz bu rapora bağlanmamış; P1-4 runbook açık |

Regkasse, **çekirdek RKSV yazılım yüzeyinde** güçlüdür. “Yeşil” FA UI veya yeşil CI, **BMF kabulü** anlamına gelmez.

**Sign-off yolu:** [`RKSV_FINAL_VALIDATION_CHECKLIST.md`](RKSV_FINAL_VALIDATION_CHECKLIST.md) maddelerini Ops + Compliance ile işaretleyin; ardından bu bölümü **GO** olarak güncelleyin.

---

## Ek A — Analiz izi (Adım 1–4)

| Adım | Konu | Ana çıktı (güncel) |
|------|------|---------------------|
| 1 | Genel RKSV gereksinimleri | Özet tablo — P0–P2 kod ✅ |
| 2 | DEP | Şema ✅, CI ✅, leaf hard-fail ✅, legacy uyarı ✅ |
| 3 | FinanzOnline | Outbox Start/Jahres ✅; Real SOAP ✅; Monatsbeleg NotRequired |
| 4 | Sürekli yükümlülükler | Ausfall ✅ kod; Mayıs 2027 ✅; cert yenileme 🟡 (P1-4) |

## Ek B — Hızlı dosya indeksi

**Backend:** `TseService`, `SignaturePipeline`, `TseProductionOptionsValidator`, `FiskalyTseKeyProvider`, `RksvDepExportService`, `AdminRksvDepExportController`, `RksvSpecialReceiptService`, `RksvFinanzOnlineSubmissionClient`, `RksvSpecialReceiptFinanzOnlineOutboxHandler`, `FinanzOnlineOutbox`, `TseCertificateService`, Ausfall episodes/services

**POS:** `ReceiptPrint.tsx`, `receiptPrinter.ts`, `offlineConfig.ts`

**FA:** `/admin/rksv/dep-export`, `/rksv/sonderbelege`, `/rksv/finanz-online-*`, `/admin/tse-management`, `/admin/tse/ausfall`, `/admin/tse/signaturkarte-program`

**CI / scripts:** `.github/workflows/dep-prueftool.yml`, `scripts/verify-rksv-dep-export.ps1`, `scripts/ensure-bmf-prueftool.ps1`

**Docs:** `DEP_EXPORT_DEVELOPMENT.md`, `TSE_PRODUCTION_CONFIG_LOCK.md`, `MONATSBELEG_FINANZONLINE_DECISION.md`, `AUSFALL_BENACHRICHTIGUNG_PLAN.md`, `MAI_2027_SIGNATURKARTE_PLAN.md`, `FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`, `RKSV_FINAL_VALIDATION_CHECKLIST.md`

---

**Son güncelleme:** 2026-07-29 — P0–P2 kod kapanışı yansıtıldı; Go/No-Go: koşullu yazılım GO / tam üretim NO-GO (cutover + P1-4).
