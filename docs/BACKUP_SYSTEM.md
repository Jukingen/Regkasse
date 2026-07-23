# Backup & Disaster Recovery System

**Audience:** Operators, Mandanten-Admins, Super Admins, backend/FA maintainers.  
**Status:** Implemented (2026-07).  
**Always-applied rules:** [`AGENTS.md`](../AGENTS.md) § Backup & Disaster Recovery.  
**Hub (short index):** [`BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md).

---

## Overview

The Backup & Disaster Recovery system protects tenant and platform data with **RKSV-oriented restore guards**:

- Two product strategies: **Tenant** vs **System** (`BackupStrategyKind`).
- Role-aware Admin UI (`/backup` → `TenantBackupView` / `SystemBackupView`).
- Validation-only restore into isolated databases — **never** production via API.
- Full audit trail for backup trigger, download, import, and restore steps.

Related docs:

| Doc | Focus |
|-----|--------|
| [`BACKUP_PERMISSIONS.md`](BACKUP_PERMISSIONS.md) | RBAC, API auth, tenant scoping |
| [`BACKUP_CONTENT_POLICY.md`](BACKUP_CONTENT_POLICY.md) | What is in each package, cost knobs |
| [`restore-boundary-notes.md`](restore-boundary-notes.md) | No production restore, same-tenant gate |
| [`RKSV_COMPLIANCE.md`](RKSV_COMPLIANCE.md) | Restore RKSV rules + audit trail mapping |
| [`backup-phase1-runbook.md`](backup-phase1-runbook.md) | Orchestration / worker |
| [`backup-phase2-runbook.md`](backup-phase2-runbook.md) | `pg_dump` System dumps |
| [`restore-verification-drill-runbook.md`](restore-verification-drill-runbook.md) | Restore drills |

---

## Backup Types

### Tenant Backup

| | |
|--|--|
| **Strategy** | `BackupStrategyKind.Tenant` |
| **Frequency** | **On demand** — Mandanten-Admin (`Manager`) manual trigger (`backup.manage` + JWT tenant context). Facade: `IBackupService.CreateTenantBackupAsync`. *Not* the daily cron product path. |
| **Scope** | Single tenant (`backup_runs.tenant_id` set) |
| **Content** | Payments, receipts, products, customers, vouchers, cash registers, fiscal/RKSV rows, invoice/report **metadata**, tenant-scoped audit. **No** AspNet Identity. PDF bytes under `report-pdfs/` are **not** inside the package. |
| **Artifact** | `*.tenant.zip` (tenant-filtered JSON tables) |
| **Retention** | **30** days default (admin API / FA clamp **7–90**) |
| **Size (estimate)** | Roughly **~100–500 MB** per tenant (volume-dependent; ZIP-compressed) |
| **Access** | Mandanten-Admin: own tenant list / download / import only. Super Admin can see all. |
| **Restore** | Tenant ZIP is **not** `pg_restore`-compatible. Mandanten-Admin **cannot** restore via API. |

### System Backup

| | |
|--|--|
| **Strategy** | `BackupStrategyKind.System` |
| **Frequency** | **Daily automatic at 02:00 UTC** by default (`scheduleCron: "0 2 * * *"`, `BackupScheduledEnqueueService`). Super Admin can also trigger manually (`CreateSystemBackupAsync`). FA schedule planner may set weekly/monthly cron — there is **no** separate hard-coded “weekly full” product beyond configurable cron. |
| **Scope** | All active tenants + platform (`tenant_id` null) |
| **Content** | All tenant business/fiscal data + **Identity users** + platform settings + deployment licenses + full audit. System ZIP nests active tenant packages. |
| **Artifact** | `pg_dump -Fc` (PostgreSQL custom format, zlib **`-Z6`**) **+** `*.system.zip` |
| **Retention** | **90** days default |
| **Size (estimate)** | Roughly **~1–5 GB** per instance dump (deployment-dependent) |
| **Access** | **Super Admin only** for list/download of System rows. Mandanten-Admin never sees System dumps (Identity / all-tenants risk). |
| **Recovery capability** | Validation restore / restore drills use System `pg_dump` into isolated `restore_validation_*` DB. Live production recovery is **operator/DBA-led** outside the API. |

---

## Architecture (control plane)

```text
FA / Admin API
  └─ enqueue only (no pg_dump / ZIP build on HTTP thread)
       └─ BackupOrchestratorHostedService (worker)
            ├─ Tenant → TenantScopedLogicalBackupExecutionAdapter → *.tenant.zip
            └─ System  → CompositeSystemBackupExecutionAdapter
                           ├─ pg_dump -Fc -Z6
                           └─ *.system.zip (GlobalsDump)
```

- Metadata: `backup_runs` (`strategy`, `tenant_id`, status, timestamps), `backup_artifacts`, `backup_verifications`.
- Access filter: `BackupRunAccessEvaluator` — Manager = Tenant strategy + own `tenant_id` only.
- Storage budget: enqueue rejected when succeeded dump sum exceeds ~**10 GB** (`BackupService.MaxStorageBytes`).

---

## Restore Process

> **Important:** API restore is **validation-only**. It does **not** write to the production database. Dual Super Admin approval where required. Mandanten-Admin has **no** restore permission.

### Validation restore (Super Admin)

Typical FA flow (`RestoreModal` + `/api/admin/restore/*`):

1. Select a **System** backup (logical dump) from the list.
2. Confirm tenant / scope match when `backup_runs.tenant_id` and ambient tenant are both set (same-tenant RKSV gate via `IRestoreService`).
3. Acknowledge RKSV / validation-only compliance (dual acknowledgements in UI).
4. Request restore → second Super Admin approval → execute into isolated `restore_validation_*` database.
5. Audit log recorded (`AuditEventType.Restore*`, correlation id, who / when / what, source backup tenant, restore scope).

Cross-tenant mismatch → **HTTP 404**. Tenant ZIP selected for restore → rejected (`TENANT_PACKAGE_RESTORE_NOT_SUPPORTED`).

### Mandanten-Admin (Tenant) — what they can do

1. Trigger / list / download **own Tenant** packages.
2. Import a previously exported dump for registration (no automatic DB restore).
3. **Cannot** execute restore or restore drills.

### System / instance recovery (outside API)

1. Super Admin / DBA selects System `pg_dump` artifact from staging / archive.
2. Follow infrastructure runbook for live PostgreSQL recovery (not automated by this API).
3. Run post-restore fiscal checks (chain continuity, sequences, FinanzOnline outbox, audit) — see restore-boundary notes.
4. Record operational change control / audit outside or alongside platform audit as required.

### Restore drills (Super Admin)

Separate surface: `/api/admin/restore-verification/*` — inspect / optional isolated `pg_restore` / fiscal SQL / smoke. See [`restore-verification-drill-runbook.md`](restore-verification-drill-runbook.md).

---

## RKSV Compliance

| Rule | Status |
|------|--------|
| Same tenant only (no cross-tenant restore via API) | Yes — ambient vs `backup_runs.tenant_id` gate; else 404 |
| Original fiscal timestamps preserved | Yes — no rewrite of `IssuedAt` / receipt times |
| Restore workflow timestamp recorded | Yes — request / approve / complete UTC fields + audit |
| Full audit trail (who, when, what) | Yes — `Restore*` + backup trigger/download/import audits |
| No production restore via API | Yes — validation-only isolated DB |
| Dual Super Admin approval | Yes — where restore workflow requires it |

---

## Cost Optimization

| Measure | Implementation |
|---------|----------------|
| Compressed backups | System: `pg_dump -Fc -Z6` (zlib in custom format). Tenant: ZIP (deflate). Not a separate `.gz` sidecar file. |
| Configurable retention | Tenant default 30 / System default 90; admin settings clamp **7–90** days |
| Smart retention (opt-in) | `Backup:SmartRetentionEnabled` — GFS 7/4/12/7 via `SmartRetentionService` |
| Storage tiers (opt-in) | `Backup:StorageTierManagementEnabled` — Hot≤7 / Warm≤30 / Cold via `StorageTierService` |
| Storage alerts at 80% | `Backup:StagingDiskUsageAlertPercent` + `StorageAlertService` (budget + staging disk, default every 6h) |
| Failed backup email | German ops mail via `EmailBackupAlertPublisher` → `IBackupFailureEmailAlertService`; recipients: `Backup:FailureAlertEmailRecipients` (e.g. `admin@regkasse.at`) |
| Artifact encryption | Opt-in AES-256-GCM (`Backup:EncryptionEnabled` + `EncryptionKeyBase64`); wired after dump/ZIP write; isolated restore decrypts temp |
| Automated cleanup | `BackupSucceededRunRetentionCleaner` removes expired succeeded artifacts after succeeded runs |
| Enqueue budget | ~10 GB summed succeeded dumps — further enqueue rejected when exceeded |

Detail: [`BACKUP_CONTENT_POLICY.md`](BACKUP_CONTENT_POLICY.md).

---

## Permissions (quick)

| Actor | View | Trigger | Download | Restore |
|-------|------|---------|----------|---------|
| Mandanten-Admin (`Manager`) | Own Tenant | Tenant (`backup.manage`) | Own Tenant packages | No |
| Super Admin | All + System | System + platform | System + all (gated) | Validation + drills |

Keys: `settings.view` (read), `backup.manage` (trigger/schedule/tenant download), `settings.manage` (execution mode; implies `backup.manage`).

---

## Frontend Admin

| Path | Purpose |
|------|---------|
| `/backup` | Role-aware overview (`TenantBackupView` / `SystemBackupView`) |
| `/backup/dashboard` | DR operator dashboard |
| `/backup/runs` | Run list / metrics |
| `/backup/configuration` | Schedule + platform execution mode |
| `/backup/audit` | Activity + audit |
| `/backup/costs` | Indicative storage cost dashboard |
| `/backup/compliance` | RKSV product-gate / recoverability readiness |
| `/backup/performance` | Backup performance metrics (when enabled) |

Legacy aliases `/settings/backup-dr` and `/admin/backup` redirect to `/backup`.

---

## Configuration (defaults)

```yaml
enabled: true
scheduleCron: "0 2 * * *"   # Daily 02:00 UTC — System strategy
retentionDays: 30           # Tenant default; System policy default 90
executionMode: "PgDump"     # Fake | PgDump | ProductionStub
PgDumpCompressionLevel: 6
StagingDiskUsageAlertPercent: 80
# SmartRetentionEnabled: false       # optional GFS 7/4/12/7
# StorageTierManagementEnabled: false  # Hot/Warm/Cold tags
# AutomaticCleanupEnabled: false     # retention delete + BACKUP_AUTO_DELETED audit
```

---

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Backup fails / stays `Failed` | Check staging disk space and `ArtifactStagingRoot`; review worker logs for `pg_dump` / ZIP exporter errors; confirm `ExecutionAdapterKind` and connection string for System dumps. |
| Backup stays `Queued` | Ensure `Backup:WorkerEnabled` is true and orchestrator is running. |
| Restore request fails | Use a **System** `pg_dump` artifact (not Tenant ZIP). Confirm Super Admin role, dual approval, and same-tenant gate. Validate artifact on disk / hash. |
| Restore blocked / 404 | Cross-tenant ambient vs labeled run, or Manager attempting System/restore — expected. |
| Storage full / staging alert | Reduce retention (within 7–90), delete/archive old succeeded artifacts, free disk under `ArtifactStagingRoot`. |
| Manager cannot see scheduled dump | Expected — scheduled runs are **System**; Managers only see own Tenant packages. |
| Download URL null | Artifact missing on disk (`Fake` / moved archive) or permission (`settings.manage` / `backup.manage` rules). |
| Enqueue rejected (storage budget) | Sum of succeeded dumps exceeded ~10 GB — clean up or raise operational capacity after review. |

---

## Key code

| Area | Path |
|------|------|
| Strategy | `backend/Models/Backup/BackupStrategyKind.cs`, `BackupStrategyPolicy.cs` |
| Facade | `backend/Services/Backup/IBackupService.cs`, `BackupService.cs` |
| Access | `backend/Services/Backup/BackupRunAccessEvaluator.cs` |
| Tenant export | `TenantScopedLogicalBackupExecutionAdapter`, `TenantScopedBackupExporter` |
| System export | `CompositeSystemBackupExecutionAdapter`, `SystemScopedBackupExporter` |
| API | `AdminBackupController`, `AdminRestoreController` |
| FA hub | `frontend-admin/src/app/(protected)/backup/page.tsx` |
