# Docker — Regkasse (English)

Hub for local and production-oriented Docker usage in this monorepo.

| Language | Doc |
|----------|-----|
| **English (this page)** | [`DOCKER.md`](DOCKER.md) |
| **Deutsch** | [`DOCKER.de.md`](DOCKER.de.md) |

**Last updated:** 2026-07-29

---

## Quick map

| Goal | Command / file |
|------|----------------|
| **Docker for beginners** | [`DOCKER_FOR_BEGINNERS.md`](DOCKER_FOR_BEGINNERS.md) |
| **Docker test plan (local)** | [`DOCKER_TEST_PLAN.md`](DOCKER_TEST_PLAN.md) |
| **Production readiness + migration** | [`DOCKER_PRODUCTION_READINESS.md`](DOCKER_PRODUCTION_READINESS.md) |
| **Setup & migration plan** | [`DOCKER_SETUP.md`](DOCKER_SETUP.md) ([DE](DOCKER_SETUP.de.md)) |
| **Production Docker (ops)** | [`DOCKER_PRODUCTION.md`](DOCKER_PRODUCTION.md) |
| **Prod env vars** | [`DOCKER_ENV_VARS.md`](DOCKER_ENV_VARS.md) |
| Install Docker Desktop (Windows) | [`DOCKER_WINDOWS_SETUP.md`](DOCKER_WINDOWS_SETUP.md) |
| Fix Docker on Windows | [`DOCKER_WINDOWS_TROUBLESHOOTING.md`](DOCKER_WINDOWS_TROUBLESHOOTING.md) · `.\scripts\docker-diagnose.ps1` |
| Build images | `.\scripts\docker-build.ps1` · `docker-build-prod.bat` |
| Start Dev / stop | `.\scripts\docker-up.ps1` · `.\scripts\docker-down.ps1` |
| Deploy prod-oriented | `.\scripts\docker-deploy.ps1` · `deploy-docker.bat` |
| **Local prod full stack** | `docker-up-prod.bat` / `docker-down-prod.bat` ([`DOCKER_PRODUCTION.md`](DOCKER_PRODUCTION.md)) |
| Push / logs (prod) | `docker-push-prod.bat` · `docker-logs-prod.bat` |
| Full Dev stack (Soft TSE) | `docker compose up --build` → [`docker-compose.yml`](../docker-compose.yml) + override |
| Infra only (host apps) | `docker compose -f docker-compose.dev.yml up -d` |
| Production-oriented | `docker compose -f docker-compose.prod.yml --env-file .env.production up -d --build` |
| Observability stack | `docker compose -f monitoring/docker-compose.monitoring.yml up -d` · [`MONITORING.md`](MONITORING.md) |
| Dev workflow detail | [`../DEVELOPMENT.md`](../DEVELOPMENT.md#docker-compose-full-stack) |
| Prod deploy steps | [`../DEPLOYMENT.md`](../DEPLOYMENT.md#docker-compose-production-oriented) |
| Env templates | [`.env.example`](../.env.example) · [`.env.production.example`](../.env.production.example) |

---

## Compose files

| File | Purpose |
|------|---------|
| [`docker-compose.yml`](../docker-compose.yml) | Postgres, Redis, API, Admin; optional profiles `pos` / `sites` |
| [`docker-compose.override.yml`](../docker-compose.override.yml) | Auto-merged on `docker compose up` — Soft TSE (`Demo`/`Fake`) + FON simulation |
| [`docker-compose.dev.yml`](../docker-compose.dev.yml) | Postgres + Redis only (hot reload on host via `npm run dev`) |
| [`docker-compose.prod.yml`](../docker-compose.prod.yml) | Production host — Device/Real TSE; **do not** merge override |

### Dockerfiles

| Service | Path | Notes |
|---------|------|--------|
| API | [`backend/Dockerfile`](../backend/Dockerfile) | Build context = **repo root** |
| Admin | [`frontend-admin/Dockerfile`](../frontend-admin/Dockerfile) | `NEXT_PUBLIC_*` at **build** |
| POS web | [`frontend/Dockerfile`](../frontend/Dockerfile) | Expo export → nginx; profile `pos` |
| Sites | [`frontend-sites/Dockerfile`](../frontend-sites/Dockerfile) | Profile `sites` |

---

## Development modes

### A — Infra in Docker, apps on host (recommended for coding)

```bash
docker compose -f docker-compose.dev.yml up -d
npm run dev
```

Point API at `localhost:5432` and Redis `localhost:6379`.

### B — Full stack in Docker (Soft TSE)

```bash
cp .env.example .env          # Windows: copy .env.example .env
# Set JWT_SECRET_KEY ≥ 32 characters
docker compose up --build
```

Override enables Development fiscal defaults. Details: [`DEVELOPMENT.md`](../DEVELOPMENT.md#docker-development-workflow).

### C — Production-oriented (no Soft TSE)

```bash
cp .env.production.example .env.production
docker compose -f docker-compose.prod.yml --env-file .env.production up -d --build
```

Fail-closed if Soft TSE / FON simulation leaks into Production. See [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md).

---

## URLs (defaults)

| URL | Service |
|-----|---------|
| http://localhost:5184 | API |
| http://localhost:3000 | Admin |
| http://localhost:8081 | POS web (`--profile pos`) |
| http://localhost:3001 | Sites (`--profile sites`) |
| localhost:5432 / 6379 | Postgres / Redis |

Browser clients must call **`localhost`**, not the Docker DNS name `backend`.

---

## Make / Just

| Target | Action |
|--------|--------|
| `just docker-up` / `make docker-up` | Full Dev stack (+ override) |
| `just docker-up-dev` | Infra only |
| `just docker-up-prod` | Requires `.env.production` |
| `just docker-up-pos` | Dev stack + POS profile |
| `just docker-down` | Stop stacks |

---

## Related

- Root overview: [`../README.md`](../README.md#docker-compose)
- Windows DE: [`DOCKER_WINDOWS_SETUP.de.md`](DOCKER_WINDOWS_SETUP.de.md) · [`DOCKER_WINDOWS_TROUBLESHOOTING.de.md`](DOCKER_WINDOWS_TROUBLESHOOTING.de.md)
