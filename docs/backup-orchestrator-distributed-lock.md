# Backup orchestrator — multi-instance safety (PostgreSQL advisory lock)

## 1. Architecture decision

**Chosen approach:** PostgreSQL **session** advisory lock via `pg_try_advisory_lock(int, int)` on a **dedicated, non-pooled** `NpgsqlConnection` (`Pooling=false`), held for the entire dequeue + backup + verification tick.

**Rationale (fit for this repo):**

- The app already depends on PostgreSQL; no new infrastructure or tables.
- Matches current semantics: **at most one backup pipeline active cluster-wide** per poll cycle (same as a single-threaded worker before scaling).
- **Crash recovery:** when the TCP session ends, PostgreSQL releases session locks (after disconnect detection). No lease TTL job or heartbeat table.

## 2. Tradeoff analysis

| Option | Why not (here) | Notes |
|--------|----------------|--------|
| **Lease row / lock table** | More moving parts: migrations, TTL, stale lease reclaim, heartbeat for long `pg_dump`. | Good when you need visibility in SQL or cross-DB coordination. |
| **Dedicated single worker deployment** | Valid operationally, but extra deployment topology and routing; not “minimal in repo”. | Still compatible: run 1 replica with worker + lock enabled. |
| **Hybrid (advisory + `SKIP LOCKED` dequeue)** | Stronger throughput (parallel backups for different runs) but changes product semantics and row-claim logic. | Future enhancement if you need N parallel backups. |
| **Redis / external distributed lock** | New dependency and HA story. | Avoided while Postgres is the system of record. |

**Session advisory lock + pooling:** Using the EF Core pooled connection for `pg_advisory_lock` is unsafe: returning a connection to the pool can leave a lock attached to a pooled session or release it unexpectedly. This implementation **always** uses `NpgsqlConnectionStringBuilder { Pooling = false }` for the lock connection only.

## 3. Files touched

| File | Role |
|------|------|
| `Configuration/BackupOptions.cs` | `OrchestratorDistributedLockEnabled`, `OrchestratorAdvisoryLockKey1/2` |
| `Services/Backup/IBackupOrchestratorDistributedLock.cs` | Gate API + `BackupOrchestratorGateAttempt` |
| `Services/Backup/BackupOrchestratorPostgreSqlAdvisoryLock.cs` | `pg_try_advisory_lock` / `pg_advisory_unlock`, lease `IAsyncDisposable` |
| `Services/Backup/IBackupOrchestratorMetrics.cs` | Metric abstraction |
| `Services/Backup/PrometheusBackupOrchestratorMetrics.cs` | `backup_orchestrator_*` metrics |
| `Services/Backup/BackupOrchestratorHostedService.cs` | Acquire lease → optional body → `DisposeAsync` |
| `Services/Backup/BackupConfigurationEvaluation.cs` | Degraded if lock disabled outside Development |
| `Program.cs` | DI registration |
| `appsettings.json` | Defaults / keys |
| `docs/ops-known-gaps.md` | Gap status |
| This document | ADR + rollout |

HTTP controllers and manual trigger **do not** acquire the lock; they only enqueue rows. The worker is the sole consumer.

## 4. Lock lifecycle (acquire / release / crash)

1. **Acquire:** Open non-pooled connection → `SELECT pg_try_advisory_lock(k1, k2)`.
   - `true` → return lease that owns the connection until disposed.
   - `false` → close connection; **no dequeue** this tick (another instance holds the lock).
2. **Work:** `BackupOrchestratorHostedService` runs existing EF-backed dequeue and pipeline while the lease lives (separate pooled connections for EF/`pg_dump` are fine).
3. **Release:** Lease `DisposeAsync` runs `pg_advisory_unlock(k1, k2)` then disposes the connection (lock dropped even if unlock returns false).
4. **Crash / SIGKILL:** Server closes the session; lock eventually clears. Until then, other instances see **contended** ticks — queued work stays **Queued** (not falsely marked success).

**Disabled mode:** `OrchestratorDistributedLockEnabled=false` returns a no-op lease and **bypasses** the database lock (single-replica dev or intentional ops choice). Admin health reports **Degraded** outside Development when disabled.

## 5. Duplicate trigger and scheduled overlap

- **Multiple manual triggers** are already constrained by `BackupManualTriggerService` (active manual run). The distributed lock does not replace that; it prevents **two workers** from picking the same `Queued` row at the same instant.
- **Rolling deploy:** Old and new pods may both run workers; only one holds the advisory lock at a time, so at most one executes the pipeline.
- **No silent success:** If the lock is not acquired (`ContendedElsewhere`) or the lock connection fails (`ConnectionFailed`), the orchestrator **does not** dequeue — logs and metrics reflect the outcome.

## 6. Observability

Prometheus (scraped via existing `/metrics`):

- `backup_orchestrator_distributed_gate_total{outcome}` — `disabled_bypass`, `acquired`, `contended`, `config_missing_connection`, `connection_open_failed`, `try_lock_failed`
- `backup_orchestrator_advisory_lock_hold_seconds` — histogram of lease duration (idle polls = short; full backup = long)

Structured logs:

- Information when lock is contended (keys included at Debug for acquire).

## 7. Failure scenarios

| Scenario | Behavior |
|----------|----------|
| Second instance during active backup | `pg_try_advisory_lock` false → skip tick; Queued unchanged. |
| DB down at tick | `ConnectionFailed` → no dequeue; warning log; alert on metric spike. |
| Process killed mid-backup | Lock released when Postgres drops session; another instance can take over; run may be `Running` stuck — **operational** follow-up (existing concern, not introduced by lock). |
| Unlock fails | Warning log; connection disposed anyway (session end). |
| Lock disabled in prod multi-replica | Health **Degraded**; duplicate dequeue risk returns. |

## 8. Acceptance criteria

- [ ] With `OrchestratorDistributedLockEnabled=true` and two API instances, only one executes dequeue+backup for a given time window (verify via logs + `contended` metric on the follower).
- [ ] With contention or DB errors, **no** transition `Queued → Running` occurs on that failed tick.
- [ ] HTTP `POST .../backup/trigger` does not reference lock types or acquire locks.
- [ ] `Pooling=false` used only on the advisory lock connection path.

## 9. Rollout notes

1. Deploy with defaults (`OrchestratorDistributedLockEnabled=true`, keys unchanged across cluster).
2. Watch `backup_orchestrator_distributed_gate_total` and lock hold histogram after scale-out.
3. If you must disable (e.g. broken sidecar networking to Postgres from worker), set `OrchestratorDistributedLockEnabled=false` and **scale API to one replica** for backup processing, or accept Degraded + race risk.
4. Change `OrchestratorAdvisoryLockKey1/2` only if another subsystem in the **same database** uses the same advisory key pair.

## 10. Companion: restore verification worker (separate lock)

Restore drills use a **different** advisory key pair (`RestoreVerification:OrchestratorAdvisoryLockKey1/2`, default distinct from backup) so backup and restore verification can **never** block each other’s lock namespace, while each remains single-flight cluster-wide for its own queue. See `docs/restore-verification-distributed-lock.md`.

## 11. Future work (not in this change)

- Row-level claim with `FOR UPDATE SKIP LOCKED` to allow **parallel** backup runs on different rows.
- Optional explicit “stuck Running” janitor for operator clarity.
