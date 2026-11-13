# Staging setup

**Last updated:** 2026-10-11  
**Related:** [`ENVIRONMENT_CONFIGURATION.md`](ENVIRONMENT_CONFIGURATION.md) · [`CANARY_LAUNCH_RUNBOOK.md`](CANARY_LAUNCH_RUNBOOK.md) · [`DEPLOYMENT_SMOKE_TEST.md`](DEPLOYMENT_SMOKE_TEST.md) · [`FISCAL_SWITZERLAND.md`](FISCAL_SWITZERLAND.md) · [`.github/environments/staging.yml`](../.github/environments/staging.yml) · [`backend/appsettings.Staging.example.json`](../backend/appsettings.Staging.example.json)

Staging is the host the canary runbook requires (`ASPNETCORE_ENVIRONMENT=Staging`). Copy the tracked example onto the host. Do not commit the filled file. DNS names below are the configured targets. This document does not claim those records or a live database already exist.

Checklist size: **35** items (21 pre-launch, 8 smoke, 6 canary).

---

## 1. Hosts and environment

| Surface | Host |
|---------|------|
| API | `https://api.staging.regkasse.at` |
| FA | `https://admin.staging.regkasse.at` |
| POS | `https://pos.staging.regkasse.at` |

| Variable | Staging value |
|----------|----------------|
| `ASPNETCORE_ENVIRONMENT` | `Staging` (loads `appsettings.Staging.json`) |
| `RELEASE_STAGE` | `staging` (or `Deployment__ReleaseStage=staging`) |

`appsettings.Staging.example.json` already sets `Deployment:ReleaseStage` to `staging`. If both the file and `RELEASE_STAGE` are empty, the API derives `staging` from `ASPNETCORE_ENVIRONMENT=Staging` ([`ReleaseStageResolver`](../backend/Services/Deployment/ReleaseStageResolver.cs)).

FA banner: `FiscalHostEnvironmentBanners` shows **STAGING**. The Ant Design color token is `gold` on a warning alert (`getReleaseStageBannerColor`). Build-time fallback is `NEXT_PUBLIC_RELEASE_STAGE=staging`. Prefer the live API field `GET /api/rksv/environment` → `releaseStage`.

POS fallback: `EXPO_PUBLIC_RELEASE_STAGE=staging`.

---

## 2. Config file

Tracked templates stay in git. Real `appsettings*.json` files under `backend/` are gitignored.

```powershell
cd backend
copy appsettings.example.json appsettings.json
copy appsettings.Staging.example.json appsettings.Staging.json
```

```bash
cd backend
cp appsettings.example.json appsettings.json
cp appsettings.Staging.example.json appsettings.Staging.json
```

Leave secret fields empty in the JSON. Overlay them with user secrets or environment variables. Load order: base → `appsettings.Staging.json` → user secrets (Staging only, optional) → environment variables. Production does not load user secrets (`ApplicationHost.ShouldAddUserSecretsAfterDefaultHostConfig`).

The staging example sets Austrian fiscal posture to the production lock with fiskaly **TEST** (`Tse:Environment` / `Fiskaly:Environment` = `Test`), `RKSV:ShowDemoLabel` = `true`, FinanzOnline simulation **false**, Redis `InstanceName` = `Regkasse_Staging`, backup roots under `/var/backups/regkasse-staging/`, and CORS / `AllowedHosts` for the hosts above.

`Tse:EnforceProductionLockInStaging` is `true`. Soft TSE and FinanzOnline simulation fail startup. The note in [`.github/environments/staging.yml`](../.github/environments/staging.yml) matches that lock.

### Config sections

Secrets stay out of the example. Empty `ApiKey` / `ApiSecret` fields are placeholders. Set the env names in the last column on the host.

| Lane | Section | Example posture | Secret (env only) |
|------|---------|-----------------|-------------------|
| Base | `Tse` | `TseMode=Device`, `Mode=Real`, `Provider=fiskaly`, `Environment=Test`, `EnforceProductionLockInStaging=true` | `Tse__Providers__fiskaly__ApiKey`, `Tse__Providers__fiskaly__ApiSecret` |
| Base | `Fiskaly` | SIGN AT host `https://rksv.fiskaly.com/api/v1`, `Environment=TEST` | `Fiskaly__ApiKey`, `Fiskaly__ApiSecret` |
| Base | `FinanzOnline` | `Mode=Production`, every `UseSimulation=false` | `FinanzOnline__Session__DefaultCredential__Username`, `Password`, `TelematikId`, `HerstellerId` |
| Base | `RKSV` | `Mode=Production`, `TseMode=Real`, `ShowDemoLabel=true` | — |
| Base | `Redis` | `Enabled=true`, `InstanceName=Regkasse_Staging`. `ConnectionString` is the placeholder `redis-cluster:6379` | `Redis__ConnectionString` |
| Base | `Security:Csrf` | `Enabled=true`, `BypassInDevelopment=false` | — |
| Base | `TwoFactorAuth` | `Enabled=true`, `BypassInDevelopment=false`. `TestToken` stays empty | — |
| Base | `Backup` | `ExecutionAdapterKind=PgDump`, roots under `/var/backups/regkasse-staging/` | — |
| Base | `JwtSettings` | Issuer `KasseAPI-Staging` | `JwtSettings__SecretKey` |
| CH canary | `Mwst` | `CanaryTenantId` empty, `UseTestEndpoint=false` | `Mwst__CanaryTenantId` (one CH tenant guid) |
| CH canary | `QrRechnung` | `BuilderMode=not-configured`, `BankSubmit.Enabled=false` | — |
| Peppol canary | `Peppol` | `Provider=not-configured`, `Environment=TEST`, `ReservedExit.Enabled=false`, `ReservedExit.CanaryTenantId` empty, `Storecove.Environment=TEST`, `Storecove.BaseUrl` empty | `Peppol__Storecove__ApiKey` (and `Peppol__ApiKey` if a hosted key is required). Neither key is in the example |
| Environment-specific | `KassenSicherheit:Provider` | File default is the DE pilot: `fiskaly-de`, `PilotMode=true`, `Environment=TEST`. A CH canary sets `Provider=not-configured` on the host copy. Do not treat the file default as the CH value | `KassenSicherheit__ApiKey`, `KassenSicherheit__ApiSecret`, `KassenSicherheit__AdminPin` (DE pilot only; leave empty in git) |

---

## 3. Database, Redis, secrets, license

### Database

Use a PostgreSQL instance that is not the production database. The staging example has no `ConnectionStrings` block. Set the string outside git:

| Store | Key |
|-------|-----|
| User secrets (local Staging) | `ConnectionStrings:DefaultConnection` |
| Host env | `ConnectionStrings__DefaultConnection` |

Apply migrations before traffic. Readiness depends on the database. `/health/migrations` must report `pendingCount=0`.

### Redis

`Redis:Enabled` is `true` in the staging example. `ConnectionString` in that file is the placeholder `redis-cluster:6379`. Point it at a staging instance, or at a dedicated logical database on a shared server. Keep `InstanceName` = `Regkasse_Staging` so keys do not collide with `Regkasse_Dev` or `Regkasse_Prod`.

### Secrets

| Where | When |
|-------|------|
| `dotnet user-secrets` | Local machine. UserSecretsId is `a8f3e1b2-9c4d-4e7f-a1b2-3c4d5e6f7080`. Staging loads this store; Development already loads it via the default host. |
| Environment variables / host vault | Staging cloud. Double underscore for nested keys (`JwtSettings__SecretKey`, `FinanzOnline__Session__DefaultCredential__Password`). |

Minimum host secrets: connection string, `JwtSettings:SecretKey` (at least 32 characters, different from production), fiskaly TEST API key and secret. Add FinanzOnline test credentials only when Staging will call SOAP.

### License

Do not install the production deployment license on Staging. Use one of:

- A deployment key issued for this host (slug `system` in the unified `REGK-…` format), or
- A mandant trial (`Trial` in `appsettings.example.json`: 14 days by default, `MaxRegistersInTrial` 1, `MaxUsersInTrial` 3). The Super Admin create-tenant wizard can start a trial. Staging does not seed the Development `dev` tenant.

`License` and `Trial` are not overridden in `appsettings.Staging.example.json`. They come from the base file unless the host sets env vars.

---

## 4. Pre-launch checklist

### Database

- [ ] PostgreSQL instance and database name are dedicated to Staging.
- [ ] `ConnectionStrings__DefaultConnection` is set in the secret store, not in git.
- [ ] Migrations are applied. `GET /health/migrations` returns `pendingCount=0`.

### Secrets

- [ ] `JwtSettings__SecretKey` is unique to Staging and at least 32 characters.
- [ ] Fiskaly TEST `ApiKey` and `ApiSecret` are set (`Tse__Providers__fiskaly__*` and `Fiskaly__*`).
- [ ] FinanzOnline credentials, if used, are BMF **test** values in the secret store. `UseSimulation` stays `false`.
- [ ] `Deployment__StatusReportToken` matches GitHub `DEPLOYMENT_STATUS_TOKEN`.
- [ ] `appsettings.Staging.json` contains no passwords, API keys, or license private keys.

### DNS and TLS

- [ ] DNS points `api.staging.regkasse.at`, `admin.staging.regkasse.at`, and `pos.staging.regkasse.at` at the Staging hosts.
- [ ] TLS certificates cover those three names.
- [ ] `Cors:AllowedOrigins` and `AllowedHosts` match [`appsettings.Staging.example.json`](../backend/appsettings.Staging.example.json).

### Process

- [ ] `ASPNETCORE_ENVIRONMENT=Staging`.
- [ ] `RELEASE_STAGE=staging`.
- [ ] `appsettings.Staging.json` was copied from the example on this host.
- [ ] Redis is a non-production instance or namespace, with `InstanceName=Regkasse_Staging`.
- [ ] Backup directories exist and are writable. `pg_dump` is on `PATH`. `Backup:ExecutionAdapterKind` is `PgDump`.

### License and clients

- [ ] Staging uses its own deployment license or a trial mandant. The production deployment key is not installed here.
- [ ] FA image uses `NEXT_PUBLIC_API_BASE_URL=https://api.staging.regkasse.at` and `NEXT_PUBLIC_RELEASE_STAGE=staging`.
- [ ] POS image uses `EXPO_PUBLIC_API_BASE_URL=https://api.staging.regkasse.at/api` and `EXPO_PUBLIC_RELEASE_STAGE=staging`.

### Before a CH canary

- [ ] The copied Staging file includes `Mwst`, `QrRechnung`, and `Peppol` from the example. `Mwst__CanaryTenantId` is set only for the one CH tenant.
- [ ] For a CH-only canary, `KassenSicherheit:Provider` is `not-configured` on the host copy. The example default stays `fiskaly-de` for the DE pilot.

---

## 5. Post-launch smoke

Run after the process is up. Details and the script: [`DEPLOYMENT_SMOKE_TEST.md`](DEPLOYMENT_SMOKE_TEST.md).

- [ ] `GET /api/health/live` returns HTTP 200.
- [ ] `GET /api/health/ready` returns HTTP 200 and JSON `releaseStage` is `staging`.
- [ ] `GET /health/tse/mode` shows `TseMode=Device`, `Mode=Real`, and a non-fake provider.
- [ ] `GET /health/finanzonline/mode` shows simulation off.
- [ ] `GET /api/rksv/environment` (admin token) returns `releaseStage=staging` and `isHostStaging=true`.
- [ ] FA at `https://admin.staging.regkasse.at` shows the **STAGING** banner (gold / warning).
- [ ] POS at `https://pos.staging.regkasse.at` loads.
- [ ] `SMOKE_TEST_EXPECTED_STAGE=staging ./scripts/smoke-test.sh` exits 0 against `API_BASE=https://api.staging.regkasse.at`.

```powershell
$env:API_BASE = 'https://api.staging.regkasse.at'
$env:FA_BASE = 'https://admin.staging.regkasse.at'
$env:POS_BASE = 'https://pos.staging.regkasse.at'
$env:SMOKE_TEST_EXPECTED_STAGE = 'staging'
.\scripts\smoke-test.ps1
```

Do not set `SMOKE_POS_PAYMENT=1` on this host. Staging is not a Soft TSE environment.

---

## 6. Canary enable (CH)

Follow [`CANARY_LAUNCH_RUNBOOK.md`](CANARY_LAUNCH_RUNBOOK.md) §1 and §2. Staging must already pass the smoke list above. Restart the API after each config or secret change. `Mwst:CanaryTenantId` is read at startup.

- [ ] Operator is Super Admin (`system.critical`). API host is `ASPNETCORE_ENVIRONMENT=Staging`. FA is `https://admin.staging.regkasse.at`.
- [ ] `Mwst__CanaryTenantId` is one CH tenant guid, format `D`. Empty denies every tenant.
- [ ] `Mwst:UseTestEndpoint=false`, `QrRechnung:BankSubmit:Enabled=false`, `QrRechnung:BuilderMode=not-configured`. `true` / `dryRun` fail Staging startup. There is no `QrRechnung:CanaryTenantId` key.
- [ ] On `/admin/tenants/{tenantId}`, accept the five open QR gaps (`official-swiss-cross`, `font-embedding-liberation-arial`, `perforation-line`, `pain001`, `bank-scan`).
- [ ] One CH sale, then PDF download. The payload text starts with `SPC`. Record match / no-match only. Do not paste the IBAN.
- [ ] Rollback: set `Mwst__CanaryTenantId` empty and restart. Optional FA `/admin/mwst` **Canary zurücknehmen** sets `Fiscal.MwstCh=false` for that configured id.

DE and Peppol canaries stay in the same runbook (§3 and §4). They are separate tenants and separate secrets.

---

## 7. Local Staging simulation

This runs the Staging host on a developer machine. It is not the cloud Staging environment. Cloud Staging keeps `Auth:RequireTenantHostMatch=true` and `AuthCookies:Secure=true`.

### Files and process

```powershell
cd backend
copy appsettings.example.json appsettings.json
copy appsettings.Staging.example.json appsettings.Staging.json

dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=kasse_staging;Username=postgres;Password=YOUR_PASSWORD"
dotnet user-secrets set "JwtSettings:SecretKey" "YOUR_RANDOM_KEY_AT_LEAST_32_CHARS"

# Local HTTP only. Do not copy these two onto the cloud host.
dotnet user-secrets set "Auth:RequireTenantHostMatch" "false"
dotnet user-secrets set "AuthCookies:Secure" "false"

$env:ASPNETCORE_ENVIRONMENT = 'Staging'
$env:RELEASE_STAGE = 'staging'
dotnet run --project KasseAPI_Final.csproj
```

User secrets are the same store Development uses. Point `Database=kasse_staging` (or another name) so Staging does not write the Development `kasse_db`.

`X-Tenant-Id` and `?tenant=` apply only when `ASPNETCORE_ENVIRONMENT=Development`. A Staging process resolves the tenant from the JWT after login. The Development demo seed does not run.

### CH canary on that process

1. Create the CH mandant while the API is in **Development** (Super Admin create-tenant wizard: country **CH**, or `PATCH /api/admin/tenants/{id}/country` on a tenant that has no signed fiscal documents from another regime). Copy `tenants.id`.
2. Stop the API. Start it again with `ASPNETCORE_ENVIRONMENT=Staging`.
3. Set the canary id and restart:

```powershell
$env:Mwst__CanaryTenantId = '00000000-0000-0000-0000-000000000000'
```

Replace the guid with the CH tenant id (format `D`). The same value as a user secret is `Mwst:CanaryTenantId`. An empty value denies every CH QR sale.

4. Confirm `QrRechnung:BuilderMode` is `not-configured` and `QrRechnung:BankSubmit:Enabled` is `false`. `dryRun` or bank submit `true` fails startup. For this CH-only process, set `KassenSicherheit:Provider` to `not-configured` on the host copy. The example default remains the DE pilot (`fiskaly-de`).
5. Sign in on FA against `http://localhost:5184` with `NEXT_PUBLIC_RELEASE_STAGE=staging` if you need the banner before the environment call returns. After login, the banner follows `releaseStage` from the API.
6. Accept the five QR gaps and run the CH smoke in the canary runbook §2. Local fiskaly TEST keys are still required for Austrian readiness.

---

## 8. Gaps in the current config

| Gap | What still needs ops |
|-----|------------------------|
| No live Staging host in git | DNS, TLS, the PostgreSQL instance, and the GitHub Environment are not created by this file. [`.github/environments/staging.yml`](../.github/environments/staging.yml) is a checklist. |
| `Mwst:CanaryTenantId` is empty | The section is in the example. An empty id denies every CH tenant until ops sets `Mwst__CanaryTenantId`. |
| `KassenSicherheit:Provider` default is the DE pilot | The example stays `fiskaly-de`. A CH canary host must set `not-configured` on its copy. One file cannot be both. |
| Redis connection is still a placeholder | `redis-cluster:6379` is not a Staging server. Set `Redis__ConnectionString`. The database connection string is still only a secret, not a block in the example. |
| Peppol Storecove URL and key | `Storecove.BaseUrl` is empty and `Peppol__Storecove__ApiKey` is not in git. A Peppol canary needs both from the secret store before `ReservedExit.Enabled` is turned on. |
| Local Staging is not Development | No `dev` seed, no `X-Tenant-Id`, and the example requires a tenant/host match plus secure cookies. The local section above overrides only the last two, on the developer machine. |
