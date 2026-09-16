# Countries and fiscal regimes

**Last updated:** 2026-09-16  
**Related:** [`AGENTS.md`](../AGENTS.md) · [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md) (stub) · [`FISCAL_SWITZERLAND.md`](FISCAL_SWITZERLAND.md) (stub) · [`EINVOICING_EU.md`](EINVOICING_EU.md) (stub) · [`FEATURE_FLAGS.md`](FEATURE_FLAGS.md) · [`ENVIRONMENT_CONFIGURATION.md`](ENVIRONMENT_CONFIGURATION.md)

This hub describes the **target** multi-country architecture. It is not a legal opinion and does not certify RKSV, KassenSichV, MWST, EN 16931, or ViDA compliance.

The Austria cash-register path (RKSV / TSE / FinanzOnline) is the production fiscal system today. Germany, Switzerland, and generic EU e-invoicing are **planned**. The `CountryProfile` registry, country-aware strategies, provisioning country step, and country feature-flag names are **not implemented in code yet**. Do not treat this document as proof that those types exist on disk.

---

## Status

| Country / profile | Fiscal status | Code status |
|-------------------|---------------|-------------|
| **AT** | Production: RKSV, TSE, FinanzOnline | Live path. Existing `company_settings.country` defaults to `AT`. Planned `CountryCode` backfill still required when that column ships. |
| **DE** | Planned (KassenSicherheit / e-invoicing) | No code. See [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md) (stub). |
| **CH** | Planned (MWST / QR-Rechnung) | No code. See [`FISCAL_SWITZERLAND.md`](FISCAL_SWITZERLAND.md) (stub). |
| **EU_DEFAULT** | Planned (EN 16931 / ViDA readiness report only) | No code. Registry-only fallback, not a selectable ISO country. See [`EINVOICING_EU.md`](EINVOICING_EU.md) (stub). |

---

## 1. Purpose

Regkasse is Austria-first. Multi-country work must not fork the live payment or TSE pipelines.

The intended flow is:

1. Resolve the mandant’s country from company settings (`CountryCode` when added; today the string column is `country`).
2. Load a **CountryProfile** from an in-code registry (not appsettings).
3. Choose VAT and invoice behavior through **TaxStrategy** / **InvoiceStrategy** plus `VatRegime`.
4. Gate country-specific fiscal and e-invoicing modules with **feature flags** stored in the existing `tenant_settings` table.

Until that layer ships, keep using the Austrian RKSV/TSE rules in [`AGENTS.md`](../AGENTS.md) and the `RKSV_*.md` docs.

---

## 2. CountryProfile

**Target:** `ICountryProfileRegistry` with seeds for `AT`, `DE`, `CH`, and `EU_DEFAULT`.

`EU_DEFAULT` is **registry-only**. It is not an ISO 3166-1 alpha-2 code and must not appear in the Super Admin create-tenant country list.

Planned profile fields (names may adjust at implementation):

| Field | Role |
|-------|------|
| Code | ISO 3166-1 alpha-2 for AT/DE/CH; sentinel `EU_DEFAULT` for the fallback profile |
| Default locale / currency / timezone | Formatting and company-settings copies |
| Fiscal system | Which cash-register fiscal module applies (RKSV vs planned DE/CH vs none) |
| E-invoicing standard(s) | Which invoice builders may run |
| VAT-ID pattern and normalization | Validator source of truth |
| Allowed `VatRegime` values | Reject incompatible regimes at provision time |

**Binding:** when `CompanySettings.CountryCode` exists, it is the tenant’s country. Missing or legacy rows must be treated as **AT** after backfill (see [Migration path](#9-migration-path)). Do not invent a second country field on `Tenant`.

---

## 3. TaxStrategy and InvoiceStrategy

**Target:** `ITaxStrategy` and `IInvoiceStrategy` selected from CountryProfile plus `VatRegime`.

Austria must keep today’s receipt and tax **output**. Wrap or select at the edges. Do not rewrite `PaymentService` internals, `TseService`, `RksvSpecialReceiptService`, or `CartMoneyHelper` as a parallel AT engine.

Non-AT strategies must not enable RKSV special receipts or Austrian TSE signing.

---

## 4. Feature flags

**Today (implemented):** `IFeatureFlagService` persists overrides in **`tenant_settings`** (`FeatureFlags:{Name}`, `tenant_id` null = global). Defaults come from appsettings `FeatureFlags`. Canonical names today: `EnableNewPaymentFlow`, `EnableDepExportV2`, `EnableOnlineOrdersV2`, `EnableAutoAusfall`. See [`FEATURE_FLAGS.md`](FEATURE_FLAGS.md). Flags do **not** live on `company_settings`.

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

**Planned resolution for country flags:** tenant override in `tenant_settings` → CountryProfile default. Do **not** use appsettings `false` as the AT RKSV default (that would disable production TSE). Existing experimental flags keep: tenant override → global override → appsettings.

`Fiscal.RksvAt` locked-true for AT is **documented here only** until code exists.

---

## 5. Tenant provisioning

**Today:** Super Admin `CreateTenantWizard` is a **single-step** form (name, slug, contact, trial, demo). There is no country step and no `GET /api/admin/countries`.

**Planned:**

1. Country + `VatRegime` (list from `GET /api/admin/countries`; `EU_DEFAULT` omitted).
2. Existing tenant details form.
3. Processing and success (unchanged UX after submit).

`POST /api/admin/tenants` must require ISO 3166-1 alpha-2 `countryCode` and a compatible `vatRegime`. Unknown codes → HTTP 400. Non-AT tenants must **not** provision Austrian TSE or AT tax-group seeds.

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

VAT-ID regexes and normalization belong **only** in CountryProfile seeds (and a validator that reads the registry). Austria’s current live pattern remains the AT seed. Do not copy country regexes into controllers or FA form constants as a second source of truth.

---

## 8. Configuration

| Store | What belongs there | What does not |
|-------|--------------------|----------------|
| **CountryProfile seeds (code)** | Locale, currency, timezone, fiscal system, e-invoicing, VAT-ID pattern, allowed regimes, default flag derivation | Secrets, per-tenant overrides, Production TSE vendor keys |
| **`company_settings`** | Per-mandant `CountryCode` / `VatRegime` / copied locale and currency **when those columns exist**; today’s `country`, `Currency`, `Language` | Feature-flag KV pairs |
| **`tenant_settings`** | Feature-flag overrides (`FeatureFlags:{Name}`) via `IFeatureFlagService` | Country profile definitions |
| **appsettings** | Existing `FeatureFlags` experimental defaults; planned sections `KassenSicherheit`, `QrRechnung`, `En16931`, `Vies` when those modules ship | CountryProfile seed data; `Fiscal.RksvAt` default for AT |

CountryProfile is **code-seeded**, not appsettings-driven. Changing a country’s VAT-ID pattern or default fiscal system is a code/review change, not an ops JSON edit.

`KassenSicherheit` must stay a **separate** configuration root from Austrian `Tse:`. Fake or simulated DE providers must fail closed outside Development, matching the existing TSE production lock idea. Do not name a required vendor in this hub.

`Vies:CheckEnabled` (or the `Vies.CheckEnabled` flag) defaults **off**. Tests must not call the live VIES network.

Details: [`ENVIRONMENT_CONFIGURATION.md`](ENVIRONMENT_CONFIGURATION.md).

---

## 9. Migration path

When `CountryCode` / `VatRegime` columns ship:

1. **Schema first** (additive). Do not rewrite existing `country`.
2. **Separate backfill:** every existing mandant with Austrian fiscal data or `country = AT` (including null/empty treated as AT) gets `CountryCode = AT` and `VatRegime = AT_RKSV_STANDARD` (name as implemented).
3. **Success:** RKSV/TSE/FinanzOnline behavior unchanged; provisioning of **new** AT tenants matches today’s result plus the new columns.
4. **If backfill fails:** fail closed. Do not guess DE/CH/EU. Do not leave mixed null `CountryCode` in Production. Retry the backfill job; keep the AT payment path on the legacy `country` / tax-number behavior until the backfill reports complete.

Do not backfill `EU_DEFAULT` onto real mandants.

---

## 10. Rollback

If a country-layer release harms Production:

1. Keep **`Fiscal.RksvAt` on** for AT (or ship without that flag until the lock exists). Never use a global flag to disable Austrian TSE.
2. Turn off DE/CH/EU flags (`Fiscal.KassenSicherheitDe`, `Fiscal.MwstCh`, `EInvoicing.*`, `Vies.CheckEnabled`) via `tenant_settings` / Admin feature flags. Those modules must no-op when off.
3. Roll back the application deploy if needed. **Do not drop** `CountryCode` as the first rollback step; additive columns can stay unused.
4. Austrian `Tse:` / `PaymentService` / special receipts must run **without** loading DE/CH/EU builders.

---

## 11. Testing strategy

| Layer | Intent |
|-------|--------|
| Unit (profiles) | Registry returns AT/DE/CH; `EU_DEFAULT` exists but is not selectable; unknown ISO code rejected |
| Unit (VAT-ID) | AT/DE/CH valid and invalid cases from seeds; VIES client mocked only |
| Regression (AT) | Provisioning and payment/TSE/signature-chain parity with pre-country behavior |
| E2E (wizard) | Country step + AT create; DE defaults; unknown country 400 — **when the wizard exists** |
| CI | No live VIES, no real DE TSE, no Swiss bank APIs |

Until the code exists, do not add empty tests that assert types that are not in the repository.

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

- Hardcode VAT-ID regexes (`ATU…`, `DE…`, `CHE-…`) in controllers, FA form rules, or POS validators as a second source of truth. Seeds (and a registry-backed validator) own the pattern.
- Hardcode VAT rates (for example Austrian 20/10/13 or other country rates) in API controllers or Admin forms. Rates belong with tax types / CountryProfile / `VatRegime`.
- Fork `PaymentService` (or clone cart money logic) per country. Select a strategy at the edge.
- Enable RKSV special receipts or Austrian TSE for non-AT tenants.
- Treat `EU_DEFAULT` as a selectable country in the create-tenant wizard.
- Claim Germany, Switzerland, or EU e-invoicing is production-ready.
- Mix Austrian `Tse:` settings with planned `KassenSicherheit` settings.
- Implement ViDA submission, Peppol transport, or Swiss bank submission from this hub.
- Invent a parallel feature-flag table or store flags on `company_settings`.
- Disable Austrian TSE through a country flag experiment.
