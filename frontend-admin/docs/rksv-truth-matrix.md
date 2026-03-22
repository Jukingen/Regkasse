# RKSV admin — field-level truth matrix

Baseline reference for operator-facing RKSV routes under `/rksv`. Terminology aligns with `src/shared/operatorTruthCopy.ts`, `src/shared/finanzOnlineReconciliationTruth.ts`, and `src/shared/rksvAdminTruth.ts`. **Do not treat this table as an API contract** — regenerate Orval clients from OpenAPI for authoritative DTO shapes.

---

## `/rksv` (RKSV Operations dashboard)

| Aspect | Classification |
|--------|----------------|
| **Truth classification** | **Mixed / diagnostic bridge** — aggregated health hints plus drill-down links; not a single authoritative fiscal record set. |
| **API sources** | `POST /api/admin/offline/payload-hash/analyze` (Orval: `postApiAdminOfflinePayloadHashAnalyze`), `GET /api/admin/offline/intent/coverage`, `GET /api/admin/finanzonline/reconciliation/metrics`, `GET /api/admin/operations/summary`, cash registers via admin client. See `RksvOperationsDashboard.tsx`. |
| **Authoritative fields** | Values returned directly in those API responses (counters, flags, percentages as documented in generated models). |
| **Derived / joined** | Health tags/colors from `map*ToHealth` normalizers; card copy from `build*CardCopy`. |
| **Best-effort / sample** | Payload-hash card: bounded `maxRows` (see page constant); coverage card: fixed UTC window noted in footnote. |
| **Misleading if read wrong** | “OK” / green on a card = no issue **in that API’s stated scope**, not “entire RKSV clean”. |
| **Missing operational-truth fields** | No per-payment FO rows; no full receipt list; no TSE signing internals. |
| **Read / mutation** | **Read-only** on this page (POST analyze is a read-only analysis query, not fiscal mutation). |

---

## `/rksv/status` (RKSV General Status)

| Aspect | Classification |
|--------|----------------|
| **Truth classification** | **Diagnostic / connection snapshot** — not payment-row FinanzOnline reconciliation truth. |
| **API sources** | `GET /api/Tse/status` (`useGetApiTseStatus`), `GET` FinanzOnline status (`useGetApiFinanzOnlineStatus`). |
| **Authoritative fields** | Connection flags, serial, Kassen-ID, certificate strings, FO `pendingInvoices`, `lastSync`, etc., as returned by those endpoints. |
| **Derived / joined** | None on page; presentation only. |
| **Best-effort** | Summary only; no row-level FO queue. |
| **Misleading if read wrong** | FO “Connected” + pending count ≠ Abgleich row cleanliness (`OPERATOR_FO_SUMMARY_SCREEN_COPY`, `OPERATOR_RKSV_GENERAL_STATUS_COPY`). |
| **Missing** | Per-payment FO status, correlation, full error classes per row (live on reconciliation list DTO). |
| **Read / mutation** | **Read-only**. |

---

## `/rksv/cmc-certificate`

| Aspect | Classification |
|--------|----------------|
| **Truth classification** | **Diagnostic (TSE device / certificate)**. |
| **API sources** | `GET /api/Tse/status`, `GET /api/Tse/devices` (generated `tse` client). |
| **Authoritative** | Fields on TSE status and device DTOs as generated. |
| **Derived** | None. |
| **Best-effort** | N/A. |
| **Misleading** | English labels mixed with German elsewhere — cosmetic, not contract drift. |
| **Missing** | RKSV legal interpretation; signing payload internals (protected backend). |
| **Read / mutation** | **Read-only** in UI. |

---

## `/rksv/verifications` (route; copy: Audit-Spur)

| Aspect | Classification |
|--------|----------------|
| **Truth classification** | **Audit-derived investigation** — keyword-sampled audit rows, not a canonical “verification results” API. |
| **API sources** | `GET /api/AuditLog` (paged), `GET /api/AuditLog/correlation/{correlationId}` when query set — Orval `AuditLogEntryDto`. |
| **Authoritative** | Raw audit row fields (`action`, `entityType`, `entityId`, `correlationId`, `timestamp`, etc.). |
| **Derived** | Client filter switches (offline / failed replay / timing); keyword subset via `auditLogMatchesVerificationsKeywordSample`. |
| **Best-effort** | Fixed `pageSize` (100); keyword matching sensitive to backend action naming. |
| **Misleading** | Route name `verifications` vs. actual audit trail — mitigated by copy in `OPERATOR_VERIFICATIONS_COPY`. |
| **Missing** | Typed verification result objects; signature-debug structured body (OpenAPI gap documented in `RKSv_ADMIN_CONTRACT_GAPS`). |
| **Read / mutation** | **Read-only**. |

---

## `/rksv/incident`

| Aspect | Classification |
|--------|----------------|
| **Truth classification** | **Mixed** — incident aggregate authoritative for its endpoint; joined FO/payment views explicitly marked derived where applicable. |
| **API sources** | `GET /api/admin/incidents/{correlationId}` and related admin APIs (see `incident/page.tsx` imports from `@/api/generated/admin/admin`). |
| **Authoritative** | Aggregate payload fields defined on incident DTO from OpenAPI. |
| **Derived / joined** | FO columns from reconciliation join over `paymentId` — badges `derived_from_foreign_row` per `operatorTruthCopy` / incident copy. |
| **Best-effort** | Parsed unstructured JSON from audit (only where documented). |
| **Misleading** | FO aggregate line is not row-level FO truth (`OPERATOR_INCIDENT_COPY.foAggregateLine`). |
| **Missing** | Per-row correlation on FO list DTO (`FINANZ_ONLINE_RECONCILIATION_ROW_CONTRACT_GAPS`). |
| **Read / mutation** | Primarily **read**; any actions must match existing buttons and permissions (unchanged in truth work). |

---

## `/rksv/replay-batch`

| Aspect | Classification |
|--------|----------------|
| **Truth classification** | **Navigation only** — no list API on this screen. |
| **API sources** | None until navigation to detail route. |
| **Authoritative** | N/A. |
| **Derived** | N/A. |
| **Best-effort** | N/A. |
| **Misleading** | Low risk if described as search shell. |
| **Missing** | Batch data until correlation entered. |
| **Read / mutation** | **Read-only** (client-side router push only). |

---

## `/rksv/replay-batch/[correlationId]`

| Aspect | Classification |
|--------|----------------|
| **Truth classification** | **Authoritative for batch detail DTO**; FO columns on payment rows **joined / derived** from reconciliation when `paymentId` matches. |
| **API sources** | `GET /api/admin/replay-batch/{correlationId}`; reconciliation rows joined in UI as implemented (see page + `rksvAdminTruth` / navigation helpers). |
| **Authoritative** | Batch metadata and payment items per generated replay batch DTO. |
| **Derived** | FO status from `FinanzOnlineReconciliationItemDto` join. |
| **Best-effort** | N/A unless noted on page. |
| **Misleading** | Treating joined FO as standalone source without reading tooltips. |
| **Missing** | Register FK on batch payment item DTO — documented in `RKSv_ADMIN_CONTRACT_GAPS`. |
| **Read / mutation** | Default **read-only** (confirm mutations in UI if any). |

---

## `/rksv/integrity`

| Aspect | Classification |
|--------|----------------|
| **Truth classification** | **Diagnostic report** — backend consistency checks over a date window; not a substitute for Abgleich or incident aggregate. |
| **API sources** | `GET /api/admin/integrity` (`getApiAdminIntegrity` → `IntegrityReportDto`). |
| **Authoritative** | Report counts and detail lists as returned by API. |
| **Derived** | UI severity tags from thresholds. |
| **Best-effort** | Scope limited by `fromDate`/`toDate` and backend rules (e.g. which tables feed sequence checks). |
| **Misleading** | “OK” on duplicate/sequence cards = **no issues in that check’s definition**, not global data quality. |
| **Missing** | FinanzOnline submission truth (use Abgleich). |
| **Read / mutation** | **Read-only**. |

---

## `/rksv/fiscal-export-diagnostics`

| Aspect | Classification |
|--------|----------------|
| **Truth classification** | **Diagnostic export preview** — JSON preview + integrity hints for export path; not live POS ledger. |
| **API sources** | Admin client: fiscal export preview/download helpers + generated params types (`GetApiAdminFiscalExportParams`); see page imports. |
| **Authoritative** | Preview JSON and integrity object as returned by backend for the selected parameters. |
| **Derived** | Checkbox/toggles that change request scope; display-only typing `FiscalExportIntegrity` in page for nested JSON (must stay aligned with API or be narrowed from `unknown` via safe parsing — follow existing page pattern). |
| **Best-effort** | Export subset depends on filters; may not include all historical rows. |
| **Misleading** | Export integrity flags vs. runtime Abgleich status. |
| **Missing** | Row-level FO queue. |
| **Read / mutation** | **Read / download**; no fiscal repair on this page unless explicitly added elsewhere. |

---

## `/rksv/finanz-online-operations`

| Aspect | Classification |
|--------|----------------|
| **Truth classification** | **Integration / diagnostics** — config, connection test, error list, invoice history lookup. |
| **API sources** | Generated `finanz-online` client: status, config, errors, history by invoice id, test connection POST. |
| **Authoritative** | Each endpoint’s DTO fields. |
| **Derived** | None beyond UI grouping. |
| **Best-effort** | Error list recency and sampling depend on backend. |
| **Misleading** | Reading as primary FO operational truth — copy directs to Abgleich (`OPERATOR_FO_OPERATIONS_PAGE_COPY`). |
| **Missing** | Per-payment reconciliation rows (use `/rksv/finanz-online-queue`). |
| **Read / mutation** | **Mixed** — test connection POST; no reconciliation retry here. |

---

## `/rksv/finanz-online-queue` (Abgleich)

| Aspect | Classification |
|--------|----------------|
| **Truth classification** | **Primary operational truth (payment-level FO)** for listed rows — within DTO and filter limits documented in UI. |
| **API sources** | `GET /api/admin/finanzonline/reconciliation`, `GET .../metrics`, `POST .../retry/{paymentId}`. Types: `FinanzOnlineReconciliationItemDto`, metrics DTOs. |
| **Authoritative** | Row fields present on list DTO (`finanzOnlineStatus`, `finanzOnlineError`, `paymentId`, `receiptNumber`, `cashRegisterId`, timestamps, etc.). |
| **Derived** | Retry button visibility from status + contract helper; register display from `viewFinanzReconciliationRegister`; URL investigation context (not extra API filter for batch correlation). |
| **Best-effort** | Metrics failure-kind buckets are aggregate, not per-row classes (`OPERATOR_FO_QUEUE_COPY.metricsFailureKindScope`). |
| **Misleading** | Assuming correlation exists on each row — it does not (`FINANZ_ONLINE_RECONCILIATION_ROW_CONTRACT_GAPS`). |
| **Missing** | Fields listed in `FINANZ_ONLINE_RECONCILIATION_ROW_CONTRACT_GAPS` / `RKSv_ADMIN_CONTRACT_GAPS`. |
| **Read / mutation** | **Mixed** — list read + per-row retry POST. |

---

## `/rksv/payload-hash-conflicts`

| Aspect | Classification |
|--------|----------------|
| **Truth classification** | **Mixed** — analysis/export (read) vs repair (write). |
| **API sources** | `POST /api/admin/offline/payload-hash/analyze`, `POST .../repair`, CSV via admin client; types `OfflinePayloadHashAnalyzeResult`, `OfflinePayloadHashRepairResult`, etc. |
| **Authoritative** | Analyze/repair response fields. |
| **Derived** | Table presentation; tab layout separates investigation vs Eingriff. |
| **Best-effort** | Analyze bounded by `maxRows` and optional register filter. |
| **Misleading** | Treating analyze scope as full DB scan. |
| **Missing** | N/A for minimal scope. |
| **Read / mutation** | **Mixed** — repair gated by `system.critical` permission in UI (unchanged). |

---

## `/rksv/offline-intent-coverage` (listed for completeness)

| Aspect | Classification |
|--------|----------------|
| **Truth classification** | **Diagnostic coverage metrics** (device/sequence coverage). |
| **API sources** | `GET /api/admin/offline/intent/coverage` with window params. |
| **Authoritative** | API numbers and alerts. |
| **Read / mutation** | **Read-only** in typical UI. |

---

### FinanzOnline summary decision (explicit)

- **Payment-level operational truth:** `/rksv/finanz-online-queue` (reconciliation list + row actions), within Orval DTO limits.
- **Not row-level FO truth:** `/rksv/status` FO card, `/rksv` dashboard FO metrics card, `/rksv/finanz-online-operations` — connection/config/diagnostics and aggregates only (`OPERATOR_FO_SUMMARY_SCREEN_COPY`, `OPERATOR_FO_OPERATIONS_PAGE_COPY`).

---

### Maintenance

When adding endpoints or DTO fields: update OpenAPI, run `npm run generate:api`, then revise this matrix and `docs/CONTRACT_TRUTH_SURFACES.md` as needed.
