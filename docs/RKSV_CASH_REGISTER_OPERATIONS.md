# RKSV Cash Register Operations — Operational Handbook

> **Legal notice:** This document only summarizes the implementation in this codebase. It is not legal advice and does not guarantee RKSV/ABG compliance. For Austrian law and official interpretations, consult a specialist or the competent authorities.

> **Scope:** Explanations are in **English**. Menu and page names that appear in German in the UI are left in **German**.

---

## 1. Tagesbericht (formal daily report)

### What
A **formal Tagesbericht** record summarized for a Vienna calendar day and cash register, which can be frozen with `SnapshotJson` + hash. An outbox message can be produced for summary submission to FinanzOnline (noted in code as a "Non-DEP summary").

### Why (business / RKSV context)
A separate **formal report** layer so it is not mixed with operational end-of-day. A summary aligned with audit and accounting plus submission status can be tracked.

### Menu / page (`frontend-admin`)
- **Tagesbericht (formal)** — `frontend-admin/src/app/(protected)/reporting/tagesbericht/page.tsx` (list), `.../tagesbericht/[id]/page.tsx` (detail).
- Side links: **Report Center** (`reporting/report-center/page.tsx`), FinanzOnline screens under **RKSV** (below).

### POS / mobile (`frontend`)
- A **real** POS screen bound to the formal Tagesbericht API was **not found**. `frontend/app/(screens)/reports.tsx` has sample data and `TODO: API` notes; in production, formal Tagesbericht is processed **only on the admin** side.

### Backend
- **Controller:** `backend/Controllers/TagesberichtReportsController.cs` — `GET/POST` under `api/reports/tagesbericht` (list, `generate`, `finalize`, `correction`, `{id}/submit-finanzonline`).
- **Service:** `ITagesberichtService` / `TagesberichtService.cs`.
- **Model:** `backend/Models/TagesberichtReport.cs` (table `tagesbericht_reports`).
- **DTO:** `backend/Models/Reports/TagesberichtDtos.cs`.

### Permissions
- **View:** `report.view` (`AppPermissions.ReportView`).
- **Generate / finalize / correction:** `report.export` (`AppPermissions.ReportExport`).
- **Submit to FinanzOnline:** `finanzonline.submit` (`AppPermissions.FinanzOnlineSubmit`).

### Step by step (summary)
1. In Admin, open the **Tagesbericht (formal)** list; use date / cash register filters.
2. If needed, **generate** a provisional (`Provisional`) summary or refresh it.
3. After the content is verified, **finalize** it so it becomes permanent (in code: `Finalized` / correction chain).
4. If needed, trigger **FinanzOnline** submission (`POST .../submit-finanzonline`); the outbox and the submission fields on the row are updated.

### Expected output
- List/detail DTOs include summary amounts, tax / payment-method breakdown, reconciliation flags, and submission status (`TagesberichtSubmissionStateDto` and similar).

### Common errors / blockers
- Unauthorized user: `403` (related `HasPermission`).
- Submission errors: `LastSubmissionError` on the report row / outbox terminal states (see the FinanzOnline outbox screen for detail).

### Missing / partial
- Formal Tagesbericht from POS is **absent** (`reports.tsx` placeholder).
- Code comments describe the FinanzOnline side as an informational summary line, **not** a DEP row (`FinanzOnlineOutbox.cs` notes).

---

## 2. Monatsbericht (formal monthly report)

### What
A month-based formal report; the lifecycle pattern is close to Tagesbericht (provisional → finalize → correction → FinanzOnline).

### Why
A monthly official summary and submission trail; it can be combined with higher-level reports (for example Jahresbericht).

### Menu / page (`frontend-admin`)
- **Monatsbericht (formal)** — `reporting/monatsbericht/page.tsx`, `reporting/monatsbericht/[id]/page.tsx`.

### POS
**Not found in current implementation** (no direct trigger).

### Backend
- `backend/Controllers/MonatsberichtReportsController.cs` — `api/reports/monatsbericht` (+ `generate`, `finalize`, `correction`, `{id}/submit-finanzonline`).
- `IMonatsberichtService` / `MonatsberichtService.cs`, model `MonatsberichtReport`.

### Permissions
Same as Tagesbericht: `ReportView`, `ReportExport`, `FinanzOnlineSubmit`.

### Step by step
Parallel to Tagesbericht: select month → generate → finalize → optional FinanzOnline submit.

### Expected output
`MonatsberichtDto` / list items; linked days and submission summary.

### Missing / partial
- FinanzOnline note: **Non-DEP monthly summary** (`FinanzOnlineOutbox.cs`).

---

## 3. Jahresbericht (formal annual report)

### What
A year-based formal report; the same controller/service pattern.

### Menu / page (`frontend-admin`)
- **Jahresbericht (formal)** — `reporting/jahresbericht/page.tsx`, `reporting/jahresbericht/[id]/page.tsx`.

### POS
**Not found in current implementation.**

### Backend
- `backend/Controllers/JahresberichtReportsController.cs` — `api/reports/jahresbericht`.

### Permissions
`ReportView`, `ReportExport`, `FinanzOnlineSubmit`.

### Missing / partial
- FinanzOnline note: **Non-DEP annual summary** (`FinanzOnlineOutbox.cs`).

---

## 4. RKSV Sonderbelege — Nullbeleg, Startbeleg, Monatsbeleg, Jahresbeleg, Schlussbeleg

RKSV special receipts are **separate from POS payment routes**; they are not created through `PaymentService`. Production uses `RksvSpecialReceiptsController` + `RksvSpecialReceiptService`; result records are written to the normal `PaymentDetails` / `Invoice` / `Receipt` tables and marked on the receipt DTO with `RksvSpecialReceiptKind`.

### Shared API (`backend`)

| Operation | HTTP | Permission (`AppPermissions`) |
|--------|------|-------------------------|
| Monats-Nullbeleg | `POST api/rksv/special-receipts/nullbeleg` | `RksvNullbelegCreate` |
| Startbeleg | `POST api/rksv/special-receipts/startbeleg` | `RksvStartbelegCreate` |
| Monatsbeleg | `POST api/rksv/special-receipts/monatsbeleg` | `RksvMonatsbelegCreate` |
| Jahresbeleg | `POST api/rksv/special-receipts/jahresbeleg` | `RksvJahresbelegCreate` |
| Schlussbeleg (Endbeleg) | `POST api/rksv/special-receipts/schlussbeleg` | `RksvSchlussbelegCreate` |

Request/response bodies: `backend/DTOs/RksvSpecialReceiptDtos.cs`. Controller: `backend/Controllers/RksvSpecialReceiptsController.cs`. Business rules and TSE zero-amount signature: `backend/Services/RksvSpecialReceiptService.cs`.

### Admin UI (`frontend-admin`)

- **Page:** `frontend-admin/src/app/(protected)/rksv/sonderbelege/page.tsx` → `RksvSonderbelegePage` (`frontend-admin/src/features/rksv-operations/components/RksvSonderbelegePage.tsx`).
- **Menu:** **Sonderbelege** under the RKSV group (`nav.rksvLeafSonderbelege`); route `/rksv/sonderbelege` (`frontend-admin/src/features/rksv/rksvAdminMenuModel.ts`).
- **Flow (summary):** Select cash register → creation cards if the related permission is present (Nullbeleg / Startbeleg / Monatsbeleg / Jahresbeleg / Schlussbeleg) → `POST /api/rksv/special-receipts/...` → the latest-receipt list is filtered via `getApiReceiptsList` from the last 300 records that have `rksvSpecialReceiptKind` set. Optional **Beleg erneut drucken** (`ReceiptReprintWizard`, `RECEIPT_REPRINT`). Schlussbeleg has an extra confirmation-text modal.
- **Receipt detail:** `receipts/[receiptId]/page.tsx` includes `RksvSpecialReceiptFinanzOnlineSubmissionCard` for Startbeleg/Jahresbeleg (`ReceiptDTO.rksvFinanzOnlineSubmission`).
- **FinanzOnline status (tracked kinds only):** In the UI, the submission badge/text for Startbeleg and Jahresbeleg is limited by `isRksvFinanzOnlineTrackedSpecialReceiptKind` (`frontend-admin/src/features/receipts/utils/rksvFinanzOnlineSubmissionUi.ts`); this matches the backend.

### FinanzOnline outbox trail (code behavior)

- **Sonderbeleg kinds enqueued to the outbox:** only **Startbeleg** and **Jahresbeleg** (`EnqueueRksvSpecialReceiptFinanzOnlineOutboxAsync` calls inside `RksvSpecialReceiptService`; message types `FinanzOnlineRksvSpecialReceiptOutboxMessageTypes.RksvStartbelegSubmission` / `RksvJahresbelegSubmission`).
- **Nullbeleg, Monatsbeleg, Schlussbeleg:** `RksvSpecialReceiptService` does not call `EnqueueRksvSpecialReceiptFinanzOnlineOutboxAsync` for these kinds; a `RksvSpecialReceiptFinanzOnlineSubmissions` row is also not added on these create paths.
- **Status row:** `RksvSpecialReceiptFinanzOnlineSubmission` entity + `ReceiptDTO.RksvFinanzOnlineSubmission` (`RksvFinanzOnlineSubmissionStatusDto`). Handler: `RksvSpecialReceiptFinanzOnlineOutboxHandler`.

### 4.1 Nullbeleg

One record per cash register for the Vienna calendar `year`/`month`; `ActsAsJahresbeleg` is optional (if the body is `null`, the service applies a December default of `true`). Guest customer + zero-amount payment/invoice/receipt creation: `RksvSpecialReceiptService.CreateNullbelegAsync`.

### 4.2 Startbeleg

One Startbeleg per cash register, unless the register is permanently disabled. After create, a FO submission row and outbox message are added (`CreateStartbelegAsync`).

### 4.3 Monatsbeleg

**Create:** TSE-signed Monatsbeleg for past Vienna calendar months; a **December** request is routed in the service to the **Jahresbeleg** path.

**FinanzOnline:** No separate `belegpruefung` outbox (product decision **NotRequired**). Mandatory FON Belegcheck applies to Startbeleg + Jahresbeleg. Detail: [`MONATSBELEG_FINANZONLINE_DECISION.md`](MONATSBELEG_FINANZONLINE_DECISION.md). FA: `MonatsbelegInfoCard` on Sonderbelege; NotRequired note on the receipt detail.

### 4.4 Jahresbeleg

Limited to the Vienna calendar year that is **the current year or the previous year**; an early-issue note can be carried in `EarlyReason`. After create: FO submission + outbox (`CreateJahresbelegAsync`).

### 4.5 Schlussbeleg (Endbeleg)

When there is no open shift and the cash register status is eligible for close; afterward the register is **permanently disabled**. No dedicated FO trail record.

---

## 9. Belege / receipts (Receipts)

### What
A **persistent** receipt created atomically with payment (`receipts` table + line items + tax rows). There is no “lazy production after payment”; `GetReceiptByPaymentId` explicitly expects a persistent receipt.

### Why
Signature, chain, QR payload, and line-level evidence for RKSV.

### Menu / page (`frontend-admin`)
- **Belege** — `frontend-admin/src/app/(protected)/receipts/page.tsx`, detail `receipts/[receiptId]/page.tsx` (nav label: `nav.receipts` / default German **Belege**).

### POS
- Payment completion: `frontend/components/PaymentModal.tsx` → `frontend/services/api/paymentService.ts` (`api/pos/payment`).
- Receipt data: `PaymentService.GetReceiptDataAsync` → `ReceiptsController` / `ReceiptService`.

### Backend
- **List / read:** `backend/Controllers/ReceiptsController.cs` — `api/Receipts/list` (`SaleView`), `api/Receipts/by-payment/{paymentId}`, `api/Receipts/{receiptId}`.
- **Create:** During payment, `_receiptService.AddReceiptFromPaymentToContextAsync` inside `PaymentService` (`PaymentService.cs`).
- **Service:** `ReceiptService.cs` (QR payload `_R1-AT1_...` format, previous signature, certificate serial number).

### Permissions
- Admin list/detail: `sale.view` (`HasPermission(AppPermissions.SaleView)` on `ReceiptsController`).
- `create-from-payment`: `sale.create`.
- POS payment: `payment.take` (`PaymentController`).

### Step by step
1. Pay the cart in POS → the server writes payment + invoice + receipt in a single transaction.
2. In Admin, search / open detail via **Belege**, or go to the related receipt from the payment record.

### Expected output
`ReceiptDTO`: `ReceiptNumber`, `Date`, `KassenID`, company, lines, `TaxRates`, `Payments`, `Signature` (JWS, previous signature, QR text).

### Common errors
- Payment exists, receipt does not: `404` consistent with the “receipt must be created at payment time” warning in the `GetReceiptDataAsync` log.

### Missing / partial
- (For Sonderbelege see **section 4**; the normal sales receipt path is unchanged.)

---

## 10. Zahlungen / payments (Payments)

### What
A sales payment based on `payment_details`; mapped to the POS invoice (`invoices` + `SourcePaymentId`).

### Why
End-of-day and formal-report **invoice mapping** checks (for example `GetPaymentsWithoutInvoiceCountAsync`) depend on payment–invoice consistency.

### Menu / page (`frontend-admin`)
- **Zahlungen** — `frontend-admin/src/app/(protected)/payments/page.tsx`.

### POS
- `PaymentModal` + `paymentService` → `POST api/pos/payment` (legacy: `api/Payment`, same controller).

### Backend
- `backend/Controllers/PaymentController.cs` — class-level `payment.take`; methods `methods`, `POST` create, and similar.

### Permissions
- Main POS payment: `payment.take`.
- Admin payment list uses the guards on that page (repo: `routePermissions` / features; for detail see the `frontend-admin` route permission mapping).

### Step by step
Take payment in POS; in Admin, use **Zahlungen** for verification and cross-check against the FinanzOnline queue or reconciliation.

### Missing / partial
- There is a comment in `paymentService` that it must not be used for the daily report (there is no separate “daily-report” route on the backend).

---

## 11. Tagesabschluss / operativer Tagesabschluss (end of day and period closings)

### What
- **Daily:** `DailyClosing` (`ClosingType = Daily`), TSE signature, `Invoice` (Paid) totals for the Vienna calendar day; if FinanzOnline is enabled, `SubmitDailyClosingAsync`.
- **Monthly / yearly:** Likewise a `Monthly` / `Yearly` closing record and TSE signature; **in a code review**, calls to `FinanzOnlineService.SubmitMonthlyClosingAsync` / `SubmitYearlyClosingAsync` for the monthly/yearly path did **not** appear the way they do on the daily flow (only the daily closing had a `SubmitDailyClosingAsync` call).

### Why
Seal the cash-register period with TSE and (when enabled) forward it to FinanzOnline.

### Menu / page (`frontend-admin`)
- **Operativer Tagesabschluss** — `frontend-admin/src/app/(protected)/tagesabschluss/page.tsx` (nav: `nav.tagesabschluss`).

### POS
- `frontend/components/TagesabschlussModal.tsx` → `frontend/services/api/tagesabschlussService.ts` → `POST /tagesabschluss/daily|monthly|yearly`, `GET .../can-close/{id}`, `history`, `statistics` (the client base may include an `/api` prefix; server class is `api/Tagesabschluss`).

### Backend
- `backend/Controllers/TagesabschlussController.cs` — **all** actions at class level `HasPermission(AppPermissions.TseSign)`.
- `backend/Services/TagesabschlussService.cs` — `PerformDailyClosingAsync`, `PerformMonthlyClosingAsync`, `PerformYearlyClosingAsync`.

### Permissions
- **`tse.sign`** (controller class).

### Step by step
1. Check blockers with `can-close` (is TSE connected, are there in-day payments without an invoice, is today already closed).
2. Run the daily closing; the result DTO includes `TseSignature` and optional `FinanzOnlineStatus` fields.

### Common errors
- TSE not connected: `InvalidOperationException` (“TSE device is not connected…”).
- Payment without invoice: `Closing blocked: N payment(s) without a matching invoice`.
- No transactions: “No transactions found for today…”.

### Nachträglicher (rückdatierter) Tagesabschluss
- Closing is possible for a past Vienna business day; `CreatedAt` / TSE signature time are **not** backdated.
- `is_backdated`, `late_creation_reason` (required), and audit `TagesabschlussBackdatedCreated`.
- Detail: [`docs/BACKDATED_TAGESABSCHLUSS.md`](BACKDATED_TAGESABSCHLUSS.md).

### After Tagesabschluss (verified RKSV behavior)

| Rule | Implementation |
|-------|----------|
| No new payment on a closed cash register | `RegisterStatus.Closed` → `ValidatePaymentRegister*` → `CASH_REGISTER_CLOSED` → HTTP 400 |
| Cash register / shift must be opened for a new sale | `TryOpenCashRegisterAsync` → `Open` |
| No second Daily closing on the same Vienna day | `CanPerformClosingAsync` + unique index `(CashRegisterId, ClosingDate, ClosingType)` |

**Note:** There is no `IsClosed` field; status is the `CashRegister.Status` enum. Reopening on the same calendar day after a Z-Bericht and taking a sale is not blocked in code; a second closing is blocked — operational-risk detail: [`docs/RKSV_AFTER_TAGESABSCHLUSS.md`](RKSV_AFTER_TAGESABSCHLUSS.md).

### Missing / partial
- Code reading shows that FinanzOnline submission on monthly/yearly closing is **not parallel** to the daily path; extra verification is recommended before a production decision.
- There is **no** hard block for reopen + payment after Tagesabschluss on the same Vienna day (see the verification document above).

---

## 12. DEP / export / audit trail

### What (in the implementation)
1. **FinanzOnline RKDB `belegpruefung`:** Structural validation for `beleg` text that matches the DEP pattern (`FinanzOnlineRkdbBelegpruefungValidator`). Receipt QR text is usually **not** the same as this pattern (comment on `FinanzOnlineService.TryResolveRkdbBelegpruefungAsync`).
2. **Fiscal export (DEP-like package):** `GET api/admin/fiscal-export` — `IFiscalExportService` produces a “DEP-like fiscal export package”; profiles `operational_preview`, `accounting_report`, `legal_compliance_export`, `diagnostic_package` (`FiscalExportController`, `FiscalExportProfileRules`).
3. **Integrity report:** `GET api/admin/integrity` — `IntegrityController`, `AuditView`.
4. **Gate before legal export:** `GET api/reports/legal-export-completeness/...` — `LegalExportCompletenessController`, `ReportView`.

### Menu / page (`frontend-admin`)
- **Fiscal export (diagnostics)** — `/rksv/fiscal-export-diagnostics` (`fiscal-export-diagnostics/page.tsx`); permission mapping `REPORT_EXPORT` in `routePermissions.ts`.
- **RKSV · Integrity** — `/rksv/integrity` (`integrity/page.tsx`).
- Text resolvers related to formal reports: `formalReportContentResolver.ts` (export profile row).

### POS
No direct DEP export; data is taken from server/admin APIs.

### Permissions (summary)
- Fiscal export: at least `report.export`; stricter profiles `audit.view` and `fiscal.export.compliance` (`FiscalExportProfileRules`).
- Integrity: `audit.view`.

### Missing / partial
- A classic “produce a flat DEP file and upload it to BMF” flow was **not verified as a separate product feature** in this document; what exists are the fiscal export JSON package and the RKDB `belegpruefung` validation layers.

---

## 13. FinanzOnline / RKSV verification and operations screens

### What
- **Configuration and diagnostics:** `FinanzOnlineController` — `api/FinanzOnline/config`, `status`, test connection, error history, and similar (`SettingsView` / `FinanzOnlineView` / `FinanzOnlineManage` combinations are split per endpoint in the file).
- **Outbox (primary lifecycle):** `FinanzOnlineOutboxAdminController` — `GET api/admin/finanzonline-outbox` (`FinanzOnlineView`).
- **Legacy payment-row reconciliation:** `FinanzOnlineReconciliationController` — `GET api/admin/finanzonline-reconciliation` (`FinanzOnlineView`), `POST .../retry/{paymentId}` (`FinanzOnlineSubmit`); the controller description notes it is **legacy** and that the outbox should be preferred.
- **Readiness summary:** `FinanzOnlineReadinessController` — `GET api/admin/finanzonline-readiness` (`FinanzOnlineView`).

### Menu / page (`frontend-admin`, German nav examples)
- **FinanzOnline · Outbox** — `/rksv/finanz-online-outbox`
- **FinanzOnline-Abgleich** (queue) — `/rksv/finanz-online-queue`
- **FinanzOnline-Abgleich (Legacy)** — legacy badge on the same queue page
- **RKSV Übersicht / Status** — `/rksv/status`
- **FinanzOnline (diagnostic)** — `/rksv/finanz-online-operations`
- **Verifications** — `/rksv/verifications` (file exists)
- **Report Center** — includes links to the FinanzOnline outbox

### POS
FinanzOnline management screens are in **admin**; after POS payment, submission runs in the background via `PaymentService` / `FinanzOnlineService` (see the related service and `DispatchPostCommitComplianceAsync`).

### Permissions (summary)
- `finanzonline.view`, `finanzonline.manage`, `finanzonline.submit` (`AppPermissions`).

### Missing / partial
- Formal-report FinanzOnline messages are **not** a DEP summary line (outbox notes).

---

## Appendix: Operative Berichte (not Tagesbericht)

**Operative Berichte** / **Report Center** / **Personal / Kassenleistung** — `OperationalReportsController.cs` (`api/Reports/operational/...`). These are operational summaries based on `payment_details`; they must not be confused with formal **Tagesbericht (formal)** (the controller XML comment has an X/Z explanation).

---

## Quick reference — API paths (evidence summary)

| Area | Base route |
|------|----------------|
| Formal Tagesbericht | `api/reports/tagesbericht` |
| Formal Monatsbericht | `api/reports/monatsbericht` |
| Formal Jahresbericht | `api/reports/jahresbericht` |
| Operational reports | `api/Reports/operational/...` |
| Legal export completeness | `api/reports/legal-export-completeness/...` |
| POS payment | `api/pos/payment` (+ legacy `api/Payment`) |
| Tagesabschluss | `api/Tagesabschluss/...` |
| RKSV Sonderbelege | `api/rksv/special-receipts/nullbeleg`, `.../startbeleg`, `.../monatsbeleg`, `.../jahresbeleg`, `.../schlussbeleg` |
| Receipts | `api/Receipts/...` |
| Fiscal export | `api/admin/fiscal-export` |
| Integrity | `api/admin/integrity` |
| FO outbox | `api/admin/finanzonline-outbox` |
| FO reconciliation (legacy) | `api/admin/finanzonline-reconciliation` |
| FO readiness | `api/admin/finanzonline-readiness` |
| FinanzOnline config/status | `api/FinanzOnline/...` |

---

## Final operations checklist

| Topic | Status |
|------|--------|
| Formal Tagesbericht from POS | Missing (placeholder screen) |
| RKSV Sonderbelege (Nullbeleg … Schlussbeleg) | Section 4 + `api/rksv/special-receipts/*`; dedicated FO trail: Startbeleg + Jahresbeleg |
| Monthly/yearly Tagesabschluss → FinanzOnline | Same automation as daily was not seen in code |
| Formal-report FO submission | Informational / Non-DEP summary (outbox note) |
