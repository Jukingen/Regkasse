# Regkasse

RKSV-compliant multi-tenant POS platform for Austrian cash registers (Registrierkassen).

| Surface | Local | Production |
|---------|-------|------------|
| POS | http://localhost:8081 | https://pos.regkasse.at |
| Admin (FA) | http://localhost:3000 | https://admin.regkasse.at |
| Tenant sites | http://localhost:3001 | `/[slug]` storefronts (+ optional custom domains) |
| API | http://localhost:5184 | https://api.regkasse.at |

---

## Project Overview

Regkasse is an npm-workspace monorepo for:

- **POS** — cashier operations (cart, payment, receipts, offline queues); UI copy in **German (de-DE)**
- **Admin (FA)** — Mandanten-Admin and Super Admin back office (users, RKSV, backup, billing, digital services); **i18n de/en/tr**
- **Tenant websites** — shared Next.js storefronts and online-order intake (`frontend-sites`; not fiscal POS)
- **API** — ASP.NET Core multi-tenant backend with RKSV/TSE, FinanzOnline outbox, backup/DR, and licensing

**Single POS UI:** production POS is one shared host (`pos.regkasse.at`); tenant comes from JWT `tenant_id` after login — not `{slug}.regkasse.at` as the POS entry point. See [`docs/POS_PRODUCTION_ARCHITECTURE.md`](docs/POS_PRODUCTION_ARCHITECTURE.md).

**Boundaries:** POS → `/api/pos/*`; Admin → `/api/admin/*`; Sites → `/api/public/*` + `/api/sites/*`. Cross-tenant access returns **HTTP 404**.

**Windows?** Prefer [`scripts/dev/start.bat`](scripts/dev/start.bat) (Legacy or Docker) — [`docs/DOCKER_VS_LEGACY.md`](docs/DOCKER_VS_LEGACY.md) · [`docs/GETTING_STARTED_SCRIPTS.md`](docs/GETTING_STARTED_SCRIPTS.md) · [`docs/SCRIPTS_REFERENCE.md`](docs/SCRIPTS_REFERENCE.md).

---

## Quick Start

### Prerequisites

| Tool | Notes |
|------|-------|
| Node.js | **20+** LTS |
| npm | Workspaces enabled at repo root |
| .NET SDK | **10.x** |
| PostgreSQL | **16+** (local or Docker) |
| Optional | Redis (`scripts/start-redis-dev.ps1`), Expo Go / Android Studio |

### Install

```bash
git clone <repo-url> Regkasse
cd Regkasse
npm install   # JS workspaces + Husky prepare

# Backend config (first time) — see backend/README.md
cd backend
copy appsettings.example.json appsettings.json
copy appsettings.Development.example.json appsettings.Development.json
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=kasse_db;Username=postgres;Password=YOUR_PASSWORD"
dotnet user-secrets set "JwtSettings:SecretKey" "YOUR_RANDOM_KEY_AT_LEAST_32_CHARS"
cd ..
```

### Run everything

```bash
npm run dev                 # parallel: API + POS + Admin + Sites
```

**Windows modes** (pick one):

| Mode | How | When |
|------|-----|------|
| **Chooser** | `scripts\dev\start.bat` | Preferred entry — Legacy or Docker |
| **Legacy** | `scripts\legacy\start-all.bat` | Daily DX; host Node/.NET/Postgres/Redis |
| **Docker** | `scripts\docker\host\up.bat` | Prod-like stack; no local SDK install |
| **npm** | `scripts\dev\start-dev.bat` | One terminal, all workspaces |

Comparison + rollback: [`docs/DOCKER_VS_LEGACY.md`](docs/DOCKER_VS_LEGACY.md). Setup: [`docs/DOCKER_SETUP.md`](docs/DOCKER_SETUP.md) · [`DEVELOPMENT.md`](DEVELOPMENT.md#docker-compose-full-stack) · Windows: [`docs/DOCKER_WINDOWS_SETUP.md`](docs/DOCKER_WINDOWS_SETUP.md).

**Docker (optional advanced):** `just docker-up` / `.\scripts\docker\docker-up.ps1` — see [Scripts](#scripts-windows).

### Run each project

| Project | Command | URL |
|---------|---------|-----|
| API | `npm run dev:backend` | http://localhost:5184 |
| Admin | `npm run dev:admin` | http://localhost:3000 |
| POS | `npm run dev:pos` | http://localhost:8081 (Expo) |
| Sites | `npm run dev:sites` | http://localhost:3001 |

Or from a package directory (`cd frontend-admin && npm run dev`). Workspace scripts: `dev`, `build`, `test`, `lint`, `typecheck` (where present).

### Dev tenant

In **Development** only: `X-Tenant-Id: dev` or `?tenant=dev`. Production authenticated traffic uses JWT `tenant_id`.

```bash
curl -H "X-Tenant-Id: dev" http://localhost:5184/api/health
```

### Development vs Production (fiscal)

| | Development | Production |
|---|-------------|------------|
| Soft TSE / FON simulation | Allowed | Fail-closed at startup |
| Defaults | `appsettings.Development.example.json` | `appsettings.Staging.example.json` / `appsettings.Production.example.json` |

Details and cutover: [`docs/ENVIRONMENT_CONFIGURATION.md`](docs/ENVIRONMENT_CONFIGURATION.md) · [`docs/RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](docs/RKSV_PRODUCTION_CUTOVER_CHECKLIST.md).

Full setup: [`CONTRIBUTING.md`](CONTRIBUTING.md).

---

## Getting Started with Scripts (Windows)

> **TL;DR:** Prefer [`scripts/dev/start.bat`](scripts/dev/start.bat) — choose **Legacy** (host processes) or **Docker** (Compose). Comparison: [`docs/DOCKER_VS_LEGACY.md`](docs/DOCKER_VS_LEGACY.md).

**Guide:** [`docs/GETTING_STARTED_SCRIPTS.md`](docs/GETTING_STARTED_SCRIPTS.md) · full catalog [`docs/SCRIPTS_REFERENCE.md`](docs/SCRIPTS_REFERENCE.md)

```batch
npm install
scripts\dev\start.bat
REM [1] Legacy  → scripts\legacy\start-all.bat
REM [2] Docker  → scripts\docker\host\up.bat
REM [3] Exit
```

| Task | Script |
|------|--------|
| Choose mode | `scripts\dev\start.bat` |
| Legacy (all windows) | `scripts\legacy\start-all.bat` |
| Docker up / down / status | `scripts\docker\host\up.bat` / `down.bat` / `status.bat` |
| npm one-terminal stack | `scripts\dev\start-dev.bat` |
| Run tests | `scripts\test\test-all.bat` |

**Rollback:** If Docker Desktop is missing or Compose fails, use Legacy mode (`scripts\dev\start.bat` → `[1]`). Logs for both modes: `C:\Scripts\logs`.

More: [Scripts (Windows)](#scripts-windows) · [`docs/SCRIPTS_QUICK_REF.md`](docs/SCRIPTS_QUICK_REF.md)

---

## Scripts (Windows)

**Start here:** [`docs/GETTING_STARTED_SCRIPTS.md`](docs/GETTING_STARTED_SCRIPTS.md) · **Legacy vs Docker:** [`docs/DOCKER_VS_LEGACY.md`](docs/DOCKER_VS_LEGACY.md)

Double-click helpers for common tasks. Full catalog: [`docs/SCRIPTS_REFERENCE.md`](docs/SCRIPTS_REFERENCE.md) · map: [`docs/SCRIPTS_ECOSYSTEM.md`](docs/SCRIPTS_ECOSYSTEM.md) · card: [`docs/SCRIPTS_QUICK_REF.md`](docs/SCRIPTS_QUICK_REF.md) · folder: [`scripts/README.md`](scripts/README.md)

### Mode chooser

| Script | Description |
|--------|-------------|
| `scripts\dev\start.bat` | Menu: Legacy Mode / Docker Mode / Exit |

### Legacy Mode (`scripts/legacy/`)

Host processes (separate windows). Needs Node, .NET, Postgres, Redis on the machine. Logs → `C:\Scripts\logs`.

| Script | Description |
|--------|-------------|
| `scripts\legacy\start-all.bat` | Redis + Backend + POS + Admin |
| `scripts\legacy\start-backend.bat` / `start-frontend.bat` / `start-frontend-admin.bat` / `start-redis.bat` | Single surface |
| `scripts\legacy\kill-ports.bat` | Free `:5184` / `:8081` / `:3000` / `:6379` |

`C:\Scripts\*.bat` shortcuts redirect here for compatibility.

### Everyday (npm workspaces)

| Script | Description |
|--------|-------------|
| `scripts\dev\start-dev.bat` | Start API + Admin + POS + Sites (`npm run dev`) in one terminal |
| `scripts\dev\start-backend.bat` / `start-admin.bat` / `start-pos.bat` / `start-sites.bat` | Single surface (`:5184` / `:3000` / `:8081` / `:3001`) |
| `scripts\test\test-all.bat` | Backend → Admin → POS tests (sequential; stops on first failure) |
| `scripts\dev\clean-all.DANGER.bat` | Confirm + remove build artifacts (`bin` / `obj` / `.next` / `.expo` / …) |

### Docker Mode (`scripts/docker/`)

Compose stack. Host bats under `scripts/docker/host/`; PowerShell under `scripts/docker/`. Logs → `C:\Scripts\logs`.

| Script | Description |
|--------|-------------|
| `scripts\docker\host\up.bat` / `down.bat` / `status.bat` / `logs.bat` | Compose start / stop / status / follow logs |
| `scripts\docker\host\clean.DANGER.bat` | Wipe Compose volumes + prune (**destructive**) |
| `scripts\docker\host\up-backend.bat` / `up-admin.bat` / `up-pos.bat` | Partial stacks |
| `scripts\docker\docker-build.ps1` / `docker-up.ps1` / `docker-down.ps1` / `docker-deploy.ps1` | Build Dev/Prod · up · down · prod deploy ([`docs/DOCKER_SETUP.md`](docs/DOCKER_SETUP.md)) |
| `scripts\docker\docker-diagnose.ps1` | Windows Docker/WSL/ports diagnose |

### Deploy & checks

| Script | Description |
|--------|-------------|
| `deploy.bat` | Prod Compose deploy (`docker-compose.prod.yml`; confirm + smoke + backup gate) |
| `rollback.bat` | `git reset --hard HEAD~1` + prod Compose rebuild (**destructive**; prefer `git revert` on shared branches) |
| `scripts\smoke-test.bat` | Lightweight curl smoke (API / Admin / POS) |
| `scripts\run-comprehensive-smoke.bat` | Full HTTP / FA / RKSV smoke suite |

Regenerate missing `.ps1` wrappers: `scripts\create-bat-wrappers.bat`. Validate pairing + docs: `npm run validate:scripts`.

---

## Docker Compose

Run PostgreSQL, Redis, API, and Admin in containers (optional POS web + Sites profiles). Prefer this when you want a full stack without installing .NET/Postgres locally.

### Layout

```text
Regkasse/
├── backend/Dockerfile              # ASP.NET Core API (net10.0) — build context: repo root
├── frontend/Dockerfile             # Expo POS → static web + nginx (Compose profile: pos)
├── frontend-admin/Dockerfile       # Next.js Admin
├── frontend-sites/Dockerfile       # Next.js tenant sites (Compose profile: sites)
├── docker-compose.yml              # Full local stack (API + Admin + DB)
├── docker-compose.override.yml     # Auto Soft TSE / FON simulation (Dev)
├── docker-compose.dev.yml          # Infra only (Postgres + Redis) for host apps
├── docker-compose.prod.yml         # Production-oriented (Device/Real TSE)
├── monitoring/                     # Optional Prometheus/Grafana/Loki stack
├── .env.example                    # Dev Compose env
├── .env.production.example         # Prod Compose env template
└── .env.production.local.example   # Localhost prod-like tests (copy → .env.production.local)
```

**Guides:** [`docs/DOCKER.md`](docs/DOCKER.md) · beginners [`docs/DOCKER_FOR_BEGINNERS.md`](docs/DOCKER_FOR_BEGINNERS.md) · test plan [`docs/DOCKER_TEST_PLAN.md`](docs/DOCKER_TEST_PLAN.md) · prod readiness [`docs/DOCKER_PRODUCTION_READINESS.md`](docs/DOCKER_PRODUCTION_READINESS.md) · monitoring [`docs/MONITORING.md`](docs/MONITORING.md) · CI/CD [`docs/CI_CD.md`](docs/CI_CD.md).

**Windows prod-like stack:** `scripts\docker\docker-up-prod.bat` (uses `docker-compose.prod.yml`; Soft TSE is **not** loaded). Dev Soft TSE: `scripts\docker\host\up.bat` / `scripts\dev\start.bat` → Docker.

Postgres and Redis are **Compose services** (`postgres:16-alpine`, `redis:7-alpine`), not folders under the repo.

| Image / service | Dockerfile | Host port (default) | Notes |
|-----------------|------------|---------------------|--------|
| `backend` | [`backend/Dockerfile`](backend/Dockerfile) | **5184** → 8080 | Multi-stage SDK → aspnet; needs `LicenseGenerator.Core` (root context) |
| `frontend-admin` | [`frontend-admin/Dockerfile`](frontend-admin/Dockerfile) | **3000** | `NEXT_PUBLIC_*` baked at **build** |
| `frontend` | [`frontend/Dockerfile`](frontend/Dockerfile) | **8081** | Profile `pos` — Expo `export --platform web` → nginx |
| `frontend-sites` | [`frontend-sites/Dockerfile`](frontend-sites/Dockerfile) | **3001** | Profile `sites` — storefronts / online orders |
| `postgres` | image only | **5432** | Volume `regkasse_pgdata` |
| `redis` | image only | **6379** | Volume `regkasse_redis` |

### Prerequisites

- Docker Desktop (or compatible engine) with **Compose v2**
- Windows: enable WSL 2 — [`docs/DOCKER_WINDOWS_SETUP.md`](docs/DOCKER_WINDOWS_SETUP.md)
- Free ports: 5184, 3000, 5432, 6379 (and 8081 / 3001 if using optional profiles)

### Quick start (full stack)

```bash
# From repository root
cp .env.example .env
# Windows: copy .env.example .env

# Edit .env — set JWT_SECRET_KEY to ≥32 random characters

# PowerShell helpers (Windows):
#   .\scripts\docker-build.ps1 -Dev
#   .\scripts\docker-up.ps1 -Build
#   .\scripts\docker-down.ps1

# Merges docker-compose.yml + docker-compose.override.yml (Soft TSE / FON simulation)
docker compose up --build

# Optional POS static web:
docker compose --profile pos up --build

# Optional tenant Sites:
docker compose --profile sites up --build

# Both optional apps:
docker compose --profile pos --profile sites up --build
```

### Quick start (infra only — host apps)

Day-to-day coding with hot reload on the host:

```bash
docker compose -f docker-compose.dev.yml up -d   # or: just docker-up-dev
npm run dev
```

Configure backend connection to `localhost:5432` and Redis `localhost:6379` (user-secrets / `appsettings.Development.json`).

### Production-oriented Compose

```bash
copy .env.production.example .env.production
# Or: .\scripts\docker-deploy.ps1 -Profile admin
docker compose -f docker-compose.prod.yml --env-file .env.production up -d --build
# Optional Admin: add --profile admin
```

See [`DEPLOYMENT.md`](DEPLOYMENT.md#docker-compose-production-oriented) and [`docs/DOCKER_SETUP.md`](docs/DOCKER_SETUP.md). Never combine with `docker-compose.override.yml`.

Detach: `docker compose up --build -d`  
Stop: `docker compose down` (add `-v` to wipe Postgres/Redis volumes).
# Infra-only stop: docker compose -f docker-compose.dev.yml down

### Smoke checks

```bash
curl -fsS http://localhost:5184/api/health/live
curl -fsS -H "X-Tenant-Id: dev" http://localhost:5184/api/health
curl -fsSI http://localhost:3000/login
```

| URL | Service |
|-----|---------|
| http://localhost:5184 | API (+ `/swagger` in Development) |
| http://localhost:3000 | Admin |
| http://localhost:8081 | POS web (`--profile pos`) |
| http://localhost:3001 | Sites (`--profile sites`) |

### Build a single image

```bash
# API — context MUST be repo root (ProjectReference to tools/LicenseGenerator.Core)
docker build -f backend/Dockerfile -t regkasse-api:local .

# Admin
docker build -f frontend-admin/Dockerfile \
  --build-arg NEXT_PUBLIC_API_BASE_URL=http://localhost:5184 \
  -t regkasse-frontend-admin:local ./frontend-admin

# POS web
docker build -f frontend/Dockerfile \
  --build-arg EXPO_PUBLIC_API_BASE_URL=http://localhost:5184/api \
  -t regkasse-frontend-pos-web:local .

# Sites
docker build -f frontend-sites/Dockerfile \
  --build-arg NEXT_PUBLIC_API_BASE_URL=http://localhost:5184 \
  -t regkasse-frontend-sites:local ./frontend-sites
```

### Important

- Browser clients must call **`localhost`**, not the Docker DNS name `backend` — bake `NEXT_PUBLIC_*` / `EXPO_PUBLIC_*` accordingly.
- Changing those public env vars requires **`docker compose build --no-cache`** for that service (values are inlined at image build).
- Do **not** bind-mount `./backend` over the API image `/app` (published DLLs live there). Use [`docker-compose.dev.yml`](docker-compose.dev.yml) + host `dotnet run` for watch mode.
- Default Compose `ASPNETCORE_ENVIRONMENT` is **Development** (CORS / Dev tenant header). Do not use this compose file as-is for production fiscal cutover.
- More detail: [`DEVELOPMENT.md`](DEVELOPMENT.md#docker-compose-full-stack) · package READMEs · [`.env.example`](.env.example)

---

## Tech Stack

| Area | Technology |
|------|------------|
| Backend | ASP.NET Core **10** (`net10.0`), EF Core **10.0.10**, PostgreSQL, JWT / Identity |
| Admin (FA) | Next.js **16.2.x**, React **19.2.x**, Ant Design **6**, TanStack Query, Orval |
| POS | Expo SDK **56**, React Native **0.85.x**, TypeScript |
| Sites | Next.js **16**, React **19** |
| i18n | Shared [`localization/`](localization/) tooling; FA locales de/en/tr; POS de-DE |
| Tooling | npm workspaces, Husky pre-commit, GitHub Actions, TestSprite YAML + Node runners |
| Infra helpers | Redis (optional), license issuer under [`tools/`](tools/) |

Stack pins are also summarized in [`AGENTS.md`](AGENTS.md) § Updated Stack Versions.

---

## Repository layout

| Folder | Purpose |
|--------|---------|
| [`backend/`](backend/) | ASP.NET Core API — auth, payments, RKSV/TSE, FinanzOnline, backup, billing, OpenAPI |
| [`frontend/`](frontend/) | Mobile POS (Expo) — cashier UI |
| [`frontend-admin/`](frontend-admin/) | Admin panel (Next.js) |
| [`frontend-sites/`](frontend-sites/) | Shared tenant websites / online-order UI |
| [`localization/`](localization/) | i18n import/export/validation and CI budgets |
| [`scripts/`](scripts/) | OpenAPI verify, seeds, git hooks, SQL helpers — [`scripts/README.md`](scripts/README.md) |
| [`tools/`](tools/) | License generator + wrappers — [`tools/README.md`](tools/README.md) |
| [`testsprite/`](testsprite/) | API/E2E specs + CI validate/smoke — [`testsprite/README.md`](testsprite/README.md) |
| [`docs/`](docs/) | Operator/developer documentation |
| [`ai/`](ai/) | AI/agent contracts and guardrails |
| [`shared/`](shared/) | Small shared constants for tooling |
| [`docker-compose.yml`](docker-compose.yml) | Local full stack (Postgres, Redis, API, Admin; optional POS / Sites) |
| [`docker-compose.override.yml`](docker-compose.override.yml) | Dev Soft TSE / FON simulation (auto-merged) |
| [`docker-compose.dev.yml`](docker-compose.dev.yml) | Local infra only (Postgres + Redis) for host apps |
| [`docker-compose.prod.yml`](docker-compose.prod.yml) | Production-oriented Compose (Device/Real TSE) |
| [`.github/workflows/`](.github/workflows/) | CI inventory — [`.github/workflows/README.md`](.github/workflows/README.md) |

---

## Documentation

| Doc | Audience |
|-----|----------|
| [`docs/README.md`](docs/README.md) | **Docs index** (start here for human guides) |
| [`CONTRIBUTING.md`](CONTRIBUTING.md) | Setup, conventions, PRs, Husky |
| [`API_CONTRACT.md`](API_CONTRACT.md) | **HTTP API index** (Auth, Users, Digital, Billing) ↔ swagger |
| [`DEVELOPMENT.md`](DEVELOPMENT.md) | **Local setup** — prerequisites, run, test, troubleshooting |
| [`docs/DOCKER.md`](docs/DOCKER.md) · [`docs/DOCKER.de.md`](docs/DOCKER.de.md) | **Docker hub** (EN / DE) — Compose, Windows, prod |
| [`docs/DOCKER_SETUP.md`](docs/DOCKER_SETUP.md) · [`docs/DOCKER_SETUP.de.md`](docs/DOCKER_SETUP.de.md) | **Docker migration & setup plan** |
| [`docs/DOCKER_WINDOWS_SETUP.md`](docs/DOCKER_WINDOWS_SETUP.md) | Docker Desktop + WSL2 on Windows (EN; [DE](docs/DOCKER_WINDOWS_SETUP.de.md)) |
| [`DEPLOYMENT.md`](DEPLOYMENT.md) | **Production deploy** — DNS/SSL, API/POS/FA, env vars, rollback |
| [`AGENTS.md`](AGENTS.md) | Always-applied agent / engineering rules |
| [`REGKASSE_AI_ONBOARDING.md`](REGKASSE_AI_ONBOARDING.md) | Product / fiscal onboarding brief |
| [`ai/README.md`](ai/README.md) | AI contract index |
| [`docs/MULTI_TENANT.md`](docs/MULTI_TENANT.md) | Tenancy & isolation |
| [`docs/POS_PRODUCTION_ARCHITECTURE.md`](docs/POS_PRODUCTION_ARCHITECTURE.md) | Single POS UI hosts |
| [`docs/BACKUP_AND_DISASTER_RECOVERY.md`](docs/BACKUP_AND_DISASTER_RECOVERY.md) | Backup / DR hub |
| [`docs/BILLING_TENANT_LICENSE.md`](docs/BILLING_TENANT_LICENSE.md) | Mandant license sales |
| [`docs/AUTH_TWO_FACTOR.md`](docs/AUTH_TWO_FACTOR.md) | SuperAdmin 2FA |
| [`docs/WORKING_HOURS.md`](docs/WORKING_HOURS.md) | Website hours (never gates POS/FA) |
| [`SECURITY.md`](SECURITY.md) | Vulnerability reporting & developer security practices |

Package READMEs: [`backend/README.md`](backend/README.md), [`frontend/README.md`](frontend/README.md), [`frontend-admin/README.md`](frontend-admin/README.md), [`frontend-sites/README.md`](frontend-sites/README.md).

### API client (OpenAPI → Orval)

Admin consumes `backend/swagger.json` via Orval:

```bash
node scripts/generate-backend-openapi.mjs   # refresh swagger
npm run generate:api                        # Orval → frontend-admin/src/api/generated
npm run verify:api-client                   # fail on drift
```

Husky pre-commit and CI (`api-client-alignment.yml`, optional auto-generate) keep the client aligned. See [`.github/workflows/README.md`](.github/workflows/README.md).

### Roles (short)

| UI (de) | Backend | Scope |
|---------|---------|-------|
| Super-Administrator | `SuperAdmin` | Platform |
| Mandanten-Admin | `Manager` | Own tenant |
| Kassierer | `Cashier` | POS |

---

## Contributing

1. Read [`CONTRIBUTING.md`](CONTRIBUTING.md) and [`AGENTS.md`](AGENTS.md) for the area you touch.
2. Prefer small, reversible PRs; do not extend legacy `/api/Payment`, `/api/Cart`, `/api/Product`.
3. After OpenAPI changes: regenerate Orval client and run `npm run verify:api-client`.
4. Pre-commit (Husky): API verify + staged-package lint/typecheck. Tests are opt-in (`HUSKY_RUN_TESTS=1`).
5. Keep POS UI strings German; Admin UI via i18n files under `frontend-admin/src/i18n/`.

```bash
npm run install:git-hooks   # ensure .husky/pre-commit
npm run precommit           # same checks as the hook
npm run lint && npm run typecheck
```

CI inventory: [`.github/workflows/README.md`](.github/workflows/README.md).

---

## Security

To report a vulnerability, see [`SECURITY.md`](SECURITY.md) (responsible disclosure to **security@regkasse.at**). Do not file public issues for security bugs.

---

## License

**Proprietary** — All rights reserved. See [`LICENSE`](LICENSE).

Unauthorized copying, distribution, or use of this software, via any medium, is strictly prohibited without prior written permission from the copyright holder.
