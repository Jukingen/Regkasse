# Admin: Correlation-ID–Centred Incident Investigation

## Overview

Single support-first screen that composes existing APIs to show replay batch, audit timeline, replayPath/payloadRepaired, FinanzOnline attempts, and payment/receipt/register links. No new backend endpoint; optional aggregation endpoint only if needed later.

## Screen Flow

1. **Entry:** RKSV → **Incident (Correlation-ID)** or URL `/rksv/incident` or `/rksv/incident?correlationId=xxx`.
2. **Search:** User enters Correlation-ID (with or without dashes), clicks **Suchen** (or pastes from URL query).
3. **Loading:** Batch is fetched first; then audit by batch’s `auditCorrelationId`; reconciliation list is fetched and filtered by batch payment IDs.
4. **Result (top to bottom):**
   - **Replay-Zusammenfassung:** totalItems, successCount, failedOrDuplicateCount, correlationId + auditCorrelationId (copyable).
   - **Timeline (Audit):** Chronological audit entries; each line shows timestamp, action, short description, and **ReplayPath** / **PayloadRepaired** when present in requestData/responseData.
   - **Zahlungen / Belege / FO:** Table with Payment (link), Receipt (link), FO Status (tag + error + retry count), Amount.
   - **Rohes Audit-Log (JSON):** Collapsible section with full `auditLogs` array for copy/debug.

## Data Sources (Composed, No New Backend)

| Source | Endpoint | When |
|--------|----------|------|
| Replay batch | `GET /api/admin/replay-batch/{correlationId}` | On search; correlationId normalized (Guid with/without dashes). |
| Audit by correlation | `GET /api/AuditLog/correlation/{auditCorrelationId}` | After batch loaded; uses `batch.auditCorrelationId` (N format). |
| FO reconciliation | `GET /api/admin/finanzonline-reconciliation?status=...&limit=500` | When batch has payments; client filters items by `batch.payments[].paymentId`. |

## Timeline Logic

- **Replay started:** First audit entry for this correlation (e.g. OfflineReplayBatchStarted or similar).
- **Dedup / Recompute / Structural:** Shown via **ReplayPath** parsed from audit `requestData`/`responseData` (e.g. `requested_id`, `hash_match`, `recompute`, `structural`).
- **Synced / Failed:** From audit action/status and batch summary (successCount, failedOrDuplicateCount).
- **FO submit/retry:** From reconciliation list filtered by batch paymentIds (status, error, retryCount per payment).

## ReplayPath and PayloadRepaired

- Stored only in **audit log** (OfflineTransactionService writes them into requestData/responseData and description).
- Incident page parses `requestData` and `responseData` (JSON) and displays:
  - **ReplayPath** as tag (e.g. requested_id, hash_match, recompute, structural).
  - **PayloadRepaired** as tag when `true`.
- Raw audit JSON is kept in the collapsible section for full detail.

## New / Extra Endpoint Need

- **None** for the current design. All data comes from existing endpoints; reconciliation list is filtered client-side by batch payment IDs (limit 500; sufficient for typical batch sizes).
- **Optional later:** If support needs a single “incident by correlationId” response (e.g. to reduce round-trips or to support very large reconciliation lists), a minimal aggregation endpoint could return `{ batch, auditLogs, foByPaymentId }` in one call.

## Test Plan

1. **Search:** Open `/rksv/incident`, enter a valid replay batch correlation ID (from logs or replay-batch detail), click Suchen → Replay summary and timeline load.
2. **URL:** Open `/rksv/incident?correlationId=<id>` → same result; input pre-filled, data loads when id valid.
3. **Timeline:** Audit entries appear in time order; entries that contain replayPath/payloadRepaired show tags.
4. **FO column:** Payments that exist in reconciliation list show FO status (Submitted/Failed/Pending) and error/retry; others show —.
5. **Links:** Payment and Receipt columns link to `/payments?paymentId=...` and `/receipts/{receiptId}`; open in new tab.
6. **Raw audit:** Collapse “Rohes Audit-Log (JSON)” → expand → full JSON visible and copyable.
7. **Not found:** Invalid or unknown correlation ID → “Keine Daten” info alert.
8. **Error:** API error (e.g. 500) → error alert with message.

## Files

| File | Change |
|------|--------|
| `frontend-admin/src/app/(protected)/rksv/incident/page.tsx` | **New.** Incident view: search, replay summary, timeline, FO table, raw audit. |
| `frontend-admin/src/app/(protected)/layout.tsx` | RKSV menu: link “Incident (Correlation-ID)” → `/rksv/incident`. |
| `docs/release/ADMIN_INCIDENT_INVESTIGATION.md` | This doc. |
