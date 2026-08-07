# Getting Started with Regkasse Scripts

> **TL;DR:** On Windows, prefer **`scripts\<category>\*.bat`** helpers — double-click or run from the repo root.  
> Full catalog: [`SCRIPTS_REFERENCE.md`](SCRIPTS_REFERENCE.md) · Pocket card: [`SCRIPTS_QUICK_REF.md`](SCRIPTS_QUICK_REF.md)

Repo root has **no** `.bat` files; everything lives under `scripts/dev|docker|legacy|ci|rksv|test|ops|lib/`.

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
scripts\dev\start.bat

REM Or pick explicitly:
REM   scripts\legacy\start-all.bat
REM   scripts\docker\host\up.bat

REM Single-terminal npm workspaces (also fine for daily DX)
scripts\dev\start-dev.bat
```

Comparison: [`DOCKER_VS_LEGACY.md`](DOCKER_VS_LEGACY.md). Both Legacy and `docker/host` write operator logs to `C:\Scripts\logs`.

| Surface | URL |
|---------|-----|
| API | http://localhost:5184 |
| Admin | http://localhost:3000 |
| POS | http://localhost:8081 |
| Sites | http://localhost:3001 |

### 3. Stop everything

```batch
REM If using Docker host chooser
scripts\docker\host\down.bat

REM If using start-dev.bat / local services
REM Press Ctrl+C in the terminal that started npm run dev
```

---

## Common workflows

### Daily development

```batch
scripts\dev\start-dev.bat

REM … edit code …

scripts\test\test-all.bat

REM End of day: Ctrl+C in the start-dev window
```

Single surface only:

```batch
scripts\dev\start-backend.bat
scripts\dev\start-admin.bat
scripts\dev\start-pos.bat
scripts\dev\start-sites.bat
```

### Testing with Docker

```batch
scripts\docker\host\up.bat
scripts\docker\host\status.bat
scripts\test\smoke-test.bat
scripts\docker\host\down.bat
```

PowerShell Compose (profiles / prod flags):

```powershell
.\scripts\docker\docker-up.ps1 -Build
.\scripts\docker\docker-down.ps1
.\scripts\docker\docker-diagnose.ps1
```

### Validate script ecosystem

```batch
npm run verify:bat-ps1
npm run validate:scripts
npm run test:scripts
```

---

## Where things live

| Need | Path |
|------|------|
| Mode chooser | `scripts\dev\start.bat` |
| npm full stack | `scripts\dev\start-dev.bat` |
| Legacy multi-window | `scripts\legacy\start-all.bat` |
| Docker host UI | `scripts\docker\host\up.bat` |
| Compose PowerShell | `scripts\docker\docker-up.ps1` |
| Deploy checklist | `scripts\ops\deploy.DANGER.bat` |
| Smoke | `scripts\test\smoke-test.bat` |
| RKSV / DEP | `scripts\rksv\` |
| Pairing / docs gate | `scripts\lib\validate-scripts.ps1` |

More: [`BATCH_FILES.md`](BATCH_FILES.md) · [`SCRIPTS_ECOSYSTEM.md`](SCRIPTS_ECOSYSTEM.md) · [`scripts/README.md`](../scripts/README.md).
