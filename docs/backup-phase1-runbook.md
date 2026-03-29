# Backup Phase 1 — Operations runbook (starter)

For PostgreSQL logical dump execution (`pg_dump`), see [backup-phase2-runbook.md](./backup-phase2-runbook.md).

## What Phase 1 is

Phase 1 establishes **orchestration boundaries**, **persistent metadata** (`backup_runs`, `backup_artifacts`, `backup_verifications`), and **hosted worker dequeue** (`BackupOrchestratorHostedService`). It is **not** a complete production backup stack: there is no automated PITR, no real `pg_basebackup` / `pg_dump` in the production adapter, and no automated restore.

## What runs where

- **HTTP (controllers):** enqueue manual jobs, read status/history only. No shell/CLI, no long-running backup on the request thread.
- **Background service:** picks `Queued` runs, executes `IBackupExecutionAdapter`, persists artifacts, runs `IBackupVerificationService`, then sets terminal state:
  - `Succeeded` **only** if verification passed.
  - `VerificationFailed` if execution succeeded but verification did not.
  - `Failed` if execution failed (e.g. `ProductionStub`).

## Configuration (`Backup` section)

See `backend/appsettings.json` and `backend/appsettings.Production.json`. Key fields:

| Key | Purpose |
|-----|---------|
| `WorkerEnabled` | Disable dequeue when `false` (runs stay `Queued`). |
| `OrchestratorPollingInterval` | Poll frequency (1s–24h; outside range fails startup validation). |
| `ExecutionAdapterKind` | `Fake` (dev simulation) or `ProductionStub` (fails closed until Phase 2). |
| `AcknowledgePhase1NoRealBackup` | **Required in non-Development** when using `ProductionStub`; documents operator acceptance that no real PostgreSQL backup runs yet. |
| `DevelopmentForceVerificationFailure` | Test hook; forces verifier failure (not allowed outside Development). |
| `ArtifactStagingRoot` | Hint path for fake artifacts; production adapter will interpret in Phase 2. |

**Startup:** `IValidateOptions<BackupOptions>` + `ValidateOnStart()` — e.g. `Fake` in Production fails process start. Production template uses `ProductionStub` + `AcknowledgePhase1NoRealBackup=true`.

**Admin status:** `GET /api/admin/backup/status/latest` includes `configurationHealth` (`Healthy` / `Degraded` / `Unhealthy`) and `artifactVerificationDisclaimer` (Phase 1 checks artifact metadata only, not restore).

## Verification scope (explicit)

Phase 1 **artifact metadata verification** checks descriptor shape and SHA-256 fields. It is **not** `pg_verifybackup`, not a restore drill, and does not prove RPO/RTO.

## API surfaces

- **Preferred:** `POST /api/admin/backup/trigger`, `GET /api/admin/backup/status/latest`, `GET /api/admin/backup/runs`, `GET /api/admin/backup/runs/{id}`, `GET /api/admin/backup/verification/latest` (`AdminBackupController`).
- **Legacy:** `POST /api/Settings/backup/now` still enqueues the same way and updates `SystemSettings.LastBackup` when a **new** run is created (not when duplicate-prevented).

Permissions: `settings.manage` (trigger), `settings.view` (reads).

## Idempotency and concurrency

- Optional `Idempotency-Key` equivalent: JSON body `idempotencyKey` on `POST .../trigger`. Reuse returns the same run.
- Only one **active** manual run (`Queued` / `Running` / `AwaitingVerification`) at a time; further manual requests return the existing run with `duplicateExecutionPrevented` and emit a `DuplicateExecutionPrevented` alert (logged in Phase 1).
- Trigger responses include `orchestrationState` (e.g. `NEW_RUN_QUEUED_AWAITING_WORKER`, `DUPLICATE_ACTIVE_MANUAL_PREVENTED`, `IDEMPOTENT_REPLAY_EXISTING_RUN`) and `newQueuedRunCreated` — **not** backup completion.

## Legacy `LastBackup` (SystemSettings)

`POST /api/Settings/backup/now` **no longer** updates `LastBackup` on enqueue (that implied “completed” under the old simulation). Until Phase 2 syncs this field from the last **Succeeded** verified run, treat `GET .../settings/backup` `LastBackup` as legacy / potentially stale.

## Single-instance / multi-instance

The orchestrator uses a **process-local** `SemaphoreSlim(1,1)` only. It does **not** provide distributed mutual exclusion: multiple API instances or pods can each dequeue work. Phase 2 options: PostgreSQL advisory lock, a `backup_worker_leases` row with TTL, `FOR UPDATE SKIP LOCKED` claim pattern, or a **single dedicated backup worker** process.

## After deploy

1. Apply EF migration `AddBackupOrchestrationPhase1`.
2. Confirm `Backup:ExecutionAdapterKind` is intentional (`Fake` in dev, `ProductionStub` only if you accept failing runs until the real adapter exists).
3. Trigger one manual backup and confirm run reaches `Succeeded` with `Fake` adapter.

## Legal hold / retention

Backup metadata tables are operational, not fiscal. Audit log append-only rules are unchanged. Long-term backup retention must respect `AuditRetention` and `LegalHold` policies for **audit content**; this runbook does not delete fiscal data.

## TSE

TSE vendor backup remains **out of scope** for this phase (`TseService` restore path still stub-level). Operators must follow hardware/vendor procedures in addition to DB backups.
