# Docker vs Legacy Mode — Comparison

> **Entry point:** [`start.bat`](../start.bat) at the repository root  
> **Folders:** [`scripts/legacy/`](../scripts/legacy/) · [`scripts/docker/`](../scripts/docker/)  
> **Shared logs:** `C:\Scripts\logs` (both modes)

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
REM From repo root (or via start.bat → [1])
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
| `start-all.bat` | Start everything (separate windows) |
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

**Commands:**

```batch
REM From repo root (or via start.bat → [2])
scripts\docker\docker-up.bat
scripts\docker\docker-down.bat
scripts\docker\docker-status.bat
scripts\docker\docker-logs.bat
scripts\docker\docker-clean.bat

REM Partial stacks
scripts\docker\docker-up-backend.bat
scripts\docker\docker-up-admin.bat
scripts\docker\docker-up-pos.bat

REM Root wrappers also work: docker-up.bat, docker-down.bat, …
```

| Script | Purpose |
|--------|---------|
| `docker-up.bat` | Start everything |
| `docker-down.bat` | Stop everything |
| `docker-status.bat` | Check status |
| `docker-logs.bat` | View logs |
| `docker-clean.bat` | Clean everything (**destructive** — volumes wiped) |
| `docker-up-backend.bat` | Infra + API only |
| `docker-up-admin.bat` | Infra + API + Admin |
| `docker-up-pos.bat` | Infra + API + POS web |

**Logs:** `C:\Scripts\logs\docker.log`, `docker_down.log`, `docker_status.log`, `docker_logs.log`, `docker_clean.log`, …

---

## Comparison Table

| Feature | Legacy | Docker |
|---------|--------|--------|
| Node.js needed | Yes | No |
| .NET needed | Yes | No |
| PostgreSQL needed | Yes | No |
| Redis needed | Yes | No |
| Startup time | Fast | Slow (first time) |
| Memory usage | Low | Medium |
| Cleanup | Manual | Easy |
| Team consistency | Varies | Same |
| Production ready | No | Yes |
| POS hot reload | Yes (Expo Metro) | No (static web export) |

> Docker still needs **Docker Desktop** installed. Host tooling (Node / .NET / Postgres / Redis) is not required for the Compose stack.

---

## Recommendation

**Start with Legacy** for daily development (faster, less resource).

**Use Docker for:**

- Testing a production-like environment
- Onboarding new team members
- Deployment testing
- When you want to test without installing dependencies

Or pick either mode from the root chooser:

```batch
start.bat
```

---

## Quick Switch

```batch
REM Current mode (Legacy)
scripts\legacy\start-all.bat
REM or: start.bat → [1]

REM Switch to Docker
scripts\docker\docker-down.bat
REM (if anything was already up)
scripts\docker\docker-up.bat
REM or: start.bat → [2] / root docker-up.bat

REM Switch back to Legacy
scripts\docker\docker-down.bat
scripts\legacy\start-all.bat
```

Stop Legacy Redis/windows (or run `kill-ports.bat`) before `docker-up`, and stop Compose before `start-all` — do not run both Redis instances on **6379**.

---

## Conflict checklist

1. Do **not** run Legacy Redis and Docker Redis together (both use **6379**).
2. Prefer **one** mode per session.
3. Stuck ports → `scripts\legacy\kill-ports.bat`.

---

## Related

- [`BATCH_FILES.md`](BATCH_FILES.md) — root `.bat` inventory  
- [`DOCKER_SETUP.md`](DOCKER_SETUP.md) / [`DOCKER_WINDOWS_SETUP.md`](DOCKER_WINDOWS_SETUP.md) — Compose setup  
- [`GETTING_STARTED_SCRIPTS.md`](GETTING_STARTED_SCRIPTS.md) — scripts onboarding  
- [`scripts/test-mode-scripts.bat`](../scripts/test-mode-scripts.bat) — structural smoke for mode scripts  
