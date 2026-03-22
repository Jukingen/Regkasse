# FinanzOnline — admin source-of-truth decision (RKSV)

**Status:** decision / operator guidance (documentation-first).  
**Scope:** `frontend-admin` surfaces vs backend contracts consumed via Orval (`src/api/generated/**`) and helpers in `src/shared/rksvAdminTruth.ts`.  
**Non-goals:** no behavior change in this note; no new local DTOs; no `any`.

## 1. Inspected surfaces (frontend)

| Route | File | Role |
|-------|------|------|
| `/rksv/finanz-online-queue` | `src/app/(protected)/rksv/finanz-online-queue/page.tsx` | Payment-based reconciliation list, filters, retry, metrics strip |
| `/rksv/finanz-online-operations` | `src/app/(protected)/rksv/finanz-online-operations/page.tsx` | Legacy-style diagnostics: config snapshot, status, errors table, invoice history, test connection |
| `/rksv/status` | `src/app/(protected)/rksv/status/page.tsx` | Summary card using **only** `GET /api/FinanzOnline/status`, links into Operations |
| Cross-links | `src/app/(protected)/layout.tsx` (menu), `InvoiceList.tsx`, `RksvOperationsDashboard.tsx`, `integrity/page.tsx` | Navigation and handoff copy |

## 2. Backend endpoints by surface

### 2.1 Authoritative for **payment-level FO submit / retry / ops queue**

| Method | Path | Orval entrypoints (representative) | Response DTOs (OpenAPI / Orval) |
|--------|------|-----------------------------------|----------------------------------|
| GET | `/api/admin/finanzonline-reconciliation` | `getApiAdminFinanzonlineReconciliation` | `FinanzOnlineReconciliationListResponse` → `FinanzOnlineReconciliationItemDto[]` |
| GET | `/api/admin/finanzonline-reconciliation/metrics` | `getApiAdminFinanzonlineReconciliationMetrics` | `FinanzOnlineMetricsResponse` |
| POST | `/api/admin/finanzonline-reconciliation/retry/{paymentId}` | `postApiAdminFinanzonlineReconciliationRetryPaymentId` | `FinanzOnlineRetryResponse` |

**Persistence:** `PaymentDetails` FO columns (`FinanzOnlineStatus`, error, reference, last attempt, retry count), updated from payment/checkout flow and `IPaymentService.RetryFinanzOnlineSubmitAsync` (see `FinanzOnlineReconciliationController`, `PaymentService`). This is the **operational reconciliation truth** for “did this payment’s FO submission succeed, what failed, can we retry”.

**Caveat (contract truth):** `FinanzOnlineMetricsResponse` reflects **in-process counters** (documented in backend as resetting on app restart). It complements but does not replace DB-backed row state.

### 2.2 Legacy / diagnostic (`/api/FinanzOnline/*`)

| Method | Path | Used by Operations page | Truth character |
|--------|------|-------------------------|-----------------|
| GET | `/api/FinanzOnline/config` | Yes | **Company + TSE toggle snapshot** (real settings); Operations UI is read-only (no PUT from this page). |
| GET | `/api/FinanzOnline/status` | Yes; also RKSV Status | **TSE-device-oriented** aggregate (`TseDevice` last sync, pending invoice/report counters, simulated connectivity). **Not** derived from `PaymentDetails` FO reconciliation. |
| GET | `/api/FinanzOnline/errors` | Yes | **Not authoritative today:** controller returns a **hard-coded sample list** (placeholder implementation). |
| GET | `/api/FinanzOnline/history/{invoiceId}` | Yes | Reads `FinanzOnlineSubmissions` by `invoiceId`. History is tied to the **legacy submission log model**; it does **not** guarantee coverage of the current payment-based FO pipeline. |
| POST | `/api/FinanzOnline/test-connection` | Yes | Exercises the same **simulated** connectivity path as status (not a substitute for reconciliation outcomes). |
| POST | `/api/FinanzOnline/submit-invoice` | Not linked in admin UI | Marked **obsolete** in backend; canonical path is admin reconciliation retry. |

### 2.3 Same payment FO fields elsewhere (supporting read)

Admin payments APIs expose the same `PaymentDetails` FO fields on list/detail DTOs (`AdminPaymentsController`). Treat as **another view of the same reconciliation truth**, not a third independent model.

## 3. Decision — what is authoritative for which use case

| Use case | Authoritative admin source | Legacy / supporting |
|----------|---------------------------|---------------------|
| “Which payments need FO attention?” / retry workflow | **`/rksv/finanz-online-queue`** + `GET/POST …/finanzonline-reconciliation` | Operations page **does not** list payment queue |
| Per-payment `FinanzOnlineStatus`, error text, reference id, retries | **`FinanzOnlineReconciliationItemDto`** (and admin payment DTOs with same fields) | `GET …/FinanzOnline/status` **does not** expose per-payment state |
| FO **configuration** (URL, user, flags from company settings, enabled on TSE) | **`GET …/FinanzOnline/config`** | Reconciliation API does not replace config reads |
| **Connectivity smoke test** (as implemented) | **`POST …/FinanzOnline/test-connection`** (diagnostic) | Do not equate with “all payments submitted” |
| **Recent FO errors list** | **Do not use** `GET …/FinanzOnline/errors` as truth (placeholder data) | Use reconciliation rows + audit/incident flows |
| **Submission history by invoice** | **Secondary / legacy log only** — `GET …/FinanzOnline/history/{invoiceId}` | For contested payments prefer reconciliation row + payment id + audit |

## 4. Proposed authoritative-flow statement (for UI copy / runbooks)

> **Operative FinanzOnline-Bereitschaft und Nacharbeit pro Zahlung** werden über **FinanzOnline Abgleich** und die APIs **`GET/POST /api/admin/finanzonline-reconciliation`** geführt; dort ist der Zustand aus **`PaymentDetails`** maßgeblich.  
> **FinanzOnline Operations** liefert **Konfigurationssicht, Verbindungsdiagnose und historische Submission-Tabelle** über **`/api/FinanzOnline/*`** — das ist **ergänzend**, nicht die gleiche Datenquelle wie der Zahlungsabgleich.  
> Die Felder **„Pending Invoices“** auf **`/api/FinanzOnline/status`** beziehen sich auf **TSE-Gerätekontext**, nicht auf die Abgleichsliste.

## 5. Where truth can diverge today (concrete)

1. **`pendingInvoices` / `pendingReports`** (`FinanzOnlineStatusResponse`) vs payments in `Pending` / `Failed` / `NeedsReconciliation` on reconciliation list — different backing stores and semantics.
2. **`GET …/errors`** static payload vs real `finanzOnlineError` on payment rows.
3. **`FinanzOnlineSubmissions` history** vs rows that only ever went through `PaymentService` FO updates (history may be empty or incomplete for modern flow).
4. **Simulated `isConnected`** (username heuristic in backend) vs actual FinanzOnline API outcome per payment.
5. **Metrics counters** vs DB — restart resets counters; list is still DB-backed.

## 6. Concrete UI labels / copy changes (no backend refactor)

Apply German operator-facing strings; prefer badges/tooltips over long titles where space is tight.

| Location | Current (representative) | Proposed |
|----------|-------------------------|----------|
| Sidebar (`layout.tsx`) | `FinanzOnline Operations` | `FinanzOnline Diagnose & Konfiguration` *(subtitle tooltip: nicht der Zahlungsabgleich)* |
| Sidebar | `FinanzOnline Abgleich` | `FinanzOnline Abgleich (Zahlungen)` or keep label + tooltip: **„Maßgeblich für FO-Status pro Zahlung“** |
| Operations `AdminPageHeader` title | `FinanzOnline Operations` | `FinanzOnline Diagnose (Legacy-Pfade)` or `FinanzOnline — Diagnose & Konfiguration` |
| Operations intro paragraph | Already mentions Abgleich for mutating reconciliation | Add one sentence: **„Pending Invoices hier ≠ Abgleichsliste.“** Add **Alert** `type="warning"` above status card: **„Keine Abgleichswahrheit — Zahlungsstatus siehe FinanzOnline Abgleich.“** |
| Operations “Recent Errors” card title | `Recent Errors` | `Letzte Fehler (API-Platzhalter — nicht operativ)` |
| Operations history card | `Submission History by Invoice ID` | `Submission-Verlauf (Legacy-Tabelle FinanzOnlineSubmissions)` |
| RKSV Status FO card (`status/page.tsx`) | Link `Open FinanzOnline Operations` | `Diagnose & Konfiguration öffnen` + secondary link **„Zahlungsabgleich öffnen“** → `/rksv/finanz-online-queue` |
| Abgleich page “Verwandt” line | `FinanzOnline Operations` | `Diagnose / Konfiguration (ohne Abgleichswahrheit)` |
| Invoice list FO retry toasts/messages | Already point to Abgleich | Ensure **paymentId** remains the operational key in copy (never imply invoice-only id is sufficient for retry API) |

**Badge discipline:** Reuse `OPERATOR_TRUTH_BADGE.diagnostic_support` framing from `src/shared/operatorTruthCopy.ts` for Operations-derived widgets; reconciliation table already uses contract-first tooltips (`OPERATOR_FO_QUEUE_COPY`).

## 7. Endpoints and DTOs impacted (reference only)

**Reconciliation (authoritative ops):**

- `GET /api/admin/finanzonline-reconciliation` → `FinanzOnlineReconciliationListResponse`, `FinanzOnlineReconciliationItemDto`
- `GET /api/admin/finanzonline-reconciliation/metrics` → `FinanzOnlineMetricsResponse`
- `POST /api/admin/finanzonline-reconciliation/retry/{paymentId}` → `FinanzOnlineRetryResponse`

**Legacy / diagnostic:**

- `GET /api/FinanzOnline/config` → `FinanzOnlineConfigResponse`
- `GET /api/FinanzOnline/status` → `FinanzOnlineStatusResponse`
- `GET /api/FinanzOnline/errors` → `FinanzOnlineErrorResponse[]`
- `GET /api/FinanzOnline/history/{invoiceId}` → `FinanzOnlineSubmission[]`
- `POST /api/FinanzOnline/test-connection` → `FinanzOnlineTestResponse`
- `PUT /api/FinanzOnline/config` → `FinanzOnlineConfigRequest` / `FinanzOnlineConfigResponse` (not used by Operations page today)

## 8. Risks if both surfaces are treated as semantically equal

- **False readiness:** green connectivity + empty placeholder “errors” while payments sit in `Failed` / `NeedsReconciliation`.
- **Wrong remediation:** operators use invoice-centric history and ignore **payment-scoped** retry (`paymentId`).
- **Permission skew:** route map uses `FINANZONLINE_VIEW` for Operations vs `FINANZONLINE_MANAGE` for Abgleich (`routePermissions.ts`) — viewers may see **misleading** summary on `/rksv/status` without access to authoritative queue.
- **Incident fatigue:** chasing `TseDevice.PendingInvoices` instead of reconciliation filters.

## 9. OpenAPI / contract gaps (do not patch with parallel TS interfaces)

Track via OpenAPI/schema updates + `npm run generate:api`; optionally cross-link new items in `RKSv_ADMIN_CONTRACT_GAPS` in `src/shared/rksvAdminTruth.ts` when picking up implementation work.

| Gap | Why it matters |
|-----|----------------|
| **`FinanzOnlineStatusResponse`** field descriptions | Clarify that counters / `lastSync` are **TSE-device scoped**, not reconciliation queue depth. |
| **`GET /api/FinanzOnline/errors`** | Either implement real data source + document semantics, or mark deprecated / remove from spec if intentionally non-authoritative (today: placeholder). |
| **`FinanzOnlineMetricsResponse`** | Document **volatile** in-process counters (restart) vs DB row state. |
| **`GET /api/FinanzOnline/history/{invoiceId}`** | Describe relationship (or lack thereof) to payment-based FO persistence; avoid implying completeness for current checkout flow. |
| **`POST /api/FinanzOnline/submit-invoice`** | Surface `deprecated` in OpenAPI if tooling should steer clients to reconciliation endpoints only. |
| **Reconciliation DTO** | Optional: `invoiceId` on `FinanzOnlineReconciliationItemDto` for invoice-centric ops without inferring — listed already as display/register gaps in `RKSv_ADMIN_CONTRACT_GAPS` (`finanzReconciliationRegisterDisplay`, etc.). |

## 10. Related docs

- `docs/finanzonline-operations-console.md` — historical console scope (diagnostic vs mutating).  
- `docs/CONTRACT_TRUTH_SURFACES.md` — register identity + FO queue contract discipline.  
- `src/shared/rksvAdminTruth.ts` — `viewFinanzReconciliationRegister`, `RKSv_ADMIN_CONTRACT_GAPS`.
