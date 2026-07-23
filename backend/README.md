# Regkasse Backend (KasseAPI)

ASP.NET Core Web API for Regkasse POS / Admin: multi-tenant auth, payments, RKSV/TSE fiscal flows, backup, and billing.

## Tech Stack

| Layer | Choice |
|-------|--------|
| Runtime | .NET **10** (`net10.0`) |
| Host | `WebApplication` in `ApplicationHost.cs` (`Program.cs` boots only — no `Startup.cs`) |
| Data | EF Core **10** + **Npgsql** / PostgreSQL |
| Auth | ASP.NET Identity + JWT Bearer; permission policies (`Authorization/`) |
| API docs | Swashbuckle / OpenAPI 3 — Dev UI at `/swagger` ([`docs/SWAGGER_GUARDRAILS.md`](docs/SWAGGER_GUARDRAILS.md)) |
| Health | `GET /api/health`, `/api/health/live`, `/api/health/ready` |
| Cache | Redis (`ICacheService`) + process `IMemoryCache` for lockout/rate-limit |
| Observability | prometheus-net; structured logging |
| Build config | [`nuget.config`](nuget.config) (nuget.org only), [`Directory.Build.props`](Directory.Build.props), [`.editorconfig`](.editorconfig) |
| Container | Multi-stage [`Dockerfile`](Dockerfile) → `aspnet:10.0`, port **8080** |
| Tests | xUnit + FluentAssertions + Moq; `Microsoft.AspNetCore.Mvc.Testing`; Testcontainers (PostgreSQL) |

Package versions are pinned in `KasseAPI_Final.csproj` (e.g. EF/Identity `10.0.10`, Swashbuckle `10.2.3`).

## Setup

### Prerequisites

- .NET SDK **10.x**
- PostgreSQL **16+** (local or Docker)
- Optional: Redis (see [`CONFIGURATION.md`](CONFIGURATION.md)), Docker Desktop (image build / Testcontainers)

### Configure

```powershell
cd backend
# First time: copy templates (real appsettings*.json are gitignored)
copy appsettings.example.json appsettings.json
copy appsettings.Development.example.json appsettings.Development.json

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=kasse_db;Username=postgres;Password=YOUR_PASSWORD"
dotnet user-secrets set "JwtSettings:SecretKey" "YOUR_RANDOM_KEY_AT_LEAST_32_CHARS"
```

| Layer | File | Role |
|-------|------|------|
| Base | `appsettings.json` | Safe defaults (no DB password / JWT secret / PEMs) |
| Development | `appsettings.Development.json` | Fake TSE, FO simulation, 2FA/CSRF off or bypass |
| Production | `appsettings.Production.json` | Real RKSV/FO flags — **still no secrets** |
| Secrets | User secrets / env | `ConnectionStrings__DefaultConnection`, `JwtSettings__SecretKey`, … |

Tracked templates: `appsettings*.example.json`. Full map: [`CONFIGURATION.md`](CONFIGURATION.md).

### Restore, build, run

```powershell
dotnet restore KasseAPI_Final.csproj
dotnet build KasseAPI_Final.csproj
dotnet run --project KasseAPI_Final.csproj
```

From repo root: `npm run dev:backend`, `npm run build:backend`, `npm run test:backend` (see [`CONTRIBUTING.md`](../CONTRIBUTING.md)).

- Default Dev URL: **`http://localhost:5184`**
- Swagger (Development only): **`http://localhost:5184/swagger`**
- OpenAPI JSON: `/swagger/v1/swagger.json`
- CORS policy `RegkasseClients`: Dev = local/LAN; Prod = `Cors:AllowedOrigins` + HTTPS `*.regkasse.at`

### Format

```powershell
dotnet format KasseAPI_Final.sln
dotnet format whitespace KasseAPI_Final.sln --verify-no-changes
dotnet format style KasseAPI_Final.sln --severity warn --verify-no-changes
```

Rules live in [`.editorconfig`](.editorconfig) (4-space C#, file-scoped namespaces preferred; Migrations marked `generated_code`).

### Multi-tenant local smoke

Requires `ASPNETCORE_ENVIRONMENT=Development` and a matching `tenants.slug` row.

```bash
# Header (simplest)
curl -H "X-Tenant-Id: dev" http://localhost:5184/api/health

# Query
curl "http://localhost:5184/api/admin/payments?tenant=dev"
```

Hosts-file option: `127.0.0.1 dev.localhost` → `http://dev.localhost:5184`. FA/POS switchers: repo root `REGKASSE_AI_ONBOARDING.md`.

### Configuration notes (2026-07-21)

Base overlays include `TwoFactorAuth`, `Security:Csrf`, `FinanzOnline*`, `Backup`, `Monitoring`, `License` (no PEMs in JSON). JWT secret and connection strings must come from user secrets / env — never commit them.

## Testing

Project: `KasseAPI_Final.Tests/` (xUnit). Test `obj`/`bin` live under `backend/obj/Tests` and `backend/bin/Tests` (see `KasseAPI_Final.Tests/Directory.Build.props`).

```powershell
cd backend

# Default local / CI unit + in-memory suite (skip PostgreSQL-tagged)
dotnet test KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj --filter "Category!=PostgreSql"

# PostgreSQL-tagged (Testcontainers if Docker is up, or set REGKASSE_TEST_POSTGRES)
dotnet test KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj --filter "Category=PostgreSql"

# Narrow filters (examples)
dotnet test --filter "FullyQualifiedName~TenantIsolation"
dotnet test --filter "FullyQualifiedName~OpenApiSchemaIdSelectorTests"
```

| CI workflow | What it runs |
|-------------|--------------|
| `.github/workflows/backend-unit-tests.yml` | `Category!=PostgreSql` |
| `.github/workflows/backend-postgres-integration-tests.yml` | `Category=PostgreSql` |
| `.github/workflows/api-contract-tests.yml` | Contract-related tests |

Clear `REGKASSE_OPENAPI_EXPORT` / accidental OpenAPI-export env when running normal tests (CI does this). Details: [`docs/backend-postgresql-integration-tests.md`](../docs/backend-postgresql-integration-tests.md).

OpenAPI contract (repo root):

```bash
node scripts/generate-backend-openapi.mjs
node scripts/validate-critical-openapi-paths.mjs
```

## Deployment

### Docker

Multi-stage image: `restore` → `build` → `publish` → `aspnet` runtime.  
**Build context must be the repository root** (`ProjectReference` → `tools/LicenseGenerator.Core`).

```bash
# From repository root
docker build -f backend/Dockerfile -t regkasse-api:local .

docker run --rm -p 5184:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e ConnectionStrings__DefaultConnection="Host=host.docker.internal;Port=5432;Database=kasse_db;Username=postgres;Password=YOUR_PASSWORD" \
  -e JwtSettings__SecretKey="YOUR_RANDOM_KEY_AT_LEAST_32_CHARS" \
  --name regkasse-api \
  regkasse-api:local
```

| Detail | Value |
|--------|--------|
| Listen | **8080** inside the container (`ASPNETCORE_URLS`) |
| Healthcheck | `GET /api/health/live` |
| Config in image | Copied from `appsettings*.example.json` only (secrets via env) |
| Ignore files | `.dockerignore`, `Dockerfile.dockerignore` (BuildKit) |

Smoke:

```bash
curl -fsS http://localhost:5184/api/health/live
curl -fsS http://localhost:5184/api/health/ready   # needs PostgreSQL
```

Without Docker: `dotnet publish -c Release -o ./artifacts/docker-publish /p:UseAppHost=false`.

### Production hosts & DNS

| Surface | Typical host |
|---------|----------------|
| API | `https://api.regkasse.at` |
| Admin (FA) | `https://admin.regkasse.at` |
| POS UI | `https://pos.regkasse.at` (single POS UI — JWT `tenant_id`) |

- Wildcard **A** / TLS for `*.regkasse.at` where subdomain routing is used.
- Reverse proxy must forward the original `Host` header.
- Authenticated API traffic is scoped by JWT `tenant_id`; do not rely on `X-Tenant-Id` in Production.

### Required environment (Production)

| Variable | Purpose |
|----------|---------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__DefaultConnection` | PostgreSQL (prefer pooling options — see `CONFIGURATION.md`) |
| `JwtSettings__SecretKey` | ≥ 32 characters |
| `JwtSettings__Issuer` / `JwtSettings__Audience` | If not baked in config |
| `Cors__AllowedOrigins__*` | Explicit origins as needed (custom website domains) |

Apply migrations before traffic:

```bash
dotnet ef database update --project KasseAPI_Final.csproj --startup-project KasseAPI_Final.csproj
```

## Multi-tenant architecture

- Tenant-scoped entities implement `ITenantEntity` with non-null `tenant_id`.
- EF global query filters in `AppDbContext` use `ICurrentTenantAccessor.TenantId`.
- Cross-tenant access by id → **HTTP 404** (not 403).
- **Development:** `X-Tenant-Id` / `?tenant={slug}` or `*.local` hosts.
- **Production:** no Dev header/query overrides; JWT + host rules per [`../docs/MULTI_TENANT.md`](../docs/MULTI_TENANT.md).

### Super Admin (`SuperAdmin`)

| Method | Route | Purpose |
|--------|-------|---------|
| `GET` | `/api/admin/tenants` | List (`?includeDeleted=`) |
| `GET` | `/api/admin/tenants/{tenantId}` | Detail |
| `POST` | `/api/admin/tenants` | Create |
| `PUT` | `/api/admin/tenants/{tenantId}` | Update / suspend / reactivate |
| `DELETE` | `/api/admin/tenants/{tenantId}` | Soft-delete |
| `POST` | `/api/admin/tenants/{tenantId}/impersonate` | Support JWT with `tenant_impersonation=true` |

Code: `Services/AdminTenants/`, `Controllers/AdminTenantsController.cs`.

### Isolation guarantees

- Filters on `ITenantEntity` are not bypassable via client query parameters.
- `IgnoreQueryFilters()` only for intentional Super Admin / platform paths.
- Singletons that touch EF must open an **`IServiceScopeFactory`** scope (never resolve scoped services from the root provider).

**Known gap:** JWT `tenant_id` may override host-resolved tenant after auth; host↔JWT binding middleware is not complete — see `docs/MULTI_TENANT.md`.

## Database & migrations

```bash
dotnet ef database update --project KasseAPI_Final.csproj --startup-project KasseAPI_Final.csproj
```

Legacy backfills use `LegacyDefaultTenantIds.Primary`. Contracts: `ai/02_DATABASE_CONTRACT.md`, [`../docs/MULTI_TENANT.md`](../docs/MULTI_TENANT.md).

### Migration inventory & squash (high risk)

| Metric | Approx. (2026-07-21) |
|--------|----------------------|
| Migration `Up` files | ~240 |
| EF-discovered migrations | ~221 |
| `Migrations/` folder | ~50 MB |

Squash is planned only via Staging → DB-copy → Production runbook — **not** applied on `main` yet.

- Runbook: [`docs/MIGRATION_SQUASH.md`](docs/MIGRATION_SQUASH.md)
- Scripts: `backend/scripts/migration-squash/` (see runbook)

### Schema / DI reminders

- `tenant_id uuid NOT NULL` + indexes on tenant-scoped tables.
- Filter: `_tenantAccessor.TenantId == null || e.TenantId == _tenantAccessor.TenantId`.
- `AppDbContext`: design-time ctor + `[ActivatorUtilitiesConstructor]` runtime ctor.
- `LicenseService`: singleton + scope-per-operation; startup evaluate fails soft to trial/file.

| Area | Path |
|------|------|
| Host → slug | `Tenancy/TenantHostNames.cs`, `Tenancy/SubdomainTenantProvider.cs` |
| Slug → Guid | `Tenancy/CurrentTenantService.cs` |
| Middleware | `Middleware/` — see [`docs/MIDDLEWARE_GUARDRAILS.md`](docs/MIDDLEWARE_GUARDRAILS.md) |
| Query filters | `Data/AppDbContext.cs` |
| Tests | `KasseAPI_Final.Tests/TenantIsolationTests.cs` |

## RKSV / TSE

Fiscal signing, special receipts, TSE chain, and FinanzOnline outbox: `Services/`, `Tse/`, `Controllers/`. See `ai/05_SECURITY_COMPLIANCE.md`, root `AGENTS.md`, [`docs/FISCAL_TSE_GUARDRAILS.md`](docs/FISCAL_TSE_GUARDRAILS.md).

### DEP §7 Export

**Status:** Implemented (F1–F5). BMF `Belege-Gruppe` JSON, certificate grouping, compact JWS, RKSV §9 machine code.

- **Endpoint:** `GET /api/admin/rksv/dep-export`
- **Permissions:** `report.export` + `audit.view` (audit `RksvDepExportJson`)
- **Params:** `cashRegisterId`, `fromUtc`, `toUtc`, `includeSpecialReceipts`, `includeDailyClosings`

```bash
curl -H "Authorization: Bearer {token}" \
     -H "X-Tenant-Id: {tenant}" \
     "http://localhost:5184/api/admin/rksv/dep-export?cashRegisterId={guid}&fromUtc=2026-01-01T00:00:00Z&toUtc=2026-01-31T23:59:59Z"
```

```powershell
# From repository root
.\scripts\verify-rksv-dep-export.ps1 -DepExportPath "./dep-export.json" -CryptoMaterialPath "./crypto-material.json"
```

Guide: [`../docs/DEP_EXPORT_DEVELOPMENT.md`](../docs/DEP_EXPORT_DEVELOPMENT.md).

## Troubleshooting

### `Cannot resolve scoped service 'ICurrentTenantAccessor' from root provider`

A singleton called `IDbContextFactory<AppDbContext>.CreateDbContext()` without a scope. Use `IServiceScopeFactory.CreateScope()` — see `Services/LicenseService.cs`.

### `Multiple constructors` on `AppDbContext`

Keep design-time ctor + one `[ActivatorUtilitiesConstructor]` runtime ctor (`Data/AppDbContext.cs`).

### Restore fails / unexpected NuGet feeds

`backend/nuget.config` clears inherited sources and allows only **nuget.org**. Confirm with:

```powershell
dotnet nuget list source --configfile nuget.config
```

### Swagger 404 at `/` or missing UI

In Development, UI is at **`/swagger`** (not site root). JSON: `/swagger/v1/swagger.json`. Production does not map Swagger by default.

### Docker build cannot find `LicenseGenerator.Core`

Build from **repo root** with `-f backend/Dockerfile`, not `cd backend && docker build .`.

### Docker / API starts but `ready` is 503

Liveness (`/api/health/live`) does not need DB; readiness does. Check `ConnectionStrings__DefaultConnection`, network (`host.docker.internal`), and migrations.

### Tests skip or fail after OpenAPI export

Unset `REGKASSE_OPENAPI_EXPORT` (and related test host env). CI clears these; a leftover shell env can force export/in-memory host modes.

### `dotnet format --verify-no-changes` exits non-zero

Prefer:

```powershell
dotnet format whitespace KasseAPI_Final.sln --verify-no-changes
dotnet format style KasseAPI_Final.sln --severity warn --verify-no-changes
```

IDE0052 (unread private fields) is suggestion-only — often intentional DI; no automatic code fix.

### Port 5184 already in use

Stop the other `KasseAPI_Final` / `dotnet run` process, or change `Urls` in Development config.

## Further reading

| Doc | Topic |
|-----|--------|
| [`CONFIGURATION.md`](CONFIGURATION.md) | Secrets, env layering, Redis |
| [`docs/SWAGGER_GUARDRAILS.md`](docs/SWAGGER_GUARDRAILS.md) | OpenAPI inclusion / regenerate |
| [`docs/MIDDLEWARE_GUARDRAILS.md`](docs/MIDDLEWARE_GUARDRAILS.md) | Pipeline order |
| [`docs/HEALTH_GUARDRAILS.md`](docs/HEALTH_GUARDRAILS.md) | Live / ready probes |
| [`docs/HELPERS_AND_EXTENSIONS.md`](docs/HELPERS_AND_EXTENSIONS.md) | Shared helpers |
| [`docs/SERVICES_LAYER.md`](docs/SERVICES_LAYER.md) | DI boundaries |
| [`docs/MIGRATION_SQUASH.md`](docs/MIGRATION_SQUASH.md) | EF squash runbook |
| [`../docs/MULTI_TENANT.md`](../docs/MULTI_TENANT.md) | Tenancy (repo) |
| [`../REGKASSE_AI_ONBOARDING.md`](../REGKASSE_AI_ONBOARDING.md) | Project brief |
| `ai/01_BACKEND_CONTRACT.md`, `ai/02_DATABASE_CONTRACT.md` | Contracts |
| `backend/swagger.json` | Committed OpenAPI artifact |

## License

Proprietary — All rights reserved. See [`../LICENSE`](../LICENSE).
