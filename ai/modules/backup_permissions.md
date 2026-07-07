# Backup permissions (Manager / tenant scoping)

**Full reference:** [`docs/BACKUP_PERMISSIONS.md`](../../docs/BACKUP_PERMISSIONS.md)

## Quick rules for agents

- **Manager default:** `settings.view` + `backup.manage` in `RolePermissionMatrix` — **not** `settings.manage`.
- **Trigger + schedule:** `AppPermissions.BackupManage` (`backup.manage`) on `POST /api/admin/backup/trigger`, `PUT /api/admin/backup/settings`, legacy `POST /api/settings/backup/now`.
- **Platform ops:** execution mode → `settings.manage` only.
- **Download + import:** `backup.manage` + tenant context; cross-tenant artifact access → HTTP 404.
- **Audit:** backup trigger, download, import, and schedule changes log with explicit `tenant_id` (EF global filter scopes Manager audit reads).
- **Tenant scoping:** Non–Super Admin trigger requires `ICurrentTenantAccessor.TenantId`; no client `tenantId` on trigger body.
- **No** `backup.view` / `backup.execute` keys — view uses `settings.view`.
- **Implication:** `settings.manage` → `backup.manage`; reverse must **not** hold (escalation guard in `PermissionImplication`).
- **`backup_runs.tenant_id`:** Nullable column (2026-07 migration). Manual/import runs stamp tenant; scheduled / all-tenants runs stay `NULL`. Access uses `BackupRunAccessEvaluator` (column + idempotency legacy + shared deployment-wide scheduled runs).
- **Data plane:** Still one deployment-wide PostgreSQL dump per run — `tenant_id` gates **access**, not separate dump files per tenant (future worker/schema work).

## Key files

| Layer | Path |
|-------|------|
| Permission constant | `backend/Authorization/AppPermissions.cs` |
| Role defaults | `backend/Authorization/RolePermissionMatrix.cs` |
| Implication | `backend/Authorization/PermissionImplication.cs` |
| API | `backend/Controllers/AdminBackupController.cs` |
| FA hooks | `frontend-admin/src/features/backup/hooks/useBackupPermissions.ts` |
| FA routes | `frontend-admin/src/shared/auth/routePermissions.ts` |

## Do not

- Grant `settings.manage` to Manager for backup-only fixes.
- Add `settings.backup` as the **only** child of `settings.manage` (implication footgun).
- Assume per-tenant backup artifacts exist without `tenant_id` on `BackupRun`.
