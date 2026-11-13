# Production Docker guide — Regkasse

Self-hosted **production-oriented** Docker stack: Device/Real TSE (fail-closed), Postgres + Redis volumes, health checks, resource limits, and operator scripts.

**Last updated:** 2026-07-29

| Related | Link |
|---------|------|
| Beginners | [`DOCKER_FOR_BEGINNERS.md`](DOCKER_FOR_BEGINNERS.md) |
| Hub | [`DOCKER.md`](DOCKER.md) |
| Local verification | [`DOCKER_TEST_PLAN.md`](DOCKER_TEST_PLAN.md) |
| **Production readiness gate** | [`DOCKER_PRODUCTION_READINESS.md`](DOCKER_PRODUCTION_READINESS.md) |
| Setup / Dev vs Prod | [`DOCKER_SETUP.md`](DOCKER_SETUP.md) |
| Deployment (incl. GH Actions) | [`../DEPLOYMENT.md`](../DEPLOYMENT.md) |
| TSE lock | [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) |
| Next steps | [`NEXT_STEPS_AFTER_SCRIPTS.md`](NEXT_STEPS_AFTER_SCRIPTS.md) |

---

## What this is (and is not)

| Is | Is not |
|----|--------|
| Reference Compose for a **staging / single-host** production-style stack | A substitute for cutover checklists |
| Fail-closed fiscal config (`TseMode=Device`, `Mode=Real`) | Soft TSE / FON simulation (those stay in `docker-compose.override.yml`) |
| Loopback-bound ports + TLS reverse proxy in front | Full multi-AZ HA / managed Postgres |

Never merge `docker-compose.override.yml` with `docker-compose.prod.yml`.

---

## Architecture

```text
  Internet
      │
      ▼
  TLS reverse proxy (nginx / Caddy / Traefik)
      │  api. / admin. / pos.regkasse.at
      ▼
  127.0.0.1 published ports
      ├── :5184 → backend:8080
      ├── :3000 → frontend-admin (profile admin)
      ├── :3001 → frontend-sites (profile sites)
      └── :8081 → frontend POS nginx (profile pos)
      │
      └── Docker network regkasse-prod
            backend ↔ postgres ↔ redis
```

Persistent volumes: `regkasse_prod_pgdata`, `regkasse_prod_redis`.

---

## Prerequisites

1. Docker Engine / Desktop with **Compose v2**
2. Copy env template and fill secrets:
   ```bat
   copy .env.production.local.example .env.production
   ```
   (Cloud hosts: use `.env.production.example` with public HTTPS URLs.)
3. Real Fiskaly (or other) credentials before expecting fiscal health
4. Diagnose: `.\scripts\docker-diagnose.ps1`

---

## Quick start

```bat
REM From repository root — local production-oriented stack (all UIs)
copy .env.production.local.example .env.production
REM Edit: POSTGRES_PASSWORD, JWT_SECRET_KEY (≥32), FISKALY_* (required for API)

docker-up-prod.bat

REM Smoke
curl -fsS http://127.0.0.1:5184/api/health/live

REM Stop (keep DB volume)
docker-down-prod.bat
```

PowerShell:

```powershell
copy .env.production.local.example .env.production
.\scripts\docker-up-prod.ps1
.\scripts\docker-down-prod.ps1
# API + DB + Redis only:
.\scripts\docker-up-prod.ps1 -ApiOnly
```

| Script | Purpose |
|--------|---------|
| [`docker-up-prod.bat`](../docker-up-prod.bat) | Build + start full prod Compose (admin/sites/pos) |
| [`docker-down-prod.bat`](../docker-down-prod.bat) | Stop stack |
| [`docker-build-prod.bat`](../docker-build-prod.bat) | Build images only |
| [`docker-logs-prod.bat`](../docker-logs-prod.bat) | Tail logs |
| [`deploy-docker.bat`](../deploy-docker.bat) | Deploy helper (confirm + optional profiles) |

Soft TSE / FON simulation stay **off**. Missing Fiskaly secrets → API exits (`docker-logs-prod.bat backend`).

---

## Operator scripts

| Script | Purpose |
|--------|---------|
| [`docker-up-prod.bat`](../docker-up-prod.bat) / [`scripts/docker-up-prod.ps1`](../scripts/docker-up-prod.ps1) | Local full stack (default profiles) |
| [`docker-down-prod.bat`](../docker-down-prod.bat) / [`scripts/docker-down-prod.ps1`](../scripts/docker-down-prod.ps1) | Stop prod Compose |
| [`deploy-docker.bat`](../deploy-docker.bat) / [`scripts/docker-deploy.ps1`](../scripts/docker-deploy.ps1) | Confirm + build + `up -d` (prod Compose) |
| [`docker-build-prod.bat`](../docker-build-prod.bat) / [`scripts/docker-build-prod.ps1`](../scripts/docker-build-prod.ps1) | Build images only |
| [`docker-push-prod.bat`](../docker-push-prod.bat) / [`scripts/docker-push-prod.ps1`](../scripts/docker-push-prod.ps1) | Tag + push to `DOCKER_REGISTRY` |
| [`docker-logs-prod.bat`](../docker-logs-prod.bat) / [`scripts/docker-logs-prod.ps1`](../scripts/docker-logs-prod.ps1) | Tail prod logs |
| [`deploy.bat`](../deploy.bat) | Heavier path: smoke → backup confirm → compose up |

**Host Compose vs GitHub Actions:** use these scripts for a **VM / Docker host**. Cloud promotion (Staging → Canary → Production) stays in [`.github/workflows/deploy-production.yml`](../.github/workflows/deploy-production.yml) — see [`DEPLOYMENT.md`](../DEPLOYMENT.md).

---

## Images (optimized)

| Service | Dockerfile | Notes |
|---------|------------|--------|
| API | [`backend/Dockerfile`](../backend/Dockerfile) | Multi-stage; **self-contained** publish (`linux-x64`); healthcheck `/api/health/live` |
| Admin | [`frontend-admin/Dockerfile`](../frontend-admin/Dockerfile) | Multi-stage; runtime `npm ci --omit=dev`; healthcheck `/login` |
| Sites | [`frontend-sites/Dockerfile`](../frontend-sites/Dockerfile) | Same pattern as Admin |
| POS web | [`frontend/Dockerfile`](../frontend/Dockerfile) | Expo export → **nginx**; healthcheck `/` |

`NEXT_PUBLIC_*` / `EXPO_PUBLIC_*` are **build-time** — set them in `.env.production` before `--build`.

---

## Health checks & monitoring

| Layer | What |
|-------|------|
| Dockerfile `HEALTHCHECK` | Per-image (API `/api/health/live`, FA/Sites `/health`, POS `/healthz`) |
| Compose `healthcheck` | Postgres, Redis, backend, admin, sites, pos |
| Observability stack | Optional [`monitoring/docker-compose.monitoring.yml`](../monitoring/docker-compose.monitoring.yml) — see [`MONITORING.md`](MONITORING.md) |
| Log rotation | `json-file` `max-size` / `max-file` (see `.env.production.example`) |
| Resource limits | `deploy.resources.limits` per service (overridable via env) |

Smoke after deploy:

```bash
curl -fsS http://127.0.0.1:5184/api/health/live
curl -fsS http://127.0.0.1:5184/health/tse/mode
# Optional authenticated smoke:
# scripts/smoke-test.sh — see docs/DEPLOYMENT_SMOKE_TEST.md
```

If the API exits immediately, check logs for `TseProductionOptionsValidator` / missing Fiskaly secrets:

```bat
docker-logs-prod.bat backend
```

---

## Secrets management

1. **Never** commit `.env.production` (gitignored via `.env.*`).
2. Host file or inject via Docker/Swarm/Kubernetes secrets / CI Environments.
3. Separate concerns: DB password ≠ JWT secret ≠ Fiskaly API secret.
4. After JWT rotation, all sessions must re-login.
5. CI deploy webhooks use GitHub Environment secrets — not this Compose file.

Full variable list: [`DOCKER_ENV_VARS.md`](DOCKER_ENV_VARS.md).

---

## Test plan (production Docker)

### 1. Local production Docker test

| Step | Pass criteria |
|------|----------------|
| `docker info` OK | Engine reachable |
| Copy `.env.production.example` → `.env.production` with test secrets | File present, not committed |
| `docker-build-prod.bat` (API only) | Images build |
| `deploy-docker.bat` | `postgres`, `redis`, `backend` healthy |
| `curl …/api/health/live` | HTTP 200 |
| Optional `--profile admin` | `/login` reachable on `:3000` |

Use **non-prod** Fiskaly/FON credentials or expect fiscal readiness to fail until vendor secrets are set. Prefer a staging VM, not the live fiscal host, for first dry-run.

### 2. Smoke tests on production Docker

| Check | Command / action |
|-------|------------------|
| Liveness | `GET /api/health/live` |
| TSE mode lock | `GET /health/tse/mode` — Device/Real, not Demo/Fake |
| Admin login | Browser → `admin` profile URL (behind TLS in real ops) |
| RKSV status read | Authenticated `GET /api/rksv/environment` (no fiscal **write** in dry-run) |
| Optional | [`scripts/smoke-test.sh`](../scripts/smoke-test.sh) / [`docs/DEPLOYMENT_SMOKE_TEST.md`](DEPLOYMENT_SMOKE_TEST.md) |

### 3. Performance tests

| Check | Notes |
|-------|--------|
| Resource limits | Confirm containers stay under Compose `memory`/`cpus` under load |
| Log growth | Confirm `LOG_MAX_SIZE` rotation |
| DB volume | Backup size / disk free before soak |
| Cold start | Measure backend `start_period` (90s) vs ready |

Not a substitute for dedicated load tests; use for host sizing.

### 4. Rollback tests

| Scenario | Action |
|----------|--------|
| Bad image on host Compose | Redeploy previous `IMAGE_TAG`; or `docker compose … up -d` with prior tag |
| Local git mistake | [`rollback.bat`](../rollback.bat) = **git** reset — not container rollback |
| Cloud CD | Actions rollback webhook / previous GHCR tag ([`DEPLOYMENT.md`](../DEPLOYMENT.md)) |
| Smoke fail after `deploy.bat` | Follow that script’s rollback prompt |

Practice once on staging: deploy tag `A` → tag `B` → revert to `A` → health OK.

### Exit criteria (staging verification)

- [ ] Prod Compose runs with locked TSE settings  
- [ ] Pre-deploy backup noted (FA/API)  
- [ ] Health + Admin smoke pass  
- [ ] Rollback path documented for the team  
- [ ] Announce: staging Docker prod path verified  

---

## Backup implication

Postgres data lives in Docker volume `regkasse_prod_pgdata`. Prefer Regkasse **System/Tenant backup** APIs (FA) before host upgrades; volume snapshots alone are not RKSV-aware application backups. See [`BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md).
