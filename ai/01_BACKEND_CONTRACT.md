# Backend contract (ASP.NET Core)

## Multi-tenant architecture

- **Identity:** Production target is a **single POS UI** — `pos.regkasse.at` / `api.regkasse.at` / `admin.regkasse.at`. Tenant is JWT `tenant_id` (`docs/POS_PRODUCTION_ARCHITECTURE.md`). `{slug}.regkasse.at` is **not** the POS entry (it may be legacy, a transition host, or a customer-site Host). In Development: `X-Tenant-Id` / `?tenant=` (slug).
- **Middleware:** `TenantResolutionMiddleware` — Dev override → skip pre-auth bind for reserved `pos`/`api`/`admin`/`www` → otherwise Host (`SubdomainTenantProvider`, `TenantDomain` / custom site host) → `CurrentTenantService` → accessor; after auth, `TenantContextMiddleware` (JWT `tenant_id` is authoritative for POS/API).
- **Data:** Global query filter on `ITenantEntity` in `AppDbContext`; cross-tenant resource access returns **404**.
- **Super Admin:** `Roles.SuperAdmin`, `/api/admin/tenants`, impersonation `POST .../impersonate` — `AdminTenantsController`, `AdminTenantService`.
- **Tests:** `TenantIsolationTests`, `SubdomainTenantProviderTests`, `SettingsTenantResolverTests`.

**Security:** Cross-tenant IDOR → 404. No `X-Tenant-Id` in Production. Reserved hosts (`pos`/`api`/`admin`/`www`) are not slugs. Detail: `docs/MULTI_TENANT.md`, `docs/POS_PRODUCTION_ARCHITECTURE.md`.

**Migration:** Wave migration chain; `SystemTenantIds.Platform` default Guid — `ai/02_DATABASE_CONTRACT.md`, `docs/MULTI_TENANT.md`.

Detail: `docs/MULTI_TENANT.md`, `docs/POS_PRODUCTION_ARCHITECTURE.md`, `REGKASSE_AI_ONBOARDING.md`, `backend/README.md`.

### Scoped service resolution (singleton + DbContext)

- `AppDbContext` and `ICurrentTenantAccessor` are registered **scoped**.
- Singleton services (`LicenseService`) must use **`IServiceScopeFactory.CreateScope()`** for database work, then resolve `IDbContextFactory<AppDbContext>` or `AppDbContext` from the scope.
- `CreateDbContext()` on the root provider → `Cannot resolve scoped service 'ICurrentTenantAccessor' from root provider`.
- `AppDbContext`: design-time ctor (`options` only, migrations); runtime ctor `[ActivatorUtilitiesConstructor]` + `ICurrentTenantAccessor`.
- `OnConfiguring`: do not call the provider when options are already configured (`IsConfigured` guard).

### Tenant / startup (background)

- Without HTTP, the accessor `TenantId` may be null → `ITenantEntity` filters are off (intentional paths only).
- `activated_licenses` is not tenant-scoped; it is read by machine fingerprint.
- `LicenseService`: singleton snapshot + scoped DB; a startup DB failure falls back to trial and does not stop the host.
- **Billing mandant license:** `Services.Billing.TenantLicenseService` — `IDbContextFactory` (scoped factory pattern); two `ITenantLicenseService` registrations (AdminTenants vs Billing) require a DI alias. Audit: `IBillingAuditService`; reminders: `IReminderService` + `BillingReminderHostedService`. See `docs/BILLING_TENANT_LICENSE.md`, `ai/modules/billing_license.md`.

## API headers

- Production (POS/API): JWT `tenant_id` after login; reserved hosts `pos` / `api` / `admin` / `www` are not tenant slugs.
- Host slug / `TenantDomain`: optional legacy slug host or **customer websites** (`frontend-sites`) — not the POS production entry.
- Development: `X-Tenant-Id` / `?tenant=` (slug only; `IsDevelopment()`).
- `/api/admin/tenants`: `[Authorize(Roles = SuperAdmin)]`; global `tenants` table; impersonation for scoped business data.
- Public / sites (non-POS, non-admin storefront): `/api/public/*`, `/api/sites/*` — see `docs/DIGITAL_SERVICES.md`, `docs/WORKING_HOURS.md`.

## Deployment requirements

### DNS configuration

- `pos.regkasse.at`, `admin.regkasse.at`, `api.regkasse.at`; optional `*.regkasse.at` for legacy; preserve `Host` at the proxy. See `docs/POS_PRODUCTION_ARCHITECTURE.md`.

### Environment variables

- `ASPNETCORE_ENVIRONMENT=Development` → header/query tenant overrides allowed.
- `Production` / non-Development → no `X-Tenant-Id` / `?tenant=`; POS tenant from JWT.

## Technical facts

- Framework: ASP.NET Core Web API, controller-based.
- Target framework: `net10.0`.
- Data: EF Core + Npgsql (`AppDbContext : IdentityDbContext<ApplicationUser>`).
- AuthN/AuthZ: JWT + policy/permission system (`HasPermission`, `AddAppAuthorization`).
- OpenAPI: Swashbuckle-generated `backend/swagger.json` is the contract source.

## Controller and route rules

- The repo hosts both canonical and leftover non-canonical families.
- **Canonical targets (preferred):**
  - Admin: `/api/admin/*`
  - POS: `/api/pos/*`
- RKSV special receipts and related admin surfaces: `/api/rksv/*` (high risk; permission-protected).
- Critical controllers that used to expose legacy aliases: `PaymentController`, `CartController`, `ProductController`.
- Do not open a legacy prefix on new endpoints; use the canonical prefix.

## High-risk route families (extra care before changing)

- `/api/pos/payment*`, TSE and register-session POS endpoints.
- Two offline systems (do not merge): `/api/offline-transactions/*` (legacy TSE intents) vs `/api/pos/offline-orders/*` + `/api/admin/offline-orders/*` (full snapshots).
- `/api/rksv/*` (Nullbeleg, Startbeleg, Monatsbeleg, Jahresbeleg, Schlussbeleg).
- `/api/admin/fiscal-export*`, `/api/admin/rksv/dep-export*`, FinanzOnline/outbox.
- Backup/restore: `/api/admin/backup*` (Tenant vs System; no production restore).

## Response / contract rules

- If the contract changes, update `backend/swagger.json` in the same PR.
- **Admin client:** After `backend/swagger.json` changes, regenerate Orval in `frontend-admin` and run `node scripts/verify-api-client.mjs` (`ai/11_OPENAPI_CONTRACT_GOVERNANCE.md`).
- Use named DTOs + `ProducesResponseType` on critical endpoints.
- Payment v2 contract is opt-in via header: `X-Regkasse-Payment-Contract: v2`.

## Security and compliance

- Default: authorize endpoints and limit them with permissions.
- **Fiscal authority:** POS guardrails are UX. Final RKSV/TSE/payment rules run on the backend (`REGKASSE_AI_ONBOARDING.md`).
- High-risk areas: payment, receipt, signature chain, daily closing, TSE signature, RKSV special receipts, voucher, FinanzOnline/outbox.
- Before a behavioral change in those areas, write scope and risk explicitly. Do not claim legal compliance.

## Backend change checklist

1. Scan related controller/service/DTO files.
2. Check authz impact (`HasPermission`, role matrix).
3. Produce a swagger diff if the contract is affected.
4. Add a migration if needed and keep the existing modeling style.
5. Run related tests and script checks.
