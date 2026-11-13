# Deployment compliance (RKSV / fiscal)

How production deployments stay **auditable** and do not violate Austrian RKSV rules.

**Related:** [`DEPLOYMENT.md`](../DEPLOYMENT.md) · [`CANARY_DEPLOYMENT.md`](CANARY_DEPLOYMENT.md) · [`DEPLOYMENT_SMOKE_TEST.md`](DEPLOYMENT_SMOKE_TEST.md) · [`ENVIRONMENT_CONFIGURATION.md`](ENVIRONMENT_CONFIGURATION.md)

**Last updated:** 2026-07-29

---

## Why this matters

Regkasse processes fiscally relevant receipts (TSE signatures, DEP §7, FinanzOnline). A broken production deploy can:

- Break TSE signing or signature chain continuity
- Produce invalid DEP exports for Betriebsprüfung
- Submit wrong or simulated FinanzOnline payloads in production
- Cross tenant boundaries (isolation failure)

Deployments are therefore treated as **compliance-sensitive operations**: immutable audit trail + pre-prod fiscal gate + ComplianceOfficer approval.

---

## How deployments affect fiscal / RKSV compliance

| Area | Risk if deploy is wrong | Mitigation |
|------|-------------------------|------------|
| **TSE signing** | Unsigned / failed receipts, offline queue growth | TSE health check in compliance gate; Soft TSE only on Staging |
| **DEP §7** | Invalid `Belege-Gruppe` / machine codes | DEP export smoke (`REQUIRE_DEP_EXPORT=1`) |
| **FinanzOnline** | Simulation left on in Production, SOAP failures | `/health/finanzonline` + Staging simulation test submission |
| **NTP** | Receipt timestamps outside allowed offset | Time-sync check before promote |
| **Tenant isolation** | Cross-tenant data leak | Unknown-tenant must return **404** (not 403) |
| **Schema** | Breaking migrations | Additive-only EF; see [`DATABASE_MIGRATION_STRATEGY.md`](DATABASE_MIGRATION_STRATEGY.md) |

Additive migrations are **not** rolled back with the image. Fiscal regressions require image rollback + incident process — never rewrite signed history.

---

## Pre-deploy checklist (required for Production)

Complete on **Staging or Canary** against the image you will promote:

- [ ] **DEP export tested** — `GET /api/admin/rksv/dep-export` returns BMF JSON (`Belege-Gruppe`)
- [ ] **TSE signature tested** — `/api/tse/health` healthy; optional test-mode payment with `tseRequired`
- [ ] **FinanzOnline test submission successful** — Staging `Mode=Simulation` outbox/submit path green; Production host must **not** be simulated
- [ ] **NTP time sync checked** — `/api/admin/system/time-sync` `isSynchronized=true` (or equivalent ops check)
- [ ] **Tenant isolation verified** — wrong `X-Tenant-Id` → HTTP **404**

CI enforces the same items via `scripts/ci/deployment-compliance-gate.sh` plus ComplianceOfficer sign-off.

---

## Audit trail

Every CI report and FA rollback writes an audit event:

| `AuditEventType` | When |
|------------------|------|
| `DeploymentStarted` | CI `deploying` / `smoke_running` / `pending` |
| `DeploymentSucceeded` | CI `succeeded` (and canary soak) |
| `DeploymentFailed` | CI `failed` |
| `DeploymentRollback` | CI `rolled_back` or FA stage/tenant rollback |
| `DeploymentComplianceApproved` | ComplianceOfficer sign-off |

Payload includes: **who** (`triggeredBy` / signed-by user), **when** (UTC), **tenants** (`tenantIds`), **version** (`imageTag`), stage, run URL, smoke result.

Legacy action strings: `DEPLOYMENT_STARTED`, `DEPLOYMENT_SUCCEEDED`, `DEPLOYMENT_FAILED`, `DEPLOYMENT_ROLLBACK`, `DEPLOYMENT_COMPLIANCE_APPROVED`.

---

## ComplianceOfficer role

| Item | Value |
|------|--------|
| Role name | `ComplianceOfficer` |
| Permission | `deployment.approve` |
| FA login | Allowed (`clientApp=admin`) |
| POS | Not allowed |
| Duties | Sign production checklist in FA; GitHub Environment reviewer for `backend-production-compliance` |

SuperAdmin also has `deployment.approve` (full catalog). Prefer a dedicated ComplianceOfficer for separation of duties (deployer ≠ approver).

Seed: role is in `Roles.Canonical` → created by `RoleSeedData` on startup.

---

## Sign-off process (critical / production)

1. Promote image through Staging → Canary; soak clean ([`CANARY_DEPLOYMENT.md`](CANARY_DEPLOYMENT.md)).
2. Run fiscal checklist on Staging/Canary (manual or CI gate script).
3. **ComplianceOfficer** (or SuperAdmin) in FA:
   - Open Deployments → Compliance
   - Confirm all five checklist items
   - `POST /api/admin/deployments/compliance/signoff` for the **exact** `imageTag`
4. Start **Deploy Production** workflow:
   - `confirm` = `deploy-production`
   - `compliance_confirm` = `approved-by-compliance-officer`
5. Job **RKSV compliance gate** (`environment: backend-production-compliance`):
   - Verifies API `gatePassed` for that image
   - Re-runs fiscal smoke / TSE / FON / NTP / isolation
   - Requires GitHub Environment reviewers (map reviewers to ComplianceOfficer holders)
6. Migrate Environment → Deploy Environment → post-smoke (manual rollback only).

Sign-offs expire (default **72h**). Expired → CI gate fails until re-approved.

---

## CI/CD gate summary

```text
workflow_dispatch (phrases)
  → compliance-gate (Environment: backend-production-compliance)
       • ComplianceOfficer sign-off (API)
       • DEP / TSE / FON / NTP / tenant isolation
  → migrate (Environment: backend-production-migrations)
  → deploy + smoke (Environment: backend-production; auto_rollback=false)
```

Script: [`scripts/ci/deployment-compliance-gate.sh`](../scripts/ci/deployment-compliance-gate.sh)

---

## APIs

| Method | Path | Auth |
|--------|------|------|
| POST | `/api/admin/deployments/compliance/signoff` | JWT + `deployment.approve` |
| GET | `/api/admin/deployments/compliance/gate?imageTag=` | JWT + `deployment.approve` |
| GET | `/api/webhooks/deployments/compliance-gate?imageTag=` | Deploy token |
| POST | `/api/webhooks/deployments/ci-report` | Deploy token (also writes deployment audit) |

---

## Operator notes

- Never skip the compliance Environment for “urgent” fiscal-affecting releases.
- Document exceptions in the incident ticket; still write audit notes.
- Production FinanzOnline must use real SOAP; simulation is Staging/Canary only.
