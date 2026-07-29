# Deployment smoke tests & rollback

**Related:** [`DEPLOYMENT.md`](../DEPLOYMENT.md) · [`DATABASE_MIGRATION_STRATEGY.md`](DATABASE_MIGRATION_STRATEGY.md) · [`scripts/smoke-test.sh`](../scripts/smoke-test.sh) · [`scripts/rollback-production.sh`](../scripts/rollback-production.sh)

**Last updated:** 2026-07-29

---

## Goal

Every Staging / Canary / Production deploy must prove the API (and optional FA/POS surfaces) still work for a target tenant before traffic stays on the new image. Canary fails closed with **auto-rollback**; Production requires **manual** rollback after a smoke failure.

---

## What each check validates

| Check id | Surface | Pass criteria |
|----------|---------|---------------|
| `api.health.live` | `GET /api/health/live` | HTTP 200 |
| `api.health.ready` | `GET /api/health/ready` | HTTP 200 (DB + fiscal config posture) |
| `api.health` | `GET /api/health` | HTTP 200 or 503 with JSON body |
| `health.migrations` | `GET /health/migrations` | HTTP 200 and `pendingCount=0` |
| `fa.ui.login` | `GET {FA_BASE}/login` | 200/3xx (optional if `FA_BASE` set) |
| `pos.ui` | `GET {POS_BASE}/` | 200/3xx (optional if `POS_BASE` set) |
| `fa.login` | `POST /api/Auth/login` `clientApp=admin` | HTTP 200 + access token |
| `rksv.environment` | `GET /api/rksv/environment` | HTTP 200 with admin token |
| `pos.login` | `POST /api/Auth/login` `clientApp=pos` | Token (falls back to admin) |
| `pos.catalog` | `GET /api/pos/list` | HTTP 200 |
| `pos.status` | `GET /api/pos/status` | 200 or 404 |
| `pos.cart.add` / `pos.payment` | Cart + cash payment | Only when `SMOKE_POS_PAYMENT=1` (test / Soft TSE) |
| `rksv.dep_export` | `GET /api/admin/rksv/dep-export` | HTTP 200 BMF JSON (needs `report.export` + `audit.view`) |

Smoke **does not** create fiscal production receipts unless you explicitly enable `SMOKE_POS_PAYMENT=1` on a Soft TSE / simulation host.

---

## How to run manually

### Bash (CI / Linux ops)

```bash
export API_BASE=https://api.staging.regkasse.at
export TENANT_ID=smoke
export LOGIN_IDENTIFIER=admin@admin.com
export LOGIN_PASSWORD='…'
# optional:
export FA_BASE=https://admin.staging.regkasse.at
export POS_BASE=https://pos.staging.regkasse.at
export SMOKE_CASH_REGISTER_ID='…'   # else discovered via /api/admin/cash-registers
export REQUIRE_DEP_EXPORT=1
export SMOKE_POS_PAYMENT=0

./scripts/smoke-test.sh
echo $?   # 0 = pass
```

### PowerShell (Windows)

```powershell
$env:API_BASE = 'https://api.staging.regkasse.at'
$env:TENANT_ID = 'smoke'
.\scripts\smoke-test.ps1
```

Uses `bash scripts/smoke-test.sh` when Git Bash is available; otherwise a smaller native subset.

### Local stack

```bash
API_BASE=http://localhost:5184 TENANT_ID=dev ./scripts/smoke-test.sh
```

---

## CI behavior

| Stage | Smoke script | On failure |
|-------|--------------|------------|
| Staging | `scripts/smoke-test.sh` | Auto-rollback webhook (`auto_rollback=true`) |
| Canary | same | **Auto-rollback** + on-call notify |
| Production | same | **No** auto-rollback — fail job, notify on-call, FA/manual rollback |

Workflow: [`.github/workflows/deploy-backend-stage.yml`](../.github/workflows/deploy-backend-stage.yml)

Secrets: `SMOKE_LOGIN_*`, `ONCALL_WEBHOOK_URL` / `SLACK_WEBHOOK_URL`, stage `*_ROLLBACK_WEBHOOK_URL`.  
Vars: `BACKEND_FA_BASE_URL`, `BACKEND_POS_BASE_URL` (optional UI probes).

Results are posted to `POST /api/webhooks/deployments/ci-report` (`smokePassed`, `smokeSummary`) and shown on FA **`/admin/deployments`**.

---

## If smoke fails

1. **Read the failing check id** in the Actions log / `SMOKE_SUMMARY`.
2. **Canary / Staging:** confirm auto-rollback ran (`rolled_back` in FA). Re-check `/health/live` on previous image.
3. **Production:** do **not** wait for auto-rollback.
   - FA → `/admin/deployments` → **Rollback** (confirm), **or**
   - Host:  
     `MODE=docker PREVIOUS_IMAGE=ghcr.io/…/regkasse-api:sha-… REGKASSE_ROLLBACK_CONFIRM=YES ./scripts/rollback-production.sh`
4. Run smoke again against the restored image.
5. Fix forward; do **not** run EF `Down()` (see migration strategy).

### Rollback script modes

| Mode | Behavior |
|------|----------|
| `MODE=files` (default) | Restore `backup/<stamp>` package trees (legacy) |
| `MODE=docker` | Webhook or compose redeploy of `PREVIOUS_IMAGE`, then smoke + on-call notify |

---

## FA dashboard

`/admin/deployments` (Super Admin):

- Current status per stage + last smoke tag / summary  
- **Rollback** button (modal confirm → `POST /api/admin/deployments/rollback` with `confirm: "rollback"`)

Config on API: `Deployment:RollbackWebhooks:{staging|canary|production}` webhook URLs.
