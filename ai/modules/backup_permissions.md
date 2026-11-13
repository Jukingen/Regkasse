# Backup permissions (Manager / tenant scoping)

**Hub:** [`docs/BACKUP_AND_DISASTER_RECOVERY.md`](../../docs/BACKUP_AND_DISASTER_RECOVERY.md)  
**Full reference:** [`docs/BACKUP_PERMISSIONS.md`](../../docs/BACKUP_PERMISSIONS.md)

## Quick rules for agents

- **Strategies:** `BackupStrategyKind.Tenant` (Mandanten-Admin) vs `System` (Super Admin). See `BackupStrategyPolicy`, `IBackupService`.
- **Manager default:** `settings.view` + `backup.manage` in `RolePermissionMatrix` — **not** `settings.manage`.
- **Trigger + schedule:** `AppPermissions.BackupManage` (`backup.manage`) on `POST /api/admin/backup/trigger`, `PUT /api/admin/backup/settings`, legacy `POST /api/settings/backup/now`.
- **Platform ops:** execution mode → `settings.manage` only.
- **List:** `GET /api/admin/backup/list` — Manager: `strategy=Tenant` + own `tenant_id` only; Super Admin: all.
- **Download + import:** `backup.manage` + tenant context; Manager never accesses System / shared dumps (Identity + all tenants). Cross-tenant → HTTP 404.
- **Restore / restore-drill:** Super Admin only (validation-only isolated DB; dual approval). Managers do **not** restore.
- **Audit:** backup trigger, download, import, schedule, and restore steps log with explicit `tenant_id` / correlation id where applicable.
- **Tenant scoping:** Non–Super Admin trigger requires `ICurrentTenantAccessor.TenantId`; no client `tenantId` on trigger body.
- **No** `backup.view` / `backup.execute` keys — view uses `settings.view`.
- **Implication:** `settings.manage` → `backup.manage`; reverse must **not** hold (escalation guard in `PermissionImplication`).
- **`backup_runs`:** `tenant_id` (nullable) + `strategy`. Access: `BackupRunAccessEvaluator` (Tenant strategy for Managers; System = Super Admin only).
- **Data plane:** Tenant → `*.tenant.zip`; System → `pg_dump` + `*.system.zip`. Tenant ZIP is not `pg_restore`-compatible.

## Key files

| Layer | Path |
|-------|------|
| Permission constant | `backend/Authorization/AppPermissions.cs` |
| Role defaults | `backend/Authorization/RolePermissionMatrix.cs` |
| Implication | `backend/Authorization/PermissionImplication.cs` |
| Access filter | `backend/Services/Backup/BackupRunAccessEvaluator.cs` |
| Facade | `backend/Services/Backup/IBackupService.cs` |
| API | `backend/Controllers/AdminBackupController.cs` |
| FA hooks | `frontend-admin/src/features/backup/hooks/useBackupPermissions.ts` |
| FA hub | `frontend-admin/src/app/(protected)/backup/page.tsx` (`TenantBackupView` / `SystemBackupView`) |
| FA routes | `frontend-admin/src/shared/auth/routePermissions.ts` |

## Do not

- Grant `settings.manage` to Manager for backup-only fixes.
- Add `settings.backup` as the **only** child of `settings.manage` (implication footgun).
- Expose System dumps (Identity / all tenants) to Mandanten-Admin via list, download, or runs UI.
- Allow Manager restore or production restore via API.
- Assume Tenant ZIP can be fed to `pg_restore` / validation restore without a System dump.
