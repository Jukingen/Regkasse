# CI/CD guide — Regkasse

How automated build, test, image publish, deploy, smoke, and rollback fit together.

**Last updated:** 2026-07-29

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
| [`scripts/ci-build.ps1`](../scripts/ci-build.ps1) | Release build and/or Compose prod image build (+ optional GHCR push) |
| [`scripts/ci-test.ps1`](../scripts/ci-test.ps1) | Backend / Admin / POS test gates |
| [`scripts/ci-deploy.ps1`](../scripts/ci-deploy.ps1) | Webhook deploy + `smoke-test.sh` + optional rollback |

```powershell
.\scripts\ci-test.ps1 -Backend
.\scripts\ci-build.ps1 -Docker -Profiles admin -NoPush
.\scripts\ci-deploy.ps1 -Stage staging -Image ghcr.io/org/regkasse-api:sha-abc1234 `
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
- [ ] Run umbrella **CI** on a PR once; **Deploy** dry-run to staging once  
