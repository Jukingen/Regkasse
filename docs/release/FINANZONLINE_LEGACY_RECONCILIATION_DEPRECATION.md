# FinanzOnline legacy payment-row reconciliation — deprecation and migration

> **Status:** Historical deprecation/migration note. Legacy `POST /api/FinanzOnline/submit-invoice` references are deprecated/historical.

This document defines the **phased** move from **payment-centric** FinanzOnline visibility (`PaymentDetails.FinanzOnline*` columns + admin reconciliation API) to the **outbox-based** SOAP pipeline (`FinanzOnlineOutboxMessages` + `GET /api/admin/finanzonline-outbox`). It is an engineering/operations note, not a legal compliance statement.

## A. Legacy dependencies (current)

### Backend

| Area | Role |
|------|------|
| `PaymentDetails` columns | `FinanzOnlineStatus`, `FinanzOnlineError`, `FinanzOnlineReferenceId`, `FinanzOnlineLastAttemptAtUtc`, `FinanzOnlineRetryCount` — updated by `PaymentService` after submit/retry. |
| `GET /api/admin/finanzonline-reconciliation` | Lists payments filtered by legacy status. |
| `GET /api/admin/finanzonline-reconciliation/metrics` | In-process submit counters (`IFinanzOnlineMetrics`). |
| `POST /api/admin/finanzonline-reconciliation/retry/{paymentId}` | Calls `RetryFinanzOnlineSubmitAsync` → `SubmitInvoiceAsync` → outbox idempotency. |
| `AdminIncidentInvestigationController` | Embeds `FinanzOnlineReconciliationItemDto` rows for incident context. |
| `POST /api/FinanzOnline/submit-invoice` | Deprecated simulated path (separate from outbox enqueue). |

### frontend-admin

| Area | Role |
|------|------|
| `/rksv/finanz-online-queue` | Primary UI for legacy list + per-payment retry (`FINANZONLINE_MANAGE`). |
| Deep links from incident, integrity, status, replay-batch, invoices, RksvOperationsDashboard | Investigation / support flows still point at queue URL + query params. |
| `buildFinanzOnlineQueueInvestigationHref` | Builds `/rksv/finanz-online-queue?...` for operators. |

### What still **depends** on legacy paths

- **Per-payment retry** from admin when operators think in “payment id” terms (same backend path as today).
- **Incident / integrity** deep links that encode register/payment/correlation into the **queue** URL.
- **Metrics** endpoint consumers (if any scripts) using reconciliation metrics.

### Primary model (not legacy)

- **Outbox** (`/rksv/finanz-online-outbox`, `GET /api/admin/finanzonline-outbox`) for SOAP pipeline state (submission, protocol, retries).
- **Normal fiscal flow** still calls `IFinanzOnlineService.SubmitInvoiceAsync` → outbox enqueue.

---

## B. Phased deprecation plan

| Phase | Scope | Exit criteria |
|-------|--------|----------------|
| **1 — Communicate (current)** | Mark legacy API + UI; prefer Outbox in nav and copy; no breaking changes. | Operators know Outbox is primary; queue labeled legacy. |
| **2 — Parity & tooling** | Document mapping: outbox row ↔ payment/invoice where possible; optional admin filters/links. | Support can triage most cases from Outbox alone or with one click to payment. |
| **3 — Consume less** | Migrate incident/integrity links to prefer Outbox + correlation; keep queue as fallback for retry. | Fewer hard dependencies on queue URL. |
| **4 — Retire** | Remove reconciliation list API + queue page **only after** payment columns are migrated or replaced and retry has a new entry point (e.g. payment detail only). | Product sign-off + no critical external integrations on old routes. |

---

## C. First safe changes (implemented in repo)

- **Backend:** `[Obsolete]` on `FinanzOnlineReconciliationController` actions (Swagger shows deprecated); XML docs clarify outbox as primary. `POST /api/FinanzOnline/submit-invoice` obsolete message updated to prefer outbox.
- **frontend-admin:** Queue page title + breadcrumb use **`nav.finanzOnlineAbgleichLegacy`**; orange **Legacy** tag; phased-deprecation info alert; Outbox-first banners retained.
- **Nav:** `rksvMenuModel` already ordered **Outbox** before **queue** with “Legacy” in the sidebar label.

---

## D. Remaining blockers before final removal

1. **Retry UX:** Operators who only have **queue** permission model (`FINANZONLINE_MANAGE`) vs **outbox** (`FINANZONLINE_VIEW`) — align RBAC or document dual permission until merge.
2. **Incident / investigation** URLs: `buildFinanzOnlineQueueInvestigationHref` and related pages still target `/rksv/finanz-online-queue` — need migration plan for bookmarks.
3. **Data model:** `PaymentDetails` FinanzOnline columns remain the **audit-friendly** payment-side view; removing the API without a **reporting** or **read** replacement loses operational history unless replicated from outbox.
4. **OpenAPI clients:** Regenerate Orval after Swagger reflects `deprecated` (optional; runtime unchanged).

---

## E. Manual verification

1. Open **admin** `/rksv/finanz-online-queue` — title shows **(Legacy)** + tag, phased info alert visible, Outbox warning banner still present.
2. Switch **de / en / tr** — `nav.finanzOnlineAbgleichLegacy` and `finanzOnlineReconciliation` strings render.
3. **Swagger:** `GET/POST` under `finanzonline-reconciliation` show as **deprecated** (after rebuild).
4. Confirm **retry** still works: `POST /api/admin/finanzonline-reconciliation/retry/{paymentId}` with a valid payment id.
5. Confirm **outbox** unchanged: `GET /api/admin/finanzonline-outbox`.
