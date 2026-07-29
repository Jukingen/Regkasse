# Docker for Beginners — Regkasse

A calm, practical guide so you can use Docker locally with confidence **before** thinking about cloud production.

**Last updated:** 2026-07-29  
**Audience:** Developers new to Docker (Windows + Docker Desktop)

| Next steps | Doc |
|------------|-----|
| Install Docker Desktop | [`DOCKER_WINDOWS_SETUP.md`](DOCKER_WINDOWS_SETUP.md) |
| Fix common Windows issues | [`DOCKER_WINDOWS_TROUBLESHOOTING.md`](DOCKER_WINDOWS_TROUBLESHOOTING.md) |
| Regkasse Docker hub | [`DOCKER.md`](DOCKER.md) |
| Local prod-style stack | [`DOCKER_PRODUCTION.md`](DOCKER_PRODUCTION.md) · `docker-up-prod.bat` |

> **Tip:** Prefer our `.bat` helpers (`docker-up-prod.bat`, `docker-status.bat`, `docker-logs-prod.bat`) while you learn. The raw `docker` / `docker compose` commands below explain what those scripts are doing.

---

## 1. What Docker actually does (simple terms)

Think of your laptop as a house.

| Without Docker | With Docker |
|----------------|-------------|
| You install PostgreSQL, Redis, .NET, Node… directly on Windows | Each piece runs in its own **shipping container** |
| “Works on my machine” fights are common | Everyone runs the **same packaged recipe** |
| Upgrading one tool can break another | Containers are isolated from each other |

**Docker** packages an app (and its dependencies) into an **image** (a recipe / snapshot).  
When you run an image, you get a **container** (a running instance — like starting a prepared meal from that recipe).

```text
  Dockerfile  →  builds  →  Image  →  runs as  →  Container
  (recipe)                  (frozen meal)         (hot plate on your desk)
```

**Docker Compose** is a shopping list for **several** containers that belong together (API + database + Redis + Admin…). One file describes the whole mini-system.

### Regkasse example

When you run the production-oriented stack locally:

| Container (typical name) | Job |
|--------------------------|-----|
| `regkasse-postgres-prod` | Database |
| `regkasse-redis-prod` | Cache |
| `regkasse-backend-prod` | ASP.NET API |
| `regkasse-frontend-admin-prod` | Admin UI (optional profile) |
| `regkasse-frontend-sites-prod` | Tenant sites (optional) |
| `regkasse-frontend-pos-prod` | POS web (optional) |

They talk to each other on a private Docker network. Your browser reaches them via `localhost` ports (e.g. API `5184`).

```
┌─────────────────────────────────────────────────────────┐
│  YOUR WINDOWS PC                                        │
│                                                         │
│   Browser ──► http://127.0.0.1:5184  (API)              │
│           ──► http://127.0.0.1:3000  (Admin)            │
│                                                         │
│   ┌────────────── Docker Desktop ─────────────────────┐ │
│   │  [postgres] [redis] [backend] [admin] …           │ │
│   └───────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────┘
```

<!-- SCREENSHOT: Docker Desktop home — Containers list showing Regkasse containers -->
> **Screenshot placeholder:** Docker Desktop → **Containers** tab with your `regkasse-*` containers listed (green “Running”).

---

## 2. What `docker compose up -d` does (step by step)

Command shape (production-oriented Regkasse example):

```bat
docker compose -f docker-compose.prod.yml --env-file .env.production --profile admin up -d --build
```

Or simply: **`docker-up-prod.bat`** (does the same idea for you).

### Step by step

1. **Read the Compose file**  
   Looks at `docker-compose.prod.yml` and sees which services to run.

2. **Load environment variables**  
   Reads `.env.production` (passwords, ports, public API URL for frontends, Fiskaly keys…).

3. **Apply profiles** (if any)  
   e.g. `--profile admin` also starts the Admin UI. Without profiles you may only get Postgres + Redis + API.

4. **Build images** (when you pass `--build`, or when the image is missing)  
   Runs each `Dockerfile` (backend, admin, …) and creates/updates images on your machine.

5. **Create a network + volumes**  
   - Network: so containers can find each other by name (`postgres`, `redis`, `backend`).  
   - Volumes: so database data **survives** when you stop containers.

6. **Start containers in the background** (`-d` = **detached**)  
   They keep running after the command finishes. Your terminal is free again.

7. **Healthchecks / depends_on**  
   Compose waits for Postgres/Redis to be healthy before starting the API (when configured that way).

8. **Publish ports**  
   Maps container ports to `127.0.0.1:5184`, `:3000`, etc. so your browser can connect.

### Flags you will see often

| Flag | Meaning |
|------|---------|
| `-f docker-compose.prod.yml` | Use this Compose file (not the default `docker-compose.yml`) |
| `--env-file .env.production` | Load secrets/settings from this file |
| `--profile admin` | Include optional services tagged with that profile |
| `up` | Create and start |
| `-d` | Detached (background) |
| `--build` | Rebuild images before starting |

**Opposite command:** `docker compose … down` (or `docker-down-prod.bat`) — stops and removes the containers (data in volumes is kept unless you add `-v`).

---

## 3. How to check if Docker is running

### A) Docker Desktop (easiest)

1. Open **Docker Desktop** from the Start menu.  
2. Look at the whale icon in the system tray / taskbar.  
3. Status should say **Engine running** (not “Starting…” or an error).

<!-- SCREENSHOT: Docker Desktop dashboard showing green Engine running -->
> **Screenshot placeholder:** Docker Desktop dashboard with **Engine running**.

### B) Terminal (definitive)

```bat
docker info
```

- **Success:** lots of text (Server Version, Containers, …).  
- **Failure:** `error during connect` / `docker is not recognized` → Desktop not running or not installed.

Quick version:

```bat
docker version
```

Regkasse helper:

```bat
scripts\docker-diagnose.bat
```

or:

```bat
docker-status.bat
```

### C) Are *our* containers up?

```bat
docker compose -f docker-compose.prod.yml --env-file .env.production ps
```

Or open Docker Desktop → **Containers**.

You want `STATUS` like `Up` / `healthy` (not `Restarting` or `Exited`).

---

## 4. How to view Docker logs

Logs = what the app printed inside the container (errors, startup messages).

### All services (follow live)

```bat
docker-logs-prod.bat
```

Or:

```bat
docker compose -f docker-compose.prod.yml --env-file .env.production --profile admin --profile sites --profile pos logs -f --tail 200
```

- `-f` = follow (Ctrl+C to stop following; containers keep running).  
- `--tail 200` = last 200 lines first.

### One service only

```bat
docker-logs-prod.bat backend
```

Raw Compose:

```bat
docker compose -f docker-compose.prod.yml --env-file .env.production logs -f --tail 200 backend
```

Service names in our prod file: `postgres`, `redis`, `backend`, `frontend-admin`, `frontend-sites`, `frontend`.

### By container name

```bat
docker logs -f --tail 200 regkasse-backend-prod
```

<!-- SCREENSHOT: Terminal showing backend logs with health/startup lines -->
> **Screenshot placeholder:** Terminal with scrolling `regkasse-backend-prod` logs.

### When the API keeps dying

Often missing Fiskaly secrets or TSE lock. Check:

```bat
docker-logs-prod.bat backend
```

Look for `TseProductionConfigRejected` or similar — see [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md).

---

## 5. How to restart a single container

You do **not** need to tear down the whole stack.

### By Compose service name (recommended)

```bat
docker compose -f docker-compose.prod.yml --env-file .env.production restart backend
```

Other examples:

```bat
docker compose -f docker-compose.prod.yml --env-file .env.production restart postgres
docker compose -f docker-compose.prod.yml --env-file .env.production --profile admin restart frontend-admin
```

### By container name

```bat
docker restart regkasse-backend-prod
```

### Stop / start (vs restart)

```bat
docker stop regkasse-backend-prod
docker start regkasse-backend-prod
```

**Restart** = stop + start in one step. Useful after changing host env that was already injected…  
**Note:** Changing `.env.production` usually needs recreate, not just restart:

```bat
docker compose -f docker-compose.prod.yml --env-file .env.production up -d --force-recreate backend
```

Or re-run `docker-up-prod.bat` after editing `.env.production`.

---

## 6. How to check container resources (CPU, memory)

### A) Docker Desktop (visual)

1. Docker Desktop → **Containers**.  
2. Click a running container.  
3. Open the **Stats** / resource graphs (CPU %, memory).

<!-- SCREENSHOT: Container stats graphs for CPU and memory -->
> **Screenshot placeholder:** Container detail page with CPU / Memory charts.

### B) Live stats in the terminal

All containers:

```bat
docker stats
```

One container:

```bat
docker stats regkasse-backend-prod
```

You will see columns like:

| Column | Meaning |
|--------|---------|
| `CPU %` | How busy the container’s CPU share is |
| `MEM USAGE / LIMIT` | RAM used vs allowed |
| `MEM %` | Memory as a percentage of its limit |
| `NET I/O` | Network traffic |
| `BLOCK I/O` | Disk read/write |

Press **Ctrl+C** to leave `docker stats` (containers keep running).

### C) One-shot snapshot (no live refresh)

```bat
docker stats --no-stream
```

### Limits in our prod Compose

`docker-compose.prod.yml` sets optional CPU/memory **limits** (see `.env.production.example`).  
If a container hits its memory limit, Docker may kill/restart it — `docker stats` helps you spot that.

---

## Common commands cheat sheet (Regkasse)

| Goal | Command / script |
|------|------------------|
| Is Docker alive? | `docker info` |
| Diagnose Windows Docker | `scripts\docker-diagnose.bat` |
| Start local prod-style stack | `docker-up-prod.bat` |
| Stop it (keep DB data) | `docker-down-prod.bat` |
| List containers | `docker-status.bat` or `docker ps` |
| Follow logs | `docker-logs-prod.bat` / `… backend` |
| Restart API only | `docker compose -f docker-compose.prod.yml --env-file .env.production restart backend` |
| CPU / RAM | `docker stats` |
| Open a shell *inside* API container | `docker exec -it regkasse-backend-prod sh` (or `bash` if present) |
| Free unused images (careful) | `docker system prune` (ask a teammate before using on shared machines) |

---

## Mental model: Dev vs “prod-oriented” local Docker

| Mode | File | Soft TSE? | When |
|------|------|-----------|------|
| Dev full stack | `docker-compose.yml` + override | Yes (OK for coding) | Daily Soft TSE work |
| Infra only | `docker-compose.dev.yml` | N/A (apps on host) | `npm run dev` + DB in Docker |
| **Prod-oriented local** | `docker-compose.prod.yml` | **No** (Device/Real) | Practice before cloud |

Never mix Soft TSE override with the production Compose file.

---

## Troubleshooting tips

| Symptom | What to try |
|---------|-------------|
| `docker` not recognized | Install/start Docker Desktop; open a **new** terminal |
| Engine not running | Start Docker Desktop; wait until green |
| Port already in use (`5184`, `5432`…) | Stop other stacks: `docker-down.bat` / `docker-down-prod.bat`; or change ports in `.env.production` |
| Container `Restarting` loop | `docker-logs-prod.bat <service>` — read the last error |
| API exits immediately | Fiskaly / TSE lock — fill `.env.production`; see TSE lock doc |
| “No such service” | Wrong Compose file or missing `--profile` |
| Changes to `.env.production` ignored | Recreate: `up -d --force-recreate` or re-run `docker-up-prod.bat` |
| Admin UI still calls wrong API | `NEXT_PUBLIC_*` are **build-time** — rebuild Admin image after changing `ADMIN_API_URL` |
| Disk full | Docker Desktop → Settings → Resources / Disk; prune unused images carefully |
| WSL / Hyper-V weirdness | [`DOCKER_WINDOWS_TROUBLESHOOTING.md`](DOCKER_WINDOWS_TROUBLESHOOTING.md) |

Regkasse one-shot doctor:

```bat
scripts\docker-diagnose.bat
```

---

## Practice checklist (feel ready)

Do these once on your machine; tick them off:

- [ ] `docker info` succeeds  
- [ ] `docker-up-prod.bat` starts (or `-ApiOnly` via PowerShell if you skip UIs)  
- [ ] Browser opens `http://127.0.0.1:5184/api/health/live` → `OK`  
- [ ] `docker-logs-prod.bat backend` shows live lines  
- [ ] `docker compose … restart backend` and health still works  
- [ ] `docker stats` shows CPU/memory for `regkasse-backend-prod`  
- [ ] `docker-down-prod.bat` stops the stack; data still there after `docker-up-prod.bat` again  

When this feels boring, you are ready to read [`DOCKER_PRODUCTION.md`](DOCKER_PRODUCTION.md) and cloud deploy docs without panic.

**Next:** run the checklist in [`DOCKER_TEST_PLAN.md`](DOCKER_TEST_PLAN.md) (services, persistence, logs, restart, `docker stats`).

---

## Glossary (tiny)

| Word | Plain meaning |
|------|----------------|
| **Image** | Frozen package of an app |
| **Container** | A running copy of an image |
| **Compose** | Tool + YAML file to run many containers together |
| **Volume** | Persistent disk for data (e.g. Postgres) |
| **Port publish** | Map container port → `localhost:…` on your PC |
| **Detached (`-d`)** | Run in the background |
| **Healthcheck** | Periodic “are you OK?” inside the container |

---

## Related

- [`DOCKER.md`](DOCKER.md) — project Docker map  
- [`DOCKER_PRODUCTION.md`](DOCKER_PRODUCTION.md) — prod-oriented Compose  
- [`DOCKER_ENV_VARS.md`](DOCKER_ENV_VARS.md) — what goes in `.env.production`  
- [`GETTING_STARTED_SCRIPTS.md`](GETTING_STARTED_SCRIPTS.md) — Windows `.bat` onboarding  
