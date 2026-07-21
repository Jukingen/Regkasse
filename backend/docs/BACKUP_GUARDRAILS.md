# Backup / restore guardrails

Short rules for `backend/Services/Backup/`. Prefer existing facades over parallel stacks.

## Strategies

| Strategy | Who | Artifact | Default retention |
|----------|-----|----------|-------------------|
| `Tenant` | Mandanten-Admin (`Manager`) | `*.tenant.zip` (no Identity) | ~30d (7–90 clamp) |
| `System` | Super Admin | `pg_dump` + `*.system.zip` (Identity + all tenants) | ~90d |

- Facade: `IBackupService` / `BackupService` — enqueue only on the HTTP thread.
- Scheduled cron uses **System** strategy (`BackupScheduledEnqueueService`).

## Access

- Source of truth: `BackupRunAccessEvaluator` (+ `BackupRunAccessScope`).
- `BackupRun.Strategy` defaults to **Tenant** — System must be set explicitly (ACL fail-closed).
- Mandanten-Admin: **Tenant strategy + own `tenant_id` only** (legacy `manual-tenant-{id}` / `import-tenant-{id}` keys allowed).
- System / `manual-all-tenants` / scheduled shared dumps → Super Admin only.
- Cross-tenant run ids → **404**, not 403.

`BackupService.ListBackupsAsync` uses `ApplyTenantScopeFilter` for non–Super Admin callers.

## Restore (validation-only)

- `IRestoreService` rejects `validationOnly: false` (`PRODUCTION_RESTORE_NOT_SUPPORTED`).
- `BackupService.RestoreBackupAsync` always queues `ValidationOnly = true` into `restore_validation_*`.
- Tenant ZIP → `TENANT_PACKAGE_RESTORE_NOT_SUPPORTED` (`ComplianceCheckService`); not `pg_restore` input.
- Automated production restore remains unavailable (`DeferredRestoreOrchestrationBoundary`).
- Detail: `docs/restore-boundary-notes.md`.

## Retention / cleanup

- Flat: Tenant vs System cutoffs (`BackupSucceededRunRetentionCleaner`).
- Smart GFS (`SmartRetentionEnabled`): **per strategy** — Tenant and System pools are thinned independently so weekly/monthly keepers do not compete across strategies.
- Daily ops: `AutomaticCleanupService` (retention delete + optional Hot/Warm/Cold retag + `BACKUP_AUTO_DELETED` audit).

## Tests

```bash
dotnet test --filter "FullyQualifiedName~Backup"
```

Also useful: `RestoreService*`, `AdminBackup*TenantScoping*`, `SmartRetention*`.
