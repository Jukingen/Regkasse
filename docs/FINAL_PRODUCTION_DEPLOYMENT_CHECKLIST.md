# Final production deployment — gate checklist

**Prepared:** 2026-08-17  
**Runbook:** [`PRODUCTION_DEPLOYMENT_RUNBOOK.md`](PRODUCTION_DEPLOYMENT_RUNBOOK.md) · [`../DEPLOYMENT.md`](../DEPLOYMENT.md) · smoke [`DEPLOYMENT_SMOKE_TEST.md`](DEPLOYMENT_SMOKE_TEST.md)

**Do not run** `REGKASSE_DEPLOY_CONFIRM=YES ./scripts/ops/deploy-production.sh` from a developer laptop. The script publishes to `/var/www/regkasse`, may run EF migrations, and restarts `systemd` unit `regkasse-api`. It belongs on the **Linux production host** after sign-off.

---

## Gate (all must be ☑ before deploy)

| Gate | Evidence | 2026-08-17 |
|------|----------|------------|
| □ All EF migrations applied **on Production** (after a DB backup) | `GET /health/migrations` → `pendingCount=0` | Host |
| □ Configuration verified | `ASPNETCORE_ENVIRONMENT=Production`; copy from `appsettings.Production.example.json` | Host |
| □ Secrets configured | JWT, DB, Redis, Fiskaly, FON — not in git | Host |
| □ Fiskaly LIVE configured | [`FISKALY_PRODUCTION_CUTOVER.md`](FISKALY_PRODUCTION_CUTOVER.md) | **Not done on this workstation** |
| □ FON Real configured | [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md) | Host |
| □ Monitoring verified | Prometheus scrapes `/metrics` (IP allowlist) | Host |
| □ Alertmanager configured | Rendered receivers; test alert acknowledged | Tracked YAML still **null**; AM not running locally |
| □ Backup drill passed | [`BACKUP_RESTORE_DRILL_EVIDENCE.md`](BACKUP_RESTORE_DRILL_EVIDENCE.md) | **Not executed** (no System dump here) |
| □ GO_LIVE signed | [`GO_LIVE_CHECKLIST.md`](GO_LIVE_CHECKLIST.md) §8 + [`GO_LIVE_SIGN_OFF_PACKET.md`](GO_LIVE_SIGN_OFF_PACKET.md) | **Unsigned** |

If any row is open → **do not deploy**.

---

## On the production host (after gates)

```bash
# 1) Preflight
./scripts/ops/preflight-production.sh

# 2) Deploy (root / deploy user; confirm phrase required)
export REGKASSE_DEPLOY_CONFIRM=YES
export CONNECTION_STRING='Host=…;Database=…;Username=…;Password=…'
sudo -E ./scripts/ops/deploy-production.sh
```

Optional skips (`SKIP_MIGRATE`, `SKIP_SMOKE`, …) are documented in the script header. Do not skip migrate or smoke for a first Production cutover.

---

## Post-deploy verify

```bash
export API_BASE=https://api.regkasse.at
export SMOKE_TEST_EXPECTED_STAGE=production
export SMOKE_POS_PAYMENT=0   # never create LIVE fiscal sales from smoke
./scripts/smoke-test.sh
```

| Check | Pass |
|-------|------|
| `GET /api/health/live` | HTTP 200 |
| `GET /api/health/ready` | HTTP 200; fiscal posture Production |
| `GET /health/migrations` | `pendingCount=0` |
| `GET /health/tse/mode` | Device / Real; not Demo/Fake |
| Smoke script | exit 0; **no** POS payment on LIVE |
| Fiscal integrity | ComplianceOfficer: Startbeleg/Nullbeleg or first pilot sale per RKSV cutover — not this script |

Rollback: `sudo REGKASSE_ROLLBACK_CONFIRM=YES ./scripts/rollback-production.sh` (see runbook).
