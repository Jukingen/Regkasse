# Tenant-based canary deployment

Progressive production updates: deploy to **one tenant**, soak **24–48 hours**, then promote to the next tenant if monitoring is clean.

**Related:** [`DEPLOYMENT.md`](../DEPLOYMENT.md) · [`ENVIRONMENT_CONFIGURATION.md`](ENVIRONMENT_CONFIGURATION.md) · FA `/admin/deployments/tenants`

**Last updated:** 2026-07-29

---

## Goals

| Goal | How |
|------|-----|
| Limit blast radius | Only listed canary tenants see the new build / `RELEASE_STAGE=canary` banner |
| Prove fiscal + app health | Smoke tests + soak monitoring on real tenant traffic |
| Controlled promotion | Re-run canary with the next slug; full production only after soak |

---

## Selecting canary tenants

1. Prefer a **low-risk, cooperative** Mandant (internal demo, friendly pilot, or low transaction volume).
2. Use the **tenant slug** (e.g. `cafe-1`), not the public hostname alone — smoke/CI use `TENANT_ID` / JWT tenant context. Hostnames like `cafe-1.regkasse.at` are documentation aliases for the same slug where applicable.
3. Order tenants by risk: internal → small live → medium → remainder → production-wide.
4. Configure the API host:
   - `Deployment:CanaryTenantSlugs` / `CanaryTenantIds` — orange **CANARY** banner for those tenants
   - `Deployment:CanaryDefaultSoakHours` — default **24** (use **48** for high-risk releases)
5. FA `/admin/deployments/status` shows `recommendedNextCanaryTenantSlug` from the configured list minus tenants already soaking/succeeded.

**Do not** start with high-volume or fiscal-critical-only tenants for the first soak of a release.

---

## Pipeline (progressive)

```text
Staging green
    → Deploy Canary workflow (ONE tenant, e.g. cafe-1)
    → Smoke (auto-rollback on fail)
    → deployment_history status = canary_soak (soak_until = now + soak_hours)
    → Monitor 24–48h
    → If clean: next tenant (re-run workflow)
    → After canary ring: production tag / Deploy Production
```

### GitHub Actions

| Input | Guidance |
|-------|----------|
| `tenant_ids` | Prefer **one** slug (`cafe-1`). Multiple allowed but not progressive. |
| `soak_hours` | `24` or `48` — written into `deployment_history` via CI report |
| `image_tag` | Existing GHCR tag, or empty to build |

Workflow: [`.github/workflows/deploy-canary.yml`](../.github/workflows/deploy-canary.yml)

Ops must also set `Deployment__CanaryTenantSlugs` on the host to match the active canary list (banner + resolver).

---

## Monitoring strategy

| Signal | Source | Action |
|--------|--------|--------|
| Smoke fail | CI / FA stage card | Auto-rollback canary (webhook); stop promotion |
| Absolute audit failures | `CanaryTenantMonitorHostedService` | Activity `CanaryTenantErrors` (bell / email per config) |
| High failed-audit rate | Same monitor | Activity `CanaryTenantHighErrorRate` |
| TSE / FON / backup critical | Existing activity types | Treat as soak failure |
| Tenant version skew | FA `/admin/deployments/tenants` | Confirm only intended tenants on new version |

### Canary monitor defaults (`Deployment:CanaryMonitor`)

| Setting | Default | Meaning |
|---------|---------|---------|
| `CheckIntervalMinutes` | 15 | Evaluation cadence |
| `WindowMinutes` | 60 | Audit lookback |
| `ErrorCountThreshold` | 10 | Failed audit count → alert |
| `ErrorRateThresholdPercent` | 5 | Failed/total % → alert |
| `MinEventsForRate` | 20 | Minimum events before rate applies |

Alerts are deduped hourly per tenant. Investigate in FA activity feed and audit logs before promoting.

---

## Rollback criteria (canary)

**Immediate rollback** (stage or tenant) when any of:

1. Smoke tests fail after deploy (CI auto-rollback).
2. Canary error / high-error-rate alerts during soak and root cause is release-related.
3. Fiscal integrity issues (TSE signing, signature chain gaps, FinanzOnline submission spike).
4. POS/FA login or payment path regressions on the canary tenant.
5. Operator judgment (support tickets, customer escalation).

### How to roll back

| Scope | Action |
|-------|--------|
| Whole canary stage | FA `/admin/deployments` → Rollback (stage webhook) or CI rollback webhook |
| Single tenant | FA `/admin/deployments/tenants` → Rollback for that row (`confirm: rollback`) → `POST /api/admin/deployments/tenants/{tenantId}/rollback` |
| Script | Stage rollback secrets / `scripts/rollback-production.sh` patterns (ops-specific) |

Tenant rollback records `deployment_history` with `status=rolled_back` and posts to `Deployment:RollbackWebhooks:canary` (payload includes `tenantId` / `tenantSlug`).

**Note:** Additive EF migrations are **not** rolled back with the image. Keep canary schema backward-compatible (see [`DATABASE_MIGRATION_STRATEGY.md`](DATABASE_MIGRATION_STRATEGY.md)).

---

## Tracking APIs & UI

| Surface | Purpose |
|---------|---------|
| Table `deployment_history` | `tenant_id`, `version`, `deployed_at`, `status`, soak, smoke |
| `GET /api/admin/deployments/status` | Overall status + soaking count + next recommended slug |
| `GET /api/admin/deployments/tenants` | Latest version per tenant |
| `POST /api/admin/deployments/tenants/{id}/rollback` | Tenant-scoped rollback |
| FA `/admin/deployments/tenants` | Super Admin UI |

CI `POST /api/webhooks/deployments/ci-report` with `tenantIds` + `imageTag` also writes per-tenant history.

---

## Promotion checklist

- [ ] Canary smoke green
- [ ] Soak ≥ configured hours with no canary alerts (or explained false positives)
- [ ] TSE / FON / backup activity normal for canary tenant(s)
- [ ] FA tenant table shows expected version / `canary_soak` completed or `promoted`
- [ ] Next tenant chosen from recommended list (or production gate if ring complete)
