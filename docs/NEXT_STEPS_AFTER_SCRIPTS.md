# Next steps after the script ecosystem

> **Date:** 2026-07-29  
> **Context:** Windows `.bat` DX is complete, including **Legacy vs Docker** modes (`start.bat`, `scripts/legacy/`, `scripts/docker/`). See [`DOCKER_VS_LEGACY.md`](DOCKER_VS_LEGACY.md).

---

## Done recently (modes)

- [x] `scripts/legacy/` — host multi-window starters (from `C:\Scripts`)
- [x] `scripts/docker/` — Compose bats with `C:\Scripts\logs`
- [x] Root `start.bat` mode chooser
- [x] Comparison guide [`DOCKER_VS_LEGACY.md`](DOCKER_VS_LEGACY.md)
- [x] Smoke: `scripts\test-mode-scripts.bat` (Compose live tests need Docker Desktop)

**Rollback:** If Docker Desktop is unavailable, use Legacy (`start.bat` → `[1]`).

---

## Priority matrix

| # | Theme | Business value | Effort | Risk if deferred | Dependencies | Verdict |
|---|--------|----------------|--------|------------------|--------------|---------|
| **1** | **Production Docker path (verified)** | High — real cutover / staging host | Medium | High — wrong TSE/FON mode in “prod-like” Compose | Scripts + `docker-compose.prod.yml` + TSE lock docs | **Do next** |
| **2** | CI/CD hardening (not greenfield) | High — safe releases | Medium | Medium — wire secrets/Environments; use `ci.yml` / `deploy.yml` + existing prod gates | Secrets, Environments, compliance | Parallel / after Docker verify |
| **3** | Pre-deploy backup automation | High — restore story | Low–Medium | Medium — FA backup already exists; deploy still manual | Backup APIs + deploy workflows | Fold into #1 / #2 |
| **4** | Monitoring & alerting | Medium–High — MTTR | Medium | Medium — Slack/Sentry/activity already partial | Stable prod URL + health | After #1–#2 |
| **5** | Documentation polish | Medium — onboarding | Low | Low | Scripts docs done | Continuous, not a blocker |

### Why not “build CI/CD from scratch”?

The repo **already has**:

- [`deploy-production.yml`](../.github/workflows/deploy-production.yml), [`deploy-canary.yml`](../.github/workflows/deploy-canary.yml), [`backend-ci.yml`](../.github/workflows/backend-ci.yml)
- Compliance gate docs: [`DEPLOYMENT_COMPLIANCE.md`](DEPLOYMENT_COMPLIANCE.md)
- Backup system (Tenant/System, schedules, FA UI) — see [`BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md)

**Gap:** prove the **prod Compose + secrets + TSE fail-closed** path on a real host, then tighten automation around what already exists.

### Why not monitoring first?

Without a known-good production/staging stack, alerts are noisy. Health endpoints and Slack failure notify already exist; deepen monitoring after deploy path is trusted.

### Parallel risk (do not ignore)

**Test suite debt** (Backend ~58 fails / Admin many Invalid-hook failures from the scripts session) undermines CI confidence. Track separately; do not block Docker prod verification, but fix before relying on `test-all.bat` as a release gate.

---

## Recommended next step

### Production Docker setup — verify & operationalize

**Goal:** A teammate can bring up a **production-oriented** stack with Device/Real TSE fail-closed config, documented secrets, health checks, and a pre-deploy backup confirmation — without inventing a second architecture.

**In scope**

1. Verify [`docker-compose.prod.yml`](../docker-compose.prod.yml) + [`.env.production.example`](../.env.production.example) on a **non-prod** host (staging VM or spare machine with Docker Desktop/Engine).
2. Align with [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) and [`DOCKER_SETUP.md`](DOCKER_SETUP.md) (Profile C).
3. Harden operator path: `scripts\docker-deploy.ps1` / `deploy.bat` vs GitHub Actions — document which is for **host Compose** vs **cloud CD**.
4. Add/confirm **pre-deploy backup** step (API/FA) into the operator checklist and, if feasible, a CI job that calls existing backup trigger before production Environment deploy.
5. Smoke: `/api/health`, Admin login, one RKSV status read (no fiscal write in dry-run).

**Out of scope (later)**

- Full canary soak redesign  
- New APM product selection  
- Rewriting backup storage tiers  

---

## Plan (1–2 weeks, incremental)

### Phase A — Inventory (½ day)

| Task | Owner hint | Done when |
|------|------------|-----------|
| List secrets required by `.env.production.example` | Platform | Checklist in DEPLOYMENT.md |
| Confirm TSE mode flags for prod Compose | Fiscal + platform | Matches TSE lock doc |
| Confirm Postgres/Redis volume strategy | Platform | Documented backup implication |
| Map `deploy.bat` vs `deploy-production.yml` | Platform | One paragraph in GETTING_STARTED / DEPLOYMENT |

### Phase B — Staging host dry-run (2–3 days)

| Task | Done when |
|------|-----------|
| Install Docker Engine/Desktop on staging host | `docker info` OK |
| Copy `.env.production.example` → `.env.production` (no secrets in git) | App starts fail-closed if TSE misconfigured |
| `scripts\docker-deploy.ps1` or compose prod up | Containers healthy |
| `curl` health + Admin reachable | Smoke PASS |
| Trigger **manual** Tenant or System backup via FA/API | Artifact listed |
| Record issues in a short runbook note | Link from DEPLOYMENT.md |

### Phase C — Wire safety into automation (2–3 days)

| Task | Done when |
|------|-----------|
| Pre-deploy backup: document required FA/API call; optional GH step calling existing backup API with deploy token | Cannot skip without explicit override |
| Ensure `deploy-production.yml` Environments + compliance phrase still required | Matches DEPLOYMENT_COMPLIANCE |
| Add/adjust Slack notify on deploy failure (existing `notify-failure.yml`) | Secret set on staging/prod |
| Update `docs/GETTING_STARTED_SCRIPTS.md` + `DEPLOYMENT.md` with “host Compose vs Actions” | Team can follow without tribal knowledge |

### Phase D — Exit criteria

- [ ] Staging host runs prod Compose with **locked** TSE production settings  
- [ ] Backup taken and noted before a practice deploy  
- [ ] Health + Admin smoke pass  
- [ ] Rollback story documented (`rollback.bat` = local git reset; cloud = Actions / previous image tag)  
- [ ] Team announcement: “staging Docker prod path verified”  

---

## Suggested order after Phase D

1. **CI/CD hardening** — enable/ greening Environments, canary, compliance sign-off on real tokens.  
2. **Backup automation** — scheduled System backup + explicit pre-prod job (reuse APIs).  
3. **Monitoring** — uptime on `api`/`admin`, fiscal alert routing (FON failure, TSE degraded), Sentry release tags.  
4. **Docs** — onboarding checklist linking scripts + this staging path.  
5. **Test debt** — separate epic for Admin React dual-copy + backend DemoProductImport failures.

---

## Immediate actions (this week)

1. Assign a **staging host** with Docker.  
2. Walk [`DOCKER_PRODUCTION.md`](DOCKER_PRODUCTION.md) + [`DOCKER_SETUP.md`](DOCKER_SETUP.md) Profile C + [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md).  
3. Wire GitHub Environments from [`.github/environments/`](../.github/environments/) + secrets; run umbrella [`ci.yml`](../.github/workflows/ci.yml) on a PR and [`deploy.yml`](../.github/workflows/deploy.yml) image publish once.  
4. Prefer **Deploy Production** (compliance) for fiscal cutover — see [`CI_CD.md`](CI_CD.md).  
5. Share [`TEAM_ANNOUNCEMENT_SCRIPTS.md`](TEAM_ANNOUNCEMENT_SCRIPTS.md) so DX lands while prod path is verified.

**Operator entrypoints:** `deploy-docker.bat`, `docker-build-prod.bat`, `docker-push-prod.bat`, `docker-logs-prod.bat` · env: [`DOCKER_ENV_VARS.md`](DOCKER_ENV_VARS.md) · CD: [`CI_CD.md`](CI_CD.md) / [`GITHUB_ACTIONS.md`](GITHUB_ACTIONS.md).

---

**Recommendation:** Start with **Production Docker verification on staging** (Phase A–B). Treat CI/CD and backup as **hardening of existing systems**, not greenfield projects.
