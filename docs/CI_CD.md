# CI/CD guide — Regkasse

How automated build, test, image publish, deploy, smoke, and rollback fit together.

**Last updated:** 2026-09-21

| Related | Link |
|---------|------|
| GitHub Actions reference | [`GITHUB_ACTIONS.md`](GITHUB_ACTIONS.md) |
| Workflow inventory | [`.github/workflows/README.md`](../.github/workflows/README.md) |
| Deployment (hosts + Compose) | [`../DEPLOYMENT.md`](../DEPLOYMENT.md) |
| Smoke tests | [`DEPLOYMENT_SMOKE_TEST.md`](DEPLOYMENT_SMOKE_TEST.md) |
| Compliance gate | [`DEPLOYMENT_COMPLIANCE.md`](DEPLOYMENT_COMPLIANCE.md) |
| Production Docker (host) | [`DOCKER_PRODUCTION.md`](DOCKER_PRODUCTION.md) |
| Environment templates | [`.github/environments/`](../.github/environments/) |

---

## Design principles

1. **Harden existing pipelines** — do not invent a second fiscal deploy path.
2. **Staging auto** on `main`/`master`; **Production gated** (Environments + phrases + compliance).
3. **Smoke after deploy**; **auto-rollback** on staging/canary; **manual rollback** on production.
4. **Host Compose** (`deploy-docker.bat`) and **Actions webhooks** are complementary, not duplicates.

---

## Pipeline map

```text
  Pull request
       │
       ├─ Path-filtered: backend-ci, frontend-admin-ci, frontend-ci, …
       └─ Umbrella: ci.yml  (backend + admin + POS tests, Docker build no-push)

  Push main/master
       │
       ├─ Backend CI → build API → GHCR → Deploy Staging (+ smoke, auto-rollback)
       ├─ Deploy.yml → build API+Admin+Sites+POS → GHCR
       │     └─ Staging API deploy only if workflow_dispatch or DEPLOY_YML_RUN_STAGING_API=true
       └─ Frontend Admin Deploy (after green Admin CI) → staging image/hooks

  Tag v* / manual Deploy Production
       │
       └─ Compliance → migrate Environment → deploy Environment → smoke
            (auto-rollback OFF — manual / FA)
```

---

## Workflows (entry points)

| Workflow | File | When | What |
|----------|------|------|------|
| **CI** | [`ci.yml`](../.github/workflows/ci.yml) | PR + dispatch | Full monorepo tests + Docker build (no push) |
| **Deploy** | [`deploy.yml`](../.github/workflows/deploy.yml) | Push main + dispatch | Multi-image GHCR push; optional Staging API; prod only with `confirm=deploy-production` |
| **Backend CI** | [`backend-ci.yml`](../.github/workflows/backend-ci.yml) | PR / push / tag `v*` | API build/test + stage gates |
| **Deploy Production** | [`deploy-production.yml`](../.github/workflows/deploy-production.yml) | Manual | Compliance + migrate + deploy (preferred prod) |
| **Deploy Canary** | [`deploy-canary.yml`](../.github/workflows/deploy-canary.yml) | Manual | Progressive tenant canary |
| Reusable stage | [`deploy-backend-stage.yml`](../.github/workflows/deploy-backend-stage.yml) | `workflow_call` | Migrate → webhook → smoke → rollback |

---

## Local / CI scripts

| Script | Purpose |
|--------|---------|
| [`scripts/ci/ci-build.ps1`](../scripts/ci/ci-build.ps1) | Release build and/or Compose prod image build (+ optional GHCR push) |
| [`scripts/ci/ci-test.ps1`](../scripts/ci/ci-test.ps1) | Backend / Admin / POS test gates |
| [`scripts/ci/ci-deploy.ps1`](../scripts/ci/ci-deploy.ps1) | Webhook deploy + `smoke-test.sh` + optional rollback |

```powershell
.\scripts\ci\ci-test.ps1 -Backend
.\scripts\ci\ci-build.ps1 -Docker -Profiles admin -NoPush
.\scripts\ci\ci-deploy.ps1 -Stage staging -Image ghcr.io/org/regkasse-api:sha-abc1234 `
  -ApiBase https://api.staging.regkasse.at -DryRun
```

---

## Automated deployment

### Staging (automatic)

1. Push to `main` / `master`.
2. **Backend CI** builds/pushes API image and deploys Staging (webhook + smoke + auto-rollback).
3. **Deploy** workflow builds/pushes API + Admin + Sites + POS images to GHCR; optional FA webhook.
4. To also run Staging API deploy from `deploy.yml`, set variable `DEPLOY_YML_RUN_STAGING_API=true` or use Actions → Deploy → `staging`.

Secrets: see [`.github/environments/staging.yml`](../.github/environments/staging.yml).

### Deploy.yml job graph (why "Resolve deploy target" is Skipped)

Workflow: [`.github/workflows/deploy.yml`](../.github/workflows/deploy.yml). This is **not** `frontend-admin-deploy.yml`.

| Job | When it runs | When it is Skipped |
|-----|--------------|--------------------|
| **Build & push images** | Always on push `main`/`master` and on `workflow_dispatch` | Never skipped |
| **Resolve deploy target** | After a **successful** image job | **Always skipped if Build & push fails** (`needs: build-and-push`, no `if: always()`). Not gated on Variables/Secrets. |
| **Deploy Staging (API)** | `workflow_dispatch` target=`staging`, **or** repo variable `DEPLOY_YML_RUN_STAGING_API=true` | Ordinary push (Backend CI owns Staging API). Missing webhooks do **not** skip this job — they fail inside the reusable stage. |
| **Frontend staging webhooks** | After image success | Optional; empty `FA_STAGING_DEPLOY_WEBHOOK_URL` logs "not set" and exits 0 |
| **Deploy Production (API)** | Dispatch target=`production` and `confirm=deploy-production` | Push to `main`; wrong confirm phrase |

A 40–60s **Build & push** failure is almost always GHCR login/push (not a missing `FA_STAGING_API_BASE_URL`). Those public build-args already have YAML defaults.

#### Required GitHub configuration (do not put secrets in YAML)

**Permissions (repo or org)**

| Setting | Why |
|---------|-----|
| Actions → General → Workflow permissions → **Read and write** | `GITHUB_TOKEN` can `packages: write` |
| GHCR package **Actions** access for `regkasse-api` / `regkasse-frontend-*` | Token may push to an existing package |
| Org **Package creation** allowed for GITHUB_TOKEN | First push of a new image name |
| Org SAML/SSO authorized for `GITHUB_TOKEN` if the org requires it | Otherwise login/push **403** in ~30–50s |

**Repository Variables (optional — defaults in the workflow)**

| Variable | Used for | Default if unset |
|----------|----------|------------------|
| `FA_STAGING_API_BASE_URL` | Admin + Sites `NEXT_PUBLIC_API_BASE_URL` | `https://api.staging.regkasse.at` |
| `FA_STAGING_RKSV_ENVIRONMENT` | Admin `NEXT_PUBLIC_RKSV_ENVIRONMENT` | `TEST` |
| `POS_STAGING_API_URL` | POS `EXPO_PUBLIC_API_BASE_URL` | `https://api.staging.regkasse.at/api` |
| `BACKEND_FA_BASE_URL` | POS `EXPO_PUBLIC_ADMIN_BASE_URL` | `https://admin.staging.regkasse.at` |
| `BACKEND_STAGING_API_BASE_URL` | Staging smoke URL | `https://api.staging.regkasse.at` |
| `DEPLOY_YML_RUN_STAGING_API` | Also deploy Staging API from this workflow | unset = image publish only |

**Repository Secrets (never commit; never hardcode in YAML)**

| Secret | Required for |
|--------|----------------|
| `GITHUB_TOKEN` | Provided by Actions — GHCR login |
| `BACKEND_STAGING_DEPLOY_WEBHOOK_URL` | Staging host pull/restart (only if Staging API job runs) |
| `BACKEND_STAGING_ROLLBACK_WEBHOOK_URL` | Staging auto-rollback |
| `BACKEND_STAGING_MIGRATE_WEBHOOK_URL` | Host EF migrate |
| `FA_STAGING_DEPLOY_WEBHOOK_URL` | Optional FA image hook |
| `DEPLOYMENT_STATUS_URL` / `DEPLOYMENT_STATUS_TOKEN` | FA `/admin/deployments` ingest |
| `SMOKE_LOGIN_IDENTIFIER` / `SMOKE_LOGIN_PASSWORD` | Authenticated smoke |
| `ONCALL_WEBHOOK_URL` / `SLACK_WEBHOOK_URL` | Alerts |

Full checklists: [`.github/environments/staging.yml`](../.github/environments/staging.yml) · [`GITHUB_ACTIONS.md`](GITHUB_ACTIONS.md).

#### Country-layer migrations (Paket 16)

`deploy.yml` Staging API (when that job runs) already sets `run_migrations: true` on [`deploy-backend-stage.yml`](../.github/workflows/deploy-backend-stage.yml). Production uses the dedicated migrate Environment, not this workflow.

Additive migrations (do **not** skip migrate on first country-layer cutover):

- `20260916110000_AddCompanySettingsCountryBilling`
- `20260921180000_AddFiscalDocumentCountryAtIssueSnapshots` (issue-time snapshots)

Order, windows, and rollback: [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md) §2. Confirm `GET /health/migrations` → `pendingCount=0` after the migrate job.

### Production (gated)

**Preferred:** Actions → **Deploy Production**  
Inputs: image tag + `deploy-production` + `approved-by-compliance-officer`.

**Also:** Backend CI on tag `v*` (Environments must approve migrate + deploy).

**Deploy.yml production target:** requires `confirm=deploy-production` but **skips** the full compliance job — use only for emergency/ops drills; prefer Deploy Production for fiscal cutover.

Pre-deploy: System/Tenant backup via FA/API ([`BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md)).

---

## Smoke tests

After every stage deploy (`deploy-backend-stage.yml`):

- `scripts/smoke-test.sh` with `REQUIRE_READY`, `REQUIRE_MIGRATIONS`, `REQUIRE_DEP_EXPORT`
- Optional auth: `SMOKE_LOGIN_IDENTIFIER` / `SMOKE_LOGIN_PASSWORD`
- Detail: [`DEPLOYMENT_SMOKE_TEST.md`](DEPLOYMENT_SMOKE_TEST.md)

Staging smoke runs when `BACKEND_STAGING_API_BASE_URL` is set, or when `BACKEND_STAGING_SMOKE_ENABLED` is `true`. If neither is set, the Deploy Staging job still succeeds and the smoke and rollback steps are skipped. Canary and production smoke are unchanged.

---

## Rollback automation

| Stage | Behavior |
|-------|----------|
| Staging / Canary | `auto_rollback=true` → `ROLLBACK_WEBHOOK_URL` on smoke fail |
| Production | `auto_rollback=false` → on-call notify; operator rolls back via webhook / FA / previous image tag |

Schema: additive migrations are **not** rolled back ([`DATABASE_MIGRATION_STRATEGY.md`](DATABASE_MIGRATION_STRATEGY.md)).

Host Compose rollback ≠ `rollback.bat` (git). See [`DOCKER_PRODUCTION.md`](DOCKER_PRODUCTION.md) § Rollback tests.

---

## Host Compose vs Actions

| Path | Use when |
|------|----------|
| `deploy-docker.bat` / `docker-compose.prod.yml` | Single Docker host / staging VM without webhook CD |
| GitHub Actions + webhooks | Multi-stage Staging → Canary → Production with GHCR |

Do not merge Soft TSE override with prod Compose.

---

## Setup checklist

- [ ] Create GitHub Environments from [`.github/environments/*.yml`](../.github/environments/) checklists  
- [ ] Set deploy/rollback/migrate webhook secrets  
- [ ] Set `DEPLOYMENT_STATUS_*` for FA `/admin/deployments`  
- [ ] Set smoke login secrets  
- [ ] Optional `SLACK_WEBHOOK_URL` / `ONCALL_WEBHOOK_URL`  
- [ ] Packages:write for GHCR (default `GITHUB_TOKEN` on public/private as configured)  
- [ ] Org/package GHCR **Actions** access + SSO so **Deploy → Build & push** can login  
- [ ] Run umbrella **CI** on a PR once; **Deploy** dry-run to staging once  
