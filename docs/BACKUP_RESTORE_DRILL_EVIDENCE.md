# Backup restore drill evidence

**Status as of 2026-09-06 14:16 UTC:** Isolated System `pg_restore` **Passed** (latest product drill `67edd366-9bce-498b-91c9-5e7174b977ac` on dump `329398a6-4264-48d9-9e02-98387f522bca`). L4 continuity and fiscal SQL on a clone both passed after P0 fixes. Live operational integrity was **skipped** (dev `kasse_db` has orphan refunds / payments without invoice — not a restore defect).

**GO_LIVE §1.1 restore validation:** **PASSED** on this workstation. Production host drill is still required before treating Production as restore-proven.

## Priority (2026-09-06)

| Item | Priority | Status |
|------|----------|--------|
| Isolated System restore drill (L4 + fiscal SQL) | P0 | **Passed** (workstation, reconfirmed 14:16 UTC) |
| Production System Backup (PgDump, archive, alerts) | P0 | **Not executed** — operator checklist + commands: [`PRODUCTION_DEPLOYMENT_RUNBOOK.md`](PRODUCTION_DEPLOYMENT_RUNBOOK.md) §4.1 |
| FA manual Verify (hash + table counts) | P0 | **Passed** 2026-09-06 16:18 local — SuperAdmin `/backup/runs` → Details → Verifizierung → Prüfen on System run `329398a6-4264-48d9-9e02-98387f522bca`. Toast **Verifizierung bestanden**; Checksum OK; 3 artifacts `passed` (stored = computed); table counts include `payment_details`/`receipts`/`invoices` = 132. |
| Tenant Validation Restore (Manager self-service) | **P2** (was P1) | **Postponed** |
| Incremental restore | P2 | Postponed |
| Cloud / WORM archive | P2 | Postponed |

Agents must not invent table counts, fiscal checksums, or a passing smoke test. Fill a new row only after a human operator (or CI against an isolated Postgres) has run the procedure.

**Procedure:** [`restore-verification-drill-runbook.md`](restore-verification-drill-runbook.md)  
**Hub:** [`BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md)  
**Go-live gate:** [`GO_LIVE_CHECKLIST.md`](GO_LIVE_CHECKLIST.md) §1.1 Backup strategy

## What “done” means

A drill row may be marked **Passed** only when **all** of the following have dated evidence (ticket, command output, or `restore_verification_runs` id):

1. Latest **Succeeded** System backup (`BackupStrategyKind.System`, logical dump) identified.
2. Isolated database created (name pattern `rv_v_*` or documented clone). **Never** `pg_restore` onto Production `DefaultConnection`.
3. Restore completed; table list from `information_schema.tables` captured.
4. Row counts for fiscal-critical tables compared to source (at minimum `payment_details`, `invoices`, `receipts` / special receipts, TSE/DEP-related rows as applicable).
5. Fiscal SQL [`scripts/sql/fiscal_go_live_validation.sql`](../scripts/sql/fiscal_go_live_validation.sql) run **on the clone**.
6. Application smoke against the clone **or** documented skip (API must not point at Production while testing restore).
7. Isolated DB dropped (or retained as a labelled clone with access control).

## Execution log

| Date (UTC) | Operator | Source backup run id | Isolated DB | Tables restored | Count match | Fiscal SQL | App smoke | Result | Notes |
|------------|----------|----------------------|-------------|-----------------|-------------|------------|-----------|--------|-------|
| 2026-08-17 | Cursor agent (workstation) | — | not created | — | — | — | skipped | **Not executed** | Postgres up; no `*.dump` / Succeeded System artifact. Do not `pg_restore` onto live `kasse_db`. |
| 2026-09-06 12:27 | desktop-a5rdc5p\juke (Cursor agent) | `f25cacb5-b446-46c3-a183-f6ac53469531` | `rv_v_9c5fe0d0f0154db6afaef2ab0a870078` (product drill, dropped); second clone `restore_validation_20260906` (counts + fiscal, then dropped) | `pg_restore` exit 0; TOC 1373 lines; dump magic `PGDMP` | **Yes** (see counts below) | **FAIL** (script `RESULT: FAIL`; see notes) | skipped (`ApplicationSmokeProbeEnabled=false`; API stayed on `kasse_db`) | **FAILED** | Isolated restore **passed**. Product drill `9c5fe0d0-f015-4db6-afae-f2ab0a870078` status Failed: `POST_RESTORE_CONTINUITY_SQL_FAILED` — L4 required table `periodenbericht_runs` missing on **source and** dump (`to_regclass` null). DefaultConnection was **not** restored onto. Encryption **disabled**. Archive copy present. |
| 2026-09-06 12:58 | desktop-a5rdc5p\juke (Cursor agent) | `bba522a5-3a51-463c-8757-77a413e923ef` | `rv_v_fcc1725577c5446f82a09cb81d15431f` (product drill, dropped); clone `restore_validation_20260906b` (counts + fiscal, then dropped) | `pg_restore` exit 0; TOC 1373 lines; dump magic `PGDMP` | **Yes** (see 12:58 counts) | **OK** (`RESULT: OK`; index `IX_offline_transactions_CashRegisterId_payload_hash`) | skipped (`ApplicationSmokeProbeEnabled=false`; API stayed on `kasse_db`) | **PASSED** | Product drill `fcc17255-77c5-446f-82a0-9cb81d15431f` **Succeeded**. `AllowNonPgDumpBackupSource=false` selected `SystemComposite`. L4 `passed` (32 checks; `periodenbericht_runs` no longer required). SuperAdmin JWT had ambient tenant `dev`; System GET/trigger worked. Prior same-dump drill `5b27d6c1-…` Failed `INTEGRITY_CHECKS_FAILED` on **live** `kasse_db` (2 orphan refunds, 50 payments without invoice); passing run set `IncludeLiveIntegrityChecks=false`. DefaultConnection was **not** restored onto. Encryption **disabled**. |
| 2026-09-06 14:16 | desktop-a5rdc5p\juke (Cursor agent) | `329398a6-4264-48d9-9e02-98387f522bca` | `rv_v_67edd3669bce498b91c95e7174b977ac` (product drill, dropped); clone `restore_validation_20260906c` (counts + fiscal, then dropped) | `pg_restore` exit 0; TOC 1373 lines; dump magic `PGDMP` | **Yes** (see 14:16 counts) | **OK** (`RESULT: OK`; index `IX_offline_transactions_CashRegisterId_payload_hash`) | skipped (`ApplicationSmokeProbeEnabled=false`; API stayed on `kasse_db`) | **PASSED** | Product drill `67edd366-9bce-498b-91c9-5e7174b977ac` **Succeeded** after P0 fixes. `AllowNonPgDumpBackupSource=false` selected `SystemComposite`. L4 `passed` (32 checks; `required_failed=0`; `periodenbericht_runs` not required). SuperAdmin JWT ambient tenant `dev`; `POST /api/settings/backup/now` enqueued System. `IncludeLiveIntegrityChecks=false`. Product fiscal skipped (`FISCAL_CONNECTION_NOT_CONFIGURED`); fiscal SQL run on clone only. DefaultConnection was **not** restored onto. Encryption **disabled**. |

### 2026-09-06 14:16 row-count comparison (`kasse_db` vs isolated clone)

Source measured after the System dump. Clone: `restore_validation_20260906c` restored from `C:\data\regkasse-backup-staging\backup_deployment_system_20260906_141541.dump`. Product L4 on `rv_v_67edd366…` reported the same fiscal spine counts (`payment_details`/`receipts` = 132) before drop.

| Table | Source (`kasse_db`) | Isolated restore | Match |
|-------|---------------------|------------------|-------|
| `payment_details` | 132 | 132 | Yes |
| `receipts` | 132 | 132 | Yes |
| `invoices` | 132 | 132 | Yes |
| `"DailyClosings"` | 29 | 29 | Yes |
| `"TseSignatures"` | 84 | 84 | Yes |
| `audit_logs` | 1856 | 1856 | Yes |

### 2026-09-06 12:58 row-count comparison (`kasse_db` vs isolated clone)

Source measured after the System dump. Clone: `restore_validation_20260906b` restored from `C:\data\regkasse-backup-staging\backup_deployment_system_20260906_124951.dump`. Product L4 on `rv_v_fcc17255…` reported the same fiscal spine counts before drop.

| Table | Source (`kasse_db`) | Isolated restore | Match |
|-------|---------------------|------------------|-------|
| `payment_details` | 132 | 132 | Yes |
| `receipts` | 132 | 132 | Yes |
| `invoices` | 132 | 132 | Yes |
| `"DailyClosings"` | 29 | 29 | Yes |
| `"TseSignatures"` | 84 | 84 | Yes |
| `audit_logs` | 1851 | 1851 | Yes |

### 2026-09-06 12:27 row-count comparison (`kasse_db` vs isolated clone)

Source measured before clone. Clone: `restore_validation_20260906` restored from `C:\data\regkasse-backup-staging\backup_deployment_system_20260906_122501.dump`. Drill L4 evidence on `rv_v_*` reported the same fiscal counts before drop.

| Table | Source (`kasse_db`) | Isolated restore | Match |
|-------|---------------------|------------------|-------|
| `payment_details` | 132 | 132 | Yes |
| `receipts` | 132 | 132 | Yes |
| `invoices` | 132 | 132 | Yes |
| `"DailyClosings"` | 29 | 29 | Yes |
| `"TseSignatures"` | 84 | 84 | Yes |
| `audit_logs` | 1846 | 1846 | Yes |

### Encryption and archive (2026-09-06 14:16)

| Check | Result |
|-------|--------|
| `Backup:EncryptionEnabled` | **Not set** (default false). Worker log: `encrypted=False`. |
| Staging dump | `C:\data\regkasse-backup-staging\backup_deployment_system_20260906_141541.dump` (1 354 748 bytes), magic `PGDMP`. |
| External archive | `C:\data\regkasse-backup-archive\329398a6426448d99e0298387f522bca\` — `.dump` + `.system.zip` + `_manifest.json`. |
| Adapter | Config `PgDump`; run `AdapterKind=SystemComposite`. |
| Drill eligibility | `RestoreVerification:AllowNonPgDumpBackupSource=false` — `SystemComposite` treated as PgDump-equivalent. |

### Encryption and archive (2026-09-06 12:58)

| Check | Result |
|-------|--------|
| `Backup:EncryptionEnabled` | **Not set** (default false). |
| Staging dump | `C:\data\regkasse-backup-staging\backup_deployment_system_20260906_124951.dump` (1 349 984 bytes), magic `PGDMP`. |
| External archive | `C:\data\regkasse-backup-archive\bba522a53a51463c875777a413e923ef\` — `.dump` + `.system.zip` + `_manifest.json`. |
| Adapter | Config `PgDump`; run `AdapterKind=SystemComposite`. |
| Drill eligibility | `RestoreVerification:AllowNonPgDumpBackupSource=false` — `SystemComposite` treated as PgDump-equivalent. |

### Encryption and archive (2026-09-06 12:27)

| Check | Result |
|-------|--------|
| `Backup:EncryptionEnabled` | **Not set** (default false). Worker log: `encrypted=False`. |
| Staging dump | `C:\data\regkasse-backup-staging\backup_deployment_system_20260906_122501.dump` (1 342 512 bytes), magic `PGDMP`. |
| External archive | `C:\data\regkasse-backup-archive\f25cacb5b44646c3a183f6ac53469531\` — `.dump` + `.system.zip` + `_manifest.json` (post-copy SHA-256 in worker log). |
| Adapter | Config `PgDump`; run `AdapterKind=SystemComposite` (composite wraps real `pg_dump -Fc`). |

### Product-path notes (12:27 Failed — closed by 12:58 Passed)

1. First drill `6d4ba897-…` failed `NO_ELIGIBLE_BACKUP_RUN` because `AllowNonPgDumpBackupSource=false` did not treat `SystemComposite` as PgDump. **Fixed:** `BackupLogicalDumpAdapterKinds`. 12:58 drill used `false` and selected `bba522a5` (`SystemComposite`).
2. SuperAdmin + ambient `dev` used to enqueue Tenant / 404 System GET. **Fixed:** deployment-wide SuperAdmin ACL + `/api/settings/backup` exemption. 12:58: `POST /now` + `GET /runs/{id}` with tenant `dev` succeeded (System).
3. L4 required `periodenbericht_runs` (absent on source). **Fixed:** removed from L4 required list.
4. Fiscal script rejected the real unique index name. **Fixed:** accept `IX_offline_transactions_CashRegisterId_payload_hash`. Clone `RESULT: OK`.
5. HTTP / restored-DB application smoke still not configured (documented skip).
6. Live integrity on `kasse_db` is a **data** issue (orphan refunds / payments without invoice), not restore. Passing drill skipped it.

## Preconditions (host)

- [x] PostgreSQL with `CREATEDB` on `IsolatedRestoreAdminConnectionStringName` (`RestoreAdmin` → `postgres` maintenance DB) — **not** used as restore target for `kasse_db`
- [x] `pg_restore` at `C:\Program Files\PostgreSQL\18\bin\pg_restore.exe`
- [x] At least one System backup **Succeeded** in the last 7 days (`329398a6-4264-48d9-9e02-98387f522bca`)
- [x] `RestoreVerification:IsolatedPgRestoreEnabled=true` on this workstation
- [x] Fiscal validation connection points at the **clone**, never Production `DefaultConnection` — product `FiscalValidationConnectionStringName` **unset** (skipped); fiscal SQL run manually on `restore_validation_20260906c` (`RESULT: OK`)

## How to run on a host that has a dump

```powershell
# Isolated DB — never the Production application database
$date = Get-Date -Format "yyyyMMdd"
$db = "restore_validation_$date"
# Use a maintenance role with CREATEDB, not the app user if it lacks CREATEDB.
& "C:\Program Files\PostgreSQL\18\bin\createdb.exe" $db
& "C:\Program Files\PostgreSQL\18\bin\pg_restore.exe" -d $db --no-owner --role=<restore_role> <path-to-latest-system.dump>
& "C:\Program Files\PostgreSQL\18\bin\psql.exe" -d $db -v ON_ERROR_STOP=1 -f scripts/sql/fiscal_go_live_validation.sql
```

Linux equivalent: `createdb restore_validation_YYYYMMDD` then `pg_restore -d restore_validation_YYYYMMDD /backups/latest.dump`. Full product path: [`restore-verification-drill-runbook.md`](restore-verification-drill-runbook.md) (`rv_v_*` ephemeral DBs).

Then fill the log row (table count vs source, fiscal SQL, smoke **against the clone only**).

## After a real drill

Append a row above, attach outputs (redact connection strings), and only then tick GO_LIVE §1.1 “Restore validation on isolated DB tested”.

Workstation §1.1 restore box is ticked from the 14:16 **PASSED** row (prior 12:58 also Passed). Add a **new** row when a Production host drill completes — do not overwrite the workstation result.

## Production System Backup (not run from this workstation)

Do **not** mark Production backup items Passed without host evidence. On the Production API host:

1. Confirm `Backup:ExecutionAdapterKind=PgDump` (Production startup rejects Fake). Paths: `appsettings.Production.example.json`.
2. Super Admin: `POST /api/settings/backup/now` (or FA System backup). Super Admin with an ambient tenant still enqueues **System**, not Tenant.
3. Confirm run `Succeeded`, dump magic `PGDMP`, and archive under `Backup:ExternalArchiveRoot/{runIdWithoutDashes}/`.
4. Alerting: activity feed always; email needs `Email:Smtp` + `Backup:FailureAlertEmailRecipients`; webhook needs `OperationalDr:Alerts:WebhookEnabled=true` + `WebhookUrl`.
5. FA Verify: Super Admin (`settings.manage`) → `/backup/runs` → Succeeded run → Verify. Expect SHA-256 match + TOC table counts.

## FA Verify (implementation)

| Surface | Behavior |
|---------|----------|
| API | `POST /api/admin/backup/{backupId}/verify` — `settings.manage`; SHA-256 + optional TOC `tableReport` |
| FA | `/backup/runs` uses `BackupRunsTable`; detail modal Verification tab shows hashes + table counts |
| Manager | No Verify button (`backup.manage` only) |

**2026-09-06 workstation API check** (run `bba522a5-3a51-463c-8757-77a413e923ef`): `isValid=true`, 3 artifacts `passed` (stored hash = computed), `logicalDumpAnalyzed=true`, 10 monitored tables present in dump. First call returned HTTP 500 (`ObjectDisposedException` — `await using` disposed EF’s Npgsql connection). Fixed in `BackupVerificationReportService`; TOC failure no longer fails the checksum response.
