# Backup & Disaster Recovery (hub)

**Audience:** Backend/FA maintainers, operators, AI agents.  
**Status:** Implemented (Tenant vs System strategies, role-aware FA, validation-only restore).  
**Always-applied rules:** [`AGENTS.md`](../AGENTS.md) § Backup & Disaster Recovery.  
**Full system guide:** [`BACKUP_SYSTEM.md`](BACKUP_SYSTEM.md).

---

## Quick model

| | **Tenant Backup** | **System Backup** |
|--|-------------------|-------------------|
| Who | Mandanten-Admin (`Manager`) | Super Admin |
| Strategy | `BackupStrategyKind.Tenant` | `BackupStrategyKind.System` |
| Artifact | `*.tenant.zip` (JSON tables) | `pg_dump -Fc` + `*.system.zip` |
| Identity | Excluded | Included |
| Retention default | 30 days | 90 days |
| Facade | `CreateTenantBackupAsync` | `CreateSystemBackupAsync` |
| Scheduled cron | — | Yes (`BackupScheduledEnqueueService`) |
| List / download (Manager) | Own tenant only | Never |
| Validation restore | Not via tenant ZIP | System dump → isolated DB only |

---

## Content (summary)

- **Tenant data:** payments, receipts, products, customers, vouchers, cash registers, fiscal / RKSV rows, invoices metadata, tenant audit.
- **Reports / invoices PDF:** DB paths only; bytes under `report-pdfs/` are not inside the logical dump (regenerate after recovery when needed).
- **Audit logs:** included as read-only history in the package.
- **User credentials (Identity):** **System Backup only**.

Detail: [`BACKUP_CONTENT_POLICY.md`](BACKUP_CONTENT_POLICY.md).

---

## Restore rules (summary)

- Same-tenant gate when ambient + `backup_runs.tenant_id` set (`IRestoreService`).
- No backdating of fiscal timestamps.
- Full audit trail (`Restore*` events + correlation id).
- Cross-tenant restore prohibited (API → 404).
- No production restore via API — validation-only (`restore_validation_*`), dual Super Admin approval.
- Operator-led live recovery remains outside the API (DBA / infra).

Detail: [`restore-boundary-notes.md`](restore-boundary-notes.md), restore drill runbook: [`restore-verification-drill-runbook.md`](restore-verification-drill-runbook.md).

---

## Permissions (summary)

| Actor | View list | Trigger | Download | Restore |
|-------|-----------|---------|----------|---------|
| Mandanten-Admin | Own Tenant runs | Tenant (`backup.manage`) | Own Tenant packages | No |
| Super Admin | All + System | System + platform | All (with `settings.manage` / implication) | Validation + drills |

Detail: [`BACKUP_PERMISSIONS.md`](BACKUP_PERMISSIONS.md), AI short: [`ai/modules/backup_permissions.md`](../ai/modules/backup_permissions.md).

---

## Frontend Admin

| Path | Purpose |
|------|---------|
| `/backup` | Role-aware hub (`TenantBackupView` / `SystemBackupView`) |
| `/backup/dashboard` | DR operator dashboard |
| `/backup/performance` | Duration / ETA metrics |
| `/backup/compliance` | RKSV product-gate readiness |
| `/backup/costs` | Indicative Hot/Warm/Cold storage costs |
| `/backup/runs` | Run list / metrics |
| `/backup/configuration` | Schedule + platform execution mode (gated) |
| `/backup/audit` | Activity + audit |

Routes: `frontend-admin/src/shared/backupAreaRoutes.ts`.

---

## Key code

| Area | Path |
|------|------|
| Strategy enum / policy | `backend/Models/Backup/BackupStrategyKind.cs`, `BackupStrategyPolicy.cs` |
| Facade | `backend/Services/Backup/IBackupService.cs`, `BackupService.cs` |
| Access filter | `backend/Services/Backup/BackupRunAccessEvaluator.cs` |
| Tenant export | `TenantScopedLogicalBackupExecutionAdapter`, `TenantScopedBackupExporter` |
| System export | `CompositeSystemBackupExecutionAdapter`, `SystemScopedBackupExporter` |
| API | `AdminBackupController`, `AdminRestoreController` |
| FA hub | `frontend-admin/src/app/(protected)/backup/page.tsx` |

---

## Related runbooks

- Phase 1 orchestration: [`backup-phase1-runbook.md`](backup-phase1-runbook.md)
- Phase 2 `pg_dump`: [`backup-phase2-runbook.md`](backup-phase2-runbook.md)
- Distributed lock (restore verification): [`restore-verification-distributed-lock.md`](restore-verification-distributed-lock.md)
