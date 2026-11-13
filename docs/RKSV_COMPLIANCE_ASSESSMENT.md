# RKSV Compliance Assessment Report

**Date:** 2026-07-29 (initial assessment)  
**Last updated:** 2026-07-29 — P0–P2 code closure + final validation checklist  
**Scope:** `backend/`, `frontend/`, `frontend-admin/`, `docs/` (code + configuration + documentation evidence)  
**Method:** Requirement → implementation mapping (combined Step 1–4 analyses)

> **Important disclaimer:** This report is based on **software evidence**. It is not official BMF/FinanzOnline acceptance, TSE hardware approval, or a legal “RKSV certificate” claim. Source: `ai/05_SECURITY_COMPLIANCE.md`.

**Related hub docs:** [`RKSV_ACTION_PLAN.md`](RKSV_ACTION_PLAN.md) · [`RKSV_FINAL_VALIDATION_CHECKLIST.md`](RKSV_FINAL_VALIDATION_CHECKLIST.md) (sign-off) · `docs/RKSV_COMPLIANCE.md` · `docs/DEP_EXPORT_DEVELOPMENT.md` · `docs/RKSV_CASH_REGISTER_OPERATIONS.md` · `docs/FINANZONLINE_PROD_CUTOVER_CHECKLIST.md` · `docs/RKSV_OFFICIAL_SOURCES.md` · `AGENTS.md` § Fiscal Rules.

---

## 1. Summary table

| # | Requirement | Status | Completion |
|---|------------|--------|------------|
| 1 | **Signaturerstellungseinheit (SCU / TSE)** — electronic signature on every fiscal transaction | ✅ Complete* | Prior (core) |
| 2 | **Datenerfassungsprotokoll (DEP)** — BMF Signaturjournal export | ✅ Complete* | F1–F5 + P2-1…P2-3 (2026-07-29) |
| 3 | **Beleg (receipt)** — legally valid customer receipt + QR/machine code | ✅ Complete* | Prior (core) |
| 4 | **FinanzOnline** — cash register registration + Sonderbeleg submission | ✅ Code ready† | P0-1, P1-1, P1-2 (2026-07-29) |
| 5a | **Ongoing:** Signaturkarte / certificate periodic renewal | 🟡 Partial‡ | Existing `TseCertificateService`; P1-4 open |
| 5b | **Ongoing:** Outage / system change → FinanzOnline notification | ✅ Code ready† | P0-3 (2026-07-29) |
| 5c | **Ongoing:** May 2027 Signaturkarte replacement mandate | ✅ Code + FA† | P1-3 (2026-07-29) |

\* “Complete” = the core software surface is covered; production configuration (real SCU, Soft TSE off, Prüftool/crypto match) is the operator’s responsibility.  
† Code surface is complete; **BMF TEST/PROD E2E / live cutover evidence** depends on Ops+Compliance sign-off — see [`RKSV_FINAL_VALIDATION_CHECKLIST.md`](RKSV_FINAL_VALIDATION_CHECKLIST.md).  
‡ Renewal API + alerts exist; vendor runbook / fleet i18n (P1-4) is not closed yet.

| Subtopic (deep analysis) | Status | Completion |
|-------------------------|--------|------------|
| DEP BMF `Belege-Gruppe` schema | ✅ Complete | F1–F5 |
| DEP normal + special + daily closing coverage | ✅ Complete (default flags) | F1–F5 |
| DEP thumbprint / leaf / CA chain | ✅ Complete (leaf hard-fail; empty CA → warning) | P2-3 leaf (2026-07-29) |
| DEP Prüftool (`verify-rksv-dep-export.ps1`) | ✅ Complete (fixture + [CI](../.github/workflows/dep-prueftool.yml)) | P2-1 (2026-07-29) |
| Pre-F5 legacy JWS warning | ✅ Complete (envelope + FA + history) | P2-2 (2026-07-29) |
| FON outbox Startbeleg / Jahresbeleg | ✅ Complete (SOAP Real + Fake; Fake banned in prod) | P0-1 (2026-07-29) |
| FON outbox Monatsbeleg | ✅ **NotRequired** ([decision](MONATSBELEG_FINANZONLINE_DECISION.md)) | P1-1 (2026-07-29) |
| FON Sonderbeleg real SOAP | ✅ Code ready (BMF E2E Ops) | P0-1 (2026-07-29) |
| FON outbox Mode ambient | ✅ Complete | P1-2 (2026-07-29) |
| FON outbox retry / error handling | ✅ Complete | Prior |
| FA FON / TSE / Ausfall / 2027 UI | ✅ Complete | P0–P1 (2026-07-29) |
| TSE Production config lock | ✅ Complete | P0-2 (2026-07-29) |
| Ausfallmeldung code surface | ✅ Complete | P0-3 (2026-07-29) |

---

## 2. Detailed findings

### 2.1 Signaturerstellungseinheit (SCU) — ✅ Complete*

Every fiscal transaction is signed with ES256 compact JWS through the configured TSE/SCU.

| Layer | File / class | Note |
|--------|----------------|-----|
| Payment gate | `backend/Services/PaymentService.cs` | Rolls back when `effectiveTseRequired` and no signature |
| Signature service | `backend/Services/TseService.cs` — `CreateInvoiceSignatureAsync` | Belegdaten → pipeline → chain |
| Pipeline | `backend/Tse/SignaturePipeline.cs` | JWS header `{"alg":"ES256"}`, §9 machine code; `IsF5CompliantJws` |
| SCU (fiskaly) | `backend/Tse/FiskalyTseKeyProvider.cs`, `FiskalyHttpClient`, `FiskalyOptions.SignatureCreationUnitId` | Private key is not exported |
| Soft / Fake | `SoftwareTseKeyProvider`, `FakeTseProvider` | Dev/demo; **P0-2 lock in Production** |
| Offline limit | `TseOptions.MaxOfflineTransactionsPerCashRegister` (50); POS `frontend/constants/offlineConfig.ts` | 80% warning (40) |
| Prod lock | `TseProductionOptionsValidator`, `/health/tse/mode`, FA banner | [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) |

**Tests:** `PaymentReceiptSignatureIntegrationTests`, `SignaturePipelineTests`, `FiskalyTseKeyProviderTests`, `TseServiceSignatureChainPostgreSqlTests`.

---

### 2.2 Datenerfassungsprotokoll (DEP) — ✅ Complete*

BMF Signaturjournal (`Belege-Gruppe`) export, structural validation, history/archive/compliance, Prüftool CI, and P2 hardening are in place (F1–F5 + P2-1…P2-3).

#### Schema

```text
RksvDepExportRootDto
  └─ "Belege-Gruppe"[]
       ├─ "Signaturzertifikat"   (leaf DER Base64 — empty emit forbidden, P2-3)
       ├─ "Zertifizierungsstellen"[]  (issuer CA DER Base64)
       └─ "Belege-kompakt"[]     (compact JWS strings)
```

| Component | Path |
|---------|-----|
| Service | `backend/Services/RksvDepExportService.cs` |
| DTO / envelope | `RksvDepExportDtos`, `RksvDepExportEnvelopeDto` (`legacyJwsCount`, …) |
| API | `AdminRksvDepExportController` — `GET /api/admin/rksv/dep-export` |
| CA chain | `TseCertificateChainBuilder`, `ITseKeyProvider.GetCertificateChainAsync` |
| FA | `/admin/rksv/dep-export`, compliance/history |
| CI | `.github/workflows/dep-prueftool.yml` |
| Docs | `docs/DEP_EXPORT_DEVELOPMENT.md`, `docs/DEP_EXPORT_COMPLETION.md` |

#### Coverage (data sources)

| Source | Type | Filter |
|--------|-----|--------|
| `payment_details` | Normal (`RksvSpecialReceiptKind == null`) | `CreatedAt` |
| `payment_details` | Nullbeleg / Startbeleg / Monatsbeleg / Jahresbeleg / Schlussbeleg | `CreatedAt` |
| `DailyClosings` | Tagesabschluss (+ monthly closing rows) | `ClosingDate` |

Defaults: `includeSpecialReceipts=true`, `includeDailyClosings=true`. Order: `IssuedAt` → `SequenceNumber`. Unsigned / invalid JWS rows are excluded by design. Max period: **366 days**.

#### Signature chain and certificate

- Grouping: `certificate_thumbprint` (falls back to the active TSE cert).
- Empty leaf → `RksvDepExportCertificateMissingException` / HTTP 500 `RKSV_DEP_EXPORT_MISSING_CERTIFICATE` (P2-3).
- Pre-F5 JSON payload JWS → `legacyJwsCount` + FA warning (P2-2); no automatic re-sign.

#### Prüftool

- Script: `scripts/verify-rksv-dep-export.ps1` + `ensure-bmf-prueftool.ps1`.
- Fixture: `backend/Tests/fixtures/prueftool/` — PASS.
- CI: JDK 17 + fixture smoke + `Category=DepPrueftool` seeded export.

**Tests:** `RksvDepExportServiceTests`, `DepExportValidationServiceTests`, `RksvDepPrueftoolFixtureTests`, `FiskalyDepExportPrueftoolTests`.

---

### 2.3 Beleg (receipt) — ✅ Complete*

Payment → TSE signature → Receipt/QR → POS print chain is in place.

| Layer | File / class |
|--------|----------------|
| Model | `backend/Models/Receipt.cs` |
| Service | `backend/Services/ReceiptService.cs`, `ReceiptSequenceService` |
| QR / §9 | `RksvReceiptQrPayloadBuilder`, `RksvMachineCodeBuilder`, `BelegdatenPayloadBuilder` |
| POS | `frontend/components/ReceiptPrint.tsx`, `frontend/services/receiptPrinter.ts` |

**Tests / docs:** `ReceiptServiceGenerateTests`, `RksvReceiptQrPayloadBuilderTests`; `docs/RKSV_RECEIPT_INVOICE_REQUIREMENTS.md`.

---

### 2.4 FinanzOnline — ✅ Code ready†

> Note: Integration lives under `backend/Services/FinanzOnlineIntegration/`.

#### Cash register / SCU registration

SOAP + simulation: `FinanzOnlineRegistrierkassenInfrastructure`, `SoapFinanzOnlineRegistrierkassenTransport`, `SimulatedFinanzOnlineAdapters`.

#### Sonderbeleg generation

`RksvSpecialReceiptService` + FA `/rksv/sonderbelege`. Docs: `docs/RKSV_CASH_REGISTER_OPERATIONS.md` §4.

#### Outbox coverage

| Type | Outbox + FO submission row |
|-----|-------------------------------|
| Startbeleg | ✅ `RksvStartbelegSubmission` |
| Jahresbeleg | ✅ `RksvJahresbelegSubmission` |
| Monatsbeleg | ✅ **NotRequired** — [`MONATSBELEG_FINANZONLINE_DECISION.md`](MONATSBELEG_FINANZONLINE_DECISION.md) |
| Nullbeleg / Schlussbeleg | ❌ (optional manual Belegcheck) |

Enqueue Mode: ambient `FinanzOnline:Mode` (`FinanzOnlineModeResolver.ResolveOutboxMode`) — P1-2.

#### Production vs Fake / Real

| ClientKind | Behavior |
|------------|----------|
| `Fake` | No network; banned in Production |
| `Real` | BMF/rkdb SOAP via `IFinanzOnlineSubmissionService` + beleg mapper |
| `Enabled=false` | Skip → `RKS_SUBMISSION_DISABLED` |
| Monatsbeleg | `RKS_MONATSBELEG_NOT_REQUIRED` |

#### Retry / Admin UI — ✅

Outbox retry + FA `/rksv/finanz-online-*`, Sonderbelege FO cards (Start/Jahres tracked; Monatsbeleg NotRequired note).

---

### 2.5 Ongoing obligations

#### 5a. Signaturkarte / certificate renewal — 🟡 Partial‡

| Capability | Evidence |
|---------|--------|
| Lifecycle | `TseCertLifecycleStatus` |
| Warning window | `TseOptions.CertificateExpiringSoonDays` (default 30) |
| Periodic scan | `TseFailoverBackgroundService` → `ProcessExpiryWarningsAsync` |
| Activity | `TseCertificateExpiringSoon` / `Expired` / `Renewed` / `RenewalScheduled` |
| API / FA | `AdminTseManagementController`, `/admin/tse-management` |

**Open (P1-4):** fiskaly card renewal runbook; fleet “expires within X days” summary; `TseCertificate*` i18n hardening.

#### 5b. Outage notification (Ausfall) — ✅ Code ready†

`rksv_ausfall_episodes`, rkdb XML, failover hooks, outbox, FA `/admin/tse/ausfall`. Detail: [`AUSFALL_BENACHRICHTIGUNG_PLAN.md`](AUSFALL_BENACHRICHTIGUNG_PLAN.md). BMF E2E is on the Ops sign-off.

#### 5c. May 2027 Signaturkarte deadline — ✅ Code + FA†

Config + reminder + FA `/admin/tse/signaturkarte-program`. Detail: [`MAI_2027_SIGNATURKARTE_PLAN.md`](MAI_2027_SIGNATURKARTE_PLAN.md).

---

## 3. Risks and gaps (current)

| Risk | Impact | Severity | Status |
|------|------|--------|--------|
| Soft TSE / `TseMode=Off` in production | Unsigned fiscal transaction | High | ✅ Code lock (P0-2); Ops must verify prod config |
| FON Sonderbeleg Fake / skeleton | Fake Verified | High | ✅ Real SOAP code (P0-1); BMF E2E open |
| No separate FON outbox for Monatsbeleg | Wrong operator expectation | Low | ✅ NotRequired + FA (P1-1) |
| Enqueue `Mode=TEST` hardcoded | Wrong environment label | Medium | ✅ Ambient Mode (P1-2) |
| No FON Ausfallmeldung | Missed legal notification | High | ✅ Code (P0-3); BMF E2E open |
| No May 2027 tracking | Missed deadline | High | ✅ Program + FA (P1-3) |
| Pre-F5 / legacy JWS | Prüftool beleg fail | Medium | ✅ Warning (P2-2); no re-sign |
| Empty `Signaturzertifikat` | Invalid DEP | Medium | ✅ Hard-fail (P2-3) |
| Demo Prüftool skip | False confidence | Low | ✅ CI hard-fail (P2-1) |
| Signaturkarte runbook / i18n (P1-4) | Operator may miss renewal | Low–Medium | ⬜ Open |
| BMF TEST/PROD cutover evidence | Official “production ready” claim | High (gate) | ⬜ Ops/Compliance |

**Business impact summary:** P0–P2 **software surface** is largely closed. The remaining blocker is **operational/BMF evidence** (TEST/PROD cutover, live Ausfall, confirmation that Soft TSE is absent in prod) and **P1-4** runbook/i18n.

---

## 4. Action recommendations (status)

### P0 — Production blockers — ✅ Code complete

1. Sonderbeleg SOAP — ✅ P0-1  
2. TSE Production lock — ✅ P0-2  
3. Ausfallmeldung — ✅ P0-3  

### P1 — Compliance — ✅ / ⬜

4. Monatsbeleg FON — ✅ NotRequired (P1-1)  
5. Enqueue Mode — ✅ (P1-2)  
6. May 2027 — ✅ (P1-3)  
7. Signaturkarte runbook + FA fleet/i18n — ⬜ **P1-4 open**

### P2 — Quality — ✅ / ⬜

8. DEP Prüftool CI — ✅ (P2-1)  
9. Legacy JWS warning — ✅ (P2-2)  
10. Empty Signaturzertifikat hard-fail — ✅ (P2-3)  
11. Post-cutover document sync — 🟡 This update + [`RKSV_FINAL_VALIDATION_CHECKLIST.md`](RKSV_FINAL_VALIDATION_CHECKLIST.md) (P2-4 partial; reconfirm after live cutover)

---

## 5. Conclusion

### Decision: **Conditional GO (software) / NO-GO (full production claim)**

| Perspective | Decision | Rationale |
|------------|--------|---------|
| **Software / platform surface (P0–P2 code)** | **GO — conditional** | SCU/Beleg/DEP, FON SOAP client, Ausfall code, TSE prod lock, May 2027 program, DEP CI + legacy/cert hardening are in code |
| **Full RKSV production / Betriebsprüfung claim** | **NO-GO** | BMF TEST (and PROD) Start/Jahres E2E evidence, live Ausfall evidence, prod Soft TSE absence, and cutover checklist signatures are not yet bound to this report; P1-4 runbook is open |

Regkasse is strong on the **core RKSV software surface**. A “green” FA UI or green CI is not **BMF acceptance**.

**Sign-off path:** Mark [`RKSV_FINAL_VALIDATION_CHECKLIST.md`](RKSV_FINAL_VALIDATION_CHECKLIST.md) items with Ops + Compliance; then update this section to **GO**.

---

## Appendix A — Analysis trail (Steps 1–4)

| Step | Topic | Main output (current) |
|------|------|---------------------|
| 1 | General RKSV requirements | Summary table — P0–P2 code ✅ |
| 2 | DEP | Schema ✅, CI ✅, leaf hard-fail ✅, legacy warning ✅ |
| 3 | FinanzOnline | Outbox Start/Jahres ✅; Real SOAP ✅; Monatsbeleg NotRequired |
| 4 | Ongoing obligations | Ausfall ✅ code; May 2027 ✅; cert renewal 🟡 (P1-4) |

## Appendix B — Quick file index

**Backend:** `TseService`, `SignaturePipeline`, `TseProductionOptionsValidator`, `FiskalyTseKeyProvider`, `RksvDepExportService`, `AdminRksvDepExportController`, `RksvSpecialReceiptService`, `RksvFinanzOnlineSubmissionClient`, `RksvSpecialReceiptFinanzOnlineOutboxHandler`, `FinanzOnlineOutbox`, `TseCertificateService`, Ausfall episodes/services

**POS:** `ReceiptPrint.tsx`, `receiptPrinter.ts`, `offlineConfig.ts`

**FA:** `/admin/rksv/dep-export`, `/rksv/sonderbelege`, `/rksv/finanz-online-*`, `/admin/tse-management`, `/admin/tse/ausfall`, `/admin/tse/signaturkarte-program`

**CI / scripts:** `.github/workflows/dep-prueftool.yml`, `scripts/verify-rksv-dep-export.ps1`, `scripts/ensure-bmf-prueftool.ps1`

**Docs:** `DEP_EXPORT_DEVELOPMENT.md`, `TSE_PRODUCTION_CONFIG_LOCK.md`, `MONATSBELEG_FINANZONLINE_DECISION.md`, `AUSFALL_BENACHRICHTIGUNG_PLAN.md`, `MAI_2027_SIGNATURKARTE_PLAN.md`, `FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`, `RKSV_FINAL_VALIDATION_CHECKLIST.md`

---

**Last updated:** 2026-07-29 — P0–P2 code closure reflected; Go/No-Go: conditional software GO / full production NO-GO (cutover + P1-4).
