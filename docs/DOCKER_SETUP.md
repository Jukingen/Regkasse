# Docker setup & migration plan — Regkasse

Complete guide to run Regkasse in Docker: what exists, how to migrate day-to-day work, and how to operate Dev vs Production-oriented stacks.

| Language | Doc |
|----------|-----|
| **English (this page)** | [`DOCKER_SETUP.md`](DOCKER_SETUP.md) |
| **Deutsch** | [`DOCKER_SETUP.de.md`](DOCKER_SETUP.de.md) |

**Hub:** [`DOCKER.md`](DOCKER.md) · Windows: [`DOCKER_WINDOWS_SETUP.md`](DOCKER_WINDOWS_SETUP.md) · Troubleshoot: [`DOCKER_WINDOWS_TROUBLESHOOTING.md`](DOCKER_WINDOWS_TROUBLESHOOTING.md)

**Last updated:** 2026-07-29

---

## 1. Migration status (checklist)

| Artifact | Path | Status |
|----------|------|--------|
| API image | [`backend/Dockerfile`](../backend/Dockerfile) | ✅ Done (multi-stage net10.0; root context) |
| Admin image | [`frontend-admin/Dockerfile`](../frontend-admin/Dockerfile) | ✅ Done (`NEXT_PUBLIC_*` build-args) |
| POS web image | [`frontend/Dockerfile`](../frontend/Dockerfile) | ✅ Done (Expo export → nginx; profile `pos`) |
| Sites image | [`frontend-sites/Dockerfile`](../frontend-sites/Dockerfile) | ✅ Done (profile `sites`) |
| Dev Compose | [`docker-compose.yml`](../docker-compose.yml) + [`docker-compose.override.yml`](../docker-compose.override.yml) | ✅ Done (Soft TSE override) |
| Infra-only Compose | [`docker-compose.dev.yml`](../docker-compose.dev.yml) | ✅ Done (Postgres + Redis) |
| Prod Compose | [`docker-compose.prod.yml`](../docker-compose.prod.yml) | ✅ Done (Device/Real TSE; limits + healthchecks) |
| Env templates | [`.env.example`](../.env.example) · [`.env.production.example`](../.env.production.example) · [`.env.production.local.example`](../.env.production.local.example) | ✅ Done |
| Prod ops docs | [`DOCKER_PRODUCTION.md`](DOCKER_PRODUCTION.md) · [`DOCKER_ENV_VARS.md`](DOCKER_ENV_VARS.md) | ✅ Done |
| Ignore files | `backend/`, `frontend-admin/`, `frontend/`, `frontend-sites/` `.dockerignore` (+ BuildKit `Dockerfile.dockerignore` where needed) | ✅ Done |
| Build / up / down / deploy | [`scripts/docker/docker-build.ps1`](../scripts/docker/docker-build.ps1) · [`docker-up.ps1`](../scripts/docker/docker-up.ps1) · [`docker-down.ps1`](../scripts/docker/docker-down.ps1) · [`docker-deploy.ps1`](../scripts/docker/docker-deploy.ps1) | ✅ Done |
| Local prod up/down | [`docker-up-prod.bat`](../docker-up-prod.bat) · [`docker-down-prod.bat`](../docker-down-prod.bat) | ✅ Done |
| Prod bat helpers | [`deploy-docker.bat`](../deploy-docker.bat) · [`docker-build-prod.bat`](../docker-build-prod.bat) · [`docker-push-prod.bat`](../docker-push-prod.bat) · [`docker-logs-prod.bat`](../docker-logs-prod.bat) | ✅ Done |
| Diagnose | [`scripts/docker/docker-diagnose.ps1`](../scripts/docker/docker-diagnose.ps1) | ✅ Done |

Ops detail: [`DOCKER_PRODUCTION.md`](DOCKER_PRODUCTION.md). Prefer `docker-up-prod.bat` for local prod-oriented testing.

---

## 2. Target architecture

```text
                    ┌─────────────────────────────────────────┐
  Host browser      │  localhost:3000 Admin / :8081 POS web   │
  (not Docker DNS)  │  localhost:3001 Sites / :5184 API       │
                    └───────────────┬─────────────────────────┘
                                    │ published ports
                    ┌───────────────▼─────────────────────────┐
                    │  Docker network: regkasse / regkasse-prod│
                    │  backend ↔ postgres ↔ redis              │
                    └─────────────────────────────────────────┘
```

| Mode | Compose | Fiscal |
|------|---------|--------|
| **A. Infra only** | `docker-compose.dev.yml` | Host `appsettings.Development` (Soft TSE OK) |
| **B. Full Dev** | `docker-compose.yml` + **override** | Soft TSE / FON simulation |
| **C. Prod-oriented** | `docker-compose.prod.yml` + `.env.production` | Device/Real — fail-closed |

Never merge `docker-compose.override.yml` with the production file.

---

## 3. Prerequisites

1. Docker Desktop (Compose v2) — Windows: [`DOCKER_WINDOWS_SETUP.md`](DOCKER_WINDOWS_SETUP.md)
2. Free ports: 5184, 5432, 6379, 3000 (optional 8081 / 3001)
3. Diagnose: `.\scripts\docker\docker-diagnose.ps1`

---

## 4. Quick start (Development)

```powershell
# From repository root
copy .env.example .env
# Set JWT_SECRET_KEY to ≥32 random characters

.\scripts\docker\docker-build.ps1 -Dev
.\scripts\docker\docker-up.ps1 -Build
# Optional POS + Sites:
.\scripts\docker\docker-up.ps1 -Profile pos,sites -Build

curl -fsS http://localhost:5184/api/health/live
.\scripts\docker\docker-down.ps1
```

Equivalent raw Compose:

```powershell
docker compose build
docker compose up -d
docker compose --profile pos --profile sites up -d --build
docker compose down
```

### Preferred coding workflow (hot reload)

```powershell
.\scripts\docker\docker-up.ps1   # stop full stack first if running: .\scripts\docker\docker-down.ps1
docker compose -f docker-compose.dev.yml up -d
npm run dev
```

---

## 5. Production-oriented deploy

```powershell
copy .env.production.example .env.production
# Fill POSTGRES_*, JWT_SECRET_KEY, ADMIN_API_URL, Fiskaly secrets

.\scripts\docker\docker-build-prod.ps1 -Profile admin
.\scripts\docker\docker-deploy.ps1 -Profile admin

# Or one-shot / Windows bats:
# deploy-docker.bat admin
.\scripts\docker\docker-deploy.ps1 -Profile admin,sites

.\scripts\docker\docker-down.ps1 -Prod
```

Step-by-step checklist: [`DOCKER_PRODUCTION.md`](DOCKER_PRODUCTION.md) · [`../DEPLOYMENT.md`](../DEPLOYMENT.md#docker-compose-production-oriented).  
Fiscal lock: [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md).  
Env reference: [`DOCKER_ENV_VARS.md`](DOCKER_ENV_VARS.md).

---

## 6. Scripts reference

| Script | Purpose |
|--------|---------|
| [`docker-build.ps1`](../scripts/docker/docker-build.ps1) | `compose build` for Dev and/or Prod |
| [`docker-up.ps1`](../scripts/docker/docker-up.ps1) | `compose up -d` (Dev default; `-Prod` optional) |
| [`docker-down.ps1`](../scripts/docker/docker-down.ps1) | `compose down` (Dev / Prod / `-All`; `-Volumes` destructive) |
| [`docker-deploy.ps1`](../scripts/docker/docker-deploy.ps1) | Prod build + up with confirmation |
| [`docker-diagnose.ps1`](../scripts/docker/docker-diagnose.ps1) | Windows Docker/WSL/ports health |

```powershell
.\scripts\docker\docker-build.ps1              # Dev + Prod images
.\scripts\docker\docker-build.ps1 -Dev         # Dev only
.\scripts\docker\docker-build.ps1 -Prod -NoCache

.\scripts\docker\docker-up.ps1                 # Dev detached
.\scripts\docker\docker-up.ps1 -Prod -Profile admin

.\scripts\docker\docker-down.ps1
.\scripts\docker\docker-down.ps1 -All
```

Also: `just docker-up` / `make docker-up` / `docker-up-dev` / `docker-up-prod`.

---

## 7. Per-service notes

### Backend

- Build: `docker build -f backend/Dockerfile -t regkasse-api:local .` (**repo root**)
- Needs `tools/LicenseGenerator.Core`
- Health: `/api/health/live` on container port **8080** → host **5184**

### Frontend-Admin

- Context: `./frontend-admin`
- Bake `NEXT_PUBLIC_API_BASE_URL` / `NEXT_PUBLIC_RKSV_ENVIRONMENT` at **build** time
- Runtime-only env will **not** fix an UNCONFIGURED RKSV badge

### Frontend (POS web)

- Optional profile `pos` — static web, not Metro/native
- Bake `EXPO_PUBLIC_*` at export time

### Frontend-Sites

- Optional profile `sites` — storefronts / online orders (non-fiscal)

---

## 8. Migration from host-only development

| Before | After |
|--------|--------|
| Local Postgres + `dotnet run` | Keep, or use `docker-compose.dev.yml` for DB/Redis |
| Everything on host | Optional full stack: `.\scripts\docker\docker-up.ps1` |
| Ad-hoc prod VM | `.\scripts\docker\docker-deploy.ps1` + reverse proxy (see DEPLOYMENT.md) |
| Soft TSE in “prod” containers | Forbidden — use `.env.production` and prod Compose only |

Rollback: `.\scripts\docker\docker-down.ps1` and return to `npm run dev` / local Postgres.

---

## 9. Related documentation

| Doc | Role |
|-----|------|
| [`DOCKER.md`](DOCKER.md) / [`DOCKER.de.md`](DOCKER.de.md) | Short hub |
| [`../DEVELOPMENT.md`](../DEVELOPMENT.md#docker-compose-full-stack) | Dev workflow A/B/C |
| [`../DEPLOYMENT.md`](../DEPLOYMENT.md#docker-compose-production-oriented) | Prod deploy steps |
| [`../README.md`](../README.md#docker-compose) | Root quick start |
| [`DOCKER_WINDOWS_TROUBLESHOOTING.md`](DOCKER_WINDOWS_TROUBLESHOOTING.md) | Windows issues |

---

## 10. Definition of done

- [ ] `.\scripts\docker\docker-diagnose.ps1` passes on the workstation
- [ ] `.\scripts\docker\docker-up.ps1 -Build` → API health live + Admin login reachable
- [ ] Soft TSE confirmed on Dev (`/api/rksv/environment` Development-friendly)
- [ ] Prod path uses `.env.production` only; override not loaded
- [ ] Team knows: browser → `localhost`, not Docker DNS `backend`
