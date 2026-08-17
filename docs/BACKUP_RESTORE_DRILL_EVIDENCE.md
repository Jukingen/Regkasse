# Backup restore drill evidence

**Status as of 2026-08-17:** **Not executed.** Workstation probe: PostgreSQL 18 service **Running**, `pg_isready` accepting connections on `:5432`, `pg_restore` on PATH. **No System logical dump** found under `C:\data\regkasse-backup-staging`, `C:\data\regkasse-backup-archive`, or the repo. Isolated DB `restore_validation_YYYYMMDD` was **not** created (restore without a dump is not a drill).

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

## Preconditions (host)

- [ ] PostgreSQL with `CREATEDB` (or equivalent) on `IsolatedRestoreAdminConnectionStringName` — **not** Production app credentials
- [ ] `pg_restore` on PATH (`RestoreVerification:PgRestoreExecutablePath` if not default)
- [ ] At least one System backup **Succeeded** in the last 7 days
- [ ] `RestoreVerification:IsolatedPgRestoreEnabled=true` on the drill host (Staging recommended)
- [ ] Fiscal validation connection points at the **clone**, never Production `DefaultConnection`

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
