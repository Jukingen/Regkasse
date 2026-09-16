# Countries and fiscal regimes

**Last updated:** 2026-09-16  
**Related:** [`AGENTS.md`](../AGENTS.md) · [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md) (stub) · [`FISCAL_SWITZERLAND.md`](FISCAL_SWITZERLAND.md) (stub) · [`EINVOICING_EU.md`](EINVOICING_EU.md) (stub) · [`FEATURE_FLAGS.md`](FEATURE_FLAGS.md) · [`ENVIRONMENT_CONFIGURATION.md`](ENVIRONMENT_CONFIGURATION.md)

This hub describes the multi-country architecture. It is not a legal opinion and does not certify RKSV, KassenSichV, MWST, EN 16931, or ViDA compliance.

Austria (RKSV / TSE / FinanzOnline) is the production fiscal system. Germany, Switzerland, and generic EU e-invoicing are **planned**. Two foundations have shipped and are wired to nothing yet: the per-tenant country and VAT-regime **columns** and the **`CountryProfile` registry** (both in [§2](#2-countryprofile-and-the-per-tenant-binding)). Country-aware tax/invoice strategies, the provisioning country step, the shared VAT-ID validator, and country feature-flag names are **not implemented in code yet**. Do not treat this document as proof that those types exist on disk.

---

## Status

| Country | Fiscal System | Status | Notes |
|---------|---------------|--------|-------|
| **AT** | RKSV / TSE / FinanzOnline | **Production (live SoT)** | Current behavior unchanged; pinned by the baseline regression suite in [§11](#11-testing-strategy) |
| **DE** | KassenSicherheit (planned) | Planned — no code | See [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md) (stub) |
| **CH** | MWST + QR-Rechnung (planned) | Planned — no code | See [`FISCAL_SWITZERLAND.md`](FISCAL_SWITZERLAND.md) (stub) |
| **EU_DEFAULT** | EN 16931 (planned) | Registry-only, **not tenant-selectable** | See [`EINVOICING_EU.md`](EINVOICING_EU.md) (stub) |

`EU_DEFAULT` is a fallback profile identifier, not an ISO 3166-1 alpha-2 code. It must never appear in the Super Admin create-tenant country list.

---

## 1. Purpose

Regkasse is Austria-first. Multi-country work must not fork the live payment or TSE pipelines.

The intended flow is:

1. Resolve the mandant's country from `CompanySettings.Country` (column `country`).
2. Load a **CountryProfile** from an in-code registry (not appsettings).
3. Choose VAT and invoice behavior through **TaxStrategy** / **InvoiceStrategy** plus `CompanySettings.VatRegime`.
4. Gate country-specific fiscal and e-invoicing modules with **feature flags** stored in the existing `tenant_settings` table.

Steps 1 and 3 have their persistence in place. Steps 2 and 4 are design targets. Until they ship, keep using the Austrian RKSV/TSE rules in [`AGENTS.md`](../AGENTS.md) and the `RKSV_*.md` docs.

---

## 2. CountryProfile and the per-tenant binding

### 2.1 Shipped columns on `company_settings`

Migration `20260916110000_AddCompanySettingsCountryBilling` added the billing and regime columns additively. The operating country column predates it and was deliberately **reused, not duplicated**.

| Column | Type | Null | Default | Role |
|--------|------|------|---------|------|
| `country` | `varchar(2)` | no | `'AT'` | **The tenant's operating country.** ISO 3166-1 alpha-2. Pre-existing. |
| `billing_country` | `varchar(2)` | yes | `null` | Optional billing country when invoicing happens elsewhere. Null means "bill in the operating country". |
| `vat_regime` | `varchar(32)` | no | `'AT_RKSV_STANDARD'` | VAT regime for tax calculation and invoice disclosures. |
| `tax_exempt` | `boolean` | no | `false` | Mandant is exempt from VAT (small-business or equivalent relief). |

**There is no `CountryCode` column, and none will be added.** `CompanySettings.Country` is the binding. A regression test (`CompanySettingsCountryFieldsTests`) fails the build if a second country column or a `CountryCode` property appears. Likewise, do not invent a country field on `Tenant`.

Two adjacent fields were also reused rather than duplicated: `Language` is the locale preference (no `PreferredLocale`), and `Currency` is the currency preference (no `PreferredCurrency`).

### 2.2 VAT-ID

VAT-ID lives on `CompanySettings.CompanyTaxNumber`; `CompanySettings.VatId` is a `[NotMapped]` alias over it. There is no separate `vat_id` column. Country-specific VAT-ID patterns belong in CountryProfile seeds (§7), not in a new column.

### 2.3 `VatRegime` values

Persisted as the enum **member name**, so renaming a member is a breaking schema change — add a new member instead.

| Value | Applies to |
|-------|------------|
| `AT_RKSV_STANDARD` | Austria, standard RKSV cash-register VAT (**default for every existing mandant**) |
| `DE_USTG_STANDARD` | Germany, standard UStG VAT |
| `DE_KLEINUNTERNEHMER` | Germany, small-business exemption |
| `CH_MWST_STANDARD` | Switzerland, standard MWST |
| `CH_KLEINUNTERNEHMER` | Switzerland, small-business exemption |
| `EU_REVERSE_CHARGE` | Intra-EU B2B supply; VAT accounted for by the recipient |
| `EU_OSS` | EU One-Stop-Shop reporting |
| `NON_EU` | Outside the EU VAT area |

Regime is independent from country: an Austrian mandant may legitimately invoice under `EU_REVERSE_CHARGE`.

### 2.4 The registry (shipped)

`ICountryProfileRegistry` / `CountryProfileRegistry` seeds `AT`, `DE`, `CH`, and `EU_DEFAULT` in code and is registered as a singleton. **Nothing calls it yet** — the strategy layer, provisioning country step, and VAT-ID validator are still to come, so adding it changed no behavior.

| Field | Role |
|-------|------|
| `Code` | ISO 3166-1 alpha-2 for AT/DE/CH; sentinel `EU_DEFAULT` for the fallback profile |
| `Name` | English display name; Admin UI should translate rather than print it |
| `IsTenantSelectable` | False for `EU_DEFAULT`; the create-tenant list uses `TenantSelectable` |
| `Currency` / `DefaultLocale` / `DefaultTimeZone` | Formatting and company-settings copies |
| `FiscalSystem` | `RKSV_AT`, `KASSENSICHERHEIT_DE`, `MWST_CH`, or `NONE` — only `RKSV_AT` is implemented |
| `EInvoicingStandards` | Which invoice builders may run once flagged on; may be empty |
| `VatIdPattern` / `VatIdRegex` | Validator source of truth for the VAT-ID shape |
| `AllowedVatRegimes` | Reject incompatible regimes at provision time (`Supports(VatRegime)`) |

Lookup semantics: `GetOrDefault` resolves unknown, legacy, or blank codes to **AT** (for reading existing tenants); `Get` throws `UnknownCountryCodeException` with error code `UNKNOWN_COUNTRY_CODE` (for validating operator input).

A profile deliberately carries **no VAT rates** — a unit test fails the build if a rate-like property is added.

**Seed verification status:** the AT seed is authoritative because it mirrors values already live in production (`EUR`, `de-DE`, `Europe/Vienna`, and the UID pattern `^ATU\d{8}$`), and regression tests pin it. The DE, CH, and EU_DEFAULT seeds describe **shape only** and have not been checked against official sources; they gate nothing today. Source verification is a separate reviewed change.

---

## 3. TaxStrategy and InvoiceStrategy

**Target:** `ITaxStrategy` and `IInvoiceStrategy` selected from CountryProfile plus `VatRegime`. Neither interface exists in code yet.

Austria must keep today's receipt and tax **output**, byte for byte where it is reproducible at all (§11). Wrap or select at the edges. Do not rewrite `PaymentService` internals, `TseService`, `RksvSpecialReceiptService`, or `CartMoneyHelper` as a parallel AT engine.

Non-AT strategies must not enable RKSV special receipts or Austrian TSE signing.

---

## 4. Feature flags

**Today (implemented and verified):** `IFeatureFlagService` persists overrides in **`tenant_settings`** under key `FeatureFlags:{Name}`, where `tenant_id = null` means a global override. Config defaults come from the appsettings `FeatureFlags` section via `FeatureFlagsOptions`. Canonical names today: `EnableNewPaymentFlow`, `EnableDepExportV2`, `EnableOnlineOrdersV2`, `EnableAutoAusfall`. Resolution order today is tenant override → global override → appsettings default. Management API: `/api/admin/feature-flags` (Super Admin). See [`FEATURE_FLAGS.md`](FEATURE_FLAGS.md).

Flags do **not** live on `company_settings`.

**Planned country flags** (not in `FeatureFlagNames` yet):

| Flag | Intended default |
|------|------------------|
| `Fiscal.RksvAt` | On for AT; **locked on** for AT tenants (cannot be turned off) |
| `Fiscal.KassenSicherheitDe` | On for DE profile; off for AT |
| `Fiscal.MwstCh` | On for CH profile; off for AT |
| `EInvoicing.Zugferd` / `EInvoicing.XRechnung` | From DE e-invoicing profile |
| `EInvoicing.QrRechnung` | From CH profile |
| `EInvoicing.En16931` | From EU_DEFAULT / explicit EU e-invoicing |
| `Vies.CheckEnabled` | Always default **off** (not derived from country) |

**Planned resolution for country flags:** tenant override in `tenant_settings` → country default derived from `CompanySettings.Country` + `VatRegime` (later, from the CountryProfile registry). Do **not** use an appsettings `false` as the AT RKSV default — that would disable production TSE. Existing experimental flags keep their current order unchanged.

`Fiscal.RksvAt` locked-true for AT is **documented here only** until code exists.

---

## 5. Tenant provisioning

**Today:** Super Admin `CreateTenantWizard` is a **single-step** form (name, slug, contact, trial, demo). There is no country step and no `GET /api/admin/countries`. New tenants receive the column defaults from §2.1: `country = 'AT'`, `vat_regime = 'AT_RKSV_STANDARD'`, `tax_exempt = false`, `billing_country = null`.

**Planned:**

1. Country + `VatRegime` (list from `GET /api/admin/countries`; `EU_DEFAULT` omitted).
2. Existing tenant details form.
3. Processing and success (unchanged UX after submit).

`POST /api/admin/tenants` must require an ISO 3166-1 alpha-2 country and a compatible `vatRegime`. Unknown codes → HTTP 400. Non-AT tenants must **not** provision Austrian TSE or AT tax-group seeds.

---

## 6. AT / DE / CH / EU matrix

| | AT | DE | CH | EU_DEFAULT |
|--|----|----|----|------------|
| Selectable at create | Yes | Yes (planned) | Yes (planned) | **No** (fallback only) |
| Fiscal system | RKSV + TSE (production) | Planned KassenSicherheit | Planned MWST | None (e-invoicing default) |
| VAT label / rates | Live AT rates | Planned; not hardcoded in this hub | Planned MWST; not hardcoded here | OSS / reverse charge via `VatRegime` |
| VAT-ID | AT UID in CountryProfile seed | Planned DE UID seed | Planned CHE-… seed | Planned EU VAT-ID seed |
| E-invoicing | Not this hub | Stub: [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md) | Stub: [`FISCAL_SWITZERLAND.md`](FISCAL_SWITZERLAND.md) | Stub: [`EINVOICING_EU.md`](EINVOICING_EU.md) |
| Default flags | `Fiscal.RksvAt` on (lock) | DE fiscal/e-invoicing flags; `Fiscal.RksvAt` off | CH flags; `Fiscal.RksvAt` off | `EInvoicing.En16931`; ViDA report only |

EU_DEFAULT supports an EN 16931-oriented invoice builder and a **read-only** ViDA readiness report. It does not submit to a tax authority or Peppol network.

---

## 7. Formatting and VAT-ID

Admin UI **language** catalogs stay `de` / `en` / `tr`. Do not add `de-DE` / `de-CH` catalogs unless copy truly diverges. Tenant **formatting** (date, currency, separators, VAT label) should follow CountryProfile once a formatting hook exists.

VAT-ID regexes and normalization belong **only** in CountryProfile seeds (and a validator that reads the registry). Austria's current live pattern remains the AT seed. Do not copy country regexes into controllers or FA form constants as a second source of truth.

The one validator that exists today is deliberately narrow: `billing_country` is checked for the two-letter ISO 3166-1 alpha-2 **shape** and upper-cased on write. It does not assert the code is an assigned country, and it does not validate VAT-ID.

---

## 8. Configuration layers

| Layer | What belongs there | What does not |
|-------|--------------------|---------------|
| **CountryProfile seeds (code)** | Locale, currency, timezone, fiscal system, e-invoicing standards, VAT-ID pattern, allowed regimes, default flag derivation | Secrets, per-tenant overrides, Production TSE vendor keys |
| **`company_settings` (per-tenant binding)** | `country`, `vat_regime`, `billing_country`, `tax_exempt`, plus existing `Currency` / `Language` | Feature-flag key/value pairs; country profile definitions |
| **`tenant_settings` (overrides)** | Feature-flag overrides under `FeatureFlags:{Name}` via `IFeatureFlagService`; `tenant_id = null` is the global row | Country profile definitions; fiscal master data |
| **appsettings** | Existing `FeatureFlags` experimental defaults; planned sections `KassenSicherheit`, `QrRechnung`, `En16931`, `Vies` when those modules ship | CountryProfile seed data; the `Fiscal.RksvAt` default for AT |

Four rules follow from this split:

1. **CountryProfile is code-seeded, not appsettings-driven.** Changing a country's VAT-ID pattern or default fiscal system is a code/review change, not an ops JSON edit.
2. **`CompanySettings.Country` is the per-tenant binding.** Resolve country from it — never from the request host, the JWT, or a second column.
3. **Feature flags live in `tenant_settings`.** Never add a parallel flag table, and never store flags on `company_settings`.
4. **Unknown or legacy country values are treated as AT.** The `country` column is `NOT NULL DEFAULT 'AT'`, so empty rows should not exist; if one is ever observed, resolve it to AT rather than guessing DE/CH/EU.

`KassenSicherheit` must stay a **separate** configuration root from Austrian `Tse:`. Fake or simulated DE providers must fail closed outside Development, matching the existing TSE production lock. Do not name a required vendor in this hub.

`Vies:CheckEnabled` (or the `Vies.CheckEnabled` flag) defaults **off**. Tests must not call the live VIES network.

Details: [`ENVIRONMENT_CONFIGURATION.md`](ENVIRONMENT_CONFIGURATION.md).

---

## 9. Migration path

### 9.1 Shipped

Migration `20260916110000_AddCompanySettingsCountryBilling` is additive only: three new columns, no altered column, no altered index, no touch to `country`.

Existing mandants were backfilled to `vat_regime = 'AT_RKSV_STANDARD'` and `tax_exempt = false`, with `billing_country` left null. PostgreSQL backfills on `ADD COLUMN … DEFAULT`; the migration also carries explicit idempotent `UPDATE` guards. `country` needed no backfill because it was already `'AT'` for every existing row.

RKSV / TSE / FinanzOnline behavior is unchanged, and that claim is enforced by the regression suite in [§11](#11-testing-strategy) rather than asserted here.

### 9.2 Remaining

1. **Schema first, always additive.** Do not rewrite `country`. Do not drop or repurpose existing columns.
2. **Backfill in a separate step** from schema introduction, as above.
3. **Success criterion:** provisioning a new AT tenant produces today's result plus the new columns, and the AT baseline fixtures still match.
4. **If a backfill fails: fail closed.** Do not guess DE/CH/EU. Do not leave rows in a mixed or unknown regime in Production. The API must not serve fiscal operations for a mandant whose regime cannot be resolved — refuse rather than fall back to a guessed regime. Retry the backfill, and keep the AT payment path on the existing `country` / tax-number behavior until it reports complete.

Do not backfill `EU_DEFAULT` onto real mandants.

---

## 10. Rollback

Rollback is per country and flag-driven, not schema-driven.

1. **AT rollback is a no-op.** Austria is the unchanged production path. Keep `Fiscal.RksvAt` on (or ship without that flag until the lock exists). Never use a global flag to disable Austrian TSE.
2. **DE rollback:** turn off `Fiscal.KassenSicherheitDe`, `EInvoicing.Zugferd`, `EInvoicing.XRechnung` in `tenant_settings`. Those modules must no-op when off.
3. **CH rollback:** turn off `Fiscal.MwstCh` and `EInvoicing.QrRechnung`.
4. **EU rollback:** turn off `EInvoicing.En16931` and `Vies.CheckEnabled`.
5. **Application rollback** if flags are not enough. **Do not drop** the country/billing columns as a first rollback step; additive columns can stay unused and dropping them destroys tenant configuration.
6. Austrian `Tse:` / `PaymentService` / special receipts must run **without** loading DE/CH/EU builders.

---

## 11. Testing strategy

| Layer | Intent | State |
|-------|--------|-------|
| Unit — schema binding | New columns are additive; `country` not duplicated; `VatId` stays an unmapped alias | **Shipped** |
| Unit — migration | Every added column carries a default; backfill present; no altered column or index | **Shipped** |
| Unit — validators | `billing_country` shape and normalization | **Shipped** |
| Regression — AT fiscal chain | Austrian output unchanged by the country layer | **Shipped** |
| Unit — CountryProfile registry | Registry returns AT/DE/CH; `EU_DEFAULT` exists but is not selectable; unknown ISO code rejected; AT seed mirrors live defaults; no VAT rates on a profile | **Shipped** |
| Unit — VAT-ID shape from seeds | AT/DE/CH valid and invalid cases resolved from `VatIdPattern` | **Shipped** |
| Unit — VAT-ID validator + VIES | Shared `IVatIdValidator`, call-site migration, VIES client mocked only | Planned |
| E2E — `CreateTenantWizard` | Country step; AT create; DE defaults; unknown country → 400 | Planned (wizard has no country step yet) |
| CI | No live VIES, no real DE TSE, no Swiss bank APIs | Standing rule |

### 11.1 AT baseline regression suite

The Austrian chain was captured **before** any country abstraction, so drift is detectable:

- Generator and tests: `backend/KasseAPI_Final.Tests/CountryBaseline/`
- Frozen fixtures: `backend/KasseAPI_Final.Tests/Fixtures/CountryBaseline/`
- Re-capture after a reviewed, intentional change: run the suite with `REGKASSE_UPDATE_BASELINE=1` and explain the diff in the PR.

It covers VAT calculation, the Austrian five-bucket tax-set projection, RKSV `BelegNr` and billing invoice numbering, the RKSV §9 machine code, the encrypted turnover counter, the receipt QR wire format, the FinanzOnline beleg string, and the mandatory receipt disclosures.

**Two values are deliberately not byte-frozen, and this is not a defect to "fix":**

- The **ES256 signature segment** — .NET ECDSA draws a fresh nonce per call, so the third JWS segment differs every run. The suite freezes the JWS *signing input* (`header.payload`) and verifies the signature cryptographically instead.
- **Sig-Voriger-Beleg** — it is SHA-256 over the *previous receipt's signature*, so the randomness propagates down the chain. Snapshot steps are therefore signed against a frozen synthetic predecessor; real chain linkage is asserted structurally in a separate test.

Anyone adding a country layer must keep these fixtures green. If they go red, the Austrian output moved.

---

## 12. Related docs

| Doc | Role |
|-----|------|
| [`../AGENTS.md`](../AGENTS.md) | Agent rules; Country & Fiscal Regimes section |
| [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md) | **Stub** — DE KassenSicherheit / e-invoicing |
| [`FISCAL_SWITZERLAND.md`](FISCAL_SWITZERLAND.md) | **Stub** — CH MWST / QR-Rechnung |
| [`EINVOICING_EU.md`](EINVOICING_EU.md) | **Stub** — EN 16931 / ViDA readiness (no submission) |
| [`RKSV_COMPLIANCE.md`](RKSV_COMPLIANCE.md) and other `RKSV_*.md` | **Austria only** — do not reuse as DE/CH law |
| [`FEATURE_FLAGS.md`](FEATURE_FLAGS.md) | Current `tenant_settings` flag mechanism |
| [`ENVIRONMENT_CONFIGURATION.md`](ENVIRONMENT_CONFIGURATION.md) | Host/env vs planned country config sections |
| [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) | Austrian TSE fail-closed lock (pattern for DE fake providers) |

---

## 13. Do not

- **Do not hardcode VAT rates** (Austrian 20/10/13 or any other country's) in API controllers, Admin forms, or POS validators. Rates belong with tax types / CountryProfile / `VatRegime`.
- **Do not hardcode VAT-ID regexes** (`ATU…`, `DE…`, `CHE-…`) outside CountryProfile seeds. Seeds and a registry-backed validator own the pattern.
- **Do not hardcode country codes** outside seeds and the `company_settings.country` binding. No host-derived, JWT-derived, or constant-list country resolution.
- **Do not fork `PaymentService` or `TseService` internals** per country, and do not clone cart money logic. Select a strategy at the edge.
- **Do not enable RKSV special receipts or Austrian TSE for non-AT tenants.**
- **Do not implement ViDA submission, Peppol transport, or Swiss bank submission in this phase.** EU support is an EN 16931 builder plus a read-only readiness report.
- Do not add a `CountryCode` column or any second country field; `company_settings.country` is the binding.
- Do not treat `EU_DEFAULT` as a selectable country in the create-tenant wizard.
- Do not claim Germany, Switzerland, or EU e-invoicing is production-ready.
- Do not mix Austrian `Tse:` settings with planned `KassenSicherheit` settings.
- Do not invent a parallel feature-flag table or store flags on `company_settings`.
- Do not disable Austrian TSE through a country flag experiment.
