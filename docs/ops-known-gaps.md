# Known gaps — backup Phase 1 vs production targets

This list is intentional scope debt; Phase 1 ships **boundaries and metadata**, not full infra.

| Gap | Risk | Next step |
|-----|------|-----------|
| No real `pg_dump` / `pg_basebackup` / WAL archive in app | No production-grade RPO from API alone | Implement `PostgreSqlBackupExecutionAdapter` + host-level WAL/PITR |
| `ProductionStub` adapter fails closed | Expected — prevents false confidence | Keep until real adapter ships |
| ~~Single-process orchestrator gate~~ | ~~Duplicate dequeue if API scaled~~ | **Mitigated (backup + restore verification):** PostgreSQL session `pg_try_advisory_lock` on **separate** key pairs per worker (`Backup:*` vs `RestoreVerification:*`), non-pooled `NpgsqlConnection`. See `docs/backup-orchestrator-distributed-lock.md` and `docs/restore-verification-distributed-lock.md`. |
| Phase 2 `pg_dump` only | No WAL/PITR / base backup | Phase 3 infra + optional dedicated worker host |
| Alerting = log publisher only | No paging | Register webhook/email `IBackupAlertPublisher` |
| No external disk / object-lock automation | Ransomware / operator errors | Phase 3 infra + copy jobs |
| TSE vendor backup | RKSV crypto not fully covered by DB | Vendor runbook + optional export |
| No automated restore drill | Untested backups | Monthly drill procedure + Phase 2 automation |
| Migration drift (if branch already applied old migration with extra `AlterColumn`) | History mismatch | If `AddBackupOrchestrationPhase1` was already applied **with** fiscal `AlterColumn` ops, do not re-run; new clones get backup-only migration. Coordinate with DBA if needed. |

## RPO / RTO targets (design goals)

- Stated ops targets (e.g. RPO ≤ 60s with WAL) require **infrastructure** not present in Phase 1 code.
- Phase 1 **metadata** supports observability; achieving the targets is a **platform** responsibility.
