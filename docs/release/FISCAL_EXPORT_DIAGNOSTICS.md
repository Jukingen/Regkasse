# Fiscal export — diagnostic semantics (ops / support)

API: `GET /api/admin/fiscal-export` → JSON package `schemaVersion` **1.3**.

## Terminology: diagnostic vs guarantee

- **Diagnostic** = best-effort, observed-within-scope only; helps ops/support interpret the export slice. **Not** a legal or global guarantee.
- **Guarantee / proof** = we do **not** provide RKSV compliance proof or global chain/sequence guarantees via this export. Integrity booleans are **diagnostics**.
- **Observed-within-scope** = the check applies only to the receipts/documents included in this export (and its time window); anything outside the slice is not validated here.
- **Warning / not-proof** = `exportScopeWarnings` and `integrityDiagnosticNotes` must be read; a "true" boolean does not mean global validity.

## Non-authoritative

All `integrity.*` booleans and `chainContinuityWarnings` are **best-effort diagnostics** (observed-within-scope only). They do **not** constitute RKSV legal proof or a guarantee. Always read `exportScopeWarnings` and `integrity.integrityDiagnosticNotes` on every export.

## Field reference

| Field | Meaning |
|-------|---------|
| `exportScopeWarnings` | Warnings and scope limits: integrity flags are diagnostic only (not legal proof), window boundaries, truncation, offline metric scope. |
| `totalReceiptsMatchingPeriod` | Count of receipts with `IssuedAt` in `[fromUtc, toUtc]` before row cap. |
| `receiptsTruncated` | True if more than 50,000 receipts matched; payload contains first 50k only. |
| `integrity.signatureChainValid` | **Diagnostic (observed-within-scope).** True iff `chainContinuityWarnings` is empty for **exported** receipts in order only. Not proof of full chain. |
| `integrity.receiptSignatureLinkageOkInExportOrder` | Same boolean as `signatureChainValid`; diagnostic only. |
| `integrity.sequenceContinuous` | **Diagnostic (observed-within-scope).** Beleg SEQ +1 per day along **exported** issuance order; unparseable numbers skipped. Not a full register audit. |
| `integrity.belegSequenceContiguousInExportedOrderPerDay` | Same as `sequenceContinuous`; diagnostic only. |
| `integrity.integrityDiagnosticNotes` | Notes on how diagnostic booleans were computed (scope, insufficient rows, what was not checked). Always read for correct interpretation. |
| `signatureChainState` | Live DB chain head for register; may differ from last receipt in file if receipts exist after `toUtc`. |

## Interpretation guide

1. **False** `signatureChainValid` → inspect `chainContinuityWarnings` and affected receipts; possible data or ordering issue **within the export slice**.
2. **True** `signatureChainValid` → does **not** rule out breaks outside the UTC window or before the first exported receipt (observed-within-scope only; not a guarantee).
3. **Truncation** → widen the date range into multiple exports or use SQL/backup for full history.
4. **Offline counts** → scoped by `OfflineCreatedAtUtc` in the same period; not the same as receipt issuance counts.

Admin UI: **RKSV → Fiscal-Export Diagnose** (German static page). See also `FISCAL_EXPORT_TERMINOLOGY_GUIDE.md` for a short terminology reference.
