# Restore verification drill (MVP)

This document describes **restore verification** (restore confidence drill), which is **not** the same as **backup artifact verification** (checksums, staging completeness, `BackupVerification` under backup admin APIs).

## Goals

- Increase confidence that a **logical PostgreSQL custom-format dump** is **readable and restorable** without applying a destructive restore to the **production application database**.
- Persist **separate** outcomes for: dump inspection (`pg_restore --list`), optional **isolated** `pg_restore` into an ephemeral database, optional **fiscal** SQL on a dedicated connection, and optional **live operational integrity** (read-only).

## Architecture

| Concern | Mechanism |
|--------|-----------|
| Dump selection | Latest **Succeeded** `BackupRun` with a **LogicalDump** artifact; path from staging or external archive (`RestoreVerificationDumpPathResolver`). |
| Dump inspection | `pg_restore --list` on the dump file (TOC readable). **Not** checksum proof. Worker only. |
| Restore attempt (optional) | When `RestoreVerification:IsolatedPgRestoreEnabled` is true: `CREATE DATABASE` + `pg_restore` into `rv_v_{runId:N}` on the server reachable via `IsolatedRestoreAdminConnectionStringName`, then **DROP DATABASE**. Never targets `DefaultConnection` in Production. |
| Fiscal validation | Optional: `scripts/sql/fiscal_go_live_validation.sql` via `RestoreVerification:FiscalValidationConnectionStringName`. **Never** `DefaultConnection` in Production. **Assumption:** for true post-restore fiscal checks, point this connection at a **clone** that matches backup content; the ephemeral DB from the isolated step is **dropped** before fiscal runs unless ops use a long-lived clone. |
| Integrity validation | Optional: `IIntegrityCheckService` (same logic as `GET /api/admin/integrity`) on the **operational** app DB — read-only. **Not** post-restore unless `DefaultConnection` in that environment *is* a restored clone (unusual). |
| TSE | **Deferred** (`tseRestoreVerification` in `DetailsJson`); vendor/crypto backup not covered. |
| FinanzOnline outbox | Drill **does not replay** outbox; replay is a separate controlled process (duplicate/invalid BMF risk). |

## Configuration

Section `RestoreVerification`:

- `WorkerEnabled` — process queued runs in `RestoreVerificationOrchestratorHostedService`.
- `OrchestratorDistributedLockEnabled` — default `true`; PostgreSQL advisory lock (key pair **distinct from** backup worker). Non-Development + `false` → readiness **Degraded**.
- `OrchestratorAdvisoryLockKey1` / `OrchestratorAdvisoryLockKey2` — override only on key collision in the same DB.
- `OrchestratorPollingInterval`
- `ScheduledWeeklyDrillEnabled` — enqueue if no successful scheduled run in 7 days and none queued/running.
- `FiscalValidationConnectionStringName` — optional clone; unset = fiscal skipped (drill may still succeed).
- `FiscalValidationScriptRelativePath` — default `..\scripts\sql\fiscal_go_live_validation.sql` from API content root.
- `PgRestoreExecutablePath` — optional; default `pg_restore` on `PATH`.
- `IsolatedPgRestoreEnabled` — default `false`; enables ephemeral full restore attempt.
- `IsolatedRestoreAdminConnectionStringName` — required when isolated restore enabled; maintenance DB (typically `postgres`); user needs **CREATEDB** (or equivalent). **Not** `DefaultConnection` in Production.
- `IsolatedPgRestoreTimeoutSeconds` — default `3600`; minimum `60` if set to a positive value &lt; 60 fails startup validation.
- `IncludeLiveIntegrityChecks` — default `true`; `IntegrityLookbackDays` Vienna calendar window.

Startup: `IValidateOptions<RestoreVerificationOptions>` — isolated enabled without admin connection name fails fast.

## Step order (worker)

1. Resolve latest logical dump → set `SourceBackupRunId`, internal descriptor.
2. **Dump inspection:** `pg_restore --list` → `PgRestoreListPassed`, exit code, line count; `details.dumpInspection` mirrors `pgRestoreList`.
3. **Restore attempt:** if disabled → `RestoreAttemptExecuted=false`, skip reason `ISOLATED_PG_RESTORE_DISABLED`. If enabled → run `IPgRestoreIsolatedRestoreRunner`; on failure → `Failed` (`ISOLATED_PG_RESTORE_FAILED` / etc.).
4. **Fiscal:** existing rules (skip / execute / fail).
5. **Integrity:** `IIntegrityCheckService` if `IncludeLiveIntegrityChecks`.
6. `Succeeded` only if all **executed** steps passed; skipped steps do not imply restore proof beyond their scope.

## API (enqueue only on HTTP)

Base route: `api/admin/restore-verification`

| Method | Path | Permission |
|--------|------|------------|
| POST | `trigger` | SettingsManage |
| GET | `runs/latest` | SettingsView |
| GET | `runs` | SettingsView |
| GET | `runs/{id}` | SettingsView |

Response DTO highlights: `DumpInspectionPassed`, `RestoreAttemptExecuted` / `RestoreAttemptPassed`, `FiscalSql*`, `IntegrityChecksPassed`, `DetailsJson` (outbox/TSE/interpretation).

## Outbox / external side effects

- **No** automatic FinanzOnline outbox replay during the drill.
- Isolated restore uses a **throwaway** database; no app connection strings should point at it.
- **Assumption:** API hosts running drills cannot trigger BMF submissions from SQL alone; still treat clone credentials and network paths as sensitive.

## Environment notes

- **Development:** Often `IsolatedPgRestoreEnabled=false`, no fiscal clone; inspection + optional live integrity on dev DB.
- **Staging:** Recommended: enable isolated restore against a non-production PostgreSQL role + fiscal connection to a **clone** restored from backup.
- **Production:** Never use `DefaultConnection` for isolated admin or fiscal validation names; misconfiguration fails the drill or skips safely per code paths.

## Acceptance criteria (MVP)

- [x] Row per drill: `restore_verification_runs`, status Queued → Running → Succeeded/Failed.
- [x] Dump inspection outcome stored (`PgRestoreListPassed` / API `DumpInspectionPassed`).
- [x] Optional isolated restore outcomes (`RestoreAttempt*` columns) when enabled.
- [x] Fiscal skipped/executed/passed and counts when configured; Production + `DefaultConnection` fiscal name blocked.
- [x] Integrity reflects `IIntegrityCheckService` when enabled.
- [x] No destructive restore on prod app DB; no restore work on HTTP thread.
- [x] Manual trigger + optional weekly enqueue.

## Related

- `GET /api/admin/integrity` — `IntegrityController`; same service as optional drill integrity step (different scope label in `DetailsJson`).
- `scripts/sql/fiscal_go_live_validation.sql`
- Backup admin APIs — artifact verification only.
