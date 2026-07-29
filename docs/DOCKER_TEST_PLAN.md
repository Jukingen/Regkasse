# Docker test plan — Regkasse

Step-by-step checks to prove your **local** Docker stack works before cloud production.

**Last updated:** 2026-07-29  
**Audience:** Developers who finished [`DOCKER_FOR_BEGINNERS.md`](DOCKER_FOR_BEGINNERS.md)

| Related | Link |
|---------|------|
| Beginners | [`DOCKER_FOR_BEGINNERS.md`](DOCKER_FOR_BEGINNERS.md) |
| Hub | [`DOCKER.md`](DOCKER.md) |
| Prod-oriented stack | [`DOCKER_PRODUCTION.md`](DOCKER_PRODUCTION.md) |
| Windows install / fix | [`DOCKER_WINDOWS_SETUP.md`](DOCKER_WINDOWS_SETUP.md) · [`DOCKER_WINDOWS_TROUBLESHOOTING.md`](DOCKER_WINDOWS_TROUBLESHOOTING.md) |

---

## Which stack are you testing?

Pick **one** track per session (do not mix Soft TSE Dev with prod Compose).

| Track | Start | Stop | Soft TSE | Best for |
|-------|--------|------|----------|----------|
| **A — Dev Compose** | `docker-up.bat` | `docker-down.bat` | Yes (override) | Learning Docker + Soft TSE |
| **B — Prod-oriented local** | `docker-up-prod.bat` | `docker-down-prod.bat` | No (Device/Real) | Pre-cloud practice |

Commands below show **both** where they differ. Prefer Track A if Fiskaly secrets are not ready; use Track B when validating fail-closed Production Compose.

### Prerequisites (both tracks)

- [ ] Docker Desktop running (`docker info` works)
- [ ] Free ports: `5432`, `6379`, `5184`, and for UIs `3000` / `8081` (optional `3001`)
- [ ] Track A: `.env` from `.env.example` (JWT ≥ 32 chars)
- [ ] Track B: `.env.production` from `.env.production.local.example` + Fiskaly keys for API uptime

### Start the stack

**Track A**

```bat
docker-up.bat
REM Optional POS + Sites:
REM   powershell -File scripts\docker-up.ps1 -Profile pos,sites -Build
```

**Track B**

```bat
docker-up-prod.bat
```

Wait until containers are `Up` / `healthy`:

```bat
docker-status.bat
```

Or:

```bat
docker compose ps
REM Track B:
docker compose -f docker-compose.prod.yml --env-file .env.production ps
```

---

## 1. Test each service individually

Mark each row **PASS** / **FAIL**. Stop and fix before continuing if API or DB fails.

### 1.1 Backend (API)

```bat
curl -fsS http://localhost:5184/api/health/live
curl -fsS http://localhost:5184/api/health
```

| Check | Expected | Result |
|-------|----------|--------|
| `/api/health/live` | HTTP 200, body `OK` | ☐ PASS ☐ FAIL |
| `/api/health` | HTTP 200 (or 503 only if DB really down) + JSON `status` | ☐ PASS ☐ FAIL |

Optional deeper (Track B / ready):

```bat
curl -fsS http://localhost:5184/api/health/ready
curl -fsS http://localhost:5184/health/tse/mode
```

| Check | Expected | Result |
|-------|----------|--------|
| `/api/health/ready` | 200 when DB + fiscal posture OK | ☐ PASS ☐ FAIL ☐ SKIP |

**If FAIL:** `docker-logs-prod.bat backend` (Track B) or  
`docker compose logs --tail 100 backend` (Track A).

---

### 1.2 Admin (FA)

Requires Admin container (Track A always builds admin; Track B needs profile `admin` — included in `docker-up-prod.bat` defaults).

1. Open a browser: [http://localhost:3000](http://localhost:3000)  
2. Prefer health probe: [http://localhost:3000/health](http://localhost:3000/health)

| Check | Expected | Result |
|-------|----------|--------|
| `/health` | JSON with `"status":"ok"` | ☐ PASS ☐ FAIL ☐ SKIP |
| `/login` (or home) | Page loads (no connection refused) | ☐ PASS ☐ FAIL ☐ SKIP |

---

### 1.3 POS (web)

Requires profile `pos`.

1. Open [http://localhost:8081](http://localhost:8081)  
2. Health: [http://localhost:8081/healthz](http://localhost:8081/healthz)

| Check | Expected | Result |
|-------|----------|--------|
| `/healthz` | `ok` | ☐ PASS ☐ FAIL ☐ SKIP |
| `/` | Static POS shell loads | ☐ PASS ☐ FAIL ☐ SKIP |

---

### 1.4 PostgreSQL (`psql` or pgAdmin)

**Defaults (Track A / `.env`):**

| Setting | Typical value |
|---------|----------------|
| Host | `127.0.0.1` |
| Port | `5432` |
| User | `postgres` (or `POSTGRES_USER`) |
| Password | from `.env` / `.env.production` |
| Database | `kasse_db` (A) or `kasse_prod_local` (B local example) |

#### Option A — `psql` via Docker (no local install)

**Track A**

```bat
docker exec -it regkasse-postgres psql -U postgres -d kasse_db -c "SELECT version();"
```

**Track B**

```bat
docker exec -it regkasse-postgres-prod psql -U postgres -d kasse_prod_local -c "SELECT version();"
```

(Adjust `-U` / `-d` to match your env file.)

#### Option B — pgAdmin / DBeaver on the host

Create a server connection to `127.0.0.1:5432` with the same credentials.

| Check | Expected | Result |
|-------|----------|--------|
| Connect succeeds | Version query or object tree visible | ☐ PASS ☐ FAIL |

---

### 1.5 Redis (`redis-cli`)

#### Via Docker (recommended)

**Track A**

```bat
docker exec -it regkasse-redis redis-cli ping
```

**Track B**

```bat
docker exec -it regkasse-redis-prod redis-cli ping
```

Expected reply: `PONG`

#### Host `redis-cli` (if installed)

```bat
redis-cli -h 127.0.0.1 -p 6379 ping
```

| Check | Expected | Result |
|-------|----------|--------|
| `PING` | `PONG` | ☐ PASS ☐ FAIL |

Optional write/read:

```bat
docker exec -it regkasse-redis redis-cli SET regkasse:docker:test 1
docker exec -it regkasse-redis redis-cli GET regkasse:docker:test
```

(Use `regkasse-redis-prod` on Track B.)

---

## 2. Test data persistence

Goal: prove **volumes** keep Postgres data after stop/start (without `-v` / volume wipe).

### 2.1 Create a marker row

**Track A**

```bat
docker exec -it regkasse-postgres psql -U postgres -d kasse_db -c "CREATE TABLE IF NOT EXISTS docker_persist_probe(id int PRIMARY KEY, note text); INSERT INTO docker_persist_probe(id, note) VALUES (1, 'hello-docker') ON CONFLICT (id) DO UPDATE SET note = EXCLUDED.note; SELECT * FROM docker_persist_probe;"
```

**Track B** (adjust DB name)

```bat
docker exec -it regkasse-postgres-prod psql -U postgres -d kasse_prod_local -c "CREATE TABLE IF NOT EXISTS docker_persist_probe(id int PRIMARY KEY, note text); INSERT INTO docker_persist_probe(id, note) VALUES (1, 'hello-docker') ON CONFLICT (id) DO UPDATE SET note = EXCLUDED.note; SELECT * FROM docker_persist_probe;"
```

| Check | Expected | Result |
|-------|----------|--------|
| Insert/select | Row `1 | hello-docker` | ☐ PASS ☐ FAIL |

### 2.2 Stop containers (**keep volumes**)

**Track A**

```bat
docker-down.bat
```

**Track B**

```bat
docker-down-prod.bat
```

Do **not** use `down -v` or `docker-down-prod.bat -Volumes` for this test.

| Check | Expected | Result |
|-------|----------|--------|
| Containers stopped | `docker ps` no longer lists the stack | ☐ PASS ☐ FAIL |

### 2.3 Start again

**Track A**

```bat
docker-up.bat
```

**Track B**

```bat
docker-up-prod.bat
```

### 2.4 Verify the row is still there

Re-run the same `SELECT * FROM docker_persist_probe;` command from §2.1.

| Check | Expected | Result |
|-------|----------|--------|
| Data survives restart | Same row `hello-docker` | ☐ PASS ☐ FAIL |

**Cleanup (optional):**

```bat
docker exec -it regkasse-postgres psql -U postgres -d kasse_db -c "DROP TABLE IF EXISTS docker_persist_probe;"
```

---

## 3. Test container logs

### 3.1 Follow / recent backend logs

**Track A**

```bat
docker compose logs backend
docker compose logs --tail=50 backend
```

**Track B**

```bat
docker-logs-prod.bat backend
```

Or:

```bat
docker compose -f docker-compose.prod.yml --env-file .env.production logs --tail=50 backend
```

| Check | Expected | Result |
|-------|----------|--------|
| Logs print | Startup / request lines visible (not empty error-only spam) | ☐ PASS ☐ FAIL |
| `--tail=50` | Only recent lines | ☐ PASS ☐ FAIL |

Follow mode (Ctrl+C to stop following):

```bat
docker compose logs -f --tail=20 backend
```

---

## 4. Test container restart

### 4.1 Restart API only

**Track A**

```bat
docker compose restart backend
```

**Track B**

```bat
docker compose -f docker-compose.prod.yml --env-file .env.production restart backend
```

### 4.2 Verify it comes back

Wait ~10–30 seconds, then:

```bat
curl -fsS http://localhost:5184/api/health/live
docker compose ps
REM Track B: add -f docker-compose.prod.yml --env-file .env.production
```

| Check | Expected | Result |
|-------|----------|--------|
| Restart completes | No error from `restart` | ☐ PASS ☐ FAIL |
| Health after restart | `/api/health/live` → `OK` | ☐ PASS ☐ FAIL |
| Status | `backend` is `Up` / `healthy` | ☐ PASS ☐ FAIL |

---

## 5. Test resource usage

### 5.1 Live stats

```bat
docker stats
```

Press **Ctrl+C** when done (containers keep running).

One container:

```bat
REM Track A
docker stats regkasse-backend

REM Track B
docker stats regkasse-backend-prod
```

Snapshot (no refresh):

```bat
docker stats --no-stream
```

| Check | Expected | Result |
|-------|----------|--------|
| `docker stats` runs | CPU % and MEM USAGE columns visible | ☐ PASS ☐ FAIL |
| Values look sane | Not stuck at limit / constant restart | ☐ PASS ☐ FAIL ☐ N/A |

Optional: Docker Desktop → Containers → select backend → **Stats**.

---

## Scorecard

| Section | Pass? | Notes |
|---------|-------|-------|
| 1 Services (API / Admin / POS / PG / Redis) | ☐ | |
| 2 Persistence | ☐ | |
| 3 Logs | ☐ | |
| 4 Restart | ☐ | |
| 5 Resources | ☐ | |

**Session:** _______________  **Track:** A / B  **Date:** _______________

### Exit criteria (local Docker “thorough”)

- [ ] API live health OK  
- [ ] Postgres + Redis reachable  
- [ ] At least one UI health endpoint OK (Admin or POS) if that profile is running  
- [ ] Persistence probe survived down/up **without** `-v`  
- [ ] Backend restart + health OK  
- [ ] `docker stats` understood  

When all are green on **Track B**, you are ready for staging-host / cloud discussions in [`DOCKER_PRODUCTION.md`](DOCKER_PRODUCTION.md) and [`NEXT_STEPS_AFTER_SCRIPTS.md`](NEXT_STEPS_AFTER_SCRIPTS.md).

---

## Quick troubleshooting

| Problem | Fix |
|---------|-----|
| Connection refused on `:5184` | Stack not up; check `docker-status.bat` / logs |
| Admin/POS connection refused | Profile not started; Track B: re-run `docker-up-prod.bat` |
| Postgres auth failed | Wrong password/DB vs `.env` / `.env.production` |
| Persistence failed | You used `down -v` or wiped volumes; repeat without `-Volumes` |
| API restart loop (Track B) | Fiskaly / TSE lock — see logs + [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) |
| Port busy | Stop the other track (`docker-down.bat` vs `docker-down-prod.bat`) |

Doctor:

```bat
scripts\docker-diagnose.bat
```
