# Regkasse Scripts Reference

> **Last updated:** 2026-08-01  
> **Purpose:** Complete reference for Windows `.bat` / `.ps1` helpers under `scripts/<category>/` and related `.ps1` tools.

Related: [`DOCKER_VS_LEGACY.md`](DOCKER_VS_LEGACY.md) · [`BATCH_FILES.md`](BATCH_FILES.md) · [`SCRIPTS_ECOSYSTEM.md`](SCRIPTS_ECOSYSTEM.md) · [`SCRIPTS_QUICK_REF.md`](SCRIPTS_QUICK_REF.md) · [`GETTING_STARTED_SCRIPTS.md`](GETTING_STARTED_SCRIPTS.md) · [`SCRIPTS_TEST_PLAN.md`](SCRIPTS_TEST_PLAN.md) · [`scripts/README.md`](../scripts/README.md) · root [`README.md`](../README.md#getting-started-with-scripts-windows) · [`CONTRIBUTING.md`](../CONTRIBUTING.md#scripts) · [`DEVELOPMENT.md`](../DEVELOPMENT.md#prefer-scripts-windows)

Each user-facing script below documents: **Path**, **Purpose**, **When to use**, **Prerequisites**, **Example**, **Underlying command(s)**, **Output**, and **Error handling**. CI requires category entry points and every `scripts/**/*.ps1` to appear in this file (root `.bat` files are forbidden) (`npm run validate:scripts`).

---

## Table of contents

| | Section |
|---|--------|
| 📋 | [Quick Start](#-quick-start-hızlı-başlangıç) |
| 🗺️ | [Script ecosystem overview](#️-script-ecosystem-overview) |
| 🚀 | [Development Scripts](#-development-scripts) |
| 🐳 | [Docker Scripts](#-docker-scripts) |
| 🛠️ | [Maintenance Scripts](#️-maintenance-scripts) |
| 🚢 | [Deployment Scripts](#-deployment-scripts) |
| 🔧 | [Helper Scripts](#-helper-scripts) |
| 📜 | [PowerShell Catalog](#-powershell-catalog) |
| 🔍 | [Troubleshooting](#-troubleshooting) |
| 📚 | [Related Documentation](#-related-documentation) |

---

## Category layout (`scripts/`)

| Folder | Contents |
|--------|----------|
| `scripts/dev/` | Mode chooser, npm start helpers, clean, redis, mail, fix-antd |
| `scripts/docker/` | PowerShell Compose (`docker-up.ps1`, prod build/push/deploy) |
| `scripts/docker/host/` | Host/chooser bats (`up.bat`, `down.bat`, …) — logs under `C:\Scripts\logs` |
| `scripts/legacy/` | Multi-window host starters (no Docker); `kill-ports.ps1`, `open-tabs.ps1` |
| `scripts/ci/` | `ci-build` / `ci-test` / `ci-deploy` |
| `scripts/rksv/` | DEP export, BMF Prüftool, fiscal validation |
| `scripts/test/` | Smoke, structural script tests, TestSprite |
| `scripts/ops/` | `deploy.bat`, `rollback.bat`, monitoring |
| `scripts/lib/` | `_common.bat`, `run-with-log.bat`, wrappers, `validate-scripts` |

Entry examples: `scripts\dev\start.bat`, `scripts\dev\start-dev.bat`, `scripts\docker\host\up.bat` (`docker/host/up.bat`), `scripts\ops\deploy.DANGER.bat`.

---

## 📋 Quick Start (Hızlı Başlangıç)

| Script | What it does | When to use |
|--------|--------------|-------------|
| `scripts\dev\start.bat` | Mode chooser → Legacy or Docker | Preferred Windows entry |
| `scripts\legacy\start-all.bat` | Host multi-window stack | Daily DX without Docker |
| `scripts\dev\start-dev.bat` | Starts all services via `npm run dev` | One-terminal npm workspaces |
| `scripts\docker\host\up.bat` | Starts Docker containers (`scripts\docker\`) | Prod-like / no local SDKs |
| `scripts\docker\docker-up-prod.bat` | Production-oriented full stack (localhost) | Local test before cloud |
| `scripts\docker\docker-down-prod.bat` | Stops prod Compose stack | After local prod test |
| `scripts\docker\host\down.bat` | Stops Docker containers | Before gaming or shutdown |
| `scripts\docker\host\status.bat` | Lists running containers | “Is the stack up?” |
| `scripts\docker\host\logs.bat` | Follow Compose logs | Debug local Docker stack |
| `scripts\test\test-all.bat` | Runs all tests | Before commit |
| `scripts\ops\deploy.DANGER.bat` | Prod Compose deploy (confirm + smoke + backup gate) | Production-style host deploy |
| `scripts\docker\docker-deploy.bat` | Thin wrapper → `scripts/docker/docker-deploy.ps1` (+ optional profiles) | Host Compose prod without full `deploy.bat` checklist |
| `scripts\docker\docker-build-prod.bat` | Build prod images | Before `deploy-docker.bat` |
| `scripts\docker\docker-push-prod.bat` | Push images to `DOCKER_REGISTRY` | GHCR / registry publish |
| `scripts\docker\docker-logs-prod.bat` | Tail prod Compose logs | Debug staging/prod host |

Comparison: [`DOCKER_VS_LEGACY.md`](DOCKER_VS_LEGACY.md). Logs (Legacy + Docker bats): `C:\Scripts\logs`.

---

## 🗺️ Script ecosystem overview

Which script to use for which task:

```mermaid
flowchart TD
  start([Need to…]) --> choice{Task category}

  choice -->|Pick mode| Mode[start.bat]
  choice -->|Daily coding| Dev[Development]
  choice -->|Containers| Docker[Docker]
  choice -->|Cleanup / fiscal fixtures| Maint[Maintenance]
  choice -->|Release dry-run| Deploy[Deployment]

  Mode --> L1[scripts/legacy/start-all.bat]
  Mode --> K0[scripts/docker/host/up.bat]

  Dev --> D1[start-dev.bat]
  Dev --> D2[start-backend / admin / pos / sites]
  Dev --> D3[test-all.bat]
  Dev --> D4[clean-all.bat]

  Docker --> K1[docker-up.bat]
  Docker --> K2[docker-status.bat]
  Docker --> K3[docker-down.bat]
  Docker --> K4[scripts\docker\host\clean.DANGER.bat]
  Docker --> K5[docker-logs.bat]

  Maint --> M1[scripts/dev/clean-backend.bat]
  Maint --> M2[scripts/rksv/ensure-bmf-prueftool.bat]
  Maint --> M3[scripts/rksv/generate-dep-export.bat]
  Maint --> M4[scripts/test/smoke-test.bat]
  Maint --> M5[scripts/dev/fix-antd.bat / dev-mail.bat]

  Deploy --> P1[deploy.bat]
  Deploy --> P2[rollback.bat]
  Deploy --> P3[scripts/lib/run-with-log.bat]

  D1 --> tip1[npm run dev]
  K1 --> tip2[Compose detached]
  M4 --> tip3[API must be up]
  P1 --> tip4[docker-compose.prod.yml — not cloud CD alone]
```

| Category | Prefer | Instead of |
|----------|--------|------------|
| Mode chooser | `scripts\dev\start.bat` | Guessing Legacy vs Docker |
| Legacy (multi-window) | `scripts\legacy\start-all.bat` | Ad-hoc host terminals |
| Development (npm) | `scripts\dev\start-dev.bat` | Manually opening 4 terminals |
| Docker | `docker-up.bat` / `docker-down.bat` | Typing full `docker compose …` |
| Maintenance | `scripts\*.bat` | Remembering long PowerShell paths |
| Deployment | `scripts\ops\deploy.DANGER.bat` | Prod Compose checklist (smoke + backup gate) |

Full details below. Short inventory: [`BATCH_FILES.md`](BATCH_FILES.md). Pairing CI: [`verify-bat-ps1-pairing.mjs`](../scripts/verify-bat-ps1-pairing.mjs) (`npm run verify:bat-ps1`).

---

## 🚀 Development Scripts

### start-dev.bat

**Path:** [`./scripts/dev/start-dev.bat`](../scripts/dev/start-dev.bat)

**Purpose:** Starts all Regkasse services in development mode (API + Admin + POS + Sites).

**When to use:** Every day when you start working.

**Prerequisites:** Node.js 20+, npm installed (`npm install` at repo root). PostgreSQL available (local or `docker-compose.dev.yml`). Backend user-secrets configured for DB/JWT.

**Example:**

```batch
start-dev.bat
```

**Underlying command:** `npm run dev` → `node scripts/dev-workspaces.mjs`

**Output:**

```text
Starting Regkasse Development Environment...

 API:   http://localhost:5184
 Admin: http://localhost:3000
 POS:   http://localhost:8081
 Sites: http://localhost:3001
```

**Error handling:** If `npm run dev` exits non-zero, the script prints `[FAILED]`, pauses, and returns that exit code.

---

### start-backend.bat

**Path:** [`./scripts/dev/start-backend.bat`](../scripts/dev/start-backend.bat)

**Purpose:** Starts only the backend API service.

**When to use:** When you only need to work on the backend.

**Prerequisites:** .NET SDK 10+, PostgreSQL, JWT/connection user-secrets.

**Example:**

```batch
start-backend.bat
```

**Underlying command:** `npm run dev:backend`

**Output:**

```text
Starting Backend API...
 URL: http://localhost:5184
```

Swagger (Development): http://localhost:5184/swagger

**Error handling:** Non-zero exit → `[FAILED] Exit code: N` + pause.

---

### start-admin.bat

**Path:** [`./scripts/dev/start-admin.bat`](../scripts/dev/start-admin.bat)

**Purpose:** Starts only the Frontend Admin (FA) service.

**When to use:** When you only need to work on the admin panel (API already running elsewhere).

**Prerequisites:** Node.js 20+. API reachable via `NEXT_PUBLIC_API_BASE_URL` (or defaults).

**Example:**

```batch
start-admin.bat
```

**Underlying command:** `npm run dev:admin`

**Output:**

```text
Starting Admin (FA)...
 URL: http://localhost:3000
```

**Error handling:** Non-zero exit → `[FAILED]` + pause.

---

### start-pos.bat

**Path:** [`./scripts/dev/start-pos.bat`](../scripts/dev/start-pos.bat)

**Purpose:** Starts only the POS (Cash Register) Expo service.

**When to use:** When you only need to work on the POS.

**Prerequisites:** Node.js 20+, Expo toolchain. Configure `EXPO_PUBLIC_API_BASE_URL` / `EXPO_PUBLIC_DEV_TENANT_ID` as needed.

**Example:**

```batch
start-pos.bat
```

**Underlying command:** `npm run dev:pos`

**Output:**

```text
Starting POS (Expo)...
 URL: http://localhost:8081
```

**Error handling:** Non-zero exit → `[FAILED]` + pause. Metro port conflicts show in Expo logs.

---

### start-sites.bat

**Path:** [`./scripts/dev/start-sites.bat`](../scripts/dev/start-sites.bat)

**Purpose:** Starts only the Tenant Sites (Next.js storefronts) service.

**When to use:** When you only need to work on customer websites / online-order UI (API already running elsewhere).

**Prerequisites:** Node.js 20+. API reachable via `NEXT_PUBLIC_API_BASE_URL` (or defaults).

**Example:**

```batch
start-sites.bat
```

**Underlying command:** `npm run dev:sites`

**Output:**

```text
Starting Tenant Sites...
 Sites: http://localhost:3001
```

**Error handling:** Non-zero exit → `[FAILED]` + pause.

---

### test-all.bat

**Path:** [`./scripts/test/test-all.bat`](../scripts/test/test-all.bat)

**Purpose:** Runs package tests **sequentially** (Backend → Admin → POS) and stops on the first failure.

**When to use:** Before committing or pushing changes.

**Prerequisites:** .NET SDK 10+, `npm install`. Backend tests often need PostgreSQL. Dev servers do **not** have to be stopped, but free RAM helps.

**Example:**

```batch
test-all.bat
```

**Underlying commands:**

1. `dotnet test backend/KasseAPI_Final.sln`
2. `npm run test` in `frontend-admin/`
3. `npm run test` in `frontend/`

**Output:**

```text
========================================
 Running All Tests
========================================

[1/3] Backend tests...
[OK] Backend tests passed!

[2/3] Admin tests...
[OK] Admin tests passed!

[3/3] POS tests...
[OK] POS tests passed!

========================================
 All tests passed!
========================================
```

**Error handling:** First failing step → `[ERROR] … failed!` + pause + that exit code. Sites tests are **not** included (run `npm run test:sites` separately if needed).

---

### clean-all.bat

**Path:** [`./scripts/dev/clean-all.DANGER.bat`](../scripts/dev/clean-all.DANGER.bat)

**Purpose:** Cleans monorepo **build artifacts** after confirmation (`bin`, `obj`, `.next`, `.expo`, `dist`, npm caches). Does **not** delete `node_modules` or Docker volumes.

**When to use:** When you have build issues or want a clean slate for compile/cache outputs.

**Prerequisites:** Stop running `dotnet`/`next`/Expo processes if files are locked. Confirms with `y/N` first.

**Example:**

```batch
clean-all.bat
```

**Underlying commands:** `dotnet clean` + `rmdir` for backend / admin / POS / sites artifact dirs.

**Output:**

```text
========================================
 Cleaning All Build Artifacts
========================================

WARNING: This will remove all build artifacts!

Are you sure? (y/N): y

[1/4] Cleaning backend...
[OK] Backend cleaned!
...
========================================
 Clean complete!
========================================
```

**Error handling:** Cancelled if confirmation is not `y`. Locked files → stop `start-*.bat` / Docker apps and retry.

**Note:** For a deeper backend wipe (nested verify trees), use `scripts\dev\clean-backend.bat`. Cross-platform equivalent remains `npm run clean` → `scripts/clean-artifacts.mjs` (also cleans LicenseGenerator dirs). Reinstall deps with `npm install` if you need a full `node_modules` reset.

---

## 🐳 Docker Scripts

### docker-up.bat

**Path:** [`./scripts/docker/host/up.bat`](../scripts/docker/host/up.bat)

**Purpose:** Starts default Docker Compose services (PostgreSQL, Redis, Backend, Admin). POS and Sites are **optional profiles**.

**When to use:** When you want to use Docker instead of local host services.

**Prerequisites:** Docker Desktop running. Copy `.env.example` → `.env` (JWT secret ≥ 32 chars).

**Example:**

```batch
docker-up.bat
```

Optional profiles (manual Compose; not in the `.bat` today):

```batch
docker compose --profile pos up -d
docker compose --profile sites up -d
```

**Underlying command:** `docker compose up -d`

**Output:**

```text
========================================
 Starting Docker Containers
========================================

Checking Docker...
[OK] Docker is running!

Starting containers...
[+] Running 4/4
 ✔ Container regkasse-postgres         Started
 ✔ Container regkasse-redis            Started
 ✔ Container regkasse-backend          Started
 ✔ Container regkasse-frontend-admin   Started

========================================
 Containers started!
========================================
  API:   http://localhost:5184
  Admin: http://localhost:3000
  POS:   http://localhost:8081
  Sites: http://localhost:3001

To view logs: docker compose logs -f
To stop: docker-down.bat
```

With `--profile pos` / `--profile sites` you also get `regkasse-frontend-pos` / `regkasse-frontend-sites`.

**Error handling:** Docker Desktop not running → `[ERROR] Docker is not running!`. Compose failure → `[ERROR] Failed to start containers!` + pause + exit code.

---

### docker-down.bat

**Path:** [`./scripts/docker/host/down.bat`](../scripts/docker/host/down.bat)

**Purpose:** Stops all Compose containers for this project and frees resources (volumes kept).

**When to use:** Before gaming, shutting down, or to free RAM.

**Prerequisites:** Docker Desktop; Compose project previously started (or no-op if already down).

**Example:**

```batch
docker-down.bat
```

**Underlying command:** `docker compose down`

**Output:**

```text
========================================
 Stopping Docker Containers
========================================

[+] Running 4/4
 ✔ Container regkasse-backend          Removed
 ✔ Container regkasse-frontend-admin   Removed
 ✔ Container regkasse-postgres         Removed
 ✔ Container regkasse-redis            Removed

========================================
 Containers stopped!
========================================

RAM and CPU freed!
```

**Error handling:** Daemon down / compose error → `[ERROR] Failed to stop containers!` + pause.

---

### scripts\docker\host\clean.DANGER.bat

**Path:** [`./scripts/docker/host/clean.DANGER.bat`](../scripts/docker/host/clean.DANGER.bat)

**Purpose:** Completely resets local Compose data — stops containers, removes **volumes** (`-v`), then `docker system prune -f` (unused Docker data). Confirms first.

**When to use:** When you want to reset Docker completely (**data loss** for Postgres/Redis volumes).

**Prerequisites:** Docker Desktop running.

**Example:**

```batch
scripts\docker\host\clean.DANGER.bat
```

**Underlying commands:** `docker compose down -v` + `docker system prune -f`

**Output:**

```text
========================================
 Docker Clean (Full Reset)
========================================

WARNING: This will remove ALL containers, volumes, and unused images!
This means ALL Compose volume data will be lost!

Are you sure? (y/N): y

Stopping and removing containers...
Removing unused images...

========================================
 Docker clean complete!
========================================

All containers, volumes, and unused images removed.
```

**Error handling:** Cancelled if confirmation is not `y`. `down -v` may warn if nothing was running; prune failure aborts with `[ERROR]`.

**Warning:** Destroys Compose volume data (DB/Redis). `system prune` removes unused images/networks — not a full wipe of every image on the machine unless they are unused.

---

### docker-status.bat

**Path:** [`./scripts/docker/host/status.bat`](../scripts/docker/host/status.bat)

**Purpose:** Shows running containers in a compact table (name, status, ports).

**When to use:** To check if everything is running.

**Prerequisites:** Docker Desktop running.

**Example:**

```batch
docker-status.bat
```

**Underlying command:** `docker ps --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"`

**Output:**

```text
========================================
 Docker Container Status
========================================

NAMES                     STATUS       PORTS
regkasse-backend          Up 2 hours   0.0.0.0:5184->8080/tcp
regkasse-frontend-admin   Up 2 hours   0.0.0.0:3000->3000/tcp
regkasse-postgres         Up 2 hours   0.0.0.0:5432->5432/tcp
regkasse-redis            Up 2 hours   0.0.0.0:6379->6379/tcp

========================================
```

**Error handling:** If Docker is not running → `[ERROR] Docker is not running!` + exit 1.

---

## 🛠️ Maintenance Scripts

### clean-backend.bat

**Path:** [`./scripts/dev/clean-backend.bat`](../scripts/dev/clean-backend.bat)  
**PowerShell:** [`./scripts/clean-backend-build.ps1`](../scripts/clean-backend-build.ps1)

**Purpose:** Cleans backend build artifacts (`bin`, `obj`, plus `_test_build_out`, `_testout`, `_ef_build`, `_build_out`). Stops a running `KasseAPI_Final` process if present.

**When to use:** When backend build fails (especially long-path / nested verify trees) or you want a clean build.

**Prerequisites:** None (PowerShell). Close IDE file locks on `backend\` if delete fails.

**Example:**

```batch
scripts\dev\clean-backend.bat
```

**Output:**

```text
Cleaning Backend Build...

Removed C:\...\backend\bin
Removed C:\...\backend\obj
Done. Rebuild with: dotnet build backend\KasseAPI_Final.csproj

Done!
```

**Error handling:** PowerShell stops on error; `.bat` prints `[FAILED]` + pause with exit code.

---

### dev-purge-tenant.bat

**Path:** [`./scripts/dev-purge-tenant.bat`](../scripts/dev-purge-tenant.bat)  
**PowerShell:** [`./scripts/dev-purge-tenant-catalog.DANGER.ps1`](../scripts/dev-purge-tenant-catalog.DANGER.ps1)

**Purpose:** Purges (clears) tenant **catalog** data (products / categories) for development demos.

**When to use:** When you want to reset test catalog data before a fresh FA import.

**Prerequisites:** Backend API must be running in **Development**. JWT with `products.manage` or SuperAdmin (login or `-Token`).

**Example:**

```batch
scripts\dev-purge-tenant.bat -TenantSlug dev -LoginIdentifier admin -Password "***"
```

**Output:**

```text
Purging Tenant Catalog...

This will delete all test data for the 'dev' tenant (Development only).

Are you sure? (y/N): y
...
Done!
```

**Error handling:** Cancelled if confirmation is not `y`. Production blocked by API; auth/tenant failures from PowerShell. Exit code preserved.

**Warning:** Irreversible catalog wipe (Development only). Does not purge full tenant fiscal history.

---

### generate-dep-export.bat

**Path:** [`./scripts/rksv/generate-dep-export.bat`](../scripts/rksv/generate-dep-export.bat)  
**PowerShell:** [`./scripts/generate-dep-export-fixtures.ps1`](../scripts/generate-dep-export-fixtures.ps1)

**Purpose:** Generates DEP (Datenerfassungsprotokoll) export **test fixtures** for BMF Prüftool (`dep-export.json`, `crypto-material.json`, `qr-code-rep.json`).

**When to use:** When testing / verifying RKSV DEP export functionality (after format changes, before Prüftool).

**Prerequisites:** .NET SDK 10+. Runs `dotnet test --filter RksvDepPrueftoolFixtureTests` (does **not** require a live API).

**Example:**

```batch
scripts\rksv\generate-dep-export.bat
```

**Output:**

```text
Generating DEP Export Fixtures...
========================================
 Generate DEP Prueftool Fixtures
========================================
Generating BMF Prüftool fixtures...
  Output: ...\backend\Tests\fixtures\prueftool
Fixtures ready:
  ...\backend\Tests\fixtures\prueftool\dep-export.json
  ...\backend\Tests\fixtures\prueftool\crypto-material.json
  ...\backend\Tests\fixtures\prueftool\qr-code-rep.json

[OK] Fixture generation finished. Exit code: 0
```

**Error handling:** Test/generation failure → `[FAILED]` + log at `%TEMP%\regkasse-generate-dep-fixtures-*.log`.

---

### ensure-bmf-prueftool.bat

**Path:** [`./scripts/rksv/ensure-bmf-prueftool.bat`](../scripts/rksv/ensure-bmf-prueftool.bat)  
**PowerShell:** [`./scripts/ensure-bmf-prueftool.ps1`](../scripts/ensure-bmf-prueftool.ps1)

**Purpose:** Ensures BMF Prüftool is installed for DEP verification (downloads official V1.1.1 ZIP/JARs).

**When to use:** Before running DEP export / receipt Prüftool verification.

**Prerequisites:** Network access; writable `backend/Tests`. JDK **17+** is required to *run* the Prüftool later (`verify-rksv-dep-export.bat`), not strictly to download the JARs.

**Example:**

```batch
scripts\rksv\ensure-bmf-prueftool.bat
scripts\rksv\ensure-bmf-prueftool.bat -Force
```

**Output:**

```text
Ensuring BMF Prueftool is installed...

BMF Prüftool already present under ...\backend\Tests (use -Force to reinstall).
```

Or on first install:

```text
Ensuring BMF Prueftool is installed...
Downloading BMF Prüftool ZIP...
...
  backend\Tests\regkassen-verification-depformat-1.1.1.jar
  backend\Tests\regkassen-verification-receipts-1.1.1.jar
  backend\Tests\lib\*.jar

Done!
```

**Error handling:** Download/SHA mismatch → PowerShell error; `.bat` pauses with exit code.

**Note:** JARs land under `backend/Tests/` (gitignored), not `./tools/bmf-prueftool.jar`.

---

### fix-antd.bat

**Path:** [`./scripts/dev/fix-antd.bat`](../scripts/dev/fix-antd.bat)  
**Node:** [`./scripts/fix-antd-deprecations.mjs`](../scripts/fix-antd-deprecations.mjs)

**Purpose:** Fixes Ant Design 6 deprecations in the admin panel (`frontend-admin/src`).

**When to use:** After an Ant Design version upgrade, or when CI flags deprecated props / static APIs.

**Prerequisites:** Node.js 20+ installed.

**Example:**

```batch
scripts\dev\fix-antd.bat
scripts\dev\fix-antd.bat --dry-run
```

**Output:**

```text
Fixing Ant Design Deprecations...
{
  "dryRun": false,
  "filesChanged": 12,
  ...
}
Done!
```

Exact counts vary by tree state. Use `--dry-run` to report without writing.

**Error handling:** Missing Node / script exception → `[FAILED]`.

**Note:** Runs via **`node`**, not PowerShell.

---

### dev-mail.bat

**Path:** [`./scripts/dev/dev-mail.bat`](../scripts/dev/dev-mail.bat)

**Purpose:** Configures development mail settings (`scripts\dev-mail.local.env`) and launches the interactive forgot-username mail test.

**When to use:** When setting up or verifying email for local development (mail is captured under `backend\App_Data\dev-mail\`, not a real inbox).

**Prerequisites:** None to create the env file. Backend API should be running for the mail test step.

**Example:**

```batch
scripts\dev\dev-mail.bat
```

**Output:**

```text
Configuring Dev Mail...

Created scripts\dev-mail.local.env from example.
Edit that file to set DEFAULT_TEST_EMAIL / BASE_URL.

========================================
 Kullanici Adi Unuttum - Yerel Test
========================================
...
```

**Error handling:** Empty email / API down → error message + non-zero exit.

**Note:** Config path is `scripts\dev-mail.local.env` (from `dev-mail.local.env.example`), **not** root `.env.local`.

---

## 🚢 Deployment Scripts

### deploy.bat

**Path:** [`./scripts/ops/deploy.DANGER.bat`](../scripts/ops/deploy.DANGER.bat)

**Purpose:** Interactive **production-style** deploy via `docker-compose.prod.yml` (confirm → smoke → backup gate → build → up → health). On health failure, offers `rollback.bat`.

**When to use:** When intentionally deploying the prod Compose stack on a host that runs `docker-compose.prod.yml`. This is **not** the GitHub Actions cloud CD path by itself — treat it as an operator checklist on the deploy host.

**Prerequisites:**

- Docker Desktop / daemon running
- `docker-compose.prod.yml` + prod `.env` / secrets configured
- Services reachable for smoke (`scripts/run-comprehensive-smoke.ps1`) **before** cutover, or expect step 1 to fail
- Manual backup completed (API or FA) — script only asks for confirmation
- `curl` available for health check (`http://localhost:5184/api/health`)

**Example:**

```batch
deploy.bat
```

**Steps:**

1. Pre-deploy smoke (`run-comprehensive-smoke.ps1` — full suite; lightweight `scripts\test\smoke-test.bat` is curl-only)
2. Operator confirms backup done
3. `docker compose -f docker-compose.prod.yml --env-file .env.production build`
4. `docker compose -f docker-compose.prod.yml --env-file .env.production up -d`
5. Health check `http://127.0.0.1:5184/api/health/live`

**Error handling:** Any failed step aborts with non-zero exit; health failure invokes `rollback.bat` (git-oriented — confirm intent).

---

### test-alertmanager-routing.ps1

**Path:** [`./scripts/ops/test-alertmanager-routing.ps1`](../scripts/ops/test-alertmanager-routing.ps1)  
**Windows:** [`./scripts/ops/test-alertmanager-routing.bat`](../scripts/ops/test-alertmanager-routing.bat)

**Purpose:** POST a synthetic alert to a local or host Alertmanager to verify routing.

**When to use:** After changing Alertmanager routes or during ops smoke checks.

**Example:**

```powershell
pwsh ./scripts/ops/test-alertmanager-routing.ps1
```

**CI/CD alternative:** GitHub Actions — [`docs/CI_CD.md`](CI_CD.md) (`ci.yml`, `deploy.yml`, `deploy-production.yml`).

### deploy-docker.bat

**Path:** [`./deploy-docker.bat`](../deploy-docker.bat)

**Purpose:** Operator shortcut to `scripts/docker/docker-deploy.ps1` (prod Compose + `.env.production`, Soft TSE override not loaded). Optional args = Compose profiles (`admin`, `sites`, `pos`).

**Example:**

```batch
deploy-docker.bat
deploy-docker.bat admin
```

### CI scripts (`scripts/ci-*.ps1`)

| Script | Purpose |
|--------|---------|
| `ci-build.ps1` | Release / Docker image build (+ optional registry push) |
| `ci-test.ps1` | Backend / Admin / POS test gates |
| `ci-deploy.ps1` | Deploy webhook + smoke + rollback (ops / CI) |

See [`CI_CD.md`](CI_CD.md) · [`GITHUB_ACTIONS.md`](GITHUB_ACTIONS.md).

---

## 🔄 Rollback Scripts

### rollback.bat

**Path:** [`./scripts/ops/rollback.DANGER.bat`](../scripts/ops/rollback.DANGER.bat)

**Purpose:** Rolls back to the previous git commit (`git reset --hard HEAD~1`), then rebuilds and redeploys `docker-compose.prod.yml`.

**When to use:** When a production-style deploy fails or serious issues are found **and** you intentionally want to discard the last commit tip. Prefer `git revert` on shared branches.

**Prerequisites:**

- A previous commit must exist (`HEAD~1`)
- Git available
- Docker daemon running (for rebuild/`up`)
- Interactive confirmation (`y`)

**Example:**

```batch
rollback.bat
```

**Underlying commands:** `docker compose -f docker-compose.prod.yml down` → `git reset --hard HEAD~1` → `build` → `up -d`

**Output:**

```text
========================================
 Production Rollback
========================================

WARNING: This will rollback to the previous git commit!
Are you sure? (y/N): y
Rolling back...
Rebuilding previous version...
========================================
 Rollback Complete!
========================================
```

**Error handling:** Cancelled if confirmation is not `y`. Git/Docker failures → `[ERROR]` + pause + exit code.

**Warning:** Rewrites the branch tip. Uncommitted work and the last commit are lost. Recover via `git reflog` if still local.

---

## 🔧 Helper Scripts

### \_common.bat

**Path:** [`./scripts/_common.bat`](../scripts/_common.bat)

**Purpose:** Shared helpers for batch files — `check_error`, `success`, `fail`, `info`, `warn` (consistent messaging / error handling).

**When to use:** Called by other scripts via `call` when authoring new `.bat` files. **Not** included automatically by existing root scripts today; not meant to be double-clicked.

**Prerequisites:** None.

**Example:** (Not meant to be run directly)

```batch
call "%~dp0_common.bat" success "Build finished"
call "%~dp0_common.bat" check_error
call "%~dp0_common.bat" fail "Build failed" 1
```

**Output:** Prefixed `[SUCCESS]` / `[ERROR]` / `[INFO]` / `[WARN]` lines.

**Error handling:** `fail` / `check_error` pause and `exit /b` with a code.

---

### run-with-log.bat

**Path:** [`./scripts/lib/run-with-log.bat`](../scripts/lib/run-with-log.bat)

**Purpose:** Runs any command with logging to `logs\` (gitignored).

**When to use:** When you need to debug a script or keep a durable log of a long/flaky command.

**Prerequisites:** Writable repo root for `logs\`.

**Example:**

```batch
scripts\lib\run-with-log.bat deploy.bat
scripts\lib\run-with-log.bat npm run test
```

**Output:**

```text
Running: deploy.bat
Log: C:\...\Regkasse\logs\run_2026-07-29_14-30-00.log

... (command output mirrored) ...

[SUCCESS] See log: C:\...\Regkasse\logs\run_2026-07-29_14-30-00.log
```

Log files are named `run_<timestamp>.log` (locale-dependent date/time sanitized). Folder: `./logs/` (gitignored).

**Error handling:** Missing args → usage help. Command failure → `[ERROR] See log: …` + pause + exit code.

---

### smoke-test.bat

**Path:** [`./scripts/test/smoke-test.bat`](../scripts/test/smoke-test.bat)

**Purpose:** Lightweight HTTP smoke — `curl` against API health, Admin `/login`, and POS `/`.

**When to use:** Quick check that local/dev stack endpoints respond. For the full FA/RKSV/DEP suite use [`run-comprehensive-smoke.bat`](../scripts/run-comprehensive-smoke.bat) / [`run-comprehensive-smoke.ps1`](../scripts/run-comprehensive-smoke.ps1) (also used by `deploy.bat` pre-checks).

**Prerequisites:** Services must be running — API `:5184`, Admin `:3000`, POS `:8081`. `curl` on PATH.

**Example:**

```batch
scripts\test\smoke-test.bat
```

**Underlying commands:**

```batch
curl -sS http://localhost:5184/api/health
curl -sS -o nul http://localhost:3000/login
curl -sS -o nul http://localhost:8081/
```

**Output:**

```text
========================================
 Smoke Tests
========================================

Testing API...
{"status":"Healthy",...}
[OK] API health check passed!

Testing Admin...
[OK] Admin check passed!

Testing POS...
[OK] POS check passed!

========================================
 All smoke tests passed!
========================================
```

**Error handling:** First failed `curl` → `[ERROR]` + pause + that exit code.

---

Individual scenarios print `PASS` / `FAIL` / `SKIP` (not only a short three-line health summary). Full output is also saved under `%TEMP%\regkasse-smoke-*.log`.

**Error handling:** Unreachable hosts or failed scenarios → FAIL rows + `[FAILED] Smoke tests failed. Exit code: N` + log path + pause.

---

### create-bat-wrappers.ps1

**Path:** [`./scripts/lib/create-bat-wrappers.ps1`](../scripts/lib/create-bat-wrappers.ps1) (+ [`.bat`](../scripts/create-bat-wrappers.bat))

**Purpose:** Auto-creates sibling `.bat` wrappers for `.ps1` scripts under `scripts/` / `tools/`.

**When to use:** After adding a new `.ps1`; keep Windows double-click coverage.

**Prerequisites:** Windows PowerShell 5.1+.

**Example:**

```batch
scripts\create-bat-wrappers.bat
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\lib\create-bat-wrappers.ps1 -Force
```

**Output:** `Created:` / `Skipped (already exists):` lines.

**Error handling:** Skips `bin`/`obj`/`node_modules`; skips library `dev-mail-config.ps1`. Existing hand-crafted wrappers are not overwritten unless `-Force`.

---

## 📜 PowerShell Catalog

Primary `.ps1` scripts under [`scripts/`](../scripts/):

| `.ps1` | Purpose | Typical `.bat` |
|--------|---------|----------------|
| `clean-backend-build.ps1` | Wipe backend build dirs | `clean-backend.bat` |
| `create-bat-wrappers.ps1` | Generate `.bat` siblings | `create-bat-wrappers.bat` |
| `dev-mail-config.ps1` | Dot-sourced mail env helper (**not** double-click) | — |
| `dev-purge-tenant-catalog.DANGER.ps1` | Dev catalog hard purge | `dev-purge-tenant.bat` |
| `docker-build.ps1` | `docker compose build` (dev/prod) | `docker-build.bat` |
| `docker-build-prod.ps1` | Production Compose image build only | `docker-build-prod.bat` (+ root `docker-build-prod.bat`) |
| `docker-deploy.ps1` | Production-oriented Compose deploy helper | `docker-deploy.bat` |
| `docker-diagnose.ps1` | Windows Docker/WSL/ports diagnose | `docker-diagnose.bat` |
| `docker-down.ps1` | Compose down helper | `scripts\docker\host\down.bat` |
| `docker-logs-prod.ps1` | Tail prod Compose logs | `docker-logs-prod.bat` (+ root) |
| `docker-push-prod.ps1` | Tag + push prod images to registry | `docker-push-prod.bat` (+ root) |
| `docker-up.ps1` | Compose up helper (profiles/build flags) | `scripts\docker\host\up.bat` |
| `docker-up-prod.ps1` | Local prod-oriented full stack (admin/sites/pos) | `docker-up-prod.bat` (+ root) |
| `docker-down-prod.ps1` | Stop prod Compose (optional `-Volumes`) | `docker-down-prod.bat` (+ root) |
| `ci-build.ps1` | CI: Release build and/or Docker images (+ optional push) | `ci-build.bat` |
| `ci-test.ps1` | CI: backend / Admin / POS test gates | `ci-test.bat` |
| `ci-deploy.ps1` | CI/ops: deploy webhook + smoke + rollback | `ci-deploy.bat` |
| `start-monitoring.ps1` | Optional Prometheus/Grafana/Loki stack | `start-monitoring.bat` |
| `ensure-bmf-prueftool.ps1` | Download BMF Prüftool JARs | `ensure-bmf-prueftool.bat` |
| `generate-dep-export-fixtures.ps1` | Write Prüftool fixtures | `generate-dep-export.bat` |
| `run-comprehensive-smoke.ps1` | Full HTTP smoke | `run-comprehensive-smoke.bat` (lightweight: `smoke-test.bat`) |
| `run-testsprite-pos-smoke.ps1` | TestSprite POS smoke | `run-testsprite-pos-smoke.bat` |
| `run-verify-dep-export-complete.ps1` | DEP build + tests + Prüftool + FA | `run-verify-dep-export-complete.bat` |
| `run_fiscal_go_live_validation.ps1` | `psql` fiscal go-live SQL gate | `run_fiscal_go_live_validation.bat` |
| `smoke-tenant-isolation.ps1` | Cross-tenant isolation HTTP smoke | `smoke-tenant-isolation.bat` |
| `smoke-test.ps1` | Deployment/staging smoke (`API_BASE` required) — see `docs/DEPLOYMENT_SMOKE_TEST.md` | sibling `.bat` if generated; **not** the same as alias `smoke-test.bat` |
| `start-redis-dev.ps1` | Portable Redis on `:6379` | `start-redis-dev.bat` |
| `test-forgot-username-email.ps1` | Forgot-username mail probe | `dev-mail.bat` |
| `test-scripts.ps1` | Dry-run structural bat tests | `test-scripts.bat` |
| `validate-scripts.ps1` | Pairing + documentation gate (CI) | `validate-scripts.bat` |
| `verify-rksv-dep-export.ps1` | BMF DEP JSON Prüftool | `verify-rksv-dep-export.bat` |
| `verify-rksv-receipt-qr.ps1` | Receipt QR Prüftool | `verify-rksv-receipt-qr.bat` |

Also (bat-only alias, no same-name `.ps1`): `test-mode-scripts.bat` — Legacy / Docker / `start.bat` structural smoke.

### Notable parameters

```powershell
# DEP Prüftool (fixtures)
.\scripts\verify-rksv-dep-export.ps1 -UseFixtures
.\scripts\verify-rksv-dep-export.ps1 -DepExportPath .\dep.json -CryptoMaterialPath .\crypto.json -DetailedOutput

# Redis
.\scripts\start-redis-dev.ps1
.\scripts\start-redis-dev.ps1 -PingOnly
.\scripts\start-redis-dev.ps1 -Stop

# Tenant purge (Development)
.\scripts\dev-purge-tenant-catalog.DANGER.ps1 -TenantSlug dev -LoginIdentifier admin -Password '***'

# Fiscal go-live (needs DATABASE_URL + psql)
$env:DATABASE_URL = "postgresql://user:pass@localhost:5432/kasse_db"
.\scripts\run_fiscal_go_live_validation.ps1

# Tenant isolation smoke
.\scripts\smoke-tenant-isolation.ps1 -BaseUrl http://localhost:5184
```

---

## 🔍 Troubleshooting

### Common Errors

| Error | Solution |
|-------|----------|
| `'docker' is not recognized` | Install [Docker Desktop](https://www.docker.com/products/docker-desktop/) and reopen the terminal |
| `'npm' is not recognized` / `'node' is not recognized` | Install Node.js **20+** LTS; reopen the terminal |
| `Docker is not running` / `[ERROR] Docker is not running!` | Start Docker Desktop first; verify with `docker-status.bat` |
| Port already in use (5184 / 3000 / 8081 / 5432) | Stop the service using that port (`docker-down.bat` or close `start-*.bat` / `dotnet` / Expo) |
| Permission denied / Access denied deleting `bin`/`obj` | Close IDE locks; retry. Elevate only if needed — prefer stopping processes over blanket “Run as Administrator” |
| Backend long-path / nested `bin` build errors | `scripts\dev\clean-backend.bat`, then rebuild |
| Prüftool / `java` not found | Install JDK **17+**; run `scripts\rksv\ensure-bmf-prueftool.bat` |
| Smoke tests all FAIL | Ensure stack is up (`start-dev.bat` or `docker-up.bat`), wait, then `scripts\test\smoke-test.bat` |
| `DATABASE_URL is not set` | Set `DATABASE_URL` before `run_fiscal_go_live_validation.bat` |
| Purge blocked / 404 | Use **Development** API + Manager/SuperAdmin; never Production |
| `rollback.bat` regret | `git reflog` if still local; avoid hard reset on shared branches |
| `.bat` window flashes closed | Run from `cmd` in the repo root so errors stay visible |

### Debugging Scripts

**Check logs** written by `run-with-log.bat` (under `./logs/`, gitignored):

```batch
dir logs
type logs\run_*.log
```

Hand-crafted wrappers also write under `%TEMP%`:

```batch
dir %TEMP%\regkasse-*.log
```

**Run with logging / verbose capture:**

```batch
scripts\lib\run-with-log.bat deploy.bat
scripts\lib\run-with-log.bat scripts\test\smoke-test.bat
```

**Run PowerShell directly** (full stack traces):

```batch
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-comprehensive-smoke.ps1
```

**Inspect exit code** after a run in `cmd`:

```batch
echo %ERRORLEVEL%
```

**Check / edit script syntax:**

```batch
notepad start-dev.bat
notepad scripts\test\smoke-test.bat
```

**Regenerate missing `.ps1` wrappers:**

```batch
scripts\create-bat-wrappers.bat
```

---

## 📚 Related Documentation

| Doc | Description |
|-----|-------------|
| [`README.md`](../README.md) | Project overview & Quick Start (incl. `.bat` shortcuts) |
| [`DEVELOPMENT.md`](../DEVELOPMENT.md) | Development guide |
| [`DEPLOYMENT.md`](../DEPLOYMENT.md) | Deployment guide |
| [`docs/DOCKER.md`](DOCKER.md) | Docker Compose overview |
| [`docs/DOCKER_WINDOWS_SETUP.md`](DOCKER_WINDOWS_SETUP.md) | Docker Desktop setup on Windows |
| [`docs/DOCKER_WINDOWS_TROUBLESHOOTING.md`](DOCKER_WINDOWS_TROUBLESHOOTING.md) | Windows Docker troubleshooting |
| [`docs/SCRIPTS_COMPLETION_SUMMARY.md`](SCRIPTS_COMPLETION_SUMMARY.md) | Delivery summary, team checklist, what’s next |
| [`docs/SCRIPTS_ECOSYSTEM.md`](SCRIPTS_ECOSYSTEM.md) | Visual decision map (mermaid) |
| [`docs/SCRIPTS_QUICK_REF.md`](SCRIPTS_QUICK_REF.md) | One-screen icon quick card |
| [`docs/SCRIPTS_TEST_PLAN.md`](SCRIPTS_TEST_PLAN.md) | Automated dry-run + manual checklist for `.bat` scripts |
| [`docs/DEP_EXPORT_DEVELOPMENT.md`](DEP_EXPORT_DEVELOPMENT.md) | DEP / Prüftool developer guide |
| [`scripts/README.md`](../scripts/README.md) | Cross-repo scripts (Node / shell / PowerShell) |

---

## Inventory map

```text
Regkasse/
├── start-dev.bat / start-backend.bat / start-admin.bat / start-pos.bat / start-sites.bat
├── test-all.bat / clean-all.bat
├── docker-up.bat / docker-down.bat / scripts\docker\host\clean.DANGER.bat / docker-status.bat
├── deploy.bat / rollback.bat
└── scripts/
    ├── _common.bat / run-with-log.bat / smoke-test.bat
    ├── clean-backend.bat / ensure-bmf-prueftool.bat / fix-antd.bat
    ├── generate-dep-export.bat / dev-purge-tenant.bat / dev-mail.bat
    ├── create-bat-wrappers.ps1 (+ .bat)
    └── *.ps1  (+ sibling .bat where applicable)
```

`.bat` files are **not** gitignored — keep them for the team.

---

### kill-ports.ps1

**Path:** `scripts/legacy/kill-ports.ps1` (+ sibling `.bat`)

**Purpose:** Free common local ports used by API/Admin/POS/Sites during Legacy DX.

### open-tabs.ps1

**Path:** `scripts/legacy/open-tabs.ps1` (+ sibling `.bat`)

**Purpose:** Open Explorer tabs for common project folders (Legacy DX helper).

### ensure-docker-desktop.ps1

**Path:** `scripts/docker/ensure-docker-desktop.ps1` (+ sibling `.bat`)

**Purpose:** Detect missing Docker Desktop / WSL; print install steps (winget / wsl --install). Use `-Install` to launch winget.

**Example:**

```powershell
.\scripts\docker\ensure-docker-desktop.ps1
.\scripts\docker\docker-diagnose.ps1 -SkipPull
```

### DANGER scripts

Destructive Windows helpers use a `.DANGER` suffix in the filename:

| Script | Risk |
|--------|------|
| `scripts/docker/host/clean.DANGER.bat` | Wipes Compose volumes + prune images |
| `scripts/ops/rollback.DANGER.bat` | `git reset --hard HEAD~1` + prod rebuild |
| `scripts/ops/deploy.DANGER.bat` | Production-style Compose deploy |
| `scripts/dev/clean-all.DANGER.bat` | Deletes local build artifacts |
| `scripts/dev/dev-purge-tenant.DANGER.bat` | Hard-deletes Dev tenant catalog (alias) |
| `scripts/dev/dev-purge-tenant-catalog.DANGER.bat` | Same purge with logging |
| `scripts/dev/dev-purge-tenant-catalog.DANGER.ps1` | Same purge API (PowerShell) |

