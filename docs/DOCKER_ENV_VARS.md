# Docker environment variables reference — Regkasse

Variables used by [`.env.production.example`](../.env.production.example) / `.env.production` with [`docker-compose.prod.yml`](../docker-compose.prod.yml), plus related Dev templates.

**Last updated:** 2026-07-29

| Related | Link |
|---------|------|
| Production Docker guide | [`DOCKER_PRODUCTION.md`](DOCKER_PRODUCTION.md) |
| Broader env model | [`ENVIRONMENT_CONFIGURATION.md`](ENVIRONMENT_CONFIGURATION.md) |
| Backend config | [`../backend/CONFIGURATION.md`](../backend/CONFIGURATION.md) |
| FA build-time env | [`../frontend-admin/docs/DEPLOYMENT_BUILD_TIME_ENV.md`](../frontend-admin/docs/DEPLOYMENT_BUILD_TIME_ENV.md) |

---

## Secrets strategy

| Do | Don't |
|----|--------|
| Keep `.env.production` on the host only (gitignored via `.env.*`) | Commit real passwords / JWT / Fiskaly keys |
| Use GitHub **Environments** secrets for Actions deploy webhooks | Bake secrets into Docker images |
| Rotate DB password, JWT, and vendor keys independently | Reuse one secret for JWT + DB + TSE |
| Prefer vault / OS secret store for multi-host | Share `.env.production` over chat / email |

Compose injects secrets as **container environment variables**. For Swarm/Kubernetes, map the same keys from secret objects.

---

## Required (minimum for Compose)

| Variable | Example | Maps to / used by |
|----------|---------|-------------------|
| `POSTGRES_USER` | `postgres` | Postgres + API connection string |
| `POSTGRES_PASSWORD` | *(strong)* | Postgres + API connection string |
| `POSTGRES_DB` | `kasse_prod` | Postgres + API connection string |
| `JWT_SECRET_KEY` | ≥32 random chars | `JwtSettings__SecretKey` |
| `ADMIN_API_URL` | `https://api.regkasse.at` | **Build-arg** `NEXT_PUBLIC_API_BASE_URL` (Admin + Sites) |

Compose fails fast if required `${VAR:?…}` placeholders are missing.

---

## Image / release

| Variable | Default | Purpose |
|----------|---------|---------|
| `IMAGE_TAG` | `prod` | Local image tag (`regkasse-api:prod`, …) |
| `RELEASE_STAGE` | `production` | `RELEASE_STAGE` / `Deployment__ReleaseStage`; FA/POS banners |
| `DOTNET_RID` | `linux-x64` | Backend self-contained RID (`backend/Dockerfile`) |
| `DOCKER_REGISTRY` | _(empty)_ | Prefix for `docker-push-prod` (e.g. `ghcr.io/org`) |

---

## Ports & binds

All host binds default to **127.0.0.1** (TLS proxy on the same host).

| Variable | Default | Service |
|----------|---------|---------|
| `POSTGRES_PORT` / `POSTGRES_HOST_BIND` | `5432` / `127.0.0.1` | Postgres |
| `REDIS_PORT` / `REDIS_HOST_BIND` | `6379` / `127.0.0.1` | Redis |
| `API_PORT` / `API_HOST_BIND` | `5184` / `127.0.0.1` | Backend → container `8080` |
| `ADMIN_PORT` / `ADMIN_HOST_BIND` | `3000` / `127.0.0.1` | Profile `admin` |
| `SITES_PORT` / `SITES_HOST_BIND` | `3001` / `127.0.0.1` | Profile `sites` |
| `POS_PORT` / `POS_HOST_BIND` | `8081` / `127.0.0.1` | Profile `pos` |

---

## API / security (runtime)

| Variable | Default | Maps to |
|----------|---------|---------|
| `JWT_ISSUER` | `Regkasse` | `JwtSettings__Issuer` |
| `JWT_AUDIENCE` | `RegkasseClients` | `JwtSettings__Audience` |
| `REDIS_INSTANCE_NAME` | `Regkasse_Prod` | `Redis__InstanceName` |
| `TWO_FACTOR_ENABLED` | `true` | `TwoFactorAuth__Enabled` |
| `CSRF_ENABLED` | `true` | `Security__Csrf__Enabled` |

Hard-coded in prod Compose (not overridable via soft flags): Soft TSE off; all `FinanzOnline__*__UseSimulation=false`; `TwoFactorAuth__BypassInDevelopment=false`; `Security__Csrf__BypassInDevelopment=false`.

---

## Fiscal / TSE / FinanzOnline

| Variable | Default | Notes |
|----------|---------|-------|
| `TSE_PROVIDER` | `fiskaly` | `Tse__Provider` |
| `FISKALY_API_KEY` | _(empty)_ | Required for real signing |
| `FISKALY_API_SECRET` | _(empty)_ | Required for real signing |
| `FISKALY_API_BASE_URL` | `https://rksv.fiskaly.com/api/v1` | Vendor API |
| `FISKALY_SCU_ID` | _(empty)_ | Signature creation unit |
| `FINANZONLINE_MODE` | `Production` | Prefer `Test` until FON cutover |

Compose forces `Tse__TseMode=Device`, `Tse__Mode=Real`, `Tse__Environment=Production`. Startup **fails closed** if Production fiscal rules are violated — see [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md).

---

## Frontend build-time (public)

| Variable | Used by | Notes |
|----------|---------|-------|
| `ADMIN_API_URL` | Admin + Sites `NEXT_PUBLIC_API_BASE_URL` | Origin **without** trailing `/api` |
| `POS_API_URL` | POS `EXPO_PUBLIC_API_BASE_URL` | Must include `/api` |
| `ADMIN_PUBLIC_URL` | POS `EXPO_PUBLIC_ADMIN_BASE_URL` | Optional |
| `NEXT_PUBLIC_RKSV_ENVIRONMENT` | Admin | Must be `PROD` (or `TEST`) for production builds |
| `NEXT_PUBLIC_SENTRY_*` | Admin | Optional monitoring |

Rebuild images after changing these values.

---

## Resource limits & logging

Optional overrides (Compose `deploy.resources` + logging):

| Variable | Example | Applies to |
|----------|---------|------------|
| `POSTGRES_CPU_LIMIT` / `POSTGRES_MEMORY_LIMIT` | `2.0` / `2G` | Postgres |
| `REDIS_CPU_LIMIT` / `REDIS_MEMORY_LIMIT` | `1.0` / `512M` | Redis |
| `API_CPU_LIMIT` / `API_MEMORY_LIMIT` | `2.0` / `2G` | Backend |
| `ADMIN_*` / `SITES_*` / `POS_*` | see example file | Frontends |
| `*_CPU_RESERVE` / `*_MEMORY_RESERVE` | — | Reservations |
| `LOG_MAX_SIZE` / `LOG_MAX_FILES` | `10m` / `5` | json-file rotation |

---

## Development template (not production)

[`.env.example`](../.env.example) feeds **Dev** Compose (`docker-compose.yml` + override with Soft TSE). Do not copy Dev fiscal defaults into `.env.production`.

---

## Checklist before first prod-oriented up

- [ ] `.env.production` filled; not in git  
- [ ] `JWT_SECRET_KEY` ≥ 32 characters  
- [ ] `ADMIN_API_URL` / `POS_API_URL` match public DNS  
- [ ] Fiskaly secrets set (or accept fiscal readiness failure until then)  
- [ ] `FINANZONLINE_MODE` aligned with cutover status  
- [ ] TLS reverse proxy planned for loopback ports  
- [ ] Backup strategy for `regkasse_prod_pgdata` + app-level backups  
