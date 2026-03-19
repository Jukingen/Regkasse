# Legacy payload_hash mismatch — measurement and repair

## Risk

Legacy backfill used `encode(digest("PayloadJson"::text, 'sha256'), 'hex')` (key order undefined). Runtime canonical hash uses **sorted JSON keys** (`OfflinePayloadHashing.NormalizeAndHash`). If stored hash ≠ runtime canonical:

- Dedup by `(CashRegisterId, payload_hash)` can fail (same payload, two hashes).
- Replay may create duplicate intent rows or inconsistent state until repaired.

## Measurement

### 1. Admin API (recommended)

- **Endpoint:** `POST /api/admin/offline-payload-hash/analyze`
- **Permission:** ReportExport
- **Body:** `{ "MaxRows": 10000, "CashRegisterId": null }`
- **Response:** `RuntimeMismatchCount` = rows where stored `payload_hash` ≠ runtime canonical; `RepairableNoConflictCount` / `SkippedWouldConflictCount`; `SampleMismatchIds`; **`MismatchRatioPercent`**, **`LegacyDataQualityRiskHigh`** (true when ratio ≥ configured threshold), **`WarningMessage`** (set when risk high).

Use this to **measure** the share of rows at risk. Run periodically (e.g. after backfill or before disabling structural fallback).

### 2. PayloadHashGuard (threshold + ops visibility)

- **Config:** `appsettings.json` → `PayloadHashGuard`: `MismatchWarningThresholdPercent` (default 10), `SampleSizeForExportCheck` (500), `RunStartupCheck` (false).
- When mismatch ratio (RuntimeMismatchCount/Scanned × 100) **exceeds** the threshold:
  - Analyze response includes `LegacyDataQualityRiskHigh: true` and `WarningMessage`.
  - **Ops log:** Warning with ratio, threshold, Scanned, RuntimeMismatchCount.
- **GET /api/admin/offline-payload-hash/risk** (ReportExport): quick sample-based risk check; returns `LegacyDataQualityRiskHigh`, `MismatchRatioPercent`, `Scanned`, `RuntimeMismatchCount`, `WarningMessage`.
- **Fiscal export:** Each export runs a sample check; if risk is high, `Integrity.LegacyDataQualityRiskHigh` and `Integrity.LegacyPayloadHashMismatchRatioPercent` are set, and `ExportScopeWarnings` gets a line so legacy problem is visible in the export file.
- **Optional startup check (ops mode):** Set `PayloadHashGuard:RunStartupCheck: true`. After ~5s delay, a one-time check runs; if ratio is high, a warning is logged so legacy risk is visible before production use.

### 3. SQL (informational only)

- `scripts/sql/offline_payload_hash_legacy.sql` — counts total, null/empty hash, and rows where stored hash equals **raw text digest** (subset of “legacy-style” hash). It **does not** compute runtime canonical hash (that requires C# key ordering), so it cannot measure true mismatch rate. Use for coverage/null checks only.

- `scripts/sql/fiscal_go_live_validation.sql` — includes `offline_payload_hash_null_active` (WARN if Pending/Synced rows have null payload_hash) and `offline_payload_hash_coverage_pct`.

## Repair

### 1. Bulk repair (admin)

- **Endpoint:** `POST /api/admin/offline-payload-hash/repair`
- **Permission:** SystemCritical
- **Body:** `{ "MaxRows": 10000, "DryRun": true }` — run with `DryRun: true` first to see counts; then `DryRun: false` to apply.
- **Behaviour:** For each row where stored hash ≠ runtime canonical, updates `payload_hash` to canonical **only if** no other row on the same register already has that canonical (unique constraint). Conflicts are skipped and reported.

### 2. Lazy repair on replay

- **Code:** `OfflineTransactionService.TryAlignStoredPayloadHashToRuntimeCanonicalAsync`
- After a successful payload match (and optional dedup by hash), if the stored hash is still legacy, the service updates it to runtime canonical when the unique `(CashRegisterId, payload_hash)` allows. No second row can hold the same canonical; conflicts are skipped and logged.

### 3. Periodic repair job (automation)

- **Config:** `appsettings.json` → `PayloadHashRepairJob`: `Enabled`, `Interval` (e.g. 01:00:00), `BatchSizePerCycle`, `MaxBatchesPerCycle`, `CompletionSampleSize`.
- **Behaviour:** Background job runs every `Interval`; each cycle runs `RepairAsync` in batches until no updates or `MaxBatchesPerCycle` reached. **Conflict strategy: report only** — conflicting rows are skipped, counted in `payload_hash_repair_conflicts_total` and logged (no auto-resolution). After each cycle, runs `AnalyzeAsync(CompletionSampleSize)` and sets **completion metric** `payload_hash_completion_percent` = 100 − MismatchRatioPercent (Prometheus gauge). When completion is 100%, fallback is no longer needed.
- **Metrics:** `payload_hash_completion_percent` (gauge 0–100), `payload_hash_repair_conflicts_total` (counter, report-only conflicts).

## Recommendation

1. **Measure** regularly with `POST .../analyze` until mismatch rate is low or zero.
2. **Bulk repair** (DryRun then apply) for large legacy sets; re-run analyze to confirm.
3. **Periodic repair job** (PayloadHashRepairJob) runs automatically; keep enabled until `payload_hash_completion_percent` is 100%. Conflicts are report-only.
4. Rely on **lazy repair** on replay for stragglers.
5. Only after mismatch is negligible consider disabling structural fallback (`AllowStructuralFallback: false`) per `OFFLINE_STRUCTURAL_FALLBACK_SIMPLIFICATION.md`.

## Open risk

- Rows that **would conflict** (same register + same canonical hash already taken by another row) cannot be repaired without business decision (e.g. merge or mark one as failed). They remain with legacy hash; replay still matches by payload and may align if the “owner” of the canonical hash is synced first.
