# Docker production readiness & migration plan — Regkasse

Use this document as the **gate** before real fiscal traffic on a production (or staging-as-prod) Docker host.

**Last updated:** 2026-07-29  
**Rule:** Do **not** start Step 3 (production deploy) until the checklist below is complete (all boxes checked, with evidence).

| Related | Link |
|---------|------|
| Local test plan | [`DOCKER_TEST_PLAN.md`](DOCKER_TEST_PLAN.md) |
| Prod Compose guide | [`DOCKER_PRODUCTION.md`](DOCKER_PRODUCTION.md) |
| Env vars | [`DOCKER_ENV_VARS.md`](DOCKER_ENV_VARS.md) |
| Beginners | [`DOCKER_FOR_BEGINNERS.md`](DOCKER_FOR_BEGINNERS.md) |
| TSE fail-closed | [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) |
| Cutover | [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md) |
| Deploy / Actions | [`../DEPLOYMENT.md`](../DEPLOYMENT.md) · [`CI_CD.md`](CI_CD.md) |
| Monitoring | [`MONITORING.md`](MONITORING.md) · [`ALERTING.md`](ALERTING.md) |
| Next steps | [`NEXT_STEPS_AFTER_SCRIPTS.md`](NEXT_STEPS_AFTER_SCRIPTS.md) |

---

## Part A — Docker production readiness checklist

Complete on a **local** machine first (Track B: `docker-up-prod.bat`), then re-confirm on the **staging host** before production.

**Evidence columns:** note date + how you verified (command / screenshot / ticket).

### 1. All services run in Docker locally

- [ ] Postgres container healthy  
- [ ] Redis container healthy  
- [ ] Backend (API) healthy (`/api/health/live`)  
- [ ] Admin UI up (if in scope) — `/health`  
- [ ] POS web up (if in scope) — `/healthz`  
- [ ] Sites up (if in scope) — `/health`  

| How | Evidence |
|-----|----------|
| `docker-up-prod.bat` + [`DOCKER_TEST_PLAN.md`](DOCKER_TEST_PLAN.md) §1 | _______________ |

### 2. Data persistence works (volumes)

- [ ] Marker data written to Postgres  
- [ ] Stack stopped **without** wiping volumes (`docker-down-prod.bat`, no `-Volumes`)  
- [ ] Stack started again; marker data still present  

| How | Evidence |
|-----|----------|
| [`DOCKER_TEST_PLAN.md`](DOCKER_TEST_PLAN.md) §2 | _______________ |

### 3. Health checks work

- [ ] Dockerfile / Compose healthchecks show `healthy` (not stuck `starting` / `unhealthy`)  
- [ ] `GET /api/health/live` → 200  
- [ ] `GET /api/health/ready` → 200 (DB + fiscal posture)  
- [ ] Optional: `/health/tse/mode` safe for Device/Real  

| How | Evidence |
|-----|----------|
| `docker compose … ps` + curl | _______________ |

### 4. Logs are accessible

- [ ] Can follow backend logs (`docker-logs-prod.bat backend`)  
- [ ] Can limit output (`logs --tail=50`)  
- [ ] Know where to look on failure (TSE lock, DB, Fiskaly)  

| How | Evidence |
|-----|----------|
| [`DOCKER_TEST_PLAN.md`](DOCKER_TEST_PLAN.md) §3 | _______________ |

### 5. Container restart works

- [ ] `restart backend` succeeds  
- [ ] Health returns after restart  

| How | Evidence |
|-----|----------|
| [`DOCKER_TEST_PLAN.md`](DOCKER_TEST_PLAN.md) §4 | _______________ |

### 6. Resource limits are set

- [ ] `docker-compose.prod.yml` defines `deploy.resources.limits` (or env overrides) for Postgres / Redis / API / UIs  
- [ ] `docker stats` reviewed under light load  
- [ ] Host has headroom (disk + RAM) for peaks + images  

| How | Evidence |
|-----|----------|
| Compose file + `docker stats` | _______________ |

### 7. Environment variables are configured

- [ ] `.env.production` exists **only on the host** (not committed)  
- [ ] `POSTGRES_*`, `JWT_SECRET_KEY` (≥32), `ADMIN_API_URL` / `POS_API_URL` set for the **target** DNS  
- [ ] Fiskaly (or vendor) secrets set for Device/Real  
- [ ] `FINANZONLINE_MODE` aligned with cutover (`Test` until approved, then `Production`)  
- [ ] Frontend **rebuild** done after changing public URL build-args  

| How | Evidence |
|-----|----------|
| [`DOCKER_ENV_VARS.md`](DOCKER_ENV_VARS.md) checklist | _______________ |

### 8. Docker images are optimized (size)

- [ ] Multi-stage Dockerfiles in use (backend / admin / sites / POS)  
- [ ] Backend self-contained publish; Admin/Sites production `node_modules` (`omit=dev`) where applicable  
- [ ] `.dockerignore` / `Dockerfile.dockerignore` keep build context small  
- [ ] Images rebuilt from clean context; no secrets baked into layers  

| How | Evidence |
|-----|----------|
| `docker images` sizes + Dockerfile review | _______________ |

Optional size snapshot:

```bat
docker images "regkasse-*"
```

### 9. Security best practices applied

- [ ] Ports bound to `127.0.0.1` (or private network); TLS reverse proxy in front for public DNS  
- [ ] Soft TSE / FON **simulation** not enabled on this stack (`docker-compose.override.yml` **not** merged)  
- [ ] `TseProductionOptionsValidator` fail-closed verified (unsafe Soft/Demo rejected)  
- [ ] Non-root where Dockerfiles set it (API `$APP_UID`, Next `nextjs` user)  
- [ ] `/metrics` not exposed publicly without auth / network restriction  
- [ ] Secrets only via env / secret store; no JWT/DB passwords in git or image  
- [ ] CSRF + SuperAdmin 2FA enabled for Production (`CSRF_ENABLED` / `TWO_FACTOR_ENABLED`)  
- [ ] Backup story known (FA System/Tenant backup + volume implications)  

| How | Evidence |
|-----|----------|
| [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) · [`MONITORING.md`](MONITORING.md) | _______________ |

---

### Gate decision

| Field | Value |
|-------|--------|
| Checklist completed by | _______________ |
| Date | _______________ |
| Track verified | Local ☐ · Staging host ☐ |
| **Ready for Part B Step 3 (production)?** | ☐ YES — all 1–9 complete · ☐ NO — stop |

If **NO**, fix gaps using [`DOCKER_TEST_PLAN.md`](DOCKER_TEST_PLAN.md) and [`DOCKER_PRODUCTION.md`](DOCKER_PRODUCTION.md). Do not skip ahead.

---

## Part B — Production Docker migration plan

Four steps. Each step has an exit criterion. **Only proceed when the previous step’s exit criterion is met** and Part A is complete before Step 3.

```text
  Part A checklist (local + staging) ──► Step 1 server ──► Step 2 dry run
                                              │
                                              ▼
                                         Step 3 production
                                              │
                                              ▼
                                         Step 4 monitor
```

### Step 1 — Set up production server with Docker

**Goal:** A clean Linux (or Windows Server) host that can run Compose and hold secrets safely.

| Task | Done |
|------|------|
| Provision VM / bare metal (CPU/RAM/disk sized from `docker stats` + growth) | ☐ |
| Install Docker Engine + Compose v2 (`docker info` OK) | ☐ |
| Create deploy user; SSH keys; disable password SSH if policy requires | ☐ |
| Firewall: only 22/80/443 (and admin VPN) public; DB/Redis **not** public | ☐ |
| Install TLS reverse proxy (Caddy / nginx / Traefik) for `api` / `admin` / `pos` | ☐ |
| Clone or sync Regkasse release artifact / tag onto the host | ☐ |
| Copy `.env.production` from vault (never chat/email); chmod restricted | ☐ |
| Confirm DNS A/AAAA for `api.regkasse.at`, `admin.regkasse.at`, `pos.regkasse.at` | ☐ |
| Optional: attach monitoring stack ([`MONITORING.md`](MONITORING.md)) on loopback | ☐ |

**Exit criterion:** `docker info` OK; proxy terminates TLS; `.env.production` present; checklist Part A items 6–9 reviewed for **this** host.

---

### Step 2 — Test deployment (dry run)

**Goal:** Prove pull/build/up/smoke **without** sending real customer fiscal traffic (prefer a **staging** hostname or maintenance window with Soft TSE still **forbidden**).

| Task | Done |
|------|------|
| Prefer staging host OR production host behind maintenance page | ☐ |
| `docker-build-prod` / `docker compose … up -d --build` (or GHCR pull + tag) | ☐ |
| Profiles as needed: `admin` / `sites` / `pos` | ☐ |
| Smoke: `/api/health/live`, `/api/health/ready`, Admin login (read-only), RKSV status **read** (no fiscal write if dry-run) | ☐ |
| Confirm TSE mode Device/Real; FON mode as intended (`Test` until cutover) | ☐ |
| Trigger **manual** System or Tenant backup via FA/API; note artifact | ☐ |
| Practice rollback: previous image tag / `docker compose` recreate | ☐ |
| Record issues in a short runbook note | ☐ |

Commands (illustrative):

```bash
# On the server (Linux example)
cp .env.production.example .env.production   # then edit from vault
docker compose -f docker-compose.prod.yml --env-file .env.production \
  --profile admin up -d --build

curl -fsS https://api.staging.example/api/health/live
curl -fsS https://api.staging.example/api/health/ready
```

Windows operator path: `docker-up-prod.bat` on a staging VM with Docker Desktop/Engine.

**Exit criterion:** Smoke PASS; backup noted; rollback rehearsed; Part A checklist re-checked on this host; **no** open P0 on TSE/FON/DB.

---

### Step 3 — Deploy to production

**Goal:** Customer-facing cutover with compliance and backup.

**Pre-conditions (hard stop if missing):**

- [ ] Part A checklist **YES**  
- [ ] Step 1 + Step 2 exit criteria met  
- [ ] [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md) / FON cutover as required  
- [ ] Pre-deploy backup completed and verified listable  
- [ ] Change window + rollback owner named  

| Task | Done |
|------|------|
| Announce maintenance / canary plan if applicable | ☐ |
| Prefer image promotion via Actions (**Deploy Production** / tag `v*`) **or** host Compose with known-good `IMAGE_TAG` | ☐ |
| Apply migrations via approved path ([`DATABASE_MIGRATION_STRATEGY.md`](DATABASE_MIGRATION_STRATEGY.md)) | ☐ |
| Deploy app containers; wait for healthy | ☐ |
| Point/verify TLS proxy → loopback ports | ☐ |
| Smoke on **production** URLs (live + ready + Admin login) | ☐ |
| Fiscal: one controlled verification per runbook (not a full load test) | ☐ |

Prefer cloud gate: Actions → **Deploy Production** (compliance phrases + Environments) — see [`DEPLOYMENT.md`](../DEPLOYMENT.md) · [`CI_CD.md`](CI_CD.md).

Host Compose alternative: `deploy-docker.bat` / Compose up with production `.env.production` (HTTPS public URLs baked into frontends).

**Exit criterion:** Production smoke PASS; no critical alerts; rollback path still available (previous tag).

---

### Step 4 — Monitor and verify

**Goal:** First 24–48h confidence.

| Task | Done |
|------|------|
| Watch `/api/health/ready` and `/health/tse/mode` | ☐ |
| Enable/confirm Alertmanager or Slack (`ONCALL_WEBHOOK_URL` / [`ALERTING.md`](ALERTING.md)) | ☐ |
| Grafana / FA `/admin/monitoring` / Sentry (if configured) | ☐ |
| Check FON / TSE activity for unexpected failures | ☐ |
| Confirm backups still scheduling | ☐ |
| Team announcement: “production Docker path live” + on-call | ☐ |

**Exit criterion:** No unresolved critical alerts; on-call knows log/restart/rollback commands; checklist archived with evidence dates.

---

## Suggested evidence pack (attach to ticket)

1. Output of `docker compose … ps` (healthy)  
2. Curl of live + ready  
3. Persistence test notes  
4. `docker images` size snapshot  
5. Backup artifact id / timestamp  
6. Smoke run id or pasted summary  
7. Signed Part A gate decision  

---

## What “done” looks like

You can say **production Docker is ready** when:

1. Part A items **1–9** are checked with evidence  
2. Steps **1–4** exit criteria are met  
3. TSE/FON cutover docs are satisfied for real fiscal traffic  
4. Rollback was practiced at least once on staging  

Until then: keep practicing on local Track B + staging dry runs. Do not rush Step 3.
