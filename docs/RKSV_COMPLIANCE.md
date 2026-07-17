# RKSV Compliance — Backup & Restore

**Audience:** Compliance operators, Super Admins, backend/FA maintainers.  
**Scope:** Restore and backup guardrails that support RKSV-oriented data integrity (same-tenant isolation, timestamp preservation, auditability).  
**Not a legal guarantee:** This document describes product controls. It is not official BMF/RKSV certification.

**Related:** [`BACKUP_SYSTEM.md`](BACKUP_SYSTEM.md), [`restore-boundary-notes.md`](restore-boundary-notes.md), [`AGENTS.md`](../AGENTS.md) § Backup & Disaster Recovery, [`RKSV_OFFICIAL_SOURCES.md`](RKSV_OFFICIAL_SOURCES.md).

---

## Restore RKSV Compliance

### Rules

1. **Restore only to the same tenant** — When ambient tenant and `backup_runs.tenant_id` are both set, `IRestoreService` / `RestoreService` enforces a same-tenant gate. Cross-tenant mismatch → HTTP **404**. Cross-tenant production restore via API is prohibited.
2. **No backdating of restored data** — The restore workflow does **not** rewrite fiscal business dates to “today” or otherwise invent chronology.
3. **Original timestamps preserved** — Fiscal fields such as receipt `IssuedAt` / Beleg times in restored clones are left as in the backup; they are not mutated by the restore pipeline.
4. **Restore timestamp recorded** — Workflow records request / approval / completion times (`RequestedAt`, `ApprovedAt`, audit `AuditRecordedAtUtc` / `CompletedAtUtc`).
5. **Full audit trail** — Every restore request, approval, rejection, completion, and failure is written with actor, role, tenant (source backup tenant or platform convention), correlation id, and RKSV compliance notes.

**Additional product rules (implemented):**

- API restore is **validation-only** into isolated `restore_validation_*` databases — **never** the production connection.
- Dual Super Admin approval where required (`ManualRestoreAudit` notes: `dual_superadmin_approval`).
- Mandanten-Admin may manage **Tenant** backup packages but **cannot** restore.
- Tenant ZIP packages are not `pg_restore` inputs; validation restore uses **System** `pg_dump` artifacts.

Embedded audit compliance string (every manual restore audit event):

```text
validation_only;isolated_target;no_production_write;no_fiscal_timestamp_rewrite;dual_superadmin_approval
```

Source: `ManualRestoreAudit.RksvComplianceNotes`.

### Pre-restore compliance check

Before a validation restore is requested, `IComplianceCheckService` / `ComplianceCheckService` evaluates:

1. **Same tenant** — via `IRestoreService.EvaluateSameTenant` (cross-tenant → fail / API 404)
2. **Backup integrity** — logical dump SHA-256 present; on-disk verify when staging file exists
3. **RKSV / content gates** — Succeeded System dump only; Tenant ZIP packages rejected; validation-only path

Wired into `ManualRestoreTriggerService.CreateRequestAsync` and `BackupService.RestoreBackupAsync`.

Super Admin preflight API:

`GET /api/admin/restore/compliance-check?backupRunId={id}&tenantId={optional}`

---

### Restore compliance report

Super Admins can fetch an RKSV-oriented report for a manual restore request:

`GET /api/admin/restore/request/{requestId}/report`

Implementation: `IRestoreReportService` / `RestoreReportService` (maps `ManualRestoreRequest` + linked `RestoreVerificationRun`; there is **no** `RestoreHistory` table).

- `complianceChecked` — report evaluated product controls
- `rksvCompliant` — derived from validation-only + isolated `restore_validation_*` target + dual Super Admin approval + terminal/drill/fiscal evidence (**never** hard-coded `true`)
- `complianceFindings` — machine-readable reasons (e.g. `dual_superadmin_approval_pending`, `linked_drill_failed`, `fiscal_sql_failed`)
- `tablesRestored` — best-effort from drill `pg_restore` list line count when present; `recordsRestored` is null (row inventory not tracked)

---

### Audit Trail

Regkasse does **not** use the fictional labels `BACKUP_CREATED` / `BACKUP_RESTORE_*` / `BACKUP_DELETED` as enum values. Use the **implemented** audit actions and `AuditEventType` values below.

#### Restore workflow (`AuditEventType` + action string)

| Step | `AuditEventType` | Action string (`AuditLogActions` / log) | When |
|------|------------------|----------------------------------------|------|
| Restore started (request) | `RestoreRequested` | `RESTORE_REQUESTED` | Super Admin requests validation-only restore |
| Approved | `RestoreApproved` | `RESTORE_APPROVED` | Second Super Admin approves |
| Rejected | `RestoreRejected` | `RESTORE_REJECTED` | Second Super Admin rejects |
| Completed | `RestoreCompleted` | `RESTORE_COMPLETED` | Validation restore succeeded |
| Failed | `RestoreFailed` | `RESTORE_FAILED` | Validation restore failed |

Implementation: `backend/Services/RestoreVerification/ManualRestoreAudit.cs`, `backend/Models/AuditEventType.cs`.

#### Backup lifecycle (audit action strings)

| Event | Action string | Notes |
|-------|---------------|--------|
| Backup created / enqueued | `BACKUP_MANUAL_ENQUEUED` | Manual trigger (`BackupManualTriggerService`) |
| Settings changed | `BACKUP_SETTINGS_UPDATED` | Schedule / retention |
| Artifact downloaded | `BACKUP_ARTIFACT_DOWNLOAD` | Download audit with `tenant_id` when known |
| Artifact imported | `BACKUP_ARTIFACT_IMPORTED` | Register dump for tenant (no auto restore) |
| Execution mode changed | `BACKUP_RUNTIME_EXECUTION_MODE_CHANGED` | Super Admin / `settings.manage` |

#### Activity feed (operator notifications — not the security audit enum)

| `ActivityEventType` | Meaning |
|---------------------|---------|
| `BackupSucceeded` | Backup run succeeded |
| `BackupFailed` | Backup run failed |
| `RestoreDrillSucceeded` | Restore drill succeeded |
| `RestoreDrillFailed` | Restore drill failed |

#### Retention cleanup

Expired succeeded runs are removed by `BackupSucceededRunRetentionCleaner` (strategy-aware retention). There is **no** dedicated `BACKUP_DELETED` / `AuditEventType` for retention cleanup today — cleanup is operational retention, not a fiscal restore event. Do not invent a `BACKUP_DELETED` audit type in docs or clients until it exists in code.

---

### Operator checklist (post operator-led live restore)

After any **DBA/infrastructure** production recovery (outside this API):

1. Verify receipt / signature chain continuity and sequences.
2. Check FinanzOnline outbox idempotency before replaying submissions.
3. Confirm audit log continuity and tenant isolation.
4. Regenerate report PDFs from data if filesystem `report-pdfs/` were not restored.

See [`restore-boundary-notes.md`](restore-boundary-notes.md).
