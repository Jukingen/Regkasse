# Deployment guide

Production-oriented deployment notes for Regkasse (API, POS, Admin).  
Local setup stays in [`DEVELOPMENT.md`](DEVELOPMENT.md). Coupled FA+API releases: [`docs/ADMIN_FA_DEPLOY.md`](docs/ADMIN_FA_DEPLOY.md).

**Last updated:** 2026-08-09

**CI/CD (Actions):** [`docs/CI_CD.md`](docs/CI_CD.md) · [`docs/GITHUB_ACTIONS.md`](docs/GITHUB_ACTIONS.md) · [`.github/workflows/README.md`](.github/workflows/README.md)

**Monitoring:** [`docs/MONITORING.md`](docs/MONITORING.md) · [`docs/ALERTING.md`](docs/ALERTING.md) · [`docs/METRICS.md`](docs/METRICS.md) · stack [`monitoring/`](monitoring/)

**Release notes (2026-08-08 wave):** [`docs/RELEASE_NOTES_2026-08-08.md`](docs/RELEASE_NOTES_2026-08-08.md) · Go-live: [`docs/GO_LIVE_CHECKLIST.md`](docs/GO_LIVE_CHECKLIST.md) · Runbook: [`docs/PRODUCTION_DEPLOYMENT_RUNBOOK.md`](docs/PRODUCTION_DEPLOYMENT_RUNBOOK.md)

**Production hosts (Single POS UI):**

| Surface | URL |
|---------|-----|
| POS | `https://pos.regkasse.at` |
| Admin (FA) | `https://admin.regkasse.at` |
| API | `https://api.regkasse.at` |
| Tenant sites | `/[slug]` (and optional verified custom domains) |

**Environment separation:** see [`docs/ENVIRONMENT_CONFIGURATION.md`](docs/ENVIRONMENT_CONFIGURATION.md) (`ASPNETCORE_ENVIRONMENT` + `RELEASE_STAGE`, Staging as Demo & QA template, FA/POS banners).

---

## Multi-stage CI/CD (backend)

GitHub Actions promotes the API image through **Staging (Demo & QA) → Canary → Production**. Canary lets you roll out to selected tenants first.

**Staging** is the primary **Demo & QA** environment: customer demonstrations, manual QA, and automated smoke after every merge to `main`. It also remains the pre-production staging area before Canary/Production promotion (same fiscal posture as Production, with clear STAGING / demo visual indicators — see [`docs/ENVIRONMENT_CONFIGURATION.md`](docs/ENVIRONMENT_CONFIGURATION.md)).

| Trigger | Build + test | Staging (Demo & QA) | Canary | Production |
|---------|--------------|---------------------|--------|------------|
| PR (`backend/**`…) | Yes | — | — | — |
| Push `main` / `master` | Yes | Yes | — | — |
| Push `release/*` | Yes | Yes | Yes (default canary tenants) | — |
| Tag `v*` | Yes | Yes | Yes | Yes (GitHub Environment approval) |
| `workflow_dispatch` (Backend CI) | Yes | per `max_stage` | per `max_stage` | per `max_stage` |

### Workflows

| Workflow | Role |
|----------|------|
| [`.github/workflows/backend-ci.yml`](.github/workflows/backend-ci.yml) | Build, unit tests, GHCR image, stage gates |
| [`.github/workflows/deploy-backend-stage.yml`](.github/workflows/deploy-backend-stage.yml) | Reusable: **migrate** → deploy webhook → smoke → auto-rollback → status |
| [`.github/workflows/deploy-canary.yml`](.github/workflows/deploy-canary.yml) | Manual: pick **one** tenant (progressive), migrate + deploy canary, smoke, soak hours, rollback on fail |
| [`.github/workflows/deploy-production.yml`](.github/workflows/deploy-production.yml) | Manual: confirm phrase → **migrate approval** → deploy + smoke |

**Tenant canary (progressive):** see [`docs/CANARY_DEPLOYMENT.md`](docs/CANARY_DEPLOYMENT.md) — select tenant → monitor 24–48h → next tenant. FA: `/admin/deployments/tenants`.

**Production compliance gate:** see [`docs/DEPLOYMENT_COMPLIANCE.md`](docs/DEPLOYMENT_COMPLIANCE.md) — ComplianceOfficer sign-off + DEP/TSE/FON/NTP/isolation checks before migrate/deploy. FA: `/admin/deployments/compliance`.

### GitHub Environments

Create Environments (Settings → Environments) with optional required reviewers:

| Environment | Used for |
|-------------|----------|
| `backend-staging` | Staging (Demo & QA): migrate + deploy — **automated from `main`** (no required reviewers by default) |
| `backend-canary` | Canary migrate + deploy |
| `backend-production-migrations` | Production schema — **require reviewers** |
| `backend-production` | Production app deploy — **require reviewers** |

Schema policy: [`docs/DATABASE_MIGRATION_STRATEGY.md`](docs/DATABASE_MIGRATION_STRATEGY.md) (additive only; Production migrate is a separate approved job).

### Secrets & variables

| Name | Purpose |
|------|---------|
| `BACKEND_*_DEPLOY_WEBHOOK_URL` | Host pull/restart hook (`staging` / `canary` / `production`) |
| `BACKEND_*_ROLLBACK_WEBHOOK_URL` | Previous-image rollback on smoke failure |
| `BACKEND_*_MIGRATE_WEBHOOK_URL` | Apply EF migrations on host (preferred) |
| `BACKEND_*_DATABASE_CONNECTION` | Fallback: runner-side `dotnet ef database update` |
| `DEPLOYMENT_STATUS_URL` | `https://api.regkasse.at/api/webhooks/deployments/ci-report` |
| `DEPLOYMENT_STATUS_TOKEN` | Same value as API `Deployment__StatusReportToken` |
| `SMOKE_LOGIN_IDENTIFIER` / `SMOKE_LOGIN_PASSWORD` | Optional authenticated smoke (`/api/rksv/environment`) |
| `BACKEND_*_API_BASE_URL` (vars) | Smoke base URLs |
| `BACKEND_CANARY_TENANT_IDS` (var) | Default canary tenant slugs/UUIDs |

Deploy webhook JSON (illustrative): `{ "image", "stage", "releaseStage", "sha", "ref", "tenantIds" }`.  
Ops should set `RELEASE_STAGE` / `Deployment__CanaryTenantSlugs` on the host for canary tenants.

Smoke script: [`scripts/smoke-test.sh`](scripts/smoke-test.sh) — see [`docs/DEPLOYMENT_SMOKE_TEST.md`](docs/DEPLOYMENT_SMOKE_TEST.md).

### FA dashboard

| Page | Purpose |
|------|---------|
| `/admin/deployments` | Stage deploy status (CI reports) |
| `/admin/database/migrations` | EF pending/applied for connected DB |

Permission: `system.critical`.

### Manual canary (selected tenants)

1. Actions → **Deploy Canary** → enter comma-separated tenant slugs/UUIDs (and optional image tag).
2. Migrations run automatically; smoke includes migration health.
3. On failure, **app** rollback webhook is invoked (additive schema is kept — see migration strategy).

### Manual production

1. Promote a known-good image tag (from Staging / Demo & QA or Canary).
2. Type confirmation phrase `deploy-production` (and compliance phrase on **Deploy Production**).
3. Approve **`backend-production-migrations`** (schema), then **`backend-production`** (app).
4. Smoke on production; **auto-rollback is off** — use rollback webhook / FA if smoke fails (schema Down is never automatic).

### CI/CD deployment steps (GitHub Actions)

Full guide: [`docs/CI_CD.md`](docs/CI_CD.md) · Actions map: [`docs/GITHUB_ACTIONS.md`](docs/GITHUB_ACTIONS.md).

| Step | Action |
|------|--------|
| 1. PR quality | Umbrella [`ci.yml`](.github/workflows/ci.yml) + path-filtered package workflows |
| 2. Merge to `main` | [`deploy.yml`](.github/workflows/deploy.yml) multi-image GHCR push (+ optional Staging API if `DEPLOY_YML_RUN_STAGING_API`); [`backend-ci.yml`](.github/workflows/backend-ci.yml) Staging (Demo & QA) API deploy via `backend-staging` + smoke + auto-rollback |
| 3. Canary (optional) | Actions → **Deploy Canary** ([`docs/CANARY_DEPLOYMENT.md`](docs/CANARY_DEPLOYMENT.md)) |
| 4. Production | Actions → **Deploy Production** (compliance + Environments) — preferred over `deploy.yml` production target |
| 5. Verify | Smoke + FA `/admin/deployments`; fiscal lock docs |

**Local helpers:** `scripts/ci-build.ps1`, `ci-test.ps1`, `ci-deploy.ps1`.  
**Environment checklists:** [`.github/environments/staging.yml`](.github/environments/staging.yml), [`production.yml`](.github/environments/production.yml).

**Host Compose alternative** (no Actions webhooks): [`docs/DOCKER_PRODUCTION.md`](docs/DOCKER_PRODUCTION.md) / `deploy-docker.bat`.

---

## Prerequisites

### DNS

| Host / pattern | Points to |
|----------------|-----------|
| `api.regkasse.at` | API load balancer / VM |
| `admin.regkasse.at` | Admin (FA) |
| `pos.regkasse.at` | POS web (if hosted) or marketing/redirect as designed |
| `*.regkasse.at` (optional) | Legacy / custom slug hosts, sites, or wildcard edge |

Reserved labels (never tenant slugs): `pos`, `api`, `admin`, `www`. Details: [`docs/POS_PRODUCTION_ARCHITECTURE.md`](docs/POS_PRODUCTION_ARCHITECTURE.md), [`docs/MULTI_TENANT.md`](docs/MULTI_TENANT.md).

### SSL / TLS

- Certificates covering `api`, `admin`, `pos` (and wildcard `*.regkasse.at` if used).
- Terminate TLS at the reverse proxy / load balancer; preserve `Host` for any Host-based routing.
- API container listens HTTP on **8080** internally (`ASPNETCORE_URLS`); put TLS in front.

### Environment / secrets readiness

- PostgreSQL production database (migrations applied).
- Redis required for Production domain `ICacheService` (see Production checklist below).
- Secrets injected via env / secret store — **never** bake JWT, DB passwords, or PEMs into images or git.
- Build-time vars for Admin (`NEXT_PUBLIC_*`) and POS (`EXPO_PUBLIC_*`) set **before** build.

### Coupled releases

When Admin consumes new API routes, deploy **backend first**, then FA, in the same window ([`docs/ADMIN_FA_DEPLOY.md`](docs/ADMIN_FA_DEPLOY.md)).

---

## Backend Deployment

### Publish (direct)

From repository root:

```bash
dotnet publish backend/KasseAPI_Final.csproj -c Release -o ./artifacts/api
```

Run (example):

```bash
export ASPNETCORE_ENVIRONMENT=Production
export RELEASE_STAGE=production
export ASPNETCORE_URLS=http://0.0.0.0:8080
export ConnectionStrings__DefaultConnection="Host=…;Database=…;Username=…;Password=…"
export JwtSettings__SecretKey="…"   # min 32 chars
dotnet ./artifacts/api/KasseAPI_Final.dll
```

**Staging cloud** (same publish artifact, different env):

```bash
export ASPNETCORE_ENVIRONMENT=Staging
export RELEASE_STAGE=staging
# Copy appsettings.Staging.example.json → appsettings.Staging.json on the host (or inject via env)
```

**Canary slice** on Production hosts:

```bash
export ASPNETCORE_ENVIRONMENT=Production
export RELEASE_STAGE=canary
# Or keep RELEASE_STAGE=production and set Deployment__CanaryTenantIds / Deployment__CanaryTenantSlugs
```

Apply schema before traffic:

```bash
dotnet ef database update \
  --project backend/KasseAPI_Final.csproj \
  --startup-project backend/KasseAPI_Final.csproj
```

### Docker

Build context **must be the repo root** (references `tools/LicenseGenerator.Core`):

```bash
docker build -f backend/Dockerfile -t regkasse-api:latest .
docker run --rm -p 8080:8080 \
  -e ASPNETCORE_ENVIRONMENT=Production \
  -e RELEASE_STAGE=production \
  -e ConnectionStrings__DefaultConnection="…" \
  -e JwtSettings__SecretKey="…" \
  regkasse-api:latest
```

- Image: self-contained publish on `mcr.microsoft.com/dotnet/aspnet:10.0`, port **8080**, entrypoint `./KasseAPI_Final`
- Healthcheck: `GET /api/health/live`
- Full config: [`backend/CONFIGURATION.md`](backend/CONFIGURATION.md), [`backend/README.md`](backend/README.md), [`docs/DOCKER_PRODUCTION.md`](docs/DOCKER_PRODUCTION.md)

### Production checklist (API)

Verify against tracked template [`backend/appsettings.Production.example.json`](backend/appsettings.Production.example.json). Set process env (systemd / Docker); **do not** commit secrets.

| Check | Expected |
|-------|----------|
| `ASPNETCORE_ENVIRONMENT` | `Production` (process env, not JSON) |
| `RELEASE_STAGE` / `Deployment:ReleaseStage` | `production` (or `canary` for canary slot) |
| `Tse:TseMode` | `Device` |
| `Tse:Mode` | `Real` |
| `RKSV:TseMode` / `RKSV:FinanzOnlineMode` | `Real` / `Real` (no Soft/Demo label) |
| `FinanzOnline:Session:UseSimulation` (and Registrierkassen / TransmissionQuery) | `false` |
| `FinanzOnline:RksvSubmission:ClientKind` | `Real` |
| `Backup:ExecutionAdapterKind` | `PgDump` |
| `TwoFactorAuth:Enabled` | `true` (`BypassInDevelopment=false`) |
| `Auth:RequireTenantMembershipForLogin` | `true` (API refuses to start if false outside Development) |
| `Auth:RequireTenantHostMatch` | `true` (mandant/custom Host must match JWT `tenant_id`; mismatch → 403 `TENANT_HOST_MISMATCH`; shared hosts exempt) |
| `Security:Csrf:Enabled` | `true` (`BypassInDevelopment=false`) |
| `Cors:AllowedOrigins` | production FA/POS/Sites origins |
| License public PEM | configured (`License` / OfflineVerification) |
| FA/POS banners | no DEVELOPMENT/STAGING; CANARY only for intended tenants |
| **Redis `Redis__Enabled`** | `true` |
| **Redis `Redis__ConnectionString`** | reachable Production Redis (e.g. `redis-cluster:6379`) |
| **Redis `Redis__InstanceName`** | distinct prefix (e.g. `Regkasse_Prod`) |
| **`CacheSettings__*` TTLs** | reviewed (defaults: license 5 / products 15 / permissions 30 / tenant settings 60) — see [`backend/CONFIGURATION.md`](backend/CONFIGURATION.md) |
| **Ready cache probe** | `GET /api/health/ready` → `entries.cache` + top-level `redisStatus` is `Healthy` (or `Degraded` only while investigating Redis); Redis alone must not force HTTP 503 |

**Cache warm-up after deployment:** There is no dedicated warm-up job. After cutover, expect a short cold-cache window — first license/product/permission reads refill Cache-Aside entries under `CacheSettings` TTLs. Prefer natural traffic (or a light smoke that hits FA license status + product list for a canary tenant) over flushing Redis. Avoid `POST /api/admin/cache/clear` with `clearAll` right after deploy unless recovering from a known stale-data incident.

**TTL tuning without redeploying code:** After deployment, cache TTLs can be tuned via `CacheSettings` / `CacheSettings__*` env overrides (license / products / permissions / tenant settings / TSE health) without code changes — restart the API process so options rebind.

Also: [`docs/TSE_PRODUCTION_CONFIG_LOCK.md`](docs/TSE_PRODUCTION_CONFIG_LOCK.md), [`docs/FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](docs/FINANZONLINE_PROD_CUTOVER_CHECKLIST.md), [`docs/ENVIRONMENT_CONFIGURATION.md`](docs/ENVIRONMENT_CONFIGURATION.md), [`backend/docs/HEALTH_GUARDRAILS.md`](backend/docs/HEALTH_GUARDRAILS.md).

### Pre-deploy verification (2026-08-08)

Run these checks before promoting out of **Staging (Demo & QA)** toward Canary or Production. After merge to `main`, Staging is deployed automatically; use this list for local/CI gates and for Demo & QA sign-off on the staging hosts.

```bash
# Backend (from repo root)
dotnet test backend/KasseAPI_Final.sln

# Critical fiscal / dashboard slice (faster gate)
cd backend && dotnet test --filter "FullyQualifiedName~DashboardControllerTests|FullyQualifiedName~RksvDepExportServiceTests|FullyQualifiedName~DepExportHistory"

# EF: list + model sync (EF Core 10 has no database update --dry-run)
cd backend
dotnet ef migrations list --project KasseAPI_Final.csproj
dotnet ef migrations has-pending-model-changes --project KasseAPI_Final.csproj
# Expect: "No changes have been made to the model since the last migration."

# Frontend
cd frontend-admin && npm run test   # see release notes if monorepo dual-React failures locally
cd frontend && npm run test
```

**Migrations to apply on Production (additive):** ensure `__EFMigrationsHistory` includes at least:

- `20260807120000_AddDepExportHistoryDownloadToken`
- `20260807150000_AddDepExportHistoryIsSimulated`
- `20260808130000_AddDownloadCountToDepExportHistory`
- `20260808214645_SyncDepExportAndPendingModelSnapshot` (snapshot-only / **no SQL**)

Apply via approved migrate job / webhook — never ad-hoc `Down` on Production. Strategy: [`docs/DATABASE_MIGRATION_STRATEGY.md`](docs/DATABASE_MIGRATION_STRATEGY.md).

### Deploy strategy (Staging / Demo & QA → Canary → Production)

This repo uses **progressive promotion**, not classic dual-cluster blue-green naming:

1. **Staging (Demo & QA)** — primary demo/QA lane; automatic on `main` via GitHub Environment `backend-staging` (`backend-ci` + deploy webhook + smoke + auto-rollback).
2. **Canary** — selected tenants (`deploy-canary.yml` / FA `/admin/deployments/tenants`); soak 24–48h.
3. **Production** — tag `v*` or `deploy-production.yml` with confirmation phrase + ComplianceOfficer gate; **auto-rollback off** (manual rollback webhook / scripts).

**Before Production cutover**

- [ ] System backup succeeded within last 24h (`Backup:ExecutionAdapterKind=PgDump`)
- [ ] `./scripts/prepare-rollback-backup.sh` (or platform image digest noted)
- [ ] Compliance sign-off ([`docs/DEPLOYMENT_COMPLIANCE.md`](docs/DEPLOYMENT_COMPLIANCE.md))
- [ ] Smoke plan ready ([`docs/DEPLOYMENT_SMOKE_TEST.md`](docs/DEPLOYMENT_SMOKE_TEST.md), `scripts/smoke-test.sh`)

**After Production cutover**

- [ ] Smoke: `/api/health/live`, `/api/health/ready`, `/health/tse/mode`, authenticated `/api/rksv/environment`
- [ ] DEP history download smoke (Manager): create → download → history status string `Completed`
- [ ] Manager dashboard: Handlungsbedarf pinned; widget reorder persists via `/api/admin/dashboard/preferences`
- [ ] Audit log list returns 200 for Mandanten-Admin
- [ ] On failure: previous image / `scripts/rollback-production.sh` (schema `Down` is **not** automatic)

### Staging checklist (API) — Demo & QA

Primary environment for **customer demos** and **QA** (also the pre-production staging lane).

- [ ] `ASPNETCORE_ENVIRONMENT=Staging` + `RELEASE_STAGE=staging`
- [ ] `appsettings.Staging.json` from `appsettings.Staging.example.json` (secrets via env/vault; `RKSV:ShowDemoLabel=true` for clear demo labeling)
- [ ] Fiscal lock Healthy (`/health/ready`, `/health/tse/mode`)
- [ ] FA/POS show yellow **STAGING** / demo banner (not confused with Production)
- [ ] Smoke + Demo & QA sign-off before promoting to Canary/Production — see [`docs/ENVIRONMENT_CONFIGURATION.md`](docs/ENVIRONMENT_CONFIGURATION.md) § Promoting a release

---

## Frontend (POS) Deployment

POS is an **Expo SDK 56** app. Production API base must be the shared API:

```text
EXPO_PUBLIC_API_BASE_URL=https://api.regkasse.at/api
```

Tenant comes from JWT after login — not per-tenant `{slug}` API hosts ([`docs/POS_PRODUCTION_ARCHITECTURE.md`](docs/POS_PRODUCTION_ARCHITECTURE.md)).

### Build

```bash
cd frontend
# Set EXPO_PUBLIC_* in env or EAS secrets BEFORE build
npm ci
npm run build          # expo export (web static) — see package.json "build"
```

Native / store binaries (preferred for registers):

```bash
cd frontend
npx eas build --platform android --profile production
# npx eas build --platform ios --profile production
```

Profile: [`frontend/eas.json`](frontend/eas.json) (`production` → Android APK, local credentials).

### Deploy channels

| Channel | Notes |
|---------|--------|
| **EAS / internal APK** | Sideload to devices; signing: [`docs/ANDROID_RELEASE_SIGNING.md`](docs/ANDROID_RELEASE_SIGNING.md) |
| **Google Play / App Store** | Configure store credentials outside this guide; do not commit keystores |
| **Web export** | Host `dist/` / Expo web output behind HTTPS on `pos.regkasse.at` if using web POS |

Install guide (de): [`docs/REGKASSE_APK_INSTALLATIONSANLEITUNG.md`](docs/REGKASSE_APK_INSTALLATIONSANLEITUNG.md).

---

## Frontend-Admin Deployment

`NEXT_PUBLIC_*` must be present at **`next build` time** — runtime-only env will not fix an already-built bundle ([`frontend-admin/docs/DEPLOYMENT_BUILD_TIME_ENV.md`](frontend-admin/docs/DEPLOYMENT_BUILD_TIME_ENV.md)).

### Build

```bash
cd frontend-admin
export NEXT_PUBLIC_API_BASE_URL=https://api.regkasse.at
export NEXT_PUBLIC_RKSV_ENVIRONMENT=PROD
npm ci
npm run build
npm run start          # serves on :3000
```

### Vercel

- Config: [`frontend-admin/vercel.json`](frontend-admin/vercel.json) (`framework: nextjs`, `npm run build`).
- Set `NEXT_PUBLIC_API_BASE_URL` and `NEXT_PUBLIC_RKSV_ENVIRONMENT` in the Vercel project **Environment Variables** for Production (available to the build).
- Do **not** set `outputDirectory` to `.next`.

### Docker

```bash
cd frontend-admin
NEXT_PUBLIC_API_BASE_URL=https://api.regkasse.at \
NEXT_PUBLIC_RKSV_ENVIRONMENT=PROD \
docker compose build
docker compose up -d
```

Or `docker build` with `--build-arg` for each `NEXT_PUBLIC_*` (see `frontend-admin/Dockerfile`). CI image: [`.github/workflows/frontend-admin-deploy.yml`](.github/workflows/frontend-admin-deploy.yml) (GHCR).

Nginx sample: `frontend-admin/nginx.conf` (`admin.regkasse.at` → `:3000`).

---

## Docker Compose (production-oriented)

Self-hosted reference stack: [`docker-compose.prod.yml`](docker-compose.prod.yml) + [`.env.production.example`](.env.production.example).

**Docker hub:** [`docs/DOCKER.md`](docs/DOCKER.md) · **Production guide:** [`docs/DOCKER_PRODUCTION.md`](docs/DOCKER_PRODUCTION.md) · **Env vars:** [`docs/DOCKER_ENV_VARS.md`](docs/DOCKER_ENV_VARS.md) · **Setup / migration:** [`docs/DOCKER_SETUP.md`](docs/DOCKER_SETUP.md) ([Deutsch](docs/DOCKER_SETUP.de.md)) · **Deutsch hub:** [`docs/DOCKER.de.md`](docs/DOCKER.de.md).

**Do not** use [`docker-compose.override.yml`](docker-compose.override.yml) here — that file enables Soft TSE / FON simulation for local Development (`docker compose up`).

**Host Compose vs GitHub Actions:** use `deploy-docker.bat` / `scripts/docker-deploy.ps1` on a Docker VM. Staging (Demo & QA) → Canary → Production image promotion stays in the workflows above (webhook + Environments).

#### Operator scripts (Windows)

| Script | Purpose |
|--------|---------|
| [`deploy-docker.bat`](deploy-docker.bat) | Prod Compose deploy (wraps `docker-deploy.ps1`) |
| [`docker-build-prod.bat`](docker-build-prod.bat) | Build prod images only |
| [`docker-push-prod.bat`](docker-push-prod.bat) | Tag + push to `DOCKER_REGISTRY` |
| [`docker-logs-prod.bat`](docker-logs-prod.bat) | Tail prod logs |
| [`deploy.bat`](deploy.bat) | Smoke → backup confirm → compose up |

#### PowerShell deploy helper

```powershell
copy .env.production.example .env.production
# Fill secrets, then:
.\scripts\docker-deploy.ps1 -Profile admin
# Or: deploy-docker.bat admin
# Stop:
.\scripts\docker-down.ps1 -Prod
```

Test plan (local / smoke / rollback): [`docs/DOCKER_PRODUCTION.md`](docs/DOCKER_PRODUCTION.md#test-plan-production-docker).

### Production Docker deployment steps

#### 1. Prerequisites

- Docker Desktop / Engine with **Compose v2** on the host
- DNS + TLS for `api` / `admin` / `pos` (see [Prerequisites](#prerequisites) above)
- Real Fiskaly (or other vendor) credentials ready
- Cutover checklists reviewed: [`docs/RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](docs/RKSV_PRODUCTION_CUTOVER_CHECKLIST.md), [`docs/FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](docs/FINANZONLINE_PROD_CUTOVER_CHECKLIST.md), [`docs/TSE_PRODUCTION_CONFIG_LOCK.md`](docs/TSE_PRODUCTION_CONFIG_LOCK.md)

#### 2. Create `.env.production` from the template

```bash
# From repository root
copy .env.production.example .env.production
# macOS/Linux: cp .env.production.example .env.production
```

Minimum values to replace (template defaults are placeholders):

| Variable | Example | Notes |
|----------|---------|--------|
| `POSTGRES_USER` | `postgres` | DB role |
| `POSTGRES_PASSWORD` | *(strong secret)* | Never commit |
| `POSTGRES_DB` | `kasse_prod` | Production database name |
| `JWT_SECRET_KEY` | *(≥32 random chars)* | `JwtSettings__SecretKey` |
| `ADMIN_API_URL` | `https://api.regkasse.at` | Public API origin; **build-arg** for Admin/Sites |

Also set before going live:

- `FISKALY_API_KEY` / `FISKALY_API_SECRET` / `FISKALY_SCU_ID`
- `POS_API_URL=https://api.regkasse.at/api` (include `/api`)
- `ADMIN_PUBLIC_URL=https://admin.regkasse.at`
- Confirm `NEXT_PUBLIC_RKSV_ENVIRONMENT=PROD`
- Until FON cutover: consider `FINANZONLINE_MODE=Test` (still `UseSimulation=false` in Compose)

`.env.production` is gitignored (matches `.env.*`). Only the `.example` file is tracked.

#### 3. Build and start the API stack

```bash
docker compose -f docker-compose.prod.yml --env-file .env.production up -d --build
```

This starts **Postgres**, **Redis**, and **backend** with:

- `ASPNETCORE_ENVIRONMENT=Production`
- `Tse__TseMode=Device`, `Tse__Mode=Real`, vendor provider (default `fiskaly`)
- All `FinanzOnline__*__UseSimulation=false`
- Host ports bound to `127.0.0.1` by default (put nginx/Caddy/Traefik + TLS in front)

#### 4. Optional frontends (Compose profiles)

`NEXT_PUBLIC_*` / `EXPO_PUBLIC_*` are baked at **image build** — set them in `.env.production` **before** `--build`.

```bash
# Admin (FA) on :3000
docker compose -f docker-compose.prod.yml --env-file .env.production --profile admin up -d --build

# Tenant Sites on :3001
docker compose -f docker-compose.prod.yml --env-file .env.production --profile sites up -d --build

# POS static web on :8081
docker compose -f docker-compose.prod.yml --env-file .env.production --profile pos up -d --build

# All optional UIs:
docker compose -f docker-compose.prod.yml --env-file .env.production \
  --profile admin --profile sites --profile pos up -d --build
```

Or: `just docker-up-prod` / `make docker-up-prod` (API stack; add profiles manually as needed).

#### 5. Smoke checks

```bash
curl -fsS http://127.0.0.1:5184/api/health/live
curl -fsS http://127.0.0.1:5184/health/tse/mode
# After TLS proxy:
curl -fsS https://api.regkasse.at/api/health/live
```

Confirm fiscal lock is safe (Device/Real, not Demo/Fake). If the API exits immediately, check `docker compose -f docker-compose.prod.yml --env-file .env.production logs backend` for `TseProductionOptionsValidator` / missing Fiskaly secrets.

#### 6. Reverse proxy

Point `api.regkasse.at` → `127.0.0.1:5184`, `admin.regkasse.at` → `127.0.0.1:3000` (if `--profile admin`). Sample FA nginx: [`frontend-admin/nginx.conf`](frontend-admin/nginx.conf).

#### 7. Stop / update

```bash
# Stop (keep volumes)
docker compose -f docker-compose.prod.yml --env-file .env.production down

# Redeploy after code or .env.production changes (rebuild images)
docker compose -f docker-compose.prod.yml --env-file .env.production up -d --build

# Wipe DB/Redis volumes — irreversible
docker compose -f docker-compose.prod.yml --env-file .env.production down -v
```

### Prod Compose defaults

| Concern | Value |
|---------|--------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| TSE | `TseMode=Device`, `Mode=Real`, `Provider=fiskaly` (override via `TSE_PROVIDER`) |
| FON | Nested `UseSimulation=false`; `FinanzOnline__Mode` from `.env.production` |
| Ports | Bound to `127.0.0.1` by default — put TLS reverse proxy in front |
| Soft TSE | **Forbidden** — startup fail-closed (`TseProductionOptionsValidator`) |

This file is a **reference**, not a full cutover. Complete the RKSV / FinanzOnline / TSE lock docs before real fiscal traffic.

Local Soft TSE workflow: [`DEVELOPMENT.md`](DEVELOPMENT.md#docker-development-workflow).

---

## Environment Variables

### Backend (required / critical)

| Variable | Purpose |
|----------|---------|
| `ASPNETCORE_ENVIRONMENT` | `Development` \| `Staging` \| `Production` |
| `RELEASE_STAGE` | `dev` \| `staging` \| `canary` \| `production` (UI + promotion lane; see ENVIRONMENT_CONFIGURATION.md) |
| `Deployment__ReleaseStage` | Same as `RELEASE_STAGE` when set via config section |
| `Deployment__CanaryTenantIds` / `CanaryTenantSlugs` | Optional canary mandants on Production |
| `ASPNETCORE_URLS` | e.g. `http://+:8080` (container) |
| `ConnectionStrings__DefaultConnection` | PostgreSQL |
| `JwtSettings__SecretKey` | JWT signing (≥32 chars) |
| `JwtSettings__Issuer` / `JwtSettings__Audience` | Token validation |
| `Redis__Enabled` | `true` in Production (domain `ICacheService`) |
| `Redis__ConnectionString` | Distributed cache endpoint (required when enabled) |
| `Redis__InstanceName` | Key prefix (e.g. `Regkasse_Prod`) |
| `CacheSettings__LicenseCacheMinutes` (etc.) | Optional TTL overrides — see CONFIGURATION.md |
| `Cors__AllowedOrigins` | Explicit origins beyond `*.regkasse.at` HTTPS |
| `TwoFactorAuth__Enabled` | `true` in Staging/Production |
| `Security__Csrf__Enabled` | `true` in Staging/Production |
| `License__OfflineVerificationPublicKeyPem` (or file paths) | Offline license verify |
| `Backup__*` | Staging/archive roots, `ExecutionAdapterKind`, `pg_dump` path when using real backups |
| Fiskaly / TSE secrets | Via secure config — see `CONFIGURATION.md` |
| FinanzOnline | Credentials typically DB/company settings; cutover tokens per runbook |

#### Redis configuration (Production)

- [ ] Redis enabled: `Redis__Enabled=true`
- [ ] Redis connection string set: `Redis__ConnectionString` (reachable cluster/host)
- [ ] Redis instance name set: `Redis__InstanceName` (e.g. `Regkasse_Prod`)
- [ ] Redis HA / connection resilience reviewed (StackExchange.Redis defaults; prefer managed Redis with failover)
- [ ] Redis health check passing: `GET /api/health/ready` → `redisStatus` / `entries.cache` is `Healthy` (or `Degraded` only while investigating)
- [ ] Cache TTLs reviewed via `CacheSettings__*` for production workload (see `backend/CONFIGURATION.md`)
- [ ] After deploy: TTL tuning possible via env without code changes (restart API to rebind options)

Full map: [`backend/CONFIGURATION.md`](backend/CONFIGURATION.md) · [`docs/ENVIRONMENT_CONFIGURATION.md`](docs/ENVIRONMENT_CONFIGURATION.md).

### Frontend Admin (build-time)

| Variable | Purpose |
|----------|---------|
| `NEXT_PUBLIC_API_BASE_URL` | `https://api.regkasse.at` |
| `NEXT_PUBLIC_RKSV_ENVIRONMENT` | `PROD` or `TEST` (label / FO mode UI) |
| `NEXT_PUBLIC_RELEASE_STAGE` | `dev` \| `staging` \| `canary` \| `production` (banner fallback) |
| `NEXT_PUBLIC_TENANT_APP_BASE_DOMAIN` | Optional; default `regkasse.at` |
| `NEXT_PUBLIC_POS_APP_URL` | Optional POS deep link |
| `NEXT_PUBLIC_SENTRY_DSN` | Optional |

### Frontend POS (build-time)

| Variable | Purpose |
|----------|---------|
| `EXPO_PUBLIC_API_BASE_URL` | `https://api.regkasse.at/api` |
| `EXPO_PUBLIC_RELEASE_STAGE` | `dev` \| `staging` \| `canary` \| `production` (banner fallback) |
| `EXPO_PUBLIC_ADMIN_BASE_URL` | Optional license / FA links |
| `EXPO_PUBLIC_DEV_TENANT_ID` | **Dev only** — omit in production builds |

### Frontend Sites (if deployed)

| Variable | Purpose |
|----------|---------|
| `NEXT_PUBLIC_API_BASE_URL` | Shared API origin |

---

## Rollback

### Application binaries (API / FA / POS packages)

Ops scripts under [`scripts/`](scripts/README.md) (typical server layout `/var/www/regkasse`):

```bash
# Preflight (pg_dump, backup dirs, Production env hints)
./scripts/ops/preflight-production.sh

# Full API deploy (confirm required)
export REGKASSE_DEPLOY_CONFIRM=YES
export CONNECTION_STRING='…'
sudo -E ./scripts/ops/deploy-production.sh

# Before deploy — archive current release (excludes secrets by design)
sudo ./scripts/prepare-rollback-backup.sh

# After a bad release — restore last (or named) stamp
sudo REGKASSE_ROLLBACK_CONFIRM=YES ./scripts/rollback-production.sh
# or: sudo REGKASSE_ROLLBACK_CONFIRM=YES ./scripts/rollback-production.sh 20260719-120000
```

**Runbook:** [`docs/PRODUCTION_DEPLOYMENT_RUNBOOK.md`](docs/PRODUCTION_DEPLOYMENT_RUNBOOK.md) (Dev Fake vs Prod PgDump, config key corrections, post-checks).

- Restores **backend**, **frontend-admin**, and **frontend** package trees from `backup/<stamp>`.
- Does **not** roll back PostgreSQL / EF migrations (schema rollback is a separate, high-risk procedure).
- Helper: `./scripts/document-rollback.sh`

### Container / platform rollbacks

| Platform | Action |
|----------|--------|
| Docker / GHCR | Redeploy previous image tag / digest |
| Vercel | Promote previous Production deployment in the Vercel dashboard |
| EAS / stores | Ship previous build; store review may apply |

### Data / fiscal

- **Do not** use API “restore” against production DB (validation-only / drills only). See [`docs/BACKUP_AND_DISASTER_RECOVERY.md`](docs/BACKUP_AND_DISASTER_RECOVERY.md), [`docs/restore-boundary-notes.md`](docs/restore-boundary-notes.md).
- Database point-in-time recovery is an ops/DR procedure outside the app rollback scripts.

### Recommended order after rollback

1. Confirm API health (`/api/health/live`, `/api/health/ready`).
2. Confirm FA build matches API contract (or roll FA to matching tag).
3. Smoke login + one read-only admin page + POS health against `api.regkasse.at`.

---

## Related docs

| Doc | Topic |
|-----|--------|
| [`DEVELOPMENT.md`](DEVELOPMENT.md) | Local setup |
| [`docs/ENVIRONMENT_CONFIGURATION.md`](docs/ENVIRONMENT_CONFIGURATION.md) | Dev / Staging / Production / Canary + promotion |
| [`docs/ADMIN_FA_DEPLOY.md`](docs/ADMIN_FA_DEPLOY.md) | Coupled FA + API |
| [`frontend-admin/docs/CI_CD.md`](frontend-admin/docs/CI_CD.md) | FA CI/CD |
| [`frontend-admin/docs/DEPLOYMENT_BUILD_TIME_ENV.md`](frontend-admin/docs/DEPLOYMENT_BUILD_TIME_ENV.md) | `NEXT_PUBLIC_*` |
| [`docs/OFFLINE_PRODUCTION_DEPLOYMENT.md`](docs/OFFLINE_PRODUCTION_DEPLOYMENT.md) | Offline systems go-live |
| [`docs/FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](docs/FINANZONLINE_PROD_CUTOVER_CHECKLIST.md) | FO production cutover |
| [`.github/workflows/README.md`](.github/workflows/README.md) | CI inventory |
