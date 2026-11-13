# Docker vs Legacy Mode — Comparison

> **Entry point:** [`scripts/dev/start.bat`](../scripts/dev/start.bat)  
> **Folders:** [`scripts/legacy/`](../scripts/legacy/) · [`scripts/docker/host/`](../scripts/docker/host/) · [`scripts/docker/`](../scripts/docker/) (PowerShell)  
> **Shared logs:** `C:\Scripts\logs` (Legacy + `docker/host`)

---

## When to Use Legacy Mode (without Docker)

**Use when:**

- You have Node.js, .NET, PostgreSQL, Redis already installed
- You want faster startup (no Docker overhead)
- You're debugging specific services
- You have limited RAM (Docker uses more resources)
- You need Expo Metro hot-reload for POS

**Commands:**

```batch
REM From repo root (or via scripts\dev\start.bat → [1])
scripts\legacy\start-all.bat
scripts\legacy\start-backend.bat
scripts\legacy\start-frontend.bat
scripts\legacy\start-frontend-admin.bat
scripts\legacy\start-redis.bat
scripts\legacy\kill-ports.bat

REM C:\Scripts\*.bat shortcuts still redirect here
```

| Script | Purpose |
|--------|---------|
| `start-all.bat` | Start Redis + API + Admin + POS (separate windows) |
| `start-backend.bat` | Start only backend |
| `start-frontend.bat` | Start only POS |
| `start-frontend-admin.bat` | Start only admin |
| `start-redis.bat` | Start Redis |
| `kill-ports.bat` | Free ports `:5184` / `:8081` / `:3000` / `:6379` |

**Logs:** `C:\Scripts\logs\backend_BE.log`, `frontend_FE.log`, `frontend-FA.log`, `redis.log`

---

## When to Use Docker Mode

**Use when:**

- You don't want to install dependencies locally
- You want a production-like environment
- You're testing deployment
- You want a consistent environment with the team
- You want easy cleanup

**Commands (host / chooser bats):**

```batch
REM From repo root (or via scripts\dev\start.bat → [2])
scripts\docker\host\up.bat
scripts\docker\host\down.bat
scripts\docker\host\status.bat
scripts\docker\host\logs.bat
scripts\docker\host\clean.DANGER.bat

REM Partial stacks
scripts\docker\host\up-backend.bat
scripts\docker\host\up-admin.bat
scripts\docker\host\up-pos.bat
```

| Script | Purpose |
|--------|---------|
| `host\up.bat` | Start everything |
| `host\down.bat` | Stop everything |
| `host\status.bat` | Check status |
| `host\logs.bat` | View logs |
| `host\clean.bat` | Clean everything (**destructive** — volumes wiped) |

**PowerShell Compose** (flags / prod): `scripts\docker\docker-up.ps1`, `docker-down.ps1`, `docker-deploy.ps1`, `docker-diagnose.ps1`.

**Logs (host bats):** `C:\Scripts\logs\docker*.log`

---

## npm single-terminal alternative

```batch
scripts\dev\start-dev.bat
```

Uses `npm run dev` (workspaces). Not the same as Legacy multi-window or Docker Compose.

---

## Decision tips

| Situation | Prefer |
|-----------|--------|
| Fast host DX, SDKs installed | Legacy |
| Consistent containers / no local SDKs | Docker host `up.bat` or `docker-up.ps1` |
| One terminal, daily coding | `scripts\dev\start-dev.bat` |
| Docker Desktop unavailable | Legacy (`start.bat` → `[1]`) |

See also: [`BATCH_FILES.md`](BATCH_FILES.md) · [`SCRIPTS_REFERENCE.md`](SCRIPTS_REFERENCE.md) · [`scripts/docker/README.md`](../scripts/docker/README.md).
