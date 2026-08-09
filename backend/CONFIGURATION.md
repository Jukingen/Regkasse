# Backend configuration (secrets and environment)

Technical documentation (English). Do not commit real secrets; `appsettings.json` and `appsettings.*.json` under `backend/` are gitignored by design. Tracked sources of truth: `appsettings.example.json`, `appsettings.Development.example.json`, `appsettings.Staging.example.json`, `appsettings.Production.example.json`.

## Environment layering (`ASPNETCORE_ENVIRONMENT` + `RELEASE_STAGE`)

| Layer | File | Role |
|-------|------|------|
| Base | `appsettings.json` | Safe shared defaults (no DB password, no JWT secret, no PEM/API keys) |
| Development | `appsettings.Development.json` | Demo/Fake TSE, FinanzOnline simulation, 2FA off + bypass, CSRF off, NTP bypass; `Deployment:ReleaseStage=dev` |
| Staging | `appsettings.Staging.json` | Production-like fiscal lock, CSRF/2FA on, staging CORS/Redis; `ReleaseStage=staging` |
| Production | `appsettings.Production.json` | Real RKSV/FO flags, CSRF on, rate limit on, SuperAdmin 2FA on — **still no secrets**; `ReleaseStage=production` or `canary` |
| Secrets | User secrets / env vars | `ConnectionStrings:DefaultConnection`, `JwtSettings:SecretKey`, Fiskaly keys, license PEMs (or `LicenseSettings` file paths under gitignored `App_Data/`) |

Also set `RELEASE_STAGE=dev|staging|canary|production` (or `Deployment__ReleaseStage`). Details: [`docs/ENVIRONMENT_CONFIGURATION.md`](../docs/ENVIRONMENT_CONFIGURATION.md).

Load order (ASP.NET Core): base → environment overlay → user secrets (Development) → environment variables.

### Hosting model (Program / ApplicationHost)

- **No `Startup.cs`** — .NET 10 `WebApplication.CreateBuilder` / `WebApplication` pipeline in `ApplicationHost.cs`; `Program.cs` only builds and runs the host.
- Controllers for domain APIs; health probes via `HealthController` + `HealthChecks/*` (see `docs/HEALTH_GUARDRAILS.md`).
- Auth: `AddAuthentication().AddJwtBearer` + `AddAppAuthorization()` (`AddAuthorization` with permission policies).
- EF: `AddDbContext` + scoped `AddDbContextFactory` (required for singleton services that open scopes).
- CORS policy `RegkasseClients`: Development trusts local/LAN/`*.local`; Production requires `Cors:AllowedOrigins` and also allows HTTPS `*.regkasse.at` (POS / FA / Sites / tenant slug hosts). Custom website domains must be listed in `Cors:AllowedOrigins`.

### Required sections (presence checklist)

| Section | Base | Development | Production | Notes |
|---------|------|-------------|------------|--------|
| `TwoFactorAuth` | yes | yes (Enabled=false) | yes (Enabled=true) | See `docs/AUTH_TWO_FACTOR.md` |
| `Backup` | yes (Fake) | yes (Fake) | yes (PgDump paths) | Staging/archive roots via secrets in Dev |
| `FinanzOnline` (+ Outbox/RetryJob) | yes | simulation | real | Credentials from DB / secrets, not tracked JSON |
| `Security:Csrf` | yes | disabled | enabled | |
| `JwtSettings` Issuer/Audience | yes | inherit | yes | **SecretKey never in JSON** |
| `ConnectionStrings` | omit | omit | omit | User secrets / env only |
## Required secrets (local, staging, production)

| Setting | JSON path | Environment variable (override) | Notes |
|--------|------------|-----------------------------------|--------|
| PostgreSQL | `ConnectionStrings:DefaultConnection` | `ConnectionStrings__DefaultConnection` | Npgsql connection string; never log unmasked. Production should include `Pooling=true;Minimum Pool Size=5;Maximum Pool Size=20;Connection Lifetime=300;` (ApplicationHost also applies these defaults in Production when omitted). |
| JWT signing key | `JwtSettings:SecretKey` | `JwtSettings__SecretKey` | Minimum 32 characters; rotate if ever exposed. |
| JWT issuer / audience | `JwtSettings:Issuer`, `JwtSettings:Audience` | `JwtSettings__Issuer`, `JwtSettings__Audience` | Non-secret values may live in example config. |
| Redis (distributed cache) | `Redis:ConnectionString` | `Redis__ConnectionString` | Default `localhost:6379`. Used by `ICacheService` (`RedisCacheService`). On Redis failure the service falls back to process-local `IMemoryCache` (Error logged; `IsRedisAvailable=false`). Process-local `IMemoryCache` also remains for lockout/rate-limit/QR. |
| Redis enabled | `Redis:Enabled` | `Redis__Enabled` | Development default `false` (memory). Production/Staging default on when unset (`true` recommended). |
| Redis key prefix | `Redis:InstanceName` | `Redis__InstanceName` | e.g. `Regkasse_Dev` / `Regkasse_Prod`. Applied by `AddStackExchangeRedisCache`. |
| Domain cache TTLs | `CacheSettings:*` | `CacheSettings__*` | See § CacheSettings below. |

### Cache Settings (domain `ICacheService`)

Bound from section `CacheSettings` (`KasseAPI_Final.Configuration.CacheSettings`). Env overrides use double-underscore (`CacheSettings__LicenseCacheMinutes`).

| Key | Default | Env override | Description |
|-----|---------|--------------|-------------|
| `CacheSettings:LicenseCacheMinutes` | `5` | `CacheSettings__LicenseCacheMinutes` | Mandant license status TTL |
| `CacheSettings:ProductCacheMinutes` | `15` | `CacheSettings__ProductCacheMinutes` | Product list TTL |
| `CacheSettings:PermissionCacheMinutes` | `30` | `CacheSettings__PermissionCacheMinutes` | Effective user permissions TTL |
| `CacheSettings:TenantSettingsCacheMinutes` | `60` | `CacheSettings__TenantSettingsCacheMinutes` | Tenant settings snapshot TTL |
| `CacheSettings:TseHealthCacheSeconds` | `30` | `CacheSettings__TseHealthCacheSeconds` | Reserved for domain TSE health via `CacheKeys.TseHealth` (process TSE monitor stays in-memory today) |

**Environment-specific TTL guidance:**

| Environment | Guidance |
|-------------|----------|
| Development | Shorter TTLs (example: products ~10m, permissions ~15m, tenant settings ~30m) for faster feedback after writes |
| Staging | Production-like or slightly longer TTLs for realistic hit/miss observation (`docs/CACHE_OPTIMIZATION.md`) |
| Production | Defaults or longer (15–60m for catalogs/settings) after Staging confirms freshness; keep **license** short (`5`) |

Related Redis keys (same process):

| Key | Default / example | Env override | Description |
|-----|-------------------|--------------|-------------|
| `Redis:Enabled` | Dev `false`; Prod/Staging `true` | `Redis__Enabled` | When false, domain cache uses `MemoryCacheService` |
| `Redis:ConnectionString` | `localhost:6379` | `Redis__ConnectionString` | StackExchange Redis endpoint |
| `Redis:InstanceName` | e.g. `Regkasse_Prod` | `Redis__InstanceName` | Key prefix via `AddStackExchangeRedisCache` |

#### Redis vs MemoryCache fallback

| Mode | When | Behavior |
|------|------|----------|
| Memory | Development default, or `Redis:Enabled=false` | `MemoryCacheService` only |
| Redis | Staging/Production when enabled | `RedisCacheService` over `IDistributedCache` |
| Fallback | Redis unreachable / op failure | Same process serves from in-process `IMemoryCache`; **Error** logged; callers of `ICacheService` see no exception |

**Transparent fallback (`RedisCacheService`):** Every Get/Set/Remove/Exists/`GetOrCreate` Redis call is try/catch’d. On transient Redis failure the service:

1. Sets **`IsRedisAvailable = false`** (returns to `true` after a later successful Redis round-trip).
2. Logs at **Error** (`Redis cache unavailable; falling back to in-process IMemoryCache…`).
3. Serves/writes via injected **`IMemoryCache`** (and keeps a memory mirror on successful Redis writes for blip resilience).

Domain services keep depending only on `ICacheService` — they do not branch on Redis vs memory.

**Health:** `GET /api/health/ready` check `cache` (`RedisCacheHealthCheck`) reads `RedisCacheService.IsRedisAvailable`. A ping that only succeeds via memory fallback still yields **`redisStatus`: `Degraded`** (never Unhealthy / HTTP 503 for Redis alone). Entry data includes `isRedisAvailable`.

Process-local `IMemoryCache` remains separately for CSRF, lockout, rate-limit, QR — never move those onto `ICacheService`.

#### Cache key naming conventions

Use `CacheKeys` helpers only (`backend/Services/Caching/CacheKeys.cs`) — no ad-hoc strings.

| Pattern | Example | Notes |
|---------|---------|-------|
| `CacheKeys.LicenseStatus` → `license_status_{tenantId}` | `license_status_a1b2-…` | Billing/mandant license Cache-Aside |
| `CacheKeys.ProductList` → `product_list_{tenantId}` | `product_list_a1b2-…` | Unfiltered list; also **prefix** for category variants |
| `CacheKeys.ProductListByCategory` → `product_list_{tenantId}_cat_{categoryId}` | `product_list_…_cat_…` | Category-filtered list |
| `CacheKeys.ProductDetail` → `product_detail_{productId}` | `product_detail_…` | Optional single-product projection |
| `CacheKeys.UserPermissions` → `user_permissions_{userId}` | `user_permissions_…` | Effective permission snapshot |
| `CacheKeys.TenantSettings` → `tenant_settings_{tenantId}` | `tenant_settings_…` | Tenant settings snapshot |
| `CacheKeys.TseHealth` → `tse_health_{scopeId}` | `tse_health_…` | Reserved template for TSE health via `ICacheService` |
| `CacheKeys.HealthPing` | `health_check_ping` | Ready probe only — not business data |

Build keys with `CacheKeys.Format(CacheKeys.LicenseStatus, tenantId)` (never interpolate magic strings).

Invalidation: `CacheInvalidationHelper` (event-based after writes). Never store passwords, voucher codes, or payment details in domain cache keys/values.

#### Cache Tuning (Staging)

Full observation worksheet: [`docs/CACHE_OPTIMIZATION.md`](../docs/CACHE_OPTIMIZATION.md). Summary:

| Rule of thumb | Action |
|---------------|--------|
| Hit rate &gt; ~80% + event invalidation on writes | Consider **increasing** TTL (Staging products → 30m, permissions → 60m) |
| Hit rate &lt; ~50% | Consider **decreasing** TTL or dropping that key later |
| License / SaaS gate | Keep **short** TTL (`5`) even if hit rate is only moderate |

**Observation (~1 week on Staging):** set `Logging:LogLevel:KasseAPI_Final.Services.Caching` to **`Debug`** (hit/miss lines are Debug-level; `Information` alone will **not** show them), then revert to **`Warning`**.

**Provisional Staging TTLs** (applied in `appsettings.Staging.example.json` pending live hit/miss fill-in):

| Domain | Staging TTL | Rationale |
|--------|-------------|-----------|
| License | **5** min | Freshness-critical SaaS gate; event invalidation on sale/activate |
| Products | **30** min | Expect high list-read hit rate; writes invalidate prefix |
| Permissions | **60** min (1h) | Expect high authz hit rate; role change invalidates key |
| Tenant settings | **90** min | Low write frequency |

Live hit rates: fill the table in `docs/CACHE_OPTIMIZATION.md` §2 after the observation week, then revise TTLs here if measured rates disagree. Do not copy Staging TTLs to Production until Demo & QA confirms freshness.

**Fallback note:** The system automatically falls back to `IMemoryCache` if Redis becomes unavailable. This ensures continued operation, though cache effectiveness may decrease. When Redis is unavailable, cache entries are stored **in memory per instance**, not shared across instances, until Redis recovers. When Redis recovers, `RedisCacheService` logs **Information** (`Redis is available again, switching back from MemoryCache`) and sets `IsRedisAvailable=true` again.

**Local Redis (Windows):** Docker/WSL optional. Portable build under `tools/redis` (gitignored). Start with:

```powershell
.\scripts\start-redis-dev.ps1
.\scripts\start-redis-dev.ps1 -PingOnly
```

### SuperAdmin two-factor authentication (`TwoFactorAuth`)

Hub: [`docs/AUTH_TWO_FACTOR.md`](../docs/AUTH_TWO_FACTOR.md).

| Setting | JSON path | Environment variable | Notes |
|--------|------------|----------------------|--------|
| Master switch | `TwoFactorAuth:Enabled` | `TwoFactorAuth__Enabled` | Production: `true`. Local Dev template may use `false`. |
| Dev login bypass | `TwoFactorAuth:BypassInDevelopment` | `TwoFactorAuth__BypassInDevelopment` | Only honored when `ASPNETCORE_ENVIRONMENT=Development`. |
| Dev test code | `TwoFactorAuth:TestToken` | `TwoFactorAuth__TestToken` | Accepted only in Development (with `DEV-2FA-BYPASS`). Leave empty in Production. |

Legacy override: `Auth:RequireSuperAdminTwoFactor` (`null` = follow `TwoFactorAuth` rules). Prefer the `TwoFactorAuth` section for new config.

FinanzOnline **user/password** for SOAP are expected from **company settings in the database** (or optional `FinanzOnline:Session` binding in non-tracked config). Do not put production FinanzOnline credentials in tracked files.

### Manual restore approval (Super Admin)

Section `ManualRestoreApproval` (`ManualRestoreApproval__*` env vars):

| Key | Default | Description |
|-----|---------|-------------|
| `Enabled` | `true` | When `false`, manual restore API returns 503. |
| `ApprovalTokenTtlMinutes` | `15` | Second-admin 6-digit approval token lifetime (BCrypt-hashed at rest). |
| `TargetDatabaseNamePrefix` | `restore_validation_` | Required prefix for isolated target DB names. |
| `AdditionalBlockedDatabaseNames` | `[]` | Extra DB names that must never be restore targets. |
| `FallbackApproverEmails` | `[]` | Used when no other Super Admin emails exist. |

Requires SMTP (`Email:Smtp`) for approval emails. API: `POST /api/admin/restore/request`, `POST /api/admin/restore/approve/{requestId}`, `GET /api/admin/restore/request/{requestId}`, `GET /api/admin/restore/history`. **Never** restores into `DefaultConnection` database.

Optional **production cutover** token: `FinanzOnline:CutoverGuard:ProdApprovalToken` → `FinanzOnline__CutoverGuard__ProdApprovalToken` (set only when your runbook requires it).

## Inventory / stock (optional rollout)

Product rows keep `StockQuantity` / `MinStockLevel` in the database; this section only gates **runtime behavior**.

| Setting | JSON path | Environment variable | Default | Effect when `false` |
|--------|------------|----------------------|---------|---------------------|
| Enforce stock on payment | `Inventory:EnforceStockOnSales` | `Inventory__EnforceStockOnSales` | `false` | When `false`, POS payment does **not** reject for low stock and does **not** change `Product.StockQuantity` on payment, storno, or refund. |

Operator-facing Lager UI in **frontend-admin** is controlled separately via **Next.js** `NEXT_PUBLIC_*` variables (baked at build). See repository `docs/inventory-lager-optional.md` (includes smoke checklist and restart/rebuild table).

## Local development (recommended)

1. Copy templates (once):

   ```bash
   cp appsettings.example.json appsettings.json
   cp appsettings.Development.example.json appsettings.Development.json
   ```

2. Set **secrets** with the user-secrets store (values are **not** written to disk in the project folder). Tracked `appsettings*.json` templates intentionally omit `ConnectionStrings` and `JwtSettings:SecretKey`; those must come from user secrets or environment variables.

   ```bash
   cd backend
   dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=kasse_db;Username=postgres;Password=YOUR_PASSWORD;Include Error Detail=true"
   dotnet user-secrets set "JwtSettings:SecretKey" "YOUR_RANDOM_KEY_AT_LEAST_32_CHARS"
   ```

   Development tip: `Include Error Detail=true` surfaces PostgreSQL exception details in EF logs (helpful for type mismatches such as `character varying = uuid`). Do **not** enable this in Production.

The project defines `<UserSecretsId>` in `KasseAPI_Final.csproj` so `dotnet user-secrets` targets this API automatically.

Alternative: set the same keys as **User** or **Machine** environment variables (names use `__` for `:`).

### Backup & DR (optional — RealPgDump selectable in Development)

Repo defaults keep `Backup:ExecutionAdapterKind` = **Fake** and staging/archive roots empty so hypothetical PgDump health stays **Unhealthy** until you configure local paths. **Do not** commit machine-specific directories to tracked `appsettings*.json`; use **user secrets** or environment variables.

| Setting (JSON) | Environment variable | Notes |
|----------------|----------------------|--------|
| `Backup:ArtifactStagingRoot` | `Backup__ArtifactStagingRoot` | Absolute directory; required for PgDump hypothetical health to clear the missing-staging blocker. |
| `Backup:ExternalArchiveRoot` | `Backup__ExternalArchiveRoot` | Absolute directory; in Development avoids degraded readiness when evaluating PgDump; non-Development it is mandatory for PgDump. |

**Quick setup (Windows, from `backend/`):** run `.\scripts\setup-backup-dr-dev-secrets.ps1` (creates `C:\data\regkasse-backup-staging` and `C:\data\regkasse-backup-archive` and writes the two secrets). Restart the API. Leave `Backup:ExecutionAdapterKind` as **Fake** until you switch mode in the admin Backup & DR UI.

Equivalent manual commands:

```bash
cd backend
dotnet user-secrets set "Backup:ArtifactStagingRoot" "C:\data\regkasse-backup-staging"
dotnet user-secrets set "Backup:ExternalArchiveRoot" "C:\data\regkasse-backup-archive"
```

`ConnectionStrings:DefaultConnection` is already required for API startup; `Backup:VerifyLogicalDumpFileOnDisk` defaults to `true`. Set `Backup:PgDumpExecutablePath` only if `pg_dump` is not on `PATH`. See `docs/BACKUP_DEVELOPMENT_REAL_PG_DUMP.md`.

### Production backup (required environment variables)

Production must **not** use `ProductionStub` or `Fake`. Set real PostgreSQL logical backups via environment variables (or equivalent entries in untracked `appsettings.Production.json`). Do not commit machine paths or credentials to tracked files.

**Minimum production backup env block:**

```bash
# PostgreSQL (required for pg_dump; use a least-privilege backup role in production)
# Pooling: Min 5 / Max 20 / Connection Lifetime 300s (ApplicationHost also applies these in Production if omitted)
ConnectionStrings__DefaultConnection="Host=...;Port=5432;Database=...;Username=...;Password=...;Pooling=true;Minimum Pool Size=5;Maximum Pool Size=20;Connection Lifetime=300;"

# Real pg_dump adapter (mandatory in production)
Backup__ExecutionAdapterKind=PgDump
Backup__ArtifactStagingRoot=/var/backups/regkasse/staging
Backup__ExternalArchiveRoot=/var/backups/regkasse/archive
Backup__PgDumpExecutablePath=/usr/bin/pg_dump
Backup__WorkerEnabled=true
Backup__AcknowledgePhase1NoRealBackup=false
```

| Setting (JSON) | Environment variable | Production notes |
|----------------|----------------------|------------------|
| `Backup:ExecutionAdapterKind` | `Backup__ExecutionAdapterKind` | Must be `PgDump`. `ProductionStub` performs no PostgreSQL I/O. |
| `Backup:ArtifactStagingRoot` | `Backup__ArtifactStagingRoot` | Absolute path; API user must be able to write `.dump` + manifest files. |
| `Backup:ExternalArchiveRoot` | `Backup__ExternalArchiveRoot` | Absolute path; mandatory for PgDump outside Development. |
| `Backup:PgDumpExecutablePath` | `Backup__PgDumpExecutablePath` | Full path when `pg_dump` is not on `PATH` (typical Linux: `/usr/bin/pg_dump`). |
| `Backup:WorkerEnabled` | `Backup__WorkerEnabled` | Must be `true` for automated dequeue and scheduled runs. |
| `Backup:AcknowledgePhase1NoRealBackup` | `Backup__AcknowledgePhase1NoRealBackup` | Must be `false` when using real backups. |

**Recommended additional production keys** (also in `appsettings.Production.json` template):

| Setting (JSON) | Environment variable | Example |
|----------------|----------------------|---------|
| `Backup:LogicalDumpConnectionStringName` | `Backup__LogicalDumpConnectionStringName` | `DefaultConnection` |
| `Backup:ExternalArchiveMutableTargetAccepted` | `Backup__ExternalArchiveMutableTargetAccepted` | `true` (local filesystem archive; use immutability flags for WORM/S3 Object Lock instead) |
| `Backup:ScheduledBackupEnabled` | `Backup__ScheduledBackupEnabled` | `true` |
| `Backup:ScheduledBackupCron` | `Backup__ScheduledBackupCron` | `0 2 * * *` (daily 02:00 UTC) |
| `Backup:RetentionPolicyMode` | `Backup__RetentionPolicyMode` | `ReportOnly` |
| `Backup:ArtifactRetentionDays` | `Backup__ArtifactRetentionDays` | `30` |

**Host prep (Linux example):**

```bash
sudo mkdir -p /var/backups/regkasse/staging /var/backups/regkasse/archive
sudo chown <api-user>:<api-group> /var/backups/regkasse/staging /var/backups/regkasse/archive
which pg_dump   # expect /usr/bin/pg_dump
```

Verify readiness in admin **Backup & DR** UI or trigger a manual backup after deploy; run a restore drill before go-live. See [`docs/BACKUP_PERMISSIONS.md`](../docs/BACKUP_PERMISSIONS.md) and [`backend/docs/BACKUP_DEVELOPMENT_REAL_PG_DUMP.md`](docs/BACKUP_DEVELOPMENT_REAL_PG_DUMP.md).

## `dotnet ef` and design-time DbContext

The design-time factory loads, in order: optional `appsettings.json`, optional `appsettings.Development.json`, user secrets, then environment variables. CI agents without local JSON files should set `ConnectionStrings__DefaultConnection`.

## Manual rotation after exposure

If a password or JWT key was ever stored in a tracked file, shared screenshot, or chat, **rotate it manually** (this repo does not rotate secrets for you):

- PostgreSQL user password (or least-privilege DB user).
- `JwtSettings:SecretKey` (invalidates existing JWTs).
- Any FinanzOnline password stored in plaintext outside the intended secure store.

## Email and user management

> **Full guide:** [`docs/EMAIL_CONFIGURATION.md`](../docs/EMAIL_CONFIGURATION.md) — SMTP keys, dev capture, feature matrix, production checklist, troubleshooting.

**Email is no longer required for day-to-day user management.** Tenant and platform users are created via admin APIs; a one-time generated password is returned in the HTTP response and must be delivered to the operator out of band. Audit logs record `USER_CREATED` with `createdByUserId`, `tenantId`, and `role` — never the password value.

Optional SMTP (`Email:Smtp`) powers welcome mail, username change notices, forgot-username/password (admin app), activity alerts, license reports, invoice resend, restore approval, and payment-reversal approval. Optional `SupportContact` is shown in transactional user emails (falls back to `From`).

Username changes are stored in `user_username_history` for compliance review (`UserUsernameHistoryService`). Forgot-username emails include the **current** username only (not history).

### Local dev (quick start)

In **Development**, forgot-username / forgot-password emails are written to `backend/App_Data/dev-mail/*.txt` when `Email:DevCapture:Enabled` is `true` (default). Real inbox delivery requires separate `Email:Smtp` configuration.

```powershell
cd backend && dotnet run
.\scripts\dev-mail-test.bat
```

See [`docs/EMAIL_CONFIGURATION.md`](../docs/EMAIL_CONFIGURATION.md) for all settings, verification steps, and production setup.

## CSRF protection (optional)

Section `Security:Csrf` (`Security__Csrf__*` env vars). Middleware: `CsrfMiddleware`. Token endpoint: `GET /api/csrf/token` (anonymous).

| Key | Default | Description |
|-----|---------|-------------|
| `Enabled` | `false` | When `true`, state-changing methods require a valid CSRF token (unless Development bypass applies). |
| `BypassInDevelopment` | `true` | When `true` **and** `ASPNETCORE_ENVIRONMENT=Development`, skip CSRF validation. Ignored outside Development. |
| `HeaderName` | `X-XSRF-TOKEN` | Request header carrying the token from `GET /api/csrf/token`. |
| `CookieName` | `XSRF-TOKEN` | Cookie set by the token endpoint (not HttpOnly — FA reads via `document.cookie` and sends `X-XSRF-TOKEN`). |
| `TokenLifetimeHours` | `24` | Cache + cookie lifetime (clamped 1–168). |

**Templates:** Development example disables CSRF + bypass; Production example enables CSRF with `BypassInDevelopment=false`. Copy into local `appsettings.Development.json` / `appsettings.Production.json` (gitignored).

**Exempt paths:** `/api/Auth/login`, `/api/Auth/refresh`, health, swagger, metrics, `/api/webhooks/*`, `/api/csrf/token`. Native clients without a cookie jar may send the same value in `X-CSRF-COOKIE`. Response on failure: HTTP 403 with message to refresh the page.

Enable in Production only after FA/POS/sites attach the token on mutations.

## RKSV cold-archive cleanup (optional)

Section `RksvDataCleanup` (`RksvDataCleanup__*` env vars). Hosted service: `RksvDataCleanupHostedService`.

| Key | Default | Description |
|-----|---------|-------------|
| `Enabled` | `false` | When `false`, the daily sweep is a no-op. |
| `RetentionYears` | `7` | Payments older than this are eligible for cold-archive ZIP copies. |
| `ExtraArchiveYears` | `3` | After retention+extra years, old archive **files** may be pruned from disk. |
| `HardDeleteEnabled` | `false` | Even when `true`, **live** fiscal payment rows are **not** deleted (TSE/RKSV signature-chain integrity). |
| `ArchiveRootRelativeDirectory` | `App_Data/rksv-cold-archives` | ZIP output directory (relative to content root unless rooted). |
| `MaxBatchSize` | `500` | Max payments archived per sweep. |
| `IntervalHours` | `24` | Delay between sweeps. |
| `StartupGraceMinutes` | `5` | Boot delay before the first sweep. |

Tracking tables: `rksv_cold_archive_runs`, `rksv_cold_archive_items`. Live `payment_details` rows remain in the database after archive.

## Logging

Connection strings printed at startup use `ConnectionStringMasking`; raw passwords must not appear in logs. Do not enable verbose logging of HTTP bodies or SOAP envelopes that might contain session tokens in production without redaction.

| Environment | Console format | Notes |
|-------------|----------------|-------|
| Development | `FormatterName=readable` (`ReadableConsoleFormatter`) | `[HH:mm:ss] LEVEL [Category] message \| Tenant: … \| User: …` |
| Production | `FormatterName=json` | Promtail/Loki aggregation — see [`docs/MONITORING.md`](../docs/MONITORING.md) |

**Noise reduction (defaults in example configs):**

- JWT validation success → **Debug** (failures stay Warning)
- Cart statistics → **Debug**
- Per-request duration → **Debug**; slow requests (≥ `Monitoring:SlowRequestThresholdMs`, default 1000) → **Warning**
- API exceptions / HTTP 500 → **Error** / **Warning** with User, Tenant, method, path+query
- Request scope middleware pushes `Tenant` / `TenantId` / `User` / `UserId` / `Role` / `CorrelationId` (unmasked) after auth

Levels: prefer `Information` for `KasseAPI_Final`, `Warning`+ for ASP.NET/EF noise. Category filters mute `KasseAPI_Final.Program` JWT chatter when raised above Debug.

Prometheus: `Monitoring:Enabled` + scrape `GET /metrics` — [`docs/METRICS.md`](../docs/METRICS.md).
