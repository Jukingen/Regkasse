# GitHub Actions reference — Regkasse

Operator-oriented map of workflows, secrets, Environments, and common failures.

**Last updated:** 2026-07-29

| Related | Link |
|---------|------|
| CI/CD guide | [`CI_CD.md`](CI_CD.md) |
| Workflow inventory | [`.github/workflows/README.md`](../.github/workflows/README.md) |
| Environment templates | [`.github/environments/`](../.github/environments/) |
| Deployment | [`../DEPLOYMENT.md`](../DEPLOYMENT.md) |

---

## Workflow catalog (by job)

### Quality / PR

| Workflow file | Display name | Trigger |
|---------------|--------------|---------|
| `ci.yml` | **CI** | `pull_request`, `workflow_dispatch` |
| `backend-ci.yml` | Backend CI | PR (backend paths), push, tag `v*`, dispatch |
| `backend-unit-tests.yml` | Backend unit tests | PR/push |
| `backend-postgres-integration-tests.yml` | Postgres integration | PR/push |
| `frontend-admin-ci.yml` | Frontend Admin CI | path-filtered |
| `frontend-ci.yml` | Frontend POS CI | path-filtered |
| `frontend-sites-ci.yml` | Frontend Sites CI | path-filtered |
| `localization-validation.yml` | i18n hard gate | path-filtered |
| `api-client-alignment.yml` / `api-contract*.yml` | OpenAPI / Orval | PR/push |
| `dep-prueftool.yml` | BMF DEP Prüftool | path-filtered |
| `scripts-bat-ps1-pairing.yml` | Scripts pairing | path-filtered |

### Deploy / release

| Workflow file | Display name | Trigger |
|---------------|--------------|---------|
| `deploy.yml` | **Deploy** | push `main`/`master`; dispatch staging\|production |
| `deploy-backend-stage.yml` | Deploy backend stage (reusable) | `workflow_call` only |
| `deploy-production.yml` | Deploy Production | dispatch + compliance phrases |
| `deploy-canary.yml` | Deploy Canary | dispatch |
| `frontend-admin-deploy.yml` | Frontend Admin Deploy | after Admin CI / dispatch |
| `notify-failure.yml` | Slack notify | `workflow_call` |

---

## Environments (UI)

Create under **Settings → Environments**. YAML under [`.github/environments/`](../.github/environments/) is a **checklist**, not applied automatically.

| Environment | Used by | Reviewers |
|-------------|---------|-----------|
| `backend-staging` | Staging migrate/deploy | Optional |
| `backend-canary` | Canary | Recommended |
| `backend-production-compliance` | Compliance gate | Required |
| `backend-production-migrations` | EF migrate | Required |
| `backend-production` | App deploy | Required |
| `frontend-admin-production` | FA prod image | Required |

---

## Secrets & variables (cheat sheet)

### Shared

| Name | Type | Purpose |
|------|------|---------|
| `DEPLOYMENT_STATUS_URL` | secret | FA CI status ingest |
| `DEPLOYMENT_STATUS_TOKEN` | secret | Must match API `Deployment__StatusReportToken` |
| `SMOKE_LOGIN_IDENTIFIER` / `SMOKE_LOGIN_PASSWORD` | secret | Authenticated smoke |
| `ONCALL_WEBHOOK_URL` / `SLACK_WEBHOOK_URL` | secret | Alerts |

### Backend stage (`STAGING` \| `CANARY` \| `PRODUCTION`)

| Pattern | Purpose |
|---------|---------|
| `BACKEND_*_DEPLOY_WEBHOOK_URL` | Pull/restart image |
| `BACKEND_*_ROLLBACK_WEBHOOK_URL` | Previous image on smoke fail |
| `BACKEND_*_MIGRATE_WEBHOOK_URL` | Host-side EF migrate |
| `BACKEND_*_DATABASE_CONNECTION` | Runner-side migrate fallback |
| `BACKEND_*_API_BASE_URL` | Variable — smoke base URL |

### Frontend Admin

| Name | Purpose |
|------|---------|
| `FA_*_DEPLOY_WEBHOOK_URL` | FA host restart |
| `FA_*_API_BASE_URL` / `FA_*_RKSV_ENVIRONMENT` | Build-time public env |

### Deploy.yml extras

| Name | Purpose |
|------|---------|
| `POS_STAGING_API_URL` | Variable — POS image `EXPO_PUBLIC_API_BASE_URL` |

Full lists: [`staging.yml`](../.github/environments/staging.yml) · [`production.yml`](../.github/environments/production.yml).

---

## Image naming (GHCR)

```text
ghcr.io/<lowercase-owner>/regkasse-api:sha-<7char>
ghcr.io/<lowercase-owner>/regkasse-frontend-admin:sha-<7char>
ghcr.io/<lowercase-owner>/regkasse-frontend-sites:sha-<7char>
ghcr.io/<lowercase-owner>/regkasse-frontend-pos-web:sha-<7char>
```

Admin deploy workflow may also tag `staging-<sha>` / `production-<sha>`.

Permissions: workflow needs `packages: write` (granted in `deploy.yml` / backend-ci).

---

## Common operator actions

### Run full CI on a PR

Automatic via `ci.yml`. Manual: Actions → **CI** → Run workflow.

### Deploy staging now

- Push to `main`, or  
- Actions → **Deploy** → target `staging`, or  
- Actions → **Backend CI** → `max_stage=staging`

### Promote production (safe)

1. Note green Staging/Canary image tag (`sha-…`).  
2. FA compliance sign-off ([`DEPLOYMENT_COMPLIANCE.md`](DEPLOYMENT_COMPLIANCE.md)).  
3. Actions → **Deploy Production** → tag + phrases.  
4. Approve Environment gates when prompted.  
5. Confirm smoke; on fail → manual rollback.

### Rollback production

```bash
# Host webhook (example payload)
curl -X POST "$BACKEND_PRODUCTION_ROLLBACK_WEBHOOK_URL" \
  -H 'Content-Type: application/json' \
  -d '{"action":"rollback","stage":"production","failedImage":"…","previousImage":"…"}'
```

Or FA `/admin/deployments` · local script notes in `deploy-backend-stage.yml` failure step.

---

## Scripts used by Actions

| Path | Role |
|------|------|
| `scripts/ci-build.ps1` | Docker/dotnet build (+ push) |
| `scripts/ci-test.ps1` | Package tests |
| `scripts/ci-deploy.ps1` | Local/ops webhook + smoke |
| `scripts/smoke-test.sh` | Post-deploy smoke |
| `scripts/ci/backend-*.sh` | Migrate, status, smoke wrappers |
| `scripts/ci/deployment-compliance-gate.sh` | Prod compliance |

---

## Troubleshooting

| Symptom | Check |
|---------|--------|
| Deploy job green but host unchanged | Webhook secret empty → workflow logs "not set" and exits 0 |
| Smoke fail + no rollback (staging) | `BACKEND_STAGING_ROLLBACK_WEBHOOK_URL` missing |
| GHCR push 403 | `packages:write`; package visibility; SSO auth for org |
| Production blocked | Environment reviewers; wrong confirm/compliance phrase |
| Duplicate CI minutes | Path-filtered workflows + `ci.yml` both run on PR — expected for full gate; use path filters alone for fast loops if needed |

---

## Related Admin docs

- FA package CI: [`frontend-admin/docs/CI_CD.md`](../frontend-admin/docs/CI_CD.md)  
