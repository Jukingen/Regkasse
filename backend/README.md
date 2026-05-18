# Regkasse Backend (KasseAPI)

ASP.NET Core Web API (`net10.0`), EF Core + PostgreSQL, JWT auth, RKSV/TSE/fiscal services.

## Quick start (local)

```powershell
dotnet run --project backend/KasseAPI_Final.csproj
```

Default development URL is configured in `appsettings.Development.json` (typically `http://localhost:5184`).

## Development Setup for Multi-Tenant Testing

Requires `ASPNETCORE_ENVIRONMENT=Development` and a matching `tenants.slug` row in the database.

### Option 1: Header-based (simplest)

```bash
curl -H "X-Tenant-Id: test_cafe" http://localhost:5184/api/health
```

### Option 2: Query string

```bash
curl "http://localhost:5184/api/admin/payments?tenant=test_cafe"
```

### Option 3: Localhost subdomains (hosts file)

```text
127.0.0.1 test-cafe.localhost
127.0.0.1 test-bar.localhost
```

Then: `http://test-cafe.localhost:5184` (slug = first host label: `test-cafe`).

FA/POS UI switchers and POS `.env`: `REGKASSE_AI_ONBOARDING.md`.

## Multi-Tenant Architecture

Regkasse uses a multi-tenant architecture where a single backend instance serves multiple tenants (companies/customers).

### Tenant Identification

- Tenants are identified by subdomain: `{tenant}.regkasse.at`
- Examples: `cafe.regkasse.at`, `bar.regkasse.at`, `market.regkasse.at`
- Super Admin accesses `admin.regkasse.at`

### Data Isolation

- Tenant-scoped entities implement `ITenantEntity` with non-null `tenant_id`
- Entity Framework global query filters in `AppDbContext` filter by `ICurrentTenantAccessor.TenantId`
- Tenants can NEVER see other tenants' data
- Cross-tenant access attempts return HTTP 404

### Development Mode

- Localhost: use `X-Tenant-Id` header or `?tenant=` query parameter (`Tenancy/SubdomainTenantProvider.cs`)
- Or use hosts-file domains: `*.regkasse.local` / `*.local` (`Tenancy/TenantHostNames.cs`)

## API Headers

### Tenant Identification

- **Production:** Tenant from `Host` subdomain (automatic).
- **Development:** `X-Tenant-Id: {slug}` (tenant slug, not UUID).
- **Development:** `?tenant={slug}` query parameter.

### Super Admin Endpoints

- `/api/admin/tenants/*` — `SuperAdmin` role required.
- Operates on global `tenants` table (not `ITenantEntity` filtered).
- Use `POST /api/admin/tenants/{tenantId}/impersonate` for tenant-scoped operational APIs.

### Super Admin

Access: `admin.regkasse.at`. Role: **`SuperAdmin`** on all `/api/admin/tenants/*` routes.

| Method | Route | Purpose |
|--------|-------|---------|
| `GET` | `/api/admin/tenants` | List tenants (`?includeDeleted=`) |
| `GET` | `/api/admin/tenants/{tenantId}` | Detail |
| `POST` | `/api/admin/tenants` | Create |
| `PUT` | `/api/admin/tenants/{tenantId}` | Update / suspend / reactivate |
| `DELETE` | `/api/admin/tenants/{tenantId}` | Soft-delete |
| `POST` | `/api/admin/tenants/{tenantId}/impersonate` | Support JWT scoped to tenant |

Impersonation: JWT includes target `tenant_id` and claim `tenant_impersonation=true`. Cannot impersonate suspended/deleted tenants.

See `Services/AdminTenants/`, `docs/MULTI_TENANT.md`.

## Multi-Tenant Security

### Tenant isolation guarantees

- EF global query filters on `ITenantEntity` — not bypassable by API query parameters.
- Cross-tenant access by id → **404** (`TenantIsolationTests`).
- `offline_transactions.tenant_id` NOT NULL; derived on insert from cash register.

### Tenant spoofing prevention

- **Production:** subdomain/`Host` only (`SubdomainTenantProvider`; no dev header/query).
- **Super Admin:** `[Authorize(Roles = SuperAdmin)]` on `AdminTenantsController`.

**Known gap:** JWT `tenant_id` may override host-resolved tenant after auth; host↔JWT binding middleware not yet present. See `docs/MULTI_TENANT.md`.

## Migrating existing databases

Use the existing EF migration chain (waves), not a single `AddTenantIdToAllTables`:

```bash
dotnet ef database update --project backend/KasseAPI_Final.csproj --startup-project backend/KasseAPI_Final.csproj
```

Key migrations: `AddTenantsAndSettingsTenantId`, Wave2/3A/3B, `AddTenantIdToFiscalAndAuditTables`, `ExtendTenantsForSuperAdmin`. Legacy rows default to `LegacyDefaultTenantIds.Primary` (Guid).

Details: `ai/02_DATABASE_CONTRACT.md`, `docs/MULTI_TENANT.md`.

## Database Schema

### Multi-Tenant Columns

Tenant-scoped tables (`ITenantEntity`):

- `tenant_id uuid NOT NULL` (PostgreSQL), FK to `tenants.id`
- Indexed in `AppDbContext`
- Populated from request tenant resolution (subdomain slug → Guid on `ICurrentTenantAccessor`)

### Global Query Filters

EF Core adds `WHERE tenant_id = @currentTenantId` to all `ITenantEntity` queries via `AppDbContext` global filters (`CreateTenantQueryFilter`).

Filter expression: `_tenantAccessor.TenantId == null || e.TenantId == _tenantAccessor.TenantId` (null accessor disables filter — use only on intentional paths).

### Scoped service resolution (singletons + EF)

`AppDbContext` and `ICurrentTenantAccessor` are **scoped**. Singletons (e.g. `LicenseService`) must use **`IServiceScopeFactory`**:

```csharp
using var scope = _scopeFactory.CreateScope();
var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
using var db = factory.CreateDbContext();
```

Do **not** inject `IDbContextFactory` into a singleton and call `CreateDbContext()` on the root provider.

**`AppDbContext` constructors:**

- Design-time: `AppDbContext(options)` — migrations (`DesignTimeDbContextFactory`).
- Runtime: `AppDbContext(options, ICurrentTenantAccessor)` — `[ActivatorUtilitiesConstructor]`.

DbContext registration: `ApplicationHost` → `ConfigureAppDbContextOptions` (Npgsql + interceptors). `services.AddMemoryCache()` is registered for other components; **`LicenseService` uses an in-memory snapshot, not `IMemoryCache`**.

### License service

| Registration | `AddSingleton<LicenseService>()` + `ILicenseService` → `ProductionLicenseService` |
| DB access | `IServiceScopeFactory` per operation |
| Startup | `EvaluateOnStartup()` — DB read in scope; failure → trial/file fallback, warning log, host still starts |

### Key code locations

| Area | Path |
|------|------|
| Host → slug | `Tenancy/TenantHostNames.cs`, `Tenancy/SubdomainTenantProvider.cs` |
| Resolve slug → Guid | `Tenancy/CurrentTenantService.cs` |
| Middleware order | `Middleware/TenantResolutionMiddleware.cs`, `Middleware/TenantContextMiddleware.cs` |
| Query filters | `Data/AppDbContext.cs` (`ITenantEntity` + `CreateTenantQueryFilter`) |
| Tenant admin | `Services/AdminTenants/`, `Controllers/AdminTenantsController.cs` |
| Tests | `KasseAPI_Final.Tests/TenantIsolationTests.cs`, `SubdomainTenantProviderTests.cs` |

## Deployment Requirements

### DNS Configuration

- Wildcard **A** record: `*.regkasse.at` → server IP (multi-tenant subdomain resolution).
- TLS certificate must include `*.regkasse.at` (and apex if needed).
- Proxy must forward the original `Host` header.

### Environment Variables

- **`ASPNETCORE_ENVIRONMENT`**
  - `Development` — `X-Tenant-Id` / `?tenant=` slug overrides enabled.
  - `Production` (and non-Development) — subdomain/`Host` resolution only.

## Troubleshooting

### `Cannot resolve scoped service 'ICurrentTenantAccessor' from root provider`

Singleton called `IDbContextFactory<AppDbContext>.CreateDbContext()` without a scope. Fix: `IServiceScopeFactory` — see `Services/LicenseService.cs`.

### `Multiple constructors` on `AppDbContext`

Use design-time ctor + single `[ActivatorUtilitiesConstructor]` runtime ctor. See `Data/AppDbContext.cs`.

## Further reading

- `docs/MULTI_TENANT.md` (architecture, setup, API, troubleshooting)
- `REGKASSE_AI_ONBOARDING.md` (repo root)
- `ai/01_BACKEND_CONTRACT.md`, `ai/02_DATABASE_CONTRACT.md`
- OpenAPI: `backend/swagger.json`
