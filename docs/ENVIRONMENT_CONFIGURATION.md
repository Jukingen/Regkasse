# Environment configuration

**Last updated:** 2026-07-29  
**Related:** [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md) · [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) · [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md) · [`DEVELOPMENT.md`](../DEVELOPMENT.md) · [`DEPLOYMENT.md`](../DEPLOYMENT.md) · [`backend/CONFIGURATION.md`](../backend/CONFIGURATION.md)

Regkasse separates three concepts:

| Concept | Variable / source | Purpose |
|---------|-------------------|---------|
| **ASP.NET host** | `ASPNETCORE_ENVIRONMENT` | Which `appsettings.{Environment}.json` layer loads |
| **Release stage** | `RELEASE_STAGE` / `Deployment:ReleaseStage` | Promotion lane for ops + UI banners (`dev` → `staging` → `canary` → `production`) |
| **Fiscal simulation** | `Tse:*`, `RKSV:*`, `FinanzOnline:*` | Soft TSE / FON simulation (fail-closed outside Development) |

---

## 1. Environment variables

### Host environment

| Value | Use |
|-------|-----|
| `ASPNETCORE_ENVIRONMENT=Development` | Local developer machines |
| `ASPNETCORE_ENVIRONMENT=Staging` | Staging cloud |
| `ASPNETCORE_ENVIRONMENT=Production` | Production cloud (and canary slices on Production hosts) |

### Release stage

| Value | Typical host | UI banner |
|-------|--------------|-----------|
| `RELEASE_STAGE=dev` | Development | **DEVELOPMENT** (green) |
| `RELEASE_STAGE=staging` | Staging | **STAGING** (yellow) |
| `RELEASE_STAGE=canary` | Production host (canary deploy) **or** canary tenant list | **CANARY** (orange) |
| `RELEASE_STAGE=production` | Production | *(none)* |

Also accepted: `Deployment__ReleaseStage` (same values). If both are empty, the API derives the stage from `ASPNETCORE_ENVIRONMENT` (`Development`→`dev`, `Staging`→`staging`, else `production`).

**Canary tenants:** on a Production host with `ReleaseStage=production`, set `Deployment:CanaryTenantIds` and/or `Deployment:CanaryTenantSlugs`. Ambient JWT tenants in that list get effective stage `canary` (orange banner) without moving the whole fleet.

### Frontend build-time (optional fallback)

| App | Variable |
|-----|----------|
| FA | `NEXT_PUBLIC_RELEASE_STAGE=dev\|staging\|canary\|production` |
| POS | `EXPO_PUBLIC_RELEASE_STAGE=dev\|staging\|canary\|production` |

Prefer the live API signal (`GET /api/rksv/environment` → `releaseStage` / `isCanary`). Build-time vars cover login shells before auth.

---

## 2. Appsettings templates

Tracked templates (copy once; real `appsettings*.json` under `backend/` are **gitignored**):

| File | Role |
|------|------|
| [`backend/appsettings.example.json`](../backend/appsettings.example.json) | Shared safe base |
| [`backend/appsettings.Development.example.json`](../backend/appsettings.Development.example.json) | Local Development |
| [`backend/appsettings.Staging.example.json`](../backend/appsettings.Staging.example.json) | Staging cloud |
| [`backend/appsettings.Production.example.json`](../backend/appsettings.Production.example.json) | Production cloud |

```bash
cd backend
cp appsettings.example.json appsettings.json
cp appsettings.Development.example.json appsettings.Development.json
# Staging / Production hosts only:
cp appsettings.Staging.example.json appsettings.Staging.json
cp appsettings.Production.example.json appsettings.Production.json
```

Load order (ASP.NET Core): base → environment overlay → user secrets (Development) → environment variables.

---

## 3. What differs between environments

| Concern | Development | Staging | Production |
|---------|-------------|---------|------------|
| Soft TSE / `TseMode=Demo` | Allowed | **Forbidden** (lock) | **Forbidden** (startup fail) |
| Fake FON / `UseSimulation=true` | Allowed | **Forbidden** | **Forbidden** |
| `RKSV:Mode` | `Demo` | `Production` | `Production` |
| `Tse:Provider` | `fake` / soft OK | Real (`fiskaly` / …) | Real |
| Dev headers (`X-Tenant-Id`, `?tenant=`) | Allowed | Not for auth tenancy | Not used |
| CSRF / SuperAdmin 2FA | Off / bypass | On | On |
| Logging | Information (app) | Information / Warning | Warning / Error |
| Backup adapter | Often `Fake` | `PgDump` (staging paths) | `PgDump` (prod paths) |
| Redis instance prefix | `Regkasse_Dev` | `Regkasse_Staging` | `Regkasse_Prod` |
| FA / POS release banner | DEVELOPMENT (green) | STAGING (yellow) | none (CANARY orange if canary) |
| `/health/ready` fiscal gate | Healthy while sim OK | Unhealthy if soft/sim | Unhealthy if soft/sim |

Staging enforces the same TSE lock as Production when `Tse:EnforceProductionLockInStaging=true` (default).

### Development fiscal defaults

```json
"RKSV": { "Mode": "Demo", "TseMode": "Simulation", "ShowDemoLabel": true },
"Tse": { "TseMode": "Demo", "Mode": "Fake", "Provider": "fake" },
"FinanzOnline": { "Mode": "Simulation", "Session": { "UseSimulation": true } }
```

### Staging / Production fiscal defaults

```json
"RKSV": { "Mode": "Production", "TseMode": "Real", "ShowDemoLabel": false },
"Tse": {
  "TseMode": "Device",
  "Mode": "Real",
  "Provider": "fiskaly",
  "AllowUnsafeFiscalModesInProduction": false,
  "EnforceProductionLockInStaging": true
},
"FinanzOnline": {
  "Mode": "Production",
  "Session": { "UseSimulation": false }
}
```

Use BMF **test** credentials on Staging where available; never point Soft TSE at live production.

---

## 4. Startup validation (Production / Staging lock)

`TseProductionOptionsValidator` (`ValidateOnStart`) uses `TseFiscalConfigLockEvaluator`. When the lock applies:

- `Tse:TseMode` must be **Device**
- `Tse:Mode` must not be **Fake**
- `Tse:Provider` must be **fiskaly**, **epson**, or **swissbit**
- `Tse:AllowSimulatedDailyClosing` must be **false**
- `RKSV:Mode` must be **Production**
- FinanzOnline simulation flags must be **false**

Escape hatch (ops emergency only): `Tse:AllowUnsafeFiscalModesInProduction=true` — logs Critical; do not use for normal go-live.

---

## 5. Health probes

| Path | Purpose |
|------|---------|
| `/health/live`, `/api/health/live` | Liveness |
| `/health/ready`, `/api/health/ready` | DB + TSE fiscal config + FON simulation gate |
| `/health/tse/mode` | TSE fiscal lock detail |
| `/health/finanzonline/mode` | FON simulation vs real |
| `/api/rksv/environment` | Host + `releaseStage` + canary + simulation flags for FA/POS |

---

## 6. Promoting a release

Recommended lane:

```text
dev (local) → staging (cloud) → canary (subset) → production (fleet)
```

1. **Dev:** merge PR; run unit/integration + local Soft TSE smoke.
2. **Staging:** deploy with `ASPNETCORE_ENVIRONMENT=Staging`, `RELEASE_STAGE=staging`, Staging secrets/DB. Confirm yellow **STAGING** banner, `/health/ready` Healthy, FA/POS smoke, DEP/Prüftool if fiscal changed.
3. **Canary:** deploy Production binaries with either:
   - `RELEASE_STAGE=canary` on a canary slot, **or**
   - `RELEASE_STAGE=production` + `Deployment:CanaryTenantIds` / `CanaryTenantSlugs` for pilot mandants.
   Confirm orange **CANARY** only for intended tenants; watch metrics/errors.
4. **Production:** set `RELEASE_STAGE=production`, clear canary list (or finish canary slot). No release-stage banner. Follow [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md) for Soft→Real fiscal cutover.

**Order for coupled FA+API:** backend first, then FA (see [`ADMIN_FA_DEPLOY.md`](ADMIN_FA_DEPLOY.md)). Rollback: [`DEPLOYMENT.md`](../DEPLOYMENT.md) § Rollback.

---

## 7. API signal for UIs

`GET /api/rksv/environment` (and POS overview `rksvEnvironment`) includes:

| Field | Meaning |
|-------|---------|
| `hostEnvironment` / `isHostDevelopment` / `isHostStaging` | ASP.NET host |
| `releaseStage` | `dev` \| `staging` \| `canary` \| `production` |
| `isCanary` | Effective canary (stage or tenant list) |
| `isSimulated` / `isFinanzOnlineSimulated` / `isSimulationMode` | Fiscal simulation banners |
| `fiscalConfigLockOk` / reasons | Production/Staging lock posture |

---

## 8. Safe Development testing

1. Copy Development example → `appsettings.Development.json`; set `ASPNETCORE_ENVIRONMENT=Development`, `RELEASE_STAGE=dev`.
2. Soft TSE + FON simulation; do not use Production BMF credentials.
3. Tenant: `X-Tenant-Id: dev` or `?tenant=dev` (Development only).
4. Confirm FA/POS show green **DEVELOPMENT** (and **SIMULATION** when fiscal sim is on).

---

**See also:** [`backend/docs/HEALTH_GUARDRAILS.md`](../backend/docs/HEALTH_GUARDRAILS.md) · [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md)
