# Restore verification orchestrator — multi-instance safety

## Decision

**Approach:** Same as backup: PostgreSQL **session** advisory lock `pg_try_advisory_lock(int, int)` on a **non-pooled** `NpgsqlConnection` (`Pooling=false`), held for the entire worker tick (**weekly enqueue check + at most one drill dequeue/run**).

**Separate scope from backup:** Default advisory keys differ (`RestoreVerification:OrchestratorAdvisoryLockKey1` default `0x52676B74` vs backup `0x52676B73`). Backup lock and restore lock are **independent** — one subsystem does not serialize the other.

## Why not lease table / dedicated worker / Redis (here)

Same tradeoffs as `docs/backup-orchestrator-distributed-lock.md`: minimal infra, crash safety via session disconnect, no new tables. Dedicated single-replica deployment remains a valid **operational** choice but is not required when the lock is enabled.

## Lifecycle

1. **Acquire:** non-pooled connection → `pg_try_advisory_lock(k1, k2)`.
2. **Work:** optional weekly enqueue (EF) + optional single `Queued` → `Running` → … pipeline.
3. **Release:** `pg_advisory_unlock` + dispose connection.
4. **Crash:** PostgreSQL drops session lock; another instance may proceed. **Stuck `Running`** rows remain an operational concern (not introduced by the lock).

## Failure semantics

- **ContendedElsewhere** or **ConnectionFailed:** tick skipped; **no** silent success; `Queued` rows unchanged on that tick.
- **DisabledBypass** (`OrchestratorDistributedLockEnabled=false`): no DB lock; multi-replica race possible — **Degraded** health outside Development (`GET /api/admin/restore-verification/readiness`).

## Observability

Prometheus:

- `restore_verification_orchestrator_distributed_gate_total{outcome}`
- `restore_verification_orchestrator_advisory_lock_hold_seconds`

## Config

`RestoreVerification:OrchestratorDistributedLockEnabled`, `OrchestratorAdvisoryLockKey1`, `OrchestratorAdvisoryLockKey2` (change only if colliding with another subsystem in the **same** database).
