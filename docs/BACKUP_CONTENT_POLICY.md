# Backup content & cost policy

**Audience:** Operators, Super Admin, backend maintainers.  
**Status:** Implemented (Tenant vs System strategies + logical dump defaults + FA monitoring).  
**Hub:** [`docs/BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md)  
**Related:** `docs/BACKUP_PERMISSIONS.md`, `docs/restore-boundary-notes.md`, `AGENTS.md` § Backup & Disaster Recovery.

---

## What a backup contains

### Strategies (product model)

| | **Tenant** | **System** |
|--|------------|------------|
| Who | Mandanten-Admin (`backup.manage`) | Super Admin |
| Artifact | `*.tenant.zip` (tenant-filtered JSON tables) | `pg_dump -Fc` **+** `*.system.zip` |
| Identity (`AspNet*`) | **Excluded** | **Included** |
| Retention default | **30** days | **90** days |
| Facade | `CreateTenantBackupAsync` | `CreateSystemBackupAsync` |

### Tenant package (business / fiscal)

| Area | Notes |
|------|--------|
| Tenant-scoped business data | Payments, receipts, products, customers, carts, vouchers, cash registers, etc. |
| Audit / activity | Tenant-scoped compliance history |
| Fiscal / RKSV rows | Special receipts, daily closings, signature metadata |
| Invoice / report **metadata** | Paths and DB rows; PDF bytes under `report-pdfs/` are **not** inside the package |

### System package / dump

Logical dumps use `pg_dump -Fc` (PostgreSQL custom format with **zlib** compression via `-Z`, default level **6**), plus a structured system ZIP (active tenants nested, Identity, platform settings, deployment licenses, full audit).

### System dump exclude override (`Backup:LogicalDumpExcludeTables`)

Only applies when explicitly configured for dump paths. Tenant strategy never includes Identity. Do **not** casually exclude fiscal or audit tables from System dumps when those tables are required for recovery evidence.

| Table (typical exclude list for non-System policies) | Reason |
|-------|--------|
| `AspNetUsers` | Password hashes / credentials |
| `AspNetUserClaims` | Identity claims tied to credentials |
| `AspNetUserLogins` | External login secrets |
| `AspNetUserTokens` | Auth tokens |

### Not in the dump (filesystem)

PDF bytes under `report-pdfs/` (and similar ContentRoot paths) are **not** copied into the logical dump or tenant ZIP. Paths remain in the database; regenerate PDFs from data after a restore when needed.

---

## Schedule, retention, cost

| Knob | Default | Notes |
|------|---------|--------|
| Cron | `0 2 * * *` (02:00 UTC daily) | System strategy; off-peak; FA planner can set weekly/monthly |
| Retention Tenant | **30** days | Admin API + FA: **7–90** days |
| Retention System | **90** days | Strategy policy default |
| Auto-delete | After each succeeded run + optional daily | `BackupSucceededRunRetentionCleaner`; `AutomaticCleanupService` when `Backup:AutomaticCleanupEnabled` |
| Smart retention (opt-in) | GFS: 7 daily / 4 weekly / 12 monthly / 7 yearly | `Backup:SmartRetentionEnabled=true` → `SmartRetentionService` (replaces flat 30/90 cutoff) |
| Storage tiers (opt-in) | Hot ≤7d / Warm ≤30d / Cold &gt;30d | `Backup:StorageTierManagementEnabled=true` → `StorageTierService` (tags artifacts; Cold prefers `ExternalArchiveRoot`) |
| Storage cost dashboard | Indicative EUR Hot/Warm/Cold | `GET /api/admin/backup/storage-costs` + FA `/backup/costs` |
| Compression | `-Fc -Z6` + content-aware ZIP | System dump zlib; tenant/system ZIP via `CompressionService` (JSON Optimal; nested `.zip`/`.dump` NoCompression) |
| Staging disk alert | **80%** used | `Backup:StagingDiskUsageAlertPercent`; FA dashboard + `StorageAlertService` (every 6h) |
| Facade | `IBackupService` / `BackupService` | Enqueue + validation restore request only (no production restore) |
| Storage budget | **10 GB** summed succeeded dumps | Enqueue reject at 100%; `StorageAlertService` alerts at **80%** (`BackupAlertKind.StoragePressure`) |
| Incremental tenant ZIP | Opt-in delta since last full watermark | `IIncrementalBackupService` → `*.tenant.incr.zip` (not standalone restore) |
| ETA hint | Historical avg preferred; else size+steps heuristic | `IBackupTimeEstimator` on `GET .../status/latest` (`estimatedRemainingSeconds`) |

Tenant packages are **not** restored with `pg_restore` (validation restore remains System dumps only).

Rough capacity example (operator estimate): ~245 MB/backup × 30 ≈ **7.35 GB** — actual size depends on tenant volume and compression.

---

## RKSV restore (unchanged boundary)

- Validation-only isolated DB (`restore_validation_*`); never production via API.
- Same-tenant gate when ambient tenant and `backup_runs.tenant_id` both set (`IRestoreService`).
- Full audit trail; original fiscal timestamps not rewritten.
- See `RestoreModal` + `docs/restore-boundary-notes.md`.

---

## Config keys (cost / content)

```text
Backup:PgDumpCompressionLevel          = 6          # 0–9
Backup:LogicalDumpExcludeTables        = AspNetUsers, AspNetUserClaims, …
Backup:StagingDiskUsageAlertPercent    = 80
Backup:StorageAlertCheckInterval       = 06:00:00   # StorageAlertService poll
Backup:SmartRetentionEnabled           = false      # true = GFS thinning (7/4/12/7)
Backup:StorageTierManagementEnabled    = false      # true = Hot/Warm/Cold tags on artifacts
Backup:AutomaticCleanupEnabled         = false      # true = daily AutomaticCleanupService pass
Backup:AutomaticCleanupInterval        = 1.00:00:00 # min 1h
Backup:ArtifactStagingRoot             = <required for PgDump>
```
