# 04 — Tenancy, Authentication, Authorization and Security

> Part 4 of the Regkasse AI briefing. Index: [`../AI_PROJECT_BRIEFING.md`](../AI_PROJECT_BRIEFING.md).
> Verified at commit `e2be4d5b` (2026-11-24). **High-risk area** (tenant isolation, auth, RBAC).

## 1. Tenant model

- A tenant ("Mandant") is a customer company. Table `tenants` (root, not filtered). External key: `tenants.slug` (for example `dev`).
- Tenant-scoped entities implement `ITenantEntity` (`backend/Models/ITenantEntity.cs`) and carry `tenant_id uuid NOT NULL` (indexed, FK to `tenants.id`). About 84 model files implement it.
- `AppDbContext.CreateTenantQueryFilter<TEntity>()` registers a global query filter: `TenantId == ICurrentTenantAccessor.TenantId` when an ambient tenant is set. With no ambient tenant the filter is effectively off, which is intentional only for specific cross-tenant paths.
- **Cross-tenant access returns HTTP 404, never 403.** This keeps IDs from leaking existence.
- `IgnoreQueryFilters()` is allowed only on deliberate Super Admin or migration paths.
- Not tenant-scoped (examples): `tenants`, Identity tables, `auth_sessions`, `refresh_tokens`, `activated_licenses` (deployment-local, filtered by machine fingerprint), `rksv_runtime_config` (one overlay per deployment), FinanzOnline outbox rows (string tenant id).

## 2. Tenant resolution

| Environment | Order (first match wins) |
|---|---|
| Development | `X-Tenant-Id` header (slug) → `?tenant=` query → Host slug → default `dev` when Host is loopback or `admin` |
| Production, shared hosts (`api`, `pos`, `admin`, `www`) | JWT `tenant_id` after authentication. Pre-auth ambient stays unset; header and query are ignored |
| Production, mandant subdomain or verified custom domain | Host slug before auth, then JWT `tenant_id` rebind after auth |

- Reserved labels: `pos`, `api`, `admin`, `www` (cannot be tenant slugs or license key slugs).
- In Production, a `?tenant=` value is ignored and logged as a Warning (`TenantResolutionMiddleware`).
- Custom website domains: verified `TenantDomain` rows map Host → tenant slug (`ITenantDomainService`). FA `/settings/website`.
- Pipeline: `TenantResolutionMiddleware` (host/header/query) → authentication → `TenantContextMiddleware` (JWT `tenant_id` sets the accessor) → `TenantValidationMiddleware` (404 when a tenant is required but missing). Do not reorder.
- JWT versus host match is **not** strictly enforced in production yet (known gap, less critical on reserved hosts). Impersonation tokens skip the host match (`Auth:RequireTenantHostMatch`).

### Background services

Startup and hosted services have no HTTP request and no ambient tenant. Singletons that need EF must create a scope first:

```csharp
using var scope = _scopeFactory.CreateScope();
var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
await using var db = await factory.CreateDbContextAsync(ct);
```

Resolving `IDbContextFactory<AppDbContext>` from the root provider throws "Cannot resolve scoped service 'ICurrentTenantAccessor' from root provider". `LicenseService` is the reference implementation.

## 3. Super Admin and impersonation

- Role `SuperAdmin` (permission `system.critical`). Works on `admin.regkasse.at`.
- **Platform prefixes that SuperAdmin may call without an ambient tenant** (`TenantValidationMiddleware`, segment-safe): `/api/admin/tenants`, `/api/admin/billing`, `/api/admin/cache`, `/api/admin/support`, `/api/admin/trials`, `/api/admin/sessions`, `/api/admin/limits`, `/api/admin/countries`, `/api/admin/rksv/config` (exact), `/api/admin/backup`, `/api/admin/restore-verification`, `/api/admin/restore`, `/api/settings/backup`, exact `/api/tenants/switcher`, exact `/api/admin/jahresbeleg-reminder/status`. Non-SuperAdmin users on those URLs still need an ambient tenant (404 otherwise). Mandant data APIs (`/api/admin/products`, POS, ...) always need an ambient tenant, even for SuperAdmin.
- **Route/body tenant target:** Super Admin operations on a specific mandant must validate that the tenant exists (404 if not) and use the explicit `tenantId`, not the ambient JWT tenant.
- **Impersonation:** `POST /api/admin/tenants/{id}/impersonate` issues a JWT with the target `tenant_id` and `tenant_impersonation=true`. EF filters then bind to the target. Cannot impersonate deleted, suspended, or inactive tenants. `impersonated_by` is not yet written to `AuditLog`; server logs record the actor and tenant.
- Tenant hard-delete is disabled in production. Use soft-delete (archive).
- There is no separate cross-tenant SaaS metrics API.

## 4. Authentication

- **Login:** `POST /api/Auth/login` with `loginIdentifier` (email **or** username, case-insensitive), `password`, and `clientApp` (`pos` or `admin`). The legacy `email` field is still accepted. Lookup: `IdentityLoginLookup.FindByLoginIdentifierAsync` (email first, then `NormalizedUserName`).
- **Username rules:** 3-50 chars, `a-z 0-9 _ -`, case-insensitive uniqueness. Auto-generated as `{rolePrefix}{n}` (for example `manager1`, `cashier2`).
- **Password:** min 8 chars, generated at user creation and shown once. No invitation emails (removed 2026-05-22). First-login password change is enforced when configured (`MustChangePasswordMiddleware`).
- **JWT:** 24 h expiry, refresh-token rotation, `sst` (security stamp) claim checked on each request, `sid` session id. Username change must write `AuditEventType.UserNameChanged`, invalidate all sessions (rotate `SecurityStamp`), and check uniqueness.
- **Cookie split:** FA uses HttpOnly cookies `rk_admin_access_token` / `rk_admin_refresh_token` (plus legacy `access_token`). POS uses Bearer tokens from SecureStore on native; web POS may use `rk_pos_*` cookies. Token resolution is **Bearer first**, then cookies. Details: `docs/AUTH_LOGOUT.md`.
- **Logout:** `POST /api/Auth/logout` (current app's sessions), `logout-all`, `revoke`. Super Admin force-logout rotates the Identity `SecurityStamp` and revokes refresh tokens; the next API call fails in `OnTokenValidated`.
- **SuperAdmin 2FA:** TOTP after password in Production (`requires2FA` → `POST /api/Auth/verify-2fa`). Development bypass via `TwoFactorAuth:BypassInDevelopment`. Config `TwoFactorAuth`. Doc: `docs/AUTH_TWO_FACTOR.md`.
- **CSRF:** `Security:Csrf` (`Enabled`, `BypassInDevelopment`). Double-submit: `X-XSRF-TOKEN` header must match the `XSRF-TOKEN` cookie plus a server cache entry. Applies to POST/PUT/PATCH/DELETE. Exempt: login, refresh, health, swagger, metrics, `/api/webhooks/*`, `/api/csrf/token`. Development: off. Production: on.
- **Sessions:** auto-logout after 30 min inactivity (client idle timeout plus `SessionActivityMiddleware`). Super Admin and Manager can list and terminate sessions via `/api/admin/sessions` (Manager: own tenant only; the caller's current session cannot be terminated).

## 5. Roles and permissions

Canonical roles (`backend/Authorization/Roles.cs`; the `Admin` role was removed after a migration, `SuperAdmin` is the single top admin):

| Role | UI label (de) | Scope |
|---|---|---|
| `SuperAdmin` | Super-Administrator | Platform-wide |
| `Manager` | **Mandanten-Admin** | Own tenant administrator |
| `Cashier` | Kassierer | POS |
| `Waiter` | Kellner | Order taking |
| `Kitchen` | Küche | Kitchen display |
| `Accountant` | Buchhaltung | Reporting, audit, invoices (read/export), FinanzOnline, RKSV (no Schlussbeleg) |
| `ReportViewer` | Berichte (nur Lesen) | Read-only reports |
| `ComplianceOfficer` | (platform) | Production deployment sign-off (RKSV gate). Not POS |

- Source of truth: `backend/Authorization/RolePermissionMatrix.cs` and `AppPermissions.cs` (about 168 permission constants, dot-separated keys such as `user.manage`, `product.view`, `payment.take`, `cash_register.decommission`, `voucher.create`, `report.export`, `audit.view`, `backup.manage`, `settings.view`, `settings.manage`, `license.manage`, `finanzonline.submit`, `digital.orders.manage`, `system.critical`). Do not invent keys; add one in `AppPermissions`, the matrix, `PermissionCatalog`, and the FA registry together.
- Endpoints are protected with `[HasPermission(...)]` and/or role requirements. App-context policies (`RequireAppContext`, `ClientAppPolicy`) separate POS and admin tokens.
- Per-user permissions are cached (`user_permissions_{userId}`, 30 min) and invalidated on role changes. Temporary permissions, permission packages and requests exist (`AdminPermission*` controllers).
- FA mirrors permissions in `frontend-admin/src/shared/auth/*` and menu mapping. CI scripts `verify-permission-keys.mjs` and `verify-menu-permissions.mjs` keep backend and FA keys aligned. Documentation: `docs/PERMISSIONS_MATRIX.md`, `docs/PERMISSIONS_MENU_MAPPING.md`.
- Spot-check expectations: Manager → RKSV, cash registers, backup read OK; `/api/admin/users` and `/api/admin/tenants` → 403. Cashier → POS status OK; `/api/admin/*` → 403. A POS cashier password reset needs the `UserManage` permission (the onboarding doc writes it as `users.manage`; the code constant is `user.manage`). The onboarding doc says Manager does not have it for this case and SuperAdmin should be used. Verify in `RolePermissionMatrix` before relying on it.

## 6. Licensing, trials, limits

- **Unified license key:** mandant `REGK-{validUntil:yyyyMMdd}-{tenantSlug}-{8 alnum}`; deployment keys use slug `system`; the legacy `REGK-XXXXX-XXXXX-XXXXX` display format is still accepted. Activate with `POST /api/license/activate` (POS and FA); status `GET /api/license/status`; cache key `license_status_{tenantId}` (5 min). Do not invent a second key family.
- Mandanten-Admin can extend a license with a key from Super Admin. License sales are independent of RKSV and logged in `billing_audit_log`.
- **Trials:** `Trial:*` config (14-day default; allowed durations 14/30/60/90; grace 7; max 1 register and 3 users in trial). Statuses `active` → `expired` (grace) → soft `deleted` or `converted`. `ITrialService`, `/api/admin/trials`, FA `/admin/trials`. `TrialLimitGuard` is separate from tenant limits.
- **Tenant limits** (`tenant_limits`, one row per tenant; defaults when missing): `maxActiveRegistersPerUser` 5, `maxProductsPerTenant` 10000, `maxUsersPerTenant` 50, `dailyMaxTransactions` 1000, `maxTransactionAmount` 10000 EUR, `dailyMaxRevenue` 50000 EUR, `maxBackupsPerTenant` 50, `maxBackupSizeMB` 500, `maxOfflineTransactions` 50. A violation returns HTTP 409 with `LimitErrorDto` (`LIMIT_EXCEEDED`). Only the register-assignment limit has a SuperAdmin `force=true` override. Doc: `docs/TENANT_LIMITS.md`.
- **Self-service portal** ("Mein Konto", permission `license.manage`): FA `/tenant/portal`, `/tenant/license`, `/tenant/invoices`, `/tenant/profile`, `/tenant/support`; Super Admin inbox `/admin/support`.

## 7. Backup and restore (security boundary)

- **Tenant strategy:** triggered by Manager (`backup.manage` + JWT tenant). Tenant data only (no Identity, no credentials). Artifact `*.tenant.zip`, retention 30 days (7-90). Not `pg_restore` compatible.
- **System strategy:** daily at 02:00 UTC (default) and Super Admin manual. `pg_dump -Fc` plus `*.system.zip` including Identity. Retention 90 days.
- A Manager never sees System dumps. Cross-tenant restore is prohibited (404). **No production restore through the API.** Restore is validation-only into an isolated `restore_validation_*` database with dual Super Admin approval. Restore workflow timestamps are recorded and original fiscal timestamps are never backdated.
- Permissions: `settings.view`, `backup.manage`, `settings.manage` (implies `backup.manage`; Manager must not receive it). Docs: `docs/BACKUP_AND_DISASTER_RECOVERY.md`, `docs/BACKUP_PERMISSIONS.md`, `docs/restore-boundary-notes.md`.

## 8. Security rules (summary)

- Validate all input on the backend. Client validation is UX only.
- Never log passwords, voucher codes, TSE secrets, or full card numbers (mask as `**** **** **** 1234`). Encrypt production secrets. Do not commit secrets (`scan-secrets.mjs` runs in CI and Husky).
- Denied access attempts to admin screens must be written to the audit log. Sensitive operations need audit events.
- Critical failures (TSE/RKSV, backup, restore, auth anomalies) produce notifications (SSE in-app; optional email and Slack/Discord/Teams webhooks).
- Production must run with `ASPNETCORE_ENVIRONMENT=Production` so tenant headers cannot spoof tenants.
- `AppDbContext` has one runtime constructor with `[ActivatorUtilitiesConstructor]` and a design-time constructor. Do not add more.
