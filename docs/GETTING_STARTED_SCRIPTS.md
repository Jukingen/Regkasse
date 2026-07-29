# Getting Started with Regkasse Scripts

> **TL;DR:** On Windows, prefer root / `scripts\` **`.bat`** helpers — double-click or run from the repo root.  
> Full catalog: [`SCRIPTS_REFERENCE.md`](SCRIPTS_REFERENCE.md) · Pocket card: [`SCRIPTS_QUICK_REF.md`](SCRIPTS_QUICK_REF.md)

---

## Quick Start (5 minutes)

### 1. First-time setup

```batch
git clone <repo-url> Regkasse
cd Regkasse
npm install
```

First-time backend secrets: see [`../backend/README.md`](../backend/README.md) or [`../CONTRIBUTING.md`](../CONTRIBUTING.md) (`dotnet user-secrets`, `appsettings*.json` copies).

### 2. Start developing

```batch
REM Preferred — choose Legacy (no Docker) or Docker Mode
start.bat

REM Or pick explicitly:
REM   scripts\legacy\start-all.bat
REM   scripts\docker\docker-up.bat

REM Single-terminal npm workspaces (also fine for daily DX)
start-dev.bat
```

Comparison: [`DOCKER_VS_LEGACY.md`](DOCKER_VS_LEGACY.md). Both Legacy and Docker write operator logs to `C:\Scripts\logs`.

| Surface | URL |
|---------|-----|
| API | http://localhost:5184 |
| Admin | http://localhost:3000 |
| POS | http://localhost:8081 |
| Sites | http://localhost:3001 |

### 3. Stop everything

```batch
REM If using Docker
docker-down.bat

REM If using start-dev.bat / local services
REM Press Ctrl+C in the terminal that started npm run dev
```

---

## Common workflows

### Daily development

```batch
start-dev.bat

REM … edit code …

test-all.bat

REM End of day: Ctrl+C in the start-dev window
```

Single surface only:

```batch
start-backend.bat
start-admin.bat
start-pos.bat
start-sites.bat
```

### Testing with Docker

```batch
docker-up.bat
docker-status.bat

docker compose logs -f

docker-down.bat
```

Quick HTTP smoke (stack must be up):

```batch
scripts\smoke-test.bat
```

### Production-style Compose deploy

```batch
REM Operator checklist on a deploy host — confirms backup + smoke first
deploy.bat

REM Only if you intentionally hard-reset the last commit
rollback.bat
```

> Prefer `git revert` on shared branches. Cloud CD remains GitHub Actions — see [`../DEPLOYMENT.md`](../DEPLOYMENT.md).

### Maintenance

```batch
clean-all.bat

scripts\clean-backend.bat
scripts\fix-antd.bat
scripts\generate-dep-export.bat
scripts\ensure-bmf-prueftool.bat
```

Validate the script ecosystem itself:

```batch
npm run validate:scripts
npm run test:scripts
```

---

## Troubleshooting

### `"docker" is not recognized`

1. Install [Docker Desktop](https://www.docker.com/products/docker-desktop/).
2. Ensure **docker** is on PATH (new terminal after install).
3. Start Docker Desktop, then retry `docker-up.bat`.

Windows setup: [`DOCKER_WINDOWS_SETUP.md`](DOCKER_WINDOWS_SETUP.md) · diagnose: `scripts\docker-diagnose.ps1`.

### `"npm" is not recognized`

1. Install **Node.js 20+** LTS.
2. Open a **new** terminal and retry `npm install` / `start-dev.bat`.

### `"dotnet" is not recognized`

1. Install **.NET SDK 10+**.
2. Open a new terminal and retry `start-backend.bat` / `test-all.bat`.

### Port already in use (5184 / 3000 / 8081 / 3001 / 5432)

1. Stop the other process (`Ctrl+C`, Task Manager, or `docker-down.bat`).
2. Or use Compose / different profiles — see [`DOCKER.md`](DOCKER.md).

### Sites / Turbopack “Next.js package not found”

Monorepo root must resolve `next` (workspaces). Config lives in `frontend-sites/next.config.mjs` (`turbopack.root` → repo root). Retry `start-sites.bat` / `start-dev.bat`.

### Tests fail in `test-all.bat`

The `.bat` stops at the **first** failing package (by design). Fix that suite, then re-run. Checklist: [`SCRIPTS_TEST_CHECKLIST.md`](SCRIPTS_TEST_CHECKLIST.md).

---

## Next steps

| Doc | Why |
|-----|-----|
| [`SCRIPTS_REFERENCE.md`](SCRIPTS_REFERENCE.md) | Every script: purpose, output, errors |
| [`SCRIPTS_ECOSYSTEM.md`](SCRIPTS_ECOSYSTEM.md) | Which script for which task |
| [`SCRIPTS_QUICK_REF.md`](SCRIPTS_QUICK_REF.md) | One-screen card |
| [`../DEVELOPMENT.md`](../DEVELOPMENT.md) | Full local setup |
| [`../DEPLOYMENT.md`](../DEPLOYMENT.md) | Production / Compose deploy |
| [`../CONTRIBUTING.md`](../CONTRIBUTING.md#scripts) | Adding new `.bat` / `.ps1` |

## Need help?

- Catalog: [`SCRIPTS_REFERENCE.md`](SCRIPTS_REFERENCE.md)
- Manual checklist: [`SCRIPTS_TEST_CHECKLIST.md`](SCRIPTS_TEST_CHECKLIST.md)
- Team note: [`SCRIPTS_TEAM_ANNOUNCEMENT.md`](SCRIPTS_TEAM_ANNOUNCEMENT.md)
- Ask in the team chat
