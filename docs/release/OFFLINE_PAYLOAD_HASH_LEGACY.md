# Legacy `payload_hash` vs runtime canonical hash

## Root cause

- **Runtime (replay / new rows):** `OfflinePayloadHashing.NormalizeAndHash` — parse JSON, recursively sort object keys, compact UTF-8 JSON, SHA-256 → lowercase hex.
- **Migration backfill (`20260318195117_OfflineTransactionFinalHardening`):** `digest("PayloadJson"::text, 'sha256')` — hash of PostgreSQL’s JSONB text representation **without** key reordering.

The same logical payload can therefore produce **different** `payload_hash` values. Direct lookup on `(cash_register_id, payload_hash)` then misses legacy rows.

## Structural fallback (before this change)

- Ran only when ID and hash lookup both failed.
- Compared `PayloadJson` via `OfflinePayloadComparer` (normalized `JsonNode`) over the **last N** rows by `created_at` (previously 50).
- **False negative:** matching row older than the window → duplicate offline row / failed dedup.
- **False positive:** theoretically possible if structural equality diverged from canonical hash (rare).

## Current behavior (after change)

1. **Direct hash lookup** (unchanged).
2. **Deterministic scan:** up to **2000** recent rows per register; recompute runtime hash from stored `PayloadJson`; first match wins (tracked entity).
3. **Structural fallback:** last resort, **150** recent rows, returns **tracked** entity (fixes prior detached-entity risk on replay updates).
4. **Lazy align:** after payload immutability passes, update `payload_hash` to runtime canonical if no unique-index conflict.

## Measurement

| Method | What it measures |
|--------|------------------|
| `POST /api/admin/offline-payload-hash/analyze` | Requires `report.export`. Scans up to 100k recent rows: `runtimeMismatchCount`, `repairableNoConflictCount`, `skippedWouldConflictCount`, sample IDs. |
| Pure SQL | Cannot reproduce runtime canonical hash; use API or C# tool. SQL below only gives row counts / null hashes. |

## Remediation

| Approach | Safety |
|----------|--------|
| **Lazy (automatic)** | On successful replay, align hash if unique allows. Idempotent. |
| **Batch** | `POST /api/admin/offline-payload-hash/repair` with `dryRun: true` first; then `dryRun: false`. Requires `system.critical` (SuperAdmin). Skips rows that would violate `(cash_register_id, payload_hash)` uniqueness. |

## Rollout

1. Deploy API (replay path + optional lazy align).
2. Run **analyze** in staging; note `repairable` vs `conflict`.
3. Run **repair** `dryRun: true`, then `false` in maintenance window if desired (optional; lazy align may suffice over time).
4. Re-run analyze until `runtimeMismatchCount` is acceptable.
5. **Conflict rows:** manual review (duplicate intents with identical canonical payload).

## Code touch list

- `Services/OfflinePayloadHashing.cs` — helpers for canonical hash comparison.
- `Services/OfflineTransactionService.cs` — recompute scan, structural limit + tracked load, lazy align.
- `Services/OfflinePayloadHashMaintenanceService.cs` — analyze/repair.
- `Controllers/OfflinePayloadHashMaintenanceController.cs` — admin endpoints.
- `Program.cs` — DI registration.
