# Environment configuration

**Last updated:** 2026-09-21  
**Related:** [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md) · [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md) · [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) · [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md) · [`COUNTRIES.md`](COUNTRIES.md) · [`FEATURE_FLAGS.md`](FEATURE_FLAGS.md) · [`DEVELOPMENT.md`](../DEVELOPMENT.md) · [`DEPLOYMENT.md`](../DEPLOYMENT.md) · [`backend/CONFIGURATION.md`](../backend/CONFIGURATION.md)

Regkasse separates three concepts:

| Concept | Variable / source | Purpose |
|---------|-------------------|---------|
| **ASP.NET host** | `ASPNETCORE_ENVIRONMENT` | Which `appsettings.{Environment}.json` layer loads |
| **Release stage** | `RELEASE_STAGE` / `Deployment:ReleaseStage` | Promotion lane for ops + UI banners (`dev` → `staging` → `canary` → `production`) |
| **Fiscal simulation** | `Tse:*`, `RKSV:*`, `FinanzOnline:*` | Austrian Soft TSE / FON simulation (fail-closed outside Development) |

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

Load order (ASP.NET Core): base → environment overlay → user secrets (Development by default; Staging via `ApplicationHost`) → environment variables. Production does not load user secrets.

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

After first Super Admin save (or first `GET /api/admin/rksv/config`), the same values are copied into `rksv_runtime_config` and that row **wins** over the file until the row is removed. See [`RKSV_RUNTIME_CONFIG.md`](RKSV_RUNTIME_CONFIG.md). Overlay `TseMode=Real` disables Development TSE health bypass. FA **Entwicklungsmodus** (`bypassTseCheck`) does not change receipt DEMO labels and is **off by default**.

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
  "Session": {
    "UseSimulation": false,
    "BaseUrl": "https://finanzonline.bmf.gv.at/fonws/ws/session",
    "DefaultCredential": {
      "Username": "(secret)",
      "Password": "(secret)",
      "TelematikId": "(secret)",
      "HerstellerId": "(secret)"
    }
  },
  "Registrierkassen": {
    "UseSimulation": false,
    "BaseUrl": "https://finanzonline.bmf.gv.at/fonws/ws/rkdb"
  }
}
```

**Required for real SOAP (Staging / Production):** `FinanzOnline:Session:BaseUrl`, `FinanzOnline:Registrierkassen:BaseUrl`, and `FinanzOnline:Session:DefaultCredential` `Username` / `Password` (plus `TelematikId` / `HerstellerId`). Set credentials via environment variables or the secret store (`FinanzOnline__Session__DefaultCredential__*`) — never in git. Alternative: `FinanzOnline:Connectivity:UseCompanySettings=true` and complete `company_settings` FON columns.

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

DE/CH use a **separate** host-level lock (`CountryFiscalLockEvaluator` / `CountryFiscalLockOptionsValidator`, `ValidateOnStart`). It does not read tenant country. Development bypasses DE/CH. Production **and** Staging always enforce DE/CH (`Tse:EnforceProductionLockInStaging=false` does **not** disable them). There is **no** DE/CH escape hatch. Checks run only when the key is set (non-whitespace):

| Section | When | Rejected outside Development |
|---------|------|------------------------------|
| `KassenSicherheit:Provider` | key set | `fake` (case-insensitive). `not-configured` is allowed. |
| `KassenSicherheit:AllowSimulatedTse` | key set | must be **false** (missing → treated as false). Independent of Provider. |
| `Mwst:UseTestEndpoint` | key set | must be **false** |
| `QrRechnung:BuilderMode` | key set | must not be `dryRun` (case-insensitive). `not-configured` is allowed. |

Do not merge `KassenSicherheit` into `Tse:`. Stubs in `appsettings.Production.example.json` are lock holders only — they do not enable DE/CH TSE.

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

## 9. Common Warnings

Startup `warn:` lines in Development are often expected. They do **not** mean the API failed to start. Treat them as Production/Staging cutover reminders.

### FinanzOnline readiness (Development-only)

`FinanzOnline.TransportStartup` logs `FinanzOnline readiness issue (blocking):` for **Error** findings and `FinanzOnline readiness note:` for **Warning** findings.

With the Development overlay (`UseSimulation=true` on Session, Registrierkassen, and TransmissionQuery), missing BMF URLs/credentials are **not** evaluated. If an FA runtime snapshot or a missing Development overlay turns `UseSimulation` off, the evaluator reports these three codes:

| Code | Config key | Development | Production / Staging |
|------|------------|-------------|----------------------|
| `FO_READINESS_SESSION_BASEURL_MISSING` | `FinanzOnline:Session:BaseUrl` | Expected (Warning). Soft FON does not call BMF. | **Required.** Example: `https://finanzonline.bmf.gv.at/fonws/ws/session` |
| `FO_READINESS_RKDB_BASEURL_MISSING` | `FinanzOnline:Registrierkassen:BaseUrl` | Expected (Warning). | **Required.** Example: `https://finanzonline.bmf.gv.at/fonws/ws/rkdb` |
| `FO_READINESS_CONFIG_SESSION_CREDENTIALS_MISSING` | `FinanzOnline:Session:DefaultCredential:Username` / `Password` | Expected (Warning). Do not store BMF credentials in local JSON. | **Required** via env/vault, **or** `Connectivity:UseCompanySettings` + `company_settings` |

Also expected in Development:

| Code | Meaning |
|------|---------|
| `FO_READINESS_SIMULATION_ACTIVE` | Soft FON — not BMF-authoritative |
| `FO_READINESS_OUTBOX_DISABLED` | `FinanzOnlineOutbox:Enabled=false` (Warning in Development, **Error** elsewhere) |

`GET /api/admin/finanzonline-readiness` (authenticated tenant) and `GET /health/finanzonline/mode` show the same findings. Real SOAP cutover: [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md).

### EF Core model validation (global query filters)

`Microsoft.EntityFrameworkCore.Model.Validation` may log `PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning` because every `ITenantEntity` has a tenant query filter. EF cannot prove two captured `ICurrentTenantAccessor.TenantId` expressions are the same, even when both ends of a relationship use the same filter.

**Expected — no schema change:** filtered ↔ filtered relationships that share the same tenant filter (payments, receipts, products, …). Queries stay tenant-scoped; isolation is still fail-closed.

**Already mitigated** (`Navigation.IsRequired(false)`, FK column stays required):

- `BenefitDailyUsage` / `BenefitAssignment` / `PaymentDetails` → `Customer` (Customer filter includes the walk-in `IsSystem` exemption)
- `LicenseReminder` → `LicenseSale` (reminder is not `ITenantEntity`)

**Expected remaining pairs** (unfiltered dependent, required FK to a filtered principal — documented in `AppDbContextTenantModelTests`):

`ActivityEventRead` → `ActivityEvent`; `CashRegisterTransaction` / `PaymentDetails` / `TagesberichtReport` / `RksvSpecialReceiptFinanzOnlineSubmission` → `CashRegister`; `OnlineOrderItem` → `OnlineOrder`; `SplitItem` → `Product` / `SplitSession`.

Do **not** remove global query filters to silence these warnings. Do **not** add `ITenantEntity` to those dependents without updating Super Admin / hosted-service discovery to `IgnoreQueryFilters()`.

### Backup success, legal hold, and archive copy

A System backup that logs success plus legal hold is working as designed:

| Signal | Meaning |
|--------|---------|
| Run `Status=Succeeded` | Adapter finished (Fake in local Dev, `PgDump` in Production) |
| `Applied System legal hold … until=` | BAO §132 / RKSV **7-year** hold (`BackupStrategyPolicy.SystemLegalRetentionYears`) |
| `External archive copy completed …` | Copy under `Backup:ExternalArchiveRoot` with post-copy SHA-256 |

In Development, `Backup:ExternalArchiveRoot` is often unset. PgDump then **skips** the archive copy (health **Degraded**, diagnostic `DevExternalArchiveNotSet`). Production **requires** an absolute `ExternalArchiveRoot`. Tenant strategy dumps do **not** get the 7-year System legal hold.

---

## 10. Country profiles and planned fiscal config (not implemented)

Hub: [`COUNTRIES.md`](COUNTRIES.md). Stubs: [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md), [`FISCAL_SWITZERLAND.md`](FISCAL_SWITZERLAND.md), [`EINVOICING_EU.md`](EINVOICING_EU.md).

`KassenSicherheit`, `Mwst`, and `QrRechnung` exist as **empty stubs** in `appsettings.Production.example.json` so Production/Staging startup can reject unsafe values ([§4](#4-startup-validation-production--staging-lock)). They are **not** DE/CH TSE or QR-bill implementations. Do not treat the stubs as a live fiscal module.

| Section / store | Role | Notes |
|-----------------|------|--------|
| **CountryProfile** | In-code registry seeds (AT, DE, CH, `EU_DEFAULT`) | **Not appsettings.** Locale, currency, fiscal system, e-invoicing, VAT-ID pattern, allowed `VatRegime`. `EU_DEFAULT` is registry-only. |
| **`company_settings`** | Per-mandant country binding | Live columns: `country` (default AT, the binding), `vat_regime` (default `AT_RKSV_STANDARD`), `billing_country`, `tax_exempt`; locale/currency reuse `Language` / `Currency`. No `CountryCode` column. Not a feature-flag store. |
| **`tenant_settings`** | Feature-flag overrides | Existing `IFeatureFlagService`, keys `FeatureFlags:{Name}`. See [`FEATURE_FLAGS.md`](FEATURE_FLAGS.md). |
| **`FeatureFlags` (appsettings)** | Global defaults for **existing** experimental flags | Must **not** default `Fiscal.RksvAt` to false. Country flags: `Fiscal.KassenSicherheitDe`, `Fiscal.MwstCh`, `EInvoicing.QrRechnung`, … |
| **`KassenSicherheit`** | DE stub + startup lock | Separate from Austrian `Tse:`. `Provider=fake` or `AllowSimulatedTse=true` fail closed in Production/Staging. Vendor choice is not fixed here. |
| **`Mwst`** | CH stub + startup lock | `UseTestEndpoint=true` fails closed in Production/Staging. |
| **`QrRechnung`** | CH QR-bill stub + startup lock | `BuilderMode=dryRun` fails closed in Production/Staging. No bank submission. Gate: `EInvoicing.QrRechnung`. |
| **`En16931`** | Planned EU invoice builder options | No Peppol/ViDA submission. Gate: `EInvoicing.En16931`. |
| **`Vies`** | Planned B2B VAT-ID check | Default **off** (`Vies.CheckEnabled`). Tests must not call the live VIES network. |

Austrian Production/Staging lock in [§4](#4-startup-validation-production--staging-lock) still applies only to `Tse:*` / `RKSV:*` / FinanzOnline. DE/CH keys are locked by `CountryFiscalLockEvaluator`, not by `TseProductionOptionsValidator`. Do not merge DE `KassenSicherheit` into `Tse:`.

Production country-layer apply order: [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md).

---

**See also:** [`backend/docs/HEALTH_GUARDRAILS.md`](../backend/docs/HEALTH_GUARDRAILS.md) · [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) · [`BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md) · [`COUNTRIES.md`](COUNTRIES.md) · [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md)
