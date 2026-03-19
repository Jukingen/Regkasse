# Payload-Hash Repair Conflict Triage

Operational visibility and triage for legacy `payload_hash` mismatch conflicts. **No auto-merge or auto-fix** — repair remains manual (DryRun first, then explicit Repair with `SystemCritical`).

## Response model (Analyze)

### Aggregates (unchanged)

| Field | Description |
|-------|-------------|
| `Scanned` | Number of offline rows scanned (by `CreatedAt` desc, optional `cashRegisterId` filter). |
| `NullOrEmptyPayloadHash` | Rows with null/empty `payload_hash`. |
| `RuntimeMismatchCount` | Rows where stored `payload_hash` ≠ runtime canonical SHA-256. |
| `RepairableNoConflictCount` | Rows that can be repaired (one per unique `(CashRegisterId, CanonicalHash)` slot). |
| `SkippedWouldConflictCount` | Rows skipped because repair would violate unique `(CashRegisterId, PayloadHash)`. |
| `MismatchRatioPercent`, `LegacyDataQualityRiskHigh`, `WarningMessage` | As before. |

### New: Conflict and repairable detail

**`ConflictGroups`** — list of conflict groups (read-only, for triage):

| Field | Description |
|-------|-------------|
| `CashRegisterId` | Register for this group. |
| `CanonicalHash` | Runtime canonical hash (target value). |
| `MismatchRowIds` | Offline row IDs that **cannot** be repaired (skipped). |
| `OccupantRowIds` | Row IDs that already have this hash and block repair (when `SkipReason = OccupantExists`). |
| `SkipReason` | `"OccupantExists"` \| `"MultipleRowsForSameSlot"`. |
| `LatestCreatedAtUtc` | Latest `CreatedAt` among mismatch rows (for ordering/priority). |
| `SeveritySuggestion` | `"High"` (OccupantExists) \| `"Medium"` (MultipleRowsForSameSlot). |

**`RepairableItems`** — list of rows that can be repaired (one per slot):

| Field | Description |
|-------|-------------|
| `CashRegisterId` | Register. |
| `CanonicalHash` | Canonical hash. |
| `RowId` | Single offline row ID that can be aligned. |
| `CreatedAtUtc` | Row `CreatedAt` (server). |

## Conflict grouping logic

1. **Scan** recent `OfflineTransaction` rows (order by `CreatedAt` desc, optional `cashRegisterId`, `maxRows`).
2. **Mismatch set**: For each row, compute runtime canonical hash; if it differs from stored `payload_hash`, add `(Id, CashRegisterId, CanonicalHash, CreatedAt)` to the mismatch set.
3. **Occupants**: Load all rows that already have `(CashRegisterId, PayloadHash)` where `PayloadHash` is one of the target canonical hashes (from the mismatch set).
4. **Per (CashRegisterId, CanonicalHash)**:
   - **OccupantExists**: If any row **other than** the mismatch rows already has this `(CashRegisterId, CanonicalHash)` → all mismatch rows in this group are **conflict**; `SkipReason = OccupantExists`, `OccupantRowIds` = those blocking row IDs.
   - **MultipleRowsForSameSlot**: If no external occupant but multiple mismatch rows want the same slot → one is chosen as repairable (oldest `CreatedAt`); the rest are conflict with `SkipReason = MultipleRowsForSameSlot`, `OccupantRowIds` empty.

No automatic resolution: ops use this to decide which rows to investigate, deduplicate, or leave as-is.

## Endpoints

| Method | Path | Description |
|--------|------|-------------|
| GET | `/api/admin/offline-payload-hash/risk` | Quick risk check (sample, ratio, warning). |
| POST | `/api/admin/offline-payload-hash/analyze` | Full analyze with `ConflictGroups` and `RepairableItems`. Body: `{ maxRows?, cashRegisterId? }`. |
| GET | `/api/admin/offline-payload-hash/export` | Same scope as analyze; returns CSV (conflicts + repairable). Query: `maxRows`, `cashRegisterId`. |
| POST | `/api/admin/offline-payload-hash/repair` | Repair (DryRun by default). Requires `SystemCritical`. |

## CSV export

- **Query params**: `maxRows` (default 10_000), `cashRegisterId` (optional).
- **Content-Type**: `text/csv`; filename `offline-payload-hash-analyze.csv`.
- **Columns**: `Type` (Conflict | Repairable), `CashRegisterId`, `CanonicalHash`, `RowId`, `CreatedAtUtc`, `SkipReason`, `SeveritySuggestion`, `MismatchRowIds` (semicolon-separated), `OccupantRowIds` (semicolon-separated), `LatestCreatedAtUtc`.
- Conflict rows have empty `RowId`/`CreatedAtUtc`; Repairable rows have empty `SkipReason`/`SeveritySuggestion`/list columns.

## Admin UI (read-only)

- **Location**: e.g. RKSV → **Payload-Hash Konflikte** (or **Offline Payload-Hash**).
- **Actions**: Run Analyze (POST with `maxRows` + optional `cashRegisterId`), Download CSV (link to GET export with same params).
- **Content**:
  - Summary cards: Scanned, RuntimeMismatchCount, RepairableNoConflictCount, SkippedWouldConflictCount, LegacyDataQualityRiskHigh.
  - **Conflict groups** table: CashRegisterId, CanonicalHash, SkipReason, SeveritySuggestion, LatestCreatedAtUtc, MismatchRowIds (copyable), OccupantRowIds (copyable).
  - **Repairable items** table: CashRegisterId, CanonicalHash, RowId (link to offline detail if exists), CreatedAtUtc.
- **No** Repair button on this page (repair stays in separate flow with DryRun and SystemCritical).
- **Severity**: Use `SeveritySuggestion` (High/Medium) for filtering or badge colour only; no automatic actions.

## Test plan

1. **Unit (existing)**  
   - `Analyze_CountsRuntimeMismatch_AndRepair_UpdatesIdempotently`: unchanged; still asserts counts and repair idempotency.

2. **Unit (new)**  
   - `Analyze_ReturnsConflictGroups_AndRepairableItems`: One row with correct hash (occupant), one mismatch same (reg, canonical) → single conflict group, `OccupantExists`, `OccupantRowIds` contains occupant id, `MismatchRowIds` contains mismatch id, `RepairableItems` empty.

3. **Integration**  
   - POST analyze with `cashRegisterId` filter: assert `ConflictGroups`/`RepairableItems` only for that register.  
   - GET export: assert CSV header and at least one data row when conflicts/repairable exist; assert no auto-repair (DB unchanged).

4. **Manual**  
   - Run analyze in admin UI; verify conflict table and repairable table; download CSV and open in Excel; confirm no Repair call from this page.

## Correctness and safety

- **No auto-merge / auto-fix**: All repair remains explicit (DryRun then Repair with elevated permission).
- **Read-only triage**: Conflict and repairable lists are for visibility and prioritisation only.
- **False positives**: Grouping is deterministic from current DB state; severity is a suggestion (High = real occupant block, Medium = multiple rows for same slot). No automatic resolution to avoid correctness risk.
