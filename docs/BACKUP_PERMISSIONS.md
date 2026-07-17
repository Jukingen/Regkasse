# Backup permissions & tenant scoping (Admin)

**Audience:** Backend/FA maintainers, QA, tenant operators.  
**Status:** Implemented (2026-07).  
**Hub:** [`BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md)  
**Source of truth (code):** `AppPermissions.BackupManage`, `RolePermissionMatrix`, `AdminBackupController`, `BackupRunAccessEvaluator`, `PermissionImplication`, FA `useBackupPermissions` / role-aware `/backup` hub.

---

## Strategy: Tenant vs System (2026-07)

| | **Tenant** (Mandanten-Admin) | **System** (Super Admin) |
|--|------------------------------|---------------------------|
| Access label | `tenant_id` set; `strategy=Tenant` | `tenant_id` null; `strategy=System` |
| Default retention | **30** days | **90** days |
| Artifact | `*.tenant.zip` (tenant JSON tables) | `pg_dump` + `*.system.zip` |
| Identity tables | **Not included** | **Included** (in system ZIP + dump) |
| Facade | `IBackupService.CreateTenantBackupAsync` | `IBackupService.CreateSystemBackupAsync` |
| Scheduled cron | — | System strategy (daily deployment dump) |
| List / download for Manager | Own tenant only | **Never** |

**Important:** Tenant strategy exports **tenant-filtered** business/fiscal JSON (worker). System strategy remains a full PostgreSQL dump plus system ZIP. Tenant ZIPs are not `pg_restore`-compatible; validation restore stays on System dumps. See `docs/BACKUP_CONTENT_POLICY.md`.

---

## Summary

Mandanten-Admins (`Manager` role) can **view** backup status and **manage** tenant-scoped backup operations (manual trigger + schedule/retention) via a **narrow** permission `backup.manage`. They do **not** receive the broad `settings.manage` permission (which also gates license, NTP, execution mode, artifact download for platform paths, etc.).

Super Admin retains full platform backup control via `settings.manage` (which **implies** `backup.manage`) and `system.critical`.

There is **no** separate `backup.view` or `backup.execute` permission in the catalog.

**Restore:** Mandanten-Admin does **not** restore. Super Admin only — validation-only isolated DB, dual approval. See `docs/restore-boundary-notes.md`.

---

## Permission keys

| Key | Constant | Purpose |
|-----|----------|---------|
| `settings.view` | `AppPermissions.SettingsView` | Read backup status, history, configuration health (route gate). |
| `backup.manage` | `AppPermissions.BackupManage` | Tenant-scoped: enqueue manual backup, edit automation schedule (cron, retention, enabled), download/import own Tenant packages. |
| `settings.manage` | `AppPermissions.SettingsManage` | Platform-wide: execution mode, deployment paths; **implies** `backup.manage`. |
| `system.critical` | `AppPermissions.SystemCritical` | Super Admin; FA `usePermissions()` treats as satisfying all checks. |

**Implication (backend + FA mirror):**

```text
settings.manage → settings.backup, backup.manage
```

**Escalation guard:** Holding only `backup.manage` or only `settings.backup` must **not** satisfy a policy for `settings.manage`.

---

## Default role matrix

| Role | `settings.view` | `backup.manage` | `settings.manage` | Restore |
|------|-----------------|-----------------|-------------------|---------|
| **Mandanten-Admin (`Manager`)** | Yes | **Yes** (default) | No | No |
| **SuperAdmin** | Yes | Yes (via implication) | Yes | Validation + drills |
| **Cashier** | No | No | No | No |

Canonical list: `backend/Authorization/RolePermissionMatrix.cs`.

---

## API authorization

| Method | Path | Permission | Notes |
|--------|------|------------|-------|
| `POST` | `/api/admin/backup/trigger` | `backup.manage` | Enqueue manual run; no `pg_dump` on HTTP thread. Strategy from role (Tenant vs System). |
| `PUT` | `/api/admin/backup/settings` | `backup.manage` | Schedule / retention / enabled. |
| `POST` | `/api/settings/backup/now` | `backup.manage` | Legacy enqueue (same orchestration). |
| `GET` | `/api/admin/backup/*` (reads) | `settings.view` | Status, runs, health, etc. |
| `GET` | `/api/admin/backup/runs/{runId}/artifacts/{artifactId}/download` | `backup.manage` | Tenant-strategy runs for the caller's `tenant_id` only (404 cross-tenant / System). **System** dumps are Super Admin only. |
| `GET` | `/api/admin/backup/list` | `settings.view` | Super Admin: all succeeded dumps. Mandanten-Admin: `strategy=Tenant` + own `tenant_id` only. |
| `POST` | `/api/admin/backup/artifacts/import` | `backup.manage` | Register uploaded dump (+ optional manifest) for current tenant; **no automatic DB restore**. |
| `PUT` | `/api/admin/backup/execution-mode` | `settings.manage` | Super Admin / platform operator only. |
| Restore / restore-drill | `/api/admin/restore/*`, restore-verification | Super Admin (`system.critical` / role) | Validation-only; not granted to Manager. |

Controller: `AdminBackupController`, `SettingsController` (legacy), `AdminRestoreController`.

---

## Tenant scoping (Manager)

**Access control (who may trigger):**

1. JWT must include resolved **tenant context** (`tenant_id` claim → `ICurrentTenantAccessor`).
2. `POST .../trigger` rejects non–Super Admin callers without tenant context: `400` `TENANT_CONTEXT_REQUIRED`.
3. The trigger body does **not** accept a target `tenantId` from the client — cross-tenant selection is impossible by construction.
4. Duplicate-active-manual suppression is **per manual trigger scope** (tenant-bound vs deployment-wide), not deployment-global — two Managers in different tenants may each queue a manual run while the other tenant's run is active.

**Data plane (what is backed up):**

- `backup_runs` carries optional `tenant_id` (nullable) and `strategy` (`Tenant` / `System`).
- Legacy rows are backfilled from `tenant_id` / `idempotency_key` where parseable.
- Mandanten-Admin manual trigger enqueues **Tenant** strategy (tenant JSON ZIP; no Identity).
- **Scheduled** cron and Super Admin **System** runs remain deployment-wide (`strategy=System`, `tenant_id` NULL) and are **not** listed or downloadable by Managers.

**Scoped read endpoints (Manager, non–Super Admin):** `GET /runs`, `GET /runs/{id}`, `GET /list`, `GET /status/latest`, `GET /runs/{id}/verification-report`, `GET /verification/latest`, `GET /dashboard/stats`, and `GET /recoverability-summary` require tenant context (`400` `TENANT_CONTEXT_REQUIRED` without it) and filter via `BackupRunAccessEvaluator` to **Tenant strategy + own tenant** — cross-tenant / System run IDs return **404**, not 403. Super Admin without tenant context sees deployment-wide data. Restore-drill rows in dashboard/recoverability remain deployment-wide (shared infra).

---

## Frontend Admin (FA)

### Routes (`routePermissions.ts`)

Canonical App Router paths (2026-07 navigation IA):

| Path | Route guard | Purpose |
|------|-------------|---------|
| `/backup` | `settings.view` | Role-aware overview hub (`TenantBackupView` / `SystemBackupView`) |
| `/backup/dashboard` | `settings.view` | DR overview / operator dashboard |
| `/backup/runs` | `settings.view` | Metrics, run list, manual trigger |
| `/backup/configuration` | `settings.view` | Schedule + execution mode (component-gated) |
| `/backup/configuration/schedule` | `backup.manage` | Sidebar virtual key → schedule section |
| `/backup/configuration/platform` | `settings.manage` | Sidebar virtual key → execution mode |
| `/backup/audit` | `settings.view` | Activity + audit log |

**Legacy redirects** (still guarded, redirect to canonical paths):

| Legacy | Redirect |
|--------|----------|
| `/settings/backup-dr` | `/backup/*` (`tab` query mapped; `runId` preserved) |
| `/settings/backup` | `/backup/dashboard` |
| `/admin/backup` | `/backup/runs` |

Manage actions remain component-gated: `backup.manage` or `settings.manage` via `useBackupManagementAccess` / `useBackupPermissions`.

### Sidebar & secondary nav

- Sidebar group: **Backup & Disaster Recovery** (`grp-backup` in `adminSidebarRegistry.ts`)
- Horizontal tabs: `BackupSecondaryNav` on all `/backup/*` pages
- Backup routes are **not** listed under Einstellungen (`SettingsSecondaryNav`)

### UI capability split

| Capability | Permission check |
|------------|------------------|
| View hub / dashboard / runs | `settings.view` |
| Manual trigger + schedule | `canManageBackup` = `backup.manage` ∨ `settings.manage` |
| Artifact download + import | `canDownloadBackup` = `backup.manage` ∨ `settings.manage` (own Tenant packages for Manager) |
| Execution mode, deployment paths | `settings.manage` only |
| Restore / restore drill | Super Admin only |

**Key files:**

- `frontend-admin/src/shared/backupAreaRoutes.ts`
- `frontend-admin/src/app/(protected)/backup/page.tsx`
- `frontend-admin/src/features/backup/components/TenantBackupView.tsx`
- `frontend-admin/src/features/backup/components/SystemBackupView.tsx`
- `frontend-admin/src/features/backup/components/BackupSecondaryNav.tsx`
- `frontend-admin/src/features/backup/components/BackupPageShell.tsx`
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
| Read / verification tenant scope | `AdminBackupReadTenantScopingTests`, `BackupRunAccessEvaluatorTests` |
| List / strategy facade | `BackupServiceTests`, `BackupRunServiceTests` |
| Trigger → succeed → download (HTTP) | `BackupTriggerDownloadIntegrationTests` |
| FA fixtures | `adminAppPermissionFixtures.ts` (`MANAGER_ADMIN_PERMISSIONS` includes `BACKUP_MANAGE`) |

```bash
cd backend && dotnet test --filter "FullyQualifiedName~Backup_BackupManage|FullyQualifiedName~AdminBackupTriggerTenantScoping|FullyQualifiedName~RoleHasPermission_Manager|FullyQualifiedName~BackupRunAccessEvaluator|FullyQualifiedName~BackupServiceTests"
cd frontend-admin && npm run test -- --run src/shared/__tests__/fixtures/adminAppPermissionFixtures.ts 2>/dev/null || true
```

---

## Related runbooks

- Hub: [`BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md)
- Content / cost: [`BACKUP_CONTENT_POLICY.md`](BACKUP_CONTENT_POLICY.md)
- Phase 1 orchestration: [`backup-phase1-runbook.md`](backup-phase1-runbook.md)
- Phase 2 `pg_dump`: [`backup-phase2-runbook.md`](backup-phase2-runbook.md)
- AGENTS: [`AGENTS.md`](../AGENTS.md) § Backup & Disaster Recovery
- AI module (short): [`ai/modules/backup_permissions.md`](../ai/modules/backup_permissions.md)

---

## Migration notes for operators

- Existing Manager users receive `backup.manage` on **next login** (JWT rebuilt from `RolePermissionMatrix`). Force re-login if UI still shows read-only after deploy.
- Do **not** grant `settings.manage` to Manager to “fix” backup — use `backup.manage` only.
- Managers no longer see or download shared System / scheduled instance dumps (Identity / all tenants). Use Tenant packages only.
