# Feature flags

**Last updated:** 2026-11-13  
**Related:** [`ENVIRONMENT_CONFIGURATION.md`](ENVIRONMENT_CONFIGURATION.md) · [`COUNTRIES.md`](COUNTRIES.md) · [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md)

Ship code with new behavior **off by default**, then enable it globally or per tenant without redeploying binaries.

`GET /api/admin/feature-flags` returns every name in `FeatureFlagNames.Catalog` (`All` plus `Reserved`). Writes still accept only `FeatureFlagNames.All`. `EInvoicing.Peppol` is listed with source `reserved` and stays off.

---

## 1. Flags

| Flag | Purpose | Default |
|------|---------|---------|
| `EnableNewPaymentFlow` | Alternate payment path hook in `PaymentService` | Off (`FeatureFlags:EnableNewPaymentFlow`) |
| `EnableDepExportV2` | DEP export adds header `X-Regkasse-Dep-Export-Schema: v2`. The BMF JSON body is unchanged | Off |
| `EnableOnlineOrdersV2` | Online-order intake marker `intakeVersion: "v2"` | Off |
| `EnableAutoAusfall` | TSE failover may enqueue a FON Ausfall immediately, and only when `Ausfall:AutoEnqueue=true` | Off |
| `Fiscal.RksvAt` | Austrian RKSV signing. Locked on when the mandant profile is `RKSV_AT` | On for AT, off otherwise. No appsettings property |
| `Fiscal.KassenSicherheitDe` | German KassenSichV signing. Profile default stays off until that package is production-ready | Off |
| `Fiscal.MwstCh` | Swiss VAT | On for CH, off otherwise |
| `EInvoicing.Zugferd` | ZUGFeRD builder. Not country-derived | Off |
| `EInvoicing.XRechnung` | XRechnung builder. Not country-derived | Off |
| `EInvoicing.QrRechnung` | QR-bill | On when the profile lists `QR_RECHNUNG` (CH) |
| `EInvoicing.En16931` | EN 16931 / UBL. Does not gate reverse-charge tax or disclosures | On when the profile lists `EN_16931` (`EU_DEFAULT`) |
| `Vies.CheckEnabled` | VIES VAT-ID check. Not country-derived | Off |
| `EInvoicing.Peppol` | Peppol send. Reserved. Not writable until package 22-b-5 moves it into `All` | Off (`source=reserved`) |

Experimental names (`Enable*`) resolve as tenant override, then global override, then appsettings. Country profile is not consulted.

Country and fiscal names resolve as: AT `Fiscal.RksvAt` lock, then tenant override, then country profile, then global override, then `FeatureFlagsOptions`, then `false`. Detail: [`COUNTRIES.md`](COUNTRIES.md) §4.

Env example: `FeatureFlags__EnableAutoAusfall=true`.

---

## 2. Admin API (Super Admin / `system.critical`)

| Method | Path | Notes |
|--------|------|--------|
| `GET` | `/api/admin/feature-flags?tenantId=` | Every catalog flag. `tenantOverrides` lists each mandant row. `tenantId` only changes the effective `enabled` / `source` for that mandant |
| `PUT` | `/api/admin/feature-flags` | Body: `{ name, enabled, tenantId?, clearOverride? }`. Omit `tenantId` for the global value. Reserved names return 400 |
| `GET` | `/api/admin/feature-flags/{name}/enabled?tenantId=` | Quick check |

Changes are audited (`AuditEventType.FeatureFlagChanged`). Overrides live in `tenant_settings` under `FeatureFlags:{Name}`. `tenant_id` null is the global row.

---

## 3. FA UI

Route `/admin/feature-flags` (sidebar, `system.critical` only).

| Column / control | Behavior |
|--------------------|----------|
| Flag | Canonical name |
| Description | Purpose from the admin catalogs (de / en / tr) |
| Current value | Switch. Writes the global override (`tenantId` null). Disabled for `EInvoicing.Peppol` |
| Tenant overrides | One tag per mandant row (`On` / `Off`) |
| Override for tenant | Dropdown of active mandants. The switch under each flag writes that mandant's override |

---

## 4. Tests

```bash
dotnet test backend/KasseAPI_Final.sln --filter "FullyQualifiedName~FeatureFlag"
cd frontend-admin && npx vitest run src/features/feature-flags/__tests__/FeatureFlagsScreen.test.tsx
cd frontend-admin && npx playwright test tests/e2e/feature-flags.spec.ts
```
