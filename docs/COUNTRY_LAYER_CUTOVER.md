# Country-layer production cutover

**Last updated:** 2026-09-21  
**Audience:** Ops / Super Admin / backend lead promoting the multi-country layer.  
**Status:** Operational plan only. This is not a legal opinion and does not certify RKSV, KassenSichV, MWST, EN 16931, or ViDA compliance.

This cutover ships **schema + country binding + Super Admin UI**. Austria remains the only production fiscal path. DE/CH/EU modules stay shape-only and must not process live foreign TSE or bank/Peppol traffic.

| Related | Link |
|---------|------|
| Country hub | [`COUNTRIES.md`](COUNTRIES.md) |
| Host / env vs planned country sections | [`ENVIRONMENT_CONFIGURATION.md`](ENVIRONMENT_CONFIGURATION.md) §4, §10 |
| API host deploy | [`PRODUCTION_DEPLOYMENT_RUNBOOK.md`](PRODUCTION_DEPLOYMENT_RUNBOOK.md) |
| EF apply / expand-only | [`DATABASE_MIGRATION_STRATEGY.md`](DATABASE_MIGRATION_STRATEGY.md) |
| Feature flags | [`FEATURE_FLAGS.md`](FEATURE_FLAGS.md) |
| FA + API coupled releases | [`ADMIN_FA_DEPLOY.md`](ADMIN_FA_DEPLOY.md) |
| Generic smoke | [`DEPLOYMENT_SMOKE_TEST.md`](DEPLOYMENT_SMOKE_TEST.md) |
| **AT fiscal cutover (do not reuse for DE/CH)** | [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md) |
| Fiskaly LIVE (Austria SIGN AT) | [`FISKALY_PRODUCTION_CUTOVER.md`](FISKALY_PRODUCTION_CUTOVER.md) |
| FinanzOnline PROD | [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md) |
| TSE lock | [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) |
| Monitoring / Sentry | [`MONITORING.md`](MONITORING.md) · [`ALERTING.md`](ALERTING.md) |
| Stubs | [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md) · [`FISCAL_SWITZERLAND.md`](FISCAL_SWITZERLAND.md) · [`EINVOICING_EU.md`](EINVOICING_EU.md) |

```text
POS:   https://pos.regkasse.at   (no country-layer rebuild expected)
FA:    https://admin.regkasse.at
API:   https://api.regkasse.at
```

---

## 0. What this cutover is (and is not)

| In scope | Out of scope |
|----------|----------------|
| Apply additive country/billing and issue-time snapshot migrations | Turning on live German KassenSicherheit / Swiss MWST TSE |
| Deploy API that reads `company_settings.country` and stamps `CountryCodeAtIssue` | Dropping or rewriting `company_settings.country` |
| Deploy FA country step + Country & Fiscal Regime card (Orval client) | POS UI rewrite or a second POS host |
| Smoke AT create / invoice / TSE; smoke DE/CH **tenant create** + flag resolution | VIES live network calls, Peppol, ViDA submission, Swiss bank submit |
| Keep `Fiscal.RksvAt` locked on for AT | Disabling Austrian TSE with a country flag |

If the goal is **Austrian simulation → production fiscal**, stop and use [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md) instead. This document assumes AT RKSV/TSE/FON is already the production path (or is being cut over on its own checklist).

Recommended lane (same as [`ENVIRONMENT_CONFIGURATION.md`](ENVIRONMENT_CONFIGURATION.md) §6):

```text
dev → staging → canary → production
```

Never apply untested country SQL on Production outside that pipeline ([`DATABASE_MIGRATION_STRATEGY.md`](DATABASE_MIGRATION_STRATEGY.md)).

---

## 1. Do not deploy during

Country-layer deploys still restart the API and run EF. That overlaps live Austrian fiscal work. **Do not start this cutover in the windows below** (Europe/Vienna calendar).

| Window | Why | Typical span |
|--------|-----|----------------|
| **Fiscal day-end (Tagesabschluss)** | Open/close, TSE-signed daily closing, Auto-Tagesabschluss | Operator close in the evening; system catch-up around the tenant cutoff (default **03:00** Vienna for *yesterday*). Stay off the API from close-time through Auto-Tagesabschluss settlement. |
| **Monatsbeleg window (first 7 days)** | RKSV: Monatsbeleg must be created within **7 days** of month end. Auto-Monatsbeleg catch-up runs Vienna **day 1–7**. POS may **block sales** when the previous month is missing (`monatsbeleg_blocking_mode`, default Strict). | **1st–7th** of each calendar month (Vienna). |
| **Jahresbeleg window (January)** | Jahresbeleg for the previous Vienna year is due by **31 January**. FON Belegcheck reminders run through **15 February**. | All of **January** (avoid February 1–15 as well if Jahresbeleg FON is still open). |

Also skip deploys while:

- A **Startbeleg** / **Jahresbeleg** FinanzOnline outbox is in flight for any live register.
- Ops is executing Fiskaly LIVE or FON PROD cutover ([`FISKALY_PRODUCTION_CUTOVER.md`](FISKALY_PRODUCTION_CUTOVER.md), [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md)).

**Preferred slot:** Staging/Canary any quiet weekday. Production: mid-morning Vienna on a day **8–28 of a month that is not January**, after Tagesabschluss for the previous day is Succeeded, with no pending Sonderbeleg FON rows.

---

## 2. Migration order

Prerequisite already in Production: `tenant_settings` (feature-flag store, including `20260729230000_AddTenantSettings`). Do not invent a second flag table.

Country-layer EF migrations (additive only, no `AlterColumn` on `country`):

| Order | Migration id | What it does | Backfill |
|-------|----------------|--------------|----------|
| 1 | `20260916110000_AddCompanySettingsCountryBilling` | Adds `company_settings.billing_country` (nullable), `vat_regime` (NOT NULL, default `AT_RKSV_STANDARD`), `tax_exempt` (NOT NULL, default `false`). **Does not touch** `country`. | PostgreSQL `ADD COLUMN … DEFAULT` plus explicit `UPDATE` guards in the migration. Existing mandants → `vat_regime = 'AT_RKSV_STANDARD'`, `tax_exempt = false`, `billing_country` null. `country` stays `'AT'`. |
| 2 | `20260921180000_AddFiscalDocumentCountryAtIssueSnapshots` | Adds nullable `country_code_at_issue` (`varchar(2)`) and `vat_regime_at_issue` (`varchar(32)`) on `invoices`, `receipts`, `payment_details`. | **None.** Legacy rows stay `NULL`. Do **not** `UPDATE` snapshots from live `company_settings.country` after a country change ([`COUNTRIES.md`](COUNTRIES.md) §15). |

`dotnet ef database update` (used by [`scripts/ops/deploy-production.sh`](../scripts/ops/deploy-production.sh)) applies **all pending** migrations. If step 1 already landed in an earlier release, only step 2 is new.

### 2.1 Apply

1. Take a **System** backup (PgDump), not Fake. See [`PRODUCTION_DEPLOYMENT_RUNBOOK.md`](PRODUCTION_DEPLOYMENT_RUNBOOK.md) §4.1.
2. Confirm `/health/migrations` is understood (`pendingCount` before vs after).
3. Run the scripted deploy **or** migrate job with Environment approval, then start the new API.
4. Confirm `GET /health/migrations` → `pendingCount=0` (also FA `/admin/database/migrations` if used).

```bash
# Scripted host (see PRODUCTION_DEPLOYMENT_RUNBOOK.md §3)
export REGKASSE_DEPLOY_CONFIRM=YES
# CONNECTION_STRING, REGKASSE_ROOT, API_PUBLISH_DIR, SYSTEMD_UNIT, API_BASE …
sudo -E ./scripts/ops/deploy-production.sh
```

Do **not** set `SKIP_MIGRATE` for this cutover.

### 2.2 Backfill checks (read-only SQL)

Run on Staging first, then Production after migrate. Super Admin / DBA only. Do not guess DE/CH/EU if a row is wrong — **fail closed** and keep AT payments on the existing `country` / tax-number path ([`COUNTRIES.md`](COUNTRIES.md) §9.2).

```sql
-- After 20260916110000: operating country unchanged; new columns at AT defaults.
SELECT
  COUNT(*) FILTER (WHERE country IS DISTINCT FROM 'AT') AS non_at_country,
  COUNT(*) FILTER (WHERE vat_regime IS DISTINCT FROM 'AT_RKSV_STANDARD') AS non_at_regime,
  COUNT(*) FILTER (WHERE tax_exempt IS DISTINCT FROM false) AS tax_exempt_true,
  COUNT(*) FILTER (WHERE billing_country IS NOT NULL) AS billing_country_set,
  COUNT(*) FILTER (WHERE vat_regime IS NULL OR vat_regime = '') AS missing_regime
FROM company_settings;

-- Expect on a pre-country-layer fleet: non_at_country = 0, missing_regime = 0.
-- non_at_regime / tax_exempt_true / billing_country_set may be > 0 only after
-- an operator explicitly set them (wizard / PATCH country) — not from this migration.

-- After 20260921180000: snapshots exist; historical rows may be NULL (expected).
SELECT
  (SELECT COUNT(*) FROM invoices WHERE country_code_at_issue IS NULL) AS invoices_legacy_null,
  (SELECT COUNT(*) FROM receipts WHERE country_code_at_issue IS NULL) AS receipts_legacy_null,
  (SELECT COUNT(*) FROM payment_details WHERE country_code_at_issue IS NULL) AS payments_legacy_null;
```

There is **no** `CountryCode` column on `company_settings`. A second country field is a defect ([`COUNTRIES.md`](COUNTRIES.md) §2.1).

### 2.3 Rollback (migrations)

| Step | Rollback | Do not |
|------|----------|--------|
| After schema apply, before or after new API | Roll back the **application package** ([`PRODUCTION_DEPLOYMENT_RUNBOOK.md`](PRODUCTION_DEPLOYMENT_RUNBOOK.md) §6). Additive columns can stay. Older API binaries ignore the extra columns. | `Down()` / `DROP COLUMN` as the first move. Dropping `vat_regime` / snapshots destroys tenant configuration and issue-time evidence. |
| Bad backfill (should not happen; step 1 only updates NULL/empty `vat_regime`) | Fail closed. Restore from the System dump / isolated clone. | Guess `DE` / `CH` / `EU_DEFAULT` onto live mandants. |

PITR / isolated restore: [`BACKUP_AND_DISASTER_RECOVERY.md`](BACKUP_AND_DISASTER_RECOVERY.md). `rollback-production.sh` does **not** undo EF.

---

## 3. Backend deploy order

Schema (step 2) **before** or **with** the API binary that writes `CountryCodeAtIssue`. Expand-then-code is already the host script order: migrate, then `systemctl restart`.

### 3.1 Config sections (do not invent keys)

Use the Production secret store / `appsettings.Production.json` / systemd `EnvironmentFile`. Template: [`backend/appsettings.Production.example.json`](../backend/appsettings.Production.example.json). Details: [`ENVIRONMENT_CONFIGURATION.md`](ENVIRONMENT_CONFIGURATION.md).

| Section | Production expectation for this cutover |
|---------|----------------------------------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `Tse:*` / `RKSV:*` / FinanzOnline | **Unchanged AT lock.** Device / Real / `UseSimulation=false`. Do not merge DE into `Tse:`. |
| `FeatureFlags` experimental | Leave `EnableNewPaymentFlow`, `EnableDepExportV2`, `EnableOnlineOrdersV2`, `EnableAutoAusfall` as they are today (defaults **false** unless already enabled on purpose). |
| `FeatureFlags:Fiscal:KassenSicherheitDe` | Appsettings default **false**. A **DE** tenant still resolves **on** from the country profile (`source=country_profile`). That is **not** live KassenSichV. |
| `FeatureFlags:Fiscal:MwstCh` | Appsettings default **false**; CH tenant profile default **on**. Not live MWST TSE. |
| `FeatureFlags:EInvoicing:*` | Keep **false** in appsettings. `EInvoicing.Zugferd` / `XRechnung` are **not** country-derived (stay off until a tenant/global override). CH profile may turn `EInvoicing.QrRechnung` on — still a stub builder. |
| `FeatureFlags:Vies:CheckEnabled` | **false**. Do not enable live VIES. |
| `Fiscal.RksvAt` | **No** `FeatureFlagsOptions` property. Must never be defaulted **false** in appsettings. Locked **on** for AT. |
| `KassenSicherheit` | Stub lock only: `Provider=not-configured` (or omit). `Provider=fake` or `AllowSimulatedTse=true` **fails startup** in Production/Staging. |
| `Mwst` | `UseTestEndpoint=false` (or omit). `true` fails startup in Production/Staging. |
| `QrRechnung` | `BuilderMode=not-configured` (or omit). `dryRun` fails startup in Production/Staging. |
| `En16931` / `Vies` | Optional / unused. Do not point at live networks. |

DE/CH host lock (`CountryFiscalLockEvaluator`) always runs in Production **and** Staging when those keys are set. There is **no** DE/CH escape hatch. AT escape hatch `Tse:AllowUnsafeFiscalModesInProduction` is unrelated and must stay **false**.

### 3.2 Flag defaults (resolution)

Country / fiscal flags ([`COUNTRIES.md`](COUNTRIES.md) §4, `IFeatureFlagService`):

```text
AT Fiscal.RksvAt lock → tenant override → country profile default → global override → FeatureFlagsOptions → false
```

Smoke `GET /api/admin/feature-flags?tenantId={id}` (`system.critical`):

| Tenant | Flag | Expected `enabled` | Expected `source` |
|--------|------|--------------------|-------------------|
| AT | `Fiscal.RksvAt` | `true` | `locked` |
| AT | `Fiscal.KassenSicherheitDe` / `Fiscal.MwstCh` | `false` | `country_profile` |
| DE | `Fiscal.RksvAt` | `false` | `country_profile` |
| DE | `Fiscal.KassenSicherheitDe` | `true` | `country_profile` |
| CH | `Fiscal.MwstCh` | `true` | `country_profile` |
| Any | `Vies.CheckEnabled` | `false` unless an override exists | `config` |

Do **not** set a global override that turns `Fiscal.RksvAt` off. For an AT tenant the lock rejects that.

PUT `/api/admin/feature-flags` is audited (`AuditEventType.FeatureFlagChanged`).

### 3.3 Rollback (backend)

| Situation | Action |
|-----------|--------|
| New API fails ready / TSE lock | `rollback-production.sh` to previous stamp ([`PRODUCTION_DEPLOYMENT_RUNBOOK.md`](PRODUCTION_DEPLOYMENT_RUNBOOK.md) §6). Schema stays. |
| DE/CH shape misbehaves on a canary tenant | Clear tenant flag overrides; do not process POS sales for that mandant. Soft-archive the test tenant if needed. |
| Someone enabled VIES / e-invoicing globally | `PUT /api/admin/feature-flags` with `enabled: false` or `clearOverride: true`. |

Cache: after migrate, prefer TTL + invalidation. Manual `POST /api/admin/cache/clear` only if tenant settings look stale ([`PRODUCTION_DEPLOYMENT_RUNBOOK.md`](PRODUCTION_DEPLOYMENT_RUNBOOK.md) Cache Management).

---

## 4. FA deploy order

FA **consumes** `GET /api/admin/countries` and `PATCH /api/admin/tenants/{id}/country`. Deploy **API first**, then Admin ([`ADMIN_FA_DEPLOY.md`](ADMIN_FA_DEPLOY.md)). Rolling FA before API yields 404 on the country catalog/card.

### 4.1 Orval

Country DTOs already ship in `backend/swagger.json`. Before the release branch is tagged:

```bash
node scripts/generate-backend-openapi.mjs
cd frontend-admin && npm run generate:api
node scripts/verify-api-client.mjs
```

CI: `api-client-alignment.yml`. Do not hand-edit `frontend-admin/src/api/generated/`.

### 4.2 Env vars (build-time)

No country-specific `NEXT_PUBLIC_*` key exists. Rebuild FA only if these change ([`docs/ADMIN_FA_DEPLOY.md`](ADMIN_FA_DEPLOY.md), `frontend-admin/docs/DEPLOYMENT_BUILD_TIME_ENV.md`):

| Variable | Production |
|----------|------------|
| `NEXT_PUBLIC_API_BASE_URL` | `https://api.regkasse.at` (origin, no trailing `/api` unless the image already expects it — match the live Admin image) |
| `NEXT_PUBLIC_RKSV_ENVIRONMENT` | `PROD` / `PRODUCTION` (org-standard; must not stay `TEST` on the production bundle) |
| `NEXT_PUBLIC_RELEASE_STAGE` | `production` (optional banner) |
| `NEXT_PUBLIC_SENTRY_DSN` | Production FA project DSN (optional but expected for monitoring) |

`NEXT_PUBLIC_*` are **baked at `next build`**. Changing them on a running container is not enough.

### 4.3 Rollback (FA)

Revert the previous Admin image/build. Older FA without the country card still works against a new API (Mandanten-Admin simply will not see the new panel). Newer FA against an old API breaks the country wizard — keep the pair in the same window.

---

## 5. POS deploy order

**None expected.**

Single POS UI: `https://pos.regkasse.at`. Tenant comes from JWT `tenant_id` after login, not from a country host ([`POS_PRODUCTION_ARCHITECTURE.md`](POS_PRODUCTION_ARCHITECTURE.md)). POS copy stays German (de-DE). There is no country-layer Expo env var and no POS OpenAPI regen for this package.

Non-AT tenants must **not** take AT RKSV/TSE payments. `RksvSpecialReceiptService` / TSE tax-set projection stay Austria-only (`NotSupportedException`). Do not smoke a **DE/CH sale** on Production POS.

Optional: `GET {POS_BASE}/` still 200 from the generic smoke script if `POS_BASE` is set. That is an uptime check, not a country check.

### 5.1 Rollback (POS)

No POS rollback for this cutover. If a non-AT JWT somehow reaches POS checkout, fail closed on the API; do not patch POS to invent AT BelegNr.

---

## 6. Smoke checks per step

Use Super Admin JWT. Cross-tenant misses stay **HTTP 404**. Do not enable `SMOKE_POS_PAYMENT=1` on Production ([`DEPLOYMENT_SMOKE_TEST.md`](DEPLOYMENT_SMOKE_TEST.md)).

### Step A — After migrations (API up, old or new binary)

| Check | Pass |
|-------|------|
| `GET /api/health/live` | 200 |
| `GET /api/health/ready` | 200 (DB + AT TSE/FON lock) |
| `GET /health/migrations` | 200, `pendingCount=0` |
| SQL backfill queries in §2.2 | No unexpected non-AT `country`; no empty `vat_regime` |

**Rollback:** app package rollback if ready fails; do not drop columns.

### Step B — After new API binary

| Check | Pass |
|-------|------|
| `GET /api/rksv/environment` | Production / not simulation (AT fiscal posture unchanged) |
| `GET /health/tse/mode` | Fail-closed Device/Real as today |
| `GET /api/admin/countries` | HTTP 200; selectable codes **AT, DE, CH** only; **`EU_DEFAULT` absent** |
| AT flag row | `Fiscal.RksvAt` enabled + `locked` for an existing AT tenant |

**Rollback:** `rollback-production.sh`.

### Step C — AT tenant create

Super Admin create-tenant wizard (country step → form) or `POST /api/admin/tenants` with `country=AT`, `vatRegime=AT_RKSV_STANDARD`.

| Check | Pass |
|-------|------|
| Tenant + `company_settings` | `country='AT'`, `vat_regime='AT_RKSV_STANDARD'` |
| Audit | `AuditEventType.TenantCreatedWithCountry` (96) |
| Flags | `Fiscal.RksvAt` locked on; DE/CH fiscal flags off |
| TSE seeds | AT tax groups / register provisioning **unchanged** vs today’s AT create |

On Production, prefer a **Staging** create if you do not want another live mandant. Creating AT on Production is allowed but is a real tenant.

**Rollback:** do not delete fiscal history; archive/soft-delete per tenant policy if the smoke tenant is disposable.

### Step D — AT invoice + AT TSE signature

On **Staging** (Soft TSE / simulation): one cash payment on an AT register (`SMOKE_POS_PAYMENT` only on simulation hosts).

On **Production**: one **real** compact JWS is a fiscal receipt. Prefer an existing AT register during a quiet moment **outside** §1 windows, or skip live payment if Compliance forbids extra Belege and instead:

- Confirm a **post-cutover** sale that already happened in the next business hour, **or**
- Create a **Nullbeleg** (still TSE-signed — treat as fiscal) only with Compliance approval.

| Check | Pass |
|-------|------|
| Payment `TseSignature` | Non-empty compact JWS (three segments) |
| `certificate_thumbprint` | Stamped |
| Invoice / receipt / `payment_details` | New rows: `country_code_at_issue='AT'`, `vat_regime_at_issue='AT_RKSV_STANDARD'` (legacy pre-migration rows may still be null) |
| CountryBaseline | Already green in CI; Production does not re-run the suite |

This is **not** a substitute for [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md) §1 TSE smoke.

**Rollback:** AT path should be byte-identical; if signatures fail, roll back the API binary immediately (TSE config, not country flags).

### Step E — DE tenant create

`POST /api/admin/tenants` with `country=DE` and a profile-allowed regime (`DE_USTG_STANDARD` or `DE_KLEINUNTERNEHMER`). FA wizard shows the non-AT “RKSV/TSE is not enabled” banner.

| Check | Pass |
|-------|------|
| `company_settings.country` | `DE` |
| Catalog | `GET /api/admin/countries` listed DE |
| Flags | `Fiscal.KassenSicherheitDe` on (`country_profile`); `Fiscal.RksvAt` off |
| AT TSE | **Not** provisioned for this tenant |
| POS sale | **Do not** take a Production DE payment. Expect fail-closed if attempted (`NotSupportedException` / feature gate — not an AT fallback). |

**Rollback:** do not rewrite historical AT rows. Soft-archive the DE smoke tenant. Optional: tenant override `Fiscal.KassenSicherheitDe=false` (does not make DE production-ready; it only turns the gate off).

### Step F — CH tenant create

Same as DE with `country=CH`, `vatRegime=CH_MWST_STANDARD` or `CH_KLEINUNTERNEHMER`.

| Check | Pass |
|-------|------|
| `company_settings.country` | `CH` |
| Flags | `Fiscal.MwstCh` on (`country_profile`); `Fiscal.RksvAt` off |
| QR stub | No bank submission; `QrRechnung:BuilderMode` is not `dryRun` |
| POS sale | **Do not** take a Production CH payment |

### Step G — Flag resolution (all tenants used above)

```http
GET /api/admin/feature-flags?tenantId={at}
GET /api/admin/feature-flags?tenantId={de}
GET /api/admin/feature-flags?tenantId={ch}
```

Compare to the table in §3.2. FA `/admin/feature-flags` is the same data.

Unknown country codes on operator input stay `UNKNOWN_COUNTRY_CODE` (HTTP 400). Reads of legacy/blank `country` still `GetOrDefault` → AT.

---

## 7. Monitoring signals

Watch for **30–60 minutes** after Production restart, then the next Tagesabschluss.

### 7.1 Health / metrics

| Signal | Where | Fail if |
|--------|-------|---------|
| API live / ready | `GET /api/health/live`, `/ready` · Prometheus `RegkasseApiDown` / `RegkasseApiNotReady` | Ready unhealthy (DB or AT fiscal lock) |
| Migrations | `GET /health/migrations` | `pendingCount > 0` after the migrate job |
| `/metrics` | [`MONITORING.md`](MONITORING.md) | Error-rate spike vs previous release |
| Canary monitor | `Deployment:CanaryMonitor` | Error count/rate over threshold → auto-rollback on canary |

### 7.2 Sentry (FA)

FA project DSN: `NEXT_PUBLIC_SENTRY_DSN` ([`ALERTING.md`](ALERTING.md), [`frontend-admin/monitoring/sentry-alert-recipes.md`](../frontend-admin/monitoring/sentry-alert-recipes.md)).

| Watch | Meaning |
|-------|---------|
| Axios 5xx on `/api/admin/countries` or `/api/admin/tenants` | FA shipped before API, or catalog regression |
| Axios 5xx on `PATCH /api/admin/tenants/{id}/country` | Country change path |
| Issue tag `COUNTRY_LOCKED_FISCAL` | Stale FA still treating post-fiscal country change as hard-fail (Paket 16 lifted this) |
| Unchanged AT payment 5xx | Stop. This is an AT fiscal regression, not a DE flag issue. |

Sentry is **not** a substitute for `/api/health/ready`. Backend fiscal failures also land in structured API logs (English) and Slack via Alertmanager.

### 7.3 Activity feed (FA bell / SSE / email / webhook)

| Event | When |
|-------|------|
| `ActivityEventType.TenantCountryChanged` (251) | Super Admin PATCH country / vatRegime |
| `ActivityEventType.TenantSettingsChangeRequested` / `Approved` | Four-eyes country change via tenant settings |
| `FeatureFlagChanged` (audit; activity if wired for flags) | Super Admin flag PUT |

Filter by the smoke tenant ids. Payload must not contain TSE/JWS secrets.

### 7.4 Audit log

| `AuditEventType` | Value | Notes |
|------------------|-------|--------|
| `TenantCreatedWithCountry` | 96 | Create-tenant with explicit country |
| `TenantCountryChanged` | 97 | Operating country / regime change |
| `TenantCountryChangedHistoricalPreserved` | 98 | `newValues.affectedRowCount`; rows **not** rewritten |
| `FeatureFlagChanged` | 56 | Flag override |

Query Super Admin audit for those types in the cutover window. Historical-preserved count must match invoice + receipt + payment_details for that tenant (join payments via `cash_registers`).

---

## 8. Sign-off

| Role | Confirms | ☐ |
|------|----------|---|
| **Ops** | Migrate + API/FA deploy outside §1 windows; health + migrations green | |
| **Backend lead** | AT TSE/FON locks unchanged; DE/CH stubs fail-closed; no `CountryCode` on `company_settings` | |
| **Compliance** | No Production DE/CH POS sales; no VIES; AT Beleg smoke (if any) approved | |
| **Product** | FA country wizard + card acceptable; Mandanten-Admin view-only | |

**Environment:** ☐ Staging smoke complete · ☐ Canary soak · ☐ Production schema + API + FA · ☐ POS unchanged

---

## 9. Quick command index

Both migration URLs return the same JSON (`pendingCount`). Either is valid.

```bash
# Migrations pending?
curl -fsS https://api.regkasse.at/health/migrations
curl -fsS https://api.regkasse.at/api/health/migrations

# Country catalog (Super Admin; ambient tenant exemption)
curl -fsS -H "Authorization: Bearer $TOKEN" https://api.regkasse.at/api/admin/countries

# Flags for one mandant
curl -fsS -H "Authorization: Bearer $TOKEN" \
  "https://api.regkasse.at/api/admin/feature-flags?tenantId=$TENANT_ID"

# AT fiscal posture (unchanged)
curl -fsS -H "Authorization: Bearer $TOKEN" https://api.regkasse.at/api/rksv/environment
```
