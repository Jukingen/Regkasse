# Restore boundary — Phase 1 notes

## Current state

- `IRestoreOrchestrationBoundary` / `DeferredRestoreOrchestrationBoundary` expose **capability metadata only** (`DescribeCapabilities`). No restore execution, no PITR orchestration, no `pg_verifybackup` integration in application code.
- `GET /api/admin/backup/status/latest` includes a small `restore` object describing that automated restore is **not** available.

## What Phase 2+ should add

1. **Restore drill pipeline:** staged cluster, restore from physical backup + WAL, then application smoke tests.
2. **Verification depth:** keep **artifact metadata verification** (Phase 1) separate from **restore verification**; wire `pg_verifybackup` (or vendor-equivalent) into new evidence fields and/or distinct verifier source labels so UI cannot confuse the two.
3. **RPO/RTO tooling:** metrics and alerting tied to WAL archive lag and last verified backup timestamp.
4. **TSE:** coordinated restore of cryptographic material per vendor; keep separate from PostgreSQL restore runbooks.

## Operator-led restore (today)

Until Phase 2, treat restore as **manual PostgreSQL operations** using infrastructure backups (outside this API). Use `backup_runs` / `backup_artifacts` rows as **evidence pointers**, not as a guarantee that files exist on disk (especially for `Fake` adapter).

## Fiscal integrity reminder

After any restore, planned checks include receipt chain continuity, `signature_chain_state`, receipt sequence allocation, FinanzOnline outbox idempotency, and audit log continuity — performed as **post-restore validation procedures**, not implemented in Phase 1 code.
