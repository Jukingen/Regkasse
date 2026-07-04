# Backup permissions & tenant scoping (Admin)

**Audience:** Backend/FA maintainers, QA, tenant operators.  
**Status:** Implemented (2026-07).  
**Source of truth (code):** `AppPermissions.BackupManage`, `RolePermissionMatrix`, `AdminBackupController`, `PermissionImplication`, FA `useBackupPermissions` / `BackupSettings`.

---

## Summary

Managers (tenant admins) can **view** backup status and **manage** tenant-scoped backup operations (manual trigger + schedule/retention) via a **narrow** permission `backup.manage`. They do **not** receive the broad `settings.manage` permission (which also gates license, NTP, execution mode, artifact download, etc.).

Super Admin retains full platform backup control via `settings.manage` (which **implies** `backup.manage`) and `system.critical`.

There is **no** separate `backup.view` or `backup.execute` permission in the catalog.

---

## Permission keys

| Key | Constant | Purpose |
|-----|----------|---------|
| `settings.view` | `AppPermissions.SettingsView` | Read backup status, history, configuration health (route gate). |
| `backup.manage` | `AppPermissions.BackupManage` | Tenant-scoped: enqueue manual backup, edit automation schedule (cron, retention, enabled). |
| `settings.manage` | `AppPermissions.SettingsManage` | Platform-wide: execution mode, artifact download, deployment paths; **implies** `backup.manage`. |
| `system.critical` | `AppPermissions.SystemCritical` | Super Admin; FA `usePermissions()` treats as satisfying all checks. |

**Implication (backend + FA mirror):**

```text
settings.manage → settings.backup, backup.manage
```

**Escalation guard:** Holding only `backup.manage` or only `settings.backup` must **not** satisfy a policy for `settings.manage`.

---

## Default role matrix

| Role | `settings.view` | `backup.manage` | `settings.manage` |
|------|-----------------|-----------------|-------------------|
| **Manager** | Yes | **Yes** (default) | No |
| **SuperAdmin** | Yes | Yes (via implication) | Yes |
| **Cashier** | No | No | No |

Canonical list: `backend/Authorization/RolePermissionMatrix.cs`.

---

## API authorization

| Method | Path | Permission | Notes |
|--------|------|------------|-------|
| `POST` | `/api/admin/backup/trigger` | `backup.manage` | Enqueue manual run; no `pg_dump` on HTTP thread. |
| `PUT` | `/api/admin/backup/settings` | `backup.manage` | Schedule / retention / enabled. |
| `POST` | `/api/settings/backup/now` | `backup.manage` | Legacy enqueue (same orchestration). |
| `GET` | `/api/admin/backup/*` (reads) | `settings.view` | Status, runs, health, etc. |
| `GET` | `/api/admin/backup/runs/{runId}/artifacts/{artifactId}/download` | `backup.manage` | Tenant-scoped: Manager may download own-tenant runs only (404 cross-tenant). Super Admin without tenant: deployment-wide. |
| `POST` | `/api/admin/backup/artifacts/import` | `backup.manage` | Register uploaded dump (+ optional manifest) for current tenant; **no automatic DB restore**. |
| `PUT` | `/api/admin/backup/execution-mode` | `settings.manage` | Super Admin / platform operator only. |
| Restore drill / restore approval | — | Super Admin (`system.critical` / role) | Not granted to Manager. |

Controller: `AdminBackupController`, `SettingsController` (legacy).

---

## Tenant scoping (Manager)

**Access control (who may trigger):**

1. JWT must include resolved **tenant context** (`tenant_id` claim → `ICurrentTenantAccessor`).
2. `POST .../trigger` rejects non–Super Admin callers without tenant context: `400` `TENANT_CONTEXT_REQUIRED`.
3. The trigger body does **not** accept a target `tenantId` from the client — cross-tenant selection is impossible by construction.

**Data plane (what is backed up):**

- `backup_runs` is currently **deployment-scoped** (no `tenant_id` on `BackupRun`; not `ITenantEntity`).
- A manual trigger enqueues one deployment-wide logical backup run today.
- Per-tenant **artifact** isolation is a future schema/orchestrator change (additive `tenant_id` on runs + worker filter).

Until per-tenant runs exist, “Manager only backs up their tenant” is enforced at **authorization + tenant binding**, not at separate dump files per tenant.

---

## Frontend Admin (FA)

### Routes (`routePermissions.ts`)

| Path | Route guard | Manage actions |
|------|-------------|----------------|
| `/settings/backup-dr` | `settings.view` | Component-gated: `backup.manage` or `settings.manage` |
| `/admin/backup` | `settings.view` | Same |

There is no `/settings/backup` or `/settings/backup/manage` route in the App Router.

### UI capability split

| Capability | Permission check |
|------------|------------------|
| View dashboard / runs | `settings.view` |
| Manual trigger + schedule | `canManageBackup` = `backup.manage` ∨ `settings.manage` |
| Artifact download + import | `canDownloadBackup` = `backup.manage` ∨ `settings.manage` (own tenant for Manager) |
| Execution mode, deployment paths | `settings.manage` only |
| Restore drill | Super Admin |

**Key files:**

- `frontend-admin/src/features/backup/hooks/useBackupPermissions.ts`
- `frontend-admin/src/features/backup/components/BackupSettings.tsx`
- `frontend-admin/src/features/backup-dr/components/BackupDrDashboard.tsx`
- `frontend-admin/src/shared/auth/permissions.ts` (`PERMISSIONS.BACKUP_MANAGE`)
- `frontend-admin/src/shared/auth/permissionImplication.ts`

**i18n:** Tenant-scoped denial copy references *„Backups verwalten“* (`backup.manage`), not *„Einstellungen verwalten“* (`settings.manage`). Platform-only surfaces still reference *„Einstellungen verwalten“*.

---

## Tests

| Area | File |
|------|------|
| Role matrix | `RolePermissionMatrixTests` |
| Policy / escalation | `EndpointAuthorizationRepresentativeTests` |
| Trigger tenant guard | `AdminBackupTriggerTenantScopingTests` |
| FA fixtures | `adminAppPermissionFixtures.ts` (`MANAGER_ADMIN_PERMISSIONS` includes `BACKUP_MANAGE`) |

```bash
cd backend && dotnet test --filter "FullyQualifiedName~Backup_BackupManage|FullyQualifiedName~AdminBackupTriggerTenantScoping|FullyQualifiedName~RoleHasPermission_Manager"
cd frontend-admin && npm run test -- --run src/shared/__tests__/fixtures/adminAppPermissionFixtures.ts 2>/dev/null || true
```

---

## Related runbooks

- Phase 1 orchestration: [`backup-phase1-runbook.md`](backup-phase1-runbook.md)
- Phase 2 `pg_dump`: [`backup-phase2-runbook.md`](backup-phase2-runbook.md)
- AGENTS backup policy table: [`AGENTS.md`](../AGENTS.md) § Backup & Restore Rules
- AI module (short): [`ai/modules/backup_permissions.md`](../ai/modules/backup_permissions.md)

---

## Migration notes for operators

- Existing Manager users receive `backup.manage` on **next login** (JWT rebuilt from `RolePermissionMatrix`). Force re-login if UI still shows read-only after deploy.
- Do **not** grant `settings.manage` to Manager to “fix” backup — use `backup.manage` only.
