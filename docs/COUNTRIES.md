# Countries and fiscal regimes

**Last updated:** 2026-09-29  
**Related:** [`AGENTS.md`](../AGENTS.md) · [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md) · [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md) (stub) · [`FISCAL_SWITZERLAND.md`](FISCAL_SWITZERLAND.md) (stub) · [`EINVOICING_EU.md`](EINVOICING_EU.md) (stub) · [`FEATURE_FLAGS.md`](FEATURE_FLAGS.md) · [`ENVIRONMENT_CONFIGURATION.md`](ENVIRONMENT_CONFIGURATION.md)

This hub describes the multi-country architecture. It is not a legal opinion and does not certify RKSV, KassenSichV, MWST, EN 16931, or ViDA compliance.

## Current state (as of HEAD)

Austria remains the production fiscal path: `AustriaTaxStrategy` / `AustriaInvoiceStrategy` are adapters and keep AT receipt/tax output. Paket 30-c wires DE/CH/EU tax and invoice strategies into `PaymentService` and `InvoiceService`; `TseService` tax-set projection and `RksvSpecialReceiptService` stay Austria-only (`NotSupportedException`). Offline BelegNr allocation goes through `IInvoiceStrategy` (Paket 30-b). AT + `EU_REVERSE_CHARGE` routes to the EU_DEFAULT strategies (Paket 12-c). The Super Admin create-tenant wizard is two-step (country → form) and consumes `GET /api/admin/countries`. Super Admin tenant detail shows a Country & Fiscal Regime card (`PATCH /api/admin/tenants/{id}/country`, Paket 14). Country change after signed fiscal data is allowed only when no signed document was issued under a different fiscal system; otherwise `AdminTenantService.UpdateCountryAsync` returns `FISCAL_COUNTRY_CHANGE_INVALID`. Historical `invoices` / `receipts` / `payment_details` keep `CountryCodeAtIssue` / `VatRegimeAtIssue` and are not rewritten (Paket 16). Shape (AT fallback via `GetOrDefault`, VAT-ID seeds in `VatIdPatterns`) is separate from what is implemented (DE SIGN HTTP in `FiskalyDeKassenSicherheitService`, CH PDF in `QrRechnungPdf`, EU validator in `En16931Schematron`) and from what stays gated (`FeatureFlagService` / `CountryFiscalLockEvaluator`). None of DE, CH, or EU is a production fiscal path. This is not a claim of KassenSichV, MWST, or EN 16931 compliance. Production apply order: [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md). Remaining work: [§16](#16-remaining-gaps).

### Shipped since Paket 19

| Paket | What landed |
|-------|-------------|
| **12-c** | AT + `EU_REVERSE_CHARGE` → `EuDefaultTaxStrategy` / `EuDefaultInvoiceStrategy`. `EInvoicing.En16931` does not gate reverse-charge tax or disclosures. |
| **30-b** | `OfflineOrderService` allocates BelegNr via `IInvoiceStrategy.AllocateReceiptNumberAsync` (Austria `FormatBelegNr`). |
| **14** | FA tenant-detail Country & Fiscal Regime card (Super Admin edit; Mandanten-Admin view-only). |
| **16** | Issue-time `CountryCodeAtIssue` / `VatRegimeAtIssue`; historical rows not rewritten; `COUNTRY_LOCKED_FISCAL` lifted. |
| **18** | [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md) |
| **30-d** | OSS destination STANDARD rates (`IOssVatRateRegistry`): 14 countries, FI 25.5, Greek VAT-ID prefix `EL` → `GR`. No AT `TaxTypes` fallback. AT + `EU_OSS` stays unsupported. |

---

## Status

| Country | Fiscal System | Status | Notes |
|---------|---------------|--------|-------|
| **AT** | RKSV / TSE / FinanzOnline | **Production (live SoT)**; adapter called | Current behavior unchanged; `AustriaTaxStrategy` / `AustriaInvoiceStrategy` delegate to the existing services and are pinned by the baseline regression suite in [§11](#11-testing-strategy) |
| **DE** | KassenSicherheit | **Partial** | Contract, Development `SoftKassenSicherheitService`, and fiskaly-de HTTP. `PaymentService` calls `IFiscalSignatureRouter`. Not a live TSE. See [§16](#16-remaining-gaps) and [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md) |
| **CH** | MWST + QR-Rechnung | **Partial** | MWST calculation and QR payload/PDF exist. Bank submit stays off. See [§16](#16-remaining-gaps) and [`FISCAL_SWITZERLAND.md`](FISCAL_SWITZERLAND.md) |
| **EU_DEFAULT** | EN 16931 | Registry-only, **not tenant-selectable**; **Partial** | UBL sketch plus embedded Schematron. Peppol send is not the default path. See [§16](#16-remaining-gaps) and [`EINVOICING_EU.md`](EINVOICING_EU.md) |

`EU_DEFAULT` is a fallback profile identifier, not an ISO 3166-1 alpha-2 code. It must never appear in the Super Admin create-tenant country list.

---

## 1. Purpose

Regkasse is Austria-first. Multi-country work must not fork the live payment or TSE pipelines.

The intended flow is:

1. Resolve the mandant's country from `CompanySettings.Country` (column `country`).
2. Load a **CountryProfile** from an in-code registry (not appsettings).
3. Choose VAT and invoice behavior through **TaxStrategy** / **InvoiceStrategy** plus `CompanySettings.VatRegime`.
4. Gate country-specific fiscal and e-invoicing modules with **feature flags** stored in the existing `tenant_settings` table.

Steps 1–4 exist in code. Paket 30-c wires tax/invoice strategies into `PaymentService` and `InvoiceService`. `TseService` tax-set projection and `RksvSpecialReceiptService` stay Austria-only (`NotSupportedException` for DE/CH/EU). The Austrian RKSV/TSE rules in [`AGENTS.md`](../AGENTS.md) and the `RKSV_*.md` docs remain the operative description of live AT behavior.

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

Regime is independent from country: an Austrian mandant may legitimately invoice under `EU_REVERSE_CHARGE`. Since Paket 12-c both resolvers route that regime to `EuDefaultTaxStrategy` / `EuDefaultInvoiceStrategy` regardless of country code. AT + `EU_OSS` is still unsupported (`ArgumentException`: `AT tenant + EU_OSS is not supported`).

### 2.4 The registry (shipped)

`ICountryProfileRegistry` / `CountryProfileRegistry` seeds `AT`, `DE`, `CH`, and `EU_DEFAULT` in code and is registered as a singleton. Production callers: strategy resolvers (`ITaxStrategyResolver` / `IInvoiceStrategyResolver` via `CountryStrategyContext` in `PaymentService`, `InvoiceService`, `TseService`, `RksvSpecialReceiptService`), `IFeatureFlagService` country defaults, Super Admin `GET /api/admin/countries` plus the create-tenant wizard, and `IVatIdValidator` (profile regex, including the EU reverse-charge buyer path). `GetOrDefault` still maps unknown/legacy codes to AT for reads; `Get` still rejects unknown operator input with `UNKNOWN_COUNTRY_CODE`.

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

**Seed verification status:** the AT seed is authoritative because it mirrors values already live in production (`EUR`, `de-DE`, `Europe/Vienna`, and the UID pattern `^ATU\d{8}$`), and regression tests pin it. Paket 13 checked every seeded **profile field** against official sources ([§14](#14-seed-sources)); Paket 13-b added `// Source:` comments on the seeds (minimum 24). VAT rates are **not** on the profile — they live on `ICountryTaxTypeRegistry` (Paket 13-c). DE/CH `CalculateTax` uses that registry (Paket 30-c); AT stays on live `TaxTypes`. DE/CH/EU modules remain shape-only and are gated by feature flags. DE TSE, CH bank submit, and Peppol are [§16](#16-remaining-gaps).

---

## 3. TaxStrategy and InvoiceStrategy

**Wired into domain (Paket 30-c); AT path byte-identical; DE/CH/EU shape only.** `ITaxStrategy` and `IInvoiceStrategy` live in `backend/Services/Countries/Strategies/`, are resolved from CountryProfile plus `VatRegime`, and are registered in DI. Call sites: `PaymentService` and `InvoiceService` (tax/invoice); `TseService` tax-set projection and `RksvSpecialReceiptService` remain Austria-only. Austria must keep today's receipt and tax **output**, byte for byte where it is reproducible at all (§11), so the Austrian classes are **adapters**: they contain no arithmetic, no rounding rule, and no bucket rule of their own.

### 3.1 Members and what Austria delegates to

| Member | Austrian delegate |
|--------|-------------------|
| `ITaxStrategy.CalculateTax` | `CartMoneyHelper.ComputeLine` + `BuildTaxSummaryFromLines` + `BuildReceiptTotalsAndBreakdown` |
| `ITaxStrategy.ProjectFiscalTaxSets` | `RksvTaxSetMapper.MapFromTaxDetailsJson` (RKSV `Betrag-Satz-*` buckets) |
| `ITaxStrategy.ValidateVatId` | `CountryProfile.MatchesVatIdShape` — the strategy holds **no** regex of its own |
| `ITaxStrategy.DetermineInvoiceFields` | Read-only projection of `CompanySettings` / `Customer`; introduces no new rule |
| `IInvoiceStrategy.AllocateReceiptNumberAsync` | `ISequenceReservationService.ReserveNextReceiptNumberAsync` (Belegnummer, gap-free per register and UTC day) |
| `IInvoiceStrategy.BuildInvoiceDocumentAsync` | `IReceiptService.GenerateReceiptAsync`; the `ReceiptDTO` is returned unchanged |
| `IInvoiceStrategy.GetMandatoryDisclosures` | Constant metadata list (key + legal basis + source field); values come from the document, not from this call |

Deliberately **out** of `IInvoiceStrategy`: TSE signing input, RKSV §9 machine code, and the QR payload. Those belong to the signature pipeline (`BelegdatenPayloadBuilder`), not to document layout. The billing invoice sequence is a different counter and is also out of scope.

**Numbering scope (decided).** Austria has two Belegnummer allocators, and the strategy wraps only one of them:

| Path | Allocator | In `IInvoiceStrategy`? |
|------|-----------|------------------------|
| Online payment, storno, refund, all Sonderbelege | `IReceiptSequenceService.AllocateNextBelegNrInTransactionAsync` (bound to the caller's `IDbContextTransaction`) | **No** — stays outside the country layer so no EF transaction leaks into a country-neutral contract |
| Offline order replay | `ISequenceReservationService.ReserveNextReceiptNumberAsync` | Yes — Paket 30-b: `OfflineOrderService` calls `IInvoiceStrategy.AllocateReceiptNumberAsync`; Austria delegates to this service (`FormatBelegNr`). DE uses `de_receipt_sequences`. CH uses `ch_receipt_sequences`. EU still throws `NotImplementedException`. |

Do not "fix" this by adding a transaction parameter to the interface without a separate decision.

`ProjectFiscalTaxSets` is separate from `CalculateTax` on purpose: it projects a persisted `payment_details.tax_details` payload, and the mapper derives each bucket's gross **from the VAT amount** (`tax × (100 + rate) / rate`) rather than reusing the line gross. A hand-written "sum the line gross" implementation drifts by cents — for 2 × 2,50 at 20 % the mapper yields `4.98`, not `5.00`. Delegate; never restate.

### 3.2 Resolution and failure

`ITaxStrategyResolver` / `IInvoiceStrategyResolver` take a `CountryProfile` and a `VatRegime`. They fail closed with `UnknownTaxRegimeException` (error code `UNKNOWN_TAX_REGIME`) when the profile does not allow the regime, or when no strategy is registered for the country. There is **no** Austrian fallback: a wrong pair must never run RKSV for a non-AT mandant.

Resolution order (Paket 12-c): (1) profile must `Supports` the regime; (2) AT + `EU_OSS` throws `ArgumentException` (`AT tenant + EU_OSS is not supported`) — destination STANDARD rates exist (Paket 30-d) but an Austrian tenant still must not use OSS; (3) `EU_REVERSE_CHARGE` routes to the `EU_DEFAULT` strategies regardless of country code (VAT regime, not fiscal system); (4) otherwise look up by profile country code.

`EInvoicing.En16931` does **not** gate reverse-charge `CalculateTax` or reverse-charge `GetMandatoryDisclosures`. Those run when the flag is off so an AT tenant can issue B2B intra-EU reverse-charge invoices. The flag still gates `EU_OSS` / `NON_EU` tax, `BuildInvoiceDocumentAsync`, `ValidateVatId`, `DetermineInvoiceFields`, and the EN 16931 / XRechnung / ZUGFeRD XML builders.

Lifetimes: tax strategies and their resolver are singletons (stateless delegators); invoice strategies and their resolver are **scoped**, because the Austrian one depends on scoped `ISequenceReservationService` and `IReceiptService`.

Non-AT `CalculateTax` / disclosures / `InvoiceDocumentDto` shape are implemented. DE and CH `ProjectFiscalTaxSets` and `AllocateReceiptNumberAsync` are implemented from their own catalogs and sequence tables. EU still throws `NotImplementedException`. TSE tax-sets and RKSV special receipts throw `NotSupportedException` for non-AT countries, so they cannot silently enable Austrian TSE signing.

Country gates live on `FeatureFlagNames`. Defaults below are the country-profile result when no tenant or global override is stored. `EInvoicing.Peppol` is declared only (`Reserved`); it is not resolved and stays off.

| Flag | AT | DE | CH | EU_DEFAULT | Can be disabled |
|------|----|----|----|------------|-----------------|
| `Fiscal.RksvAt` | On, **locked** | Off | Off | Off | No for AT. Other countries are already off; the lock applies only when the profile fiscal system is `RKSV_AT`. |
| `Fiscal.KassenSicherheitDe` | Off | Off | Off | Off | Yes (tenant override). Profile default is off even for DE. |
| `Fiscal.MwstCh` | Off | Off | On | Off | Yes (tenant override) |
| `EInvoicing.Zugferd` | Off | Off | Off | Off | Yes. Not country-derived; a tenant override may turn it on. |
| `EInvoicing.XRechnung` | Off | Off | Off | Off | Yes. Not country-derived; a tenant override may turn it on. |
| `EInvoicing.QrRechnung` | Off | Off | On | Off | Yes (tenant override) |
| `EInvoicing.En16931` | Off | Off | Off | On | Yes (tenant override). Does not gate reverse-charge tax or disclosures. |
| `Vies.CheckEnabled` | Off | Off | Off | Off | Yes. Not country-derived. |
| `EInvoicing.Peppol` | Off | Off | Off | Off | Not wired. Stays off until a later phase adds it to resolution. |

---

## 4. Feature flags

**Today (implemented and verified):** `IFeatureFlagService` persists overrides in **`tenant_settings`** under key `FeatureFlags:{Name}`, where `tenant_id = null` means a global override. Config defaults come from the appsettings `FeatureFlags` section via `FeatureFlagsOptions`. Canonical names: `EnableNewPaymentFlow`, `EnableDepExportV2`, `EnableOnlineOrdersV2`, `EnableAutoAusfall`, plus the country/fiscal names below. Management API: `/api/admin/feature-flags` (Super Admin). See [`FEATURE_FLAGS.md`](FEATURE_FLAGS.md).

Flags do **not** live on `company_settings`. Country is read from `CompanySettings.Country` and resolved through `ICountryProfileRegistry`.

**Experimental flags** (unchanged): tenant override → global override → appsettings. Country profile is never consulted.

**Country / fiscal flags** (`FeatureFlagNames`): AT `Fiscal.RksvAt` lock → tenant override → country profile default → global override → `FeatureFlagsOptions` → `false`.

| Flag | Default |
|------|---------|
| `Fiscal.RksvAt` | On for AT; **locked on** (cannot be turned off). No `FeatureFlagsOptions` property — never an appsettings `false` default. |
| `Fiscal.KassenSicherheitDe` | Off for every profile, including DE. A pilot turns it on with a tenant override. |
| `Fiscal.MwstCh` | On for CH profile (`FiscalSystem.MWST_CH`); off otherwise. Tenant override allowed. |
| `EInvoicing.Zugferd` / `EInvoicing.XRechnung` | **Always default off** (DE builders are skeletons). Tenant override to `true` is allowed. |
| `EInvoicing.QrRechnung` | On when the profile lists `QR_RECHNUNG` (CH). |
| `EInvoicing.En16931` | On when the profile lists `EN_16931` (`EU_DEFAULT`). |
| `Vies.CheckEnabled` | Always default **off** (not derived from country). Tenant override allowed. |

`GetStatusesAsync` `Source` values: `config`, `global_override`, `tenant_override`, `country_profile`, `locked`.

---

## 5. Tenant provisioning

**Today:** Super Admin `CreateTenantWizard` is **two-step**: country (`phase === 'country'`, `CreateTenantCountryStep`) then tenant form (`phase === 'form'`). The country step consumes `GET /api/admin/countries` (`useCountries`): tenant-selectable profiles only (AT, DE, CH; `EU_DEFAULT` omitted). `vatRegime` options are the selected profile’s `allowedVatRegimes`. A non-AT banner states that RKSV/TSE is not enabled. Defaults remain AT / `AT_RKSV_STANDARD` until the operator changes them. After submit, processing and success UX is unchanged.

Existing mandants that never went through this step still have the column defaults from §2.1: `country = 'AT'`, `vat_regime = 'AT_RKSV_STANDARD'`, `tax_exempt = false`, `billing_country = null`.

`POST /api/admin/tenants` must require an ISO 3166-1 alpha-2 country and a compatible `vatRegime`. Unknown codes → HTTP 400. Non-AT tenants must **not** provision Austrian TSE or AT tax-group seeds.

---

## 6. AT / DE / CH / EU matrix

| | AT | DE | CH | EU_DEFAULT |
|--|----|----|----|------------|
| Selectable at create | Yes | Yes | Yes | **No** (fallback only) |
| Fiscal system | RKSV + TSE (production) | Planned KassenSicherheit | Planned MWST | None (e-invoicing default) |
| VAT label / rates | Live AT `TaxTypes` | CountryTaxType 19/7 in `CalculateTax` (shape) | CountryTaxType 8.1 / 2.6 / 3.8 in `CalculateTax` (shape) | Reverse charge 0% (Paket 12-c); OSS destination STANDARD rates (Paket 30-d). AT + `EU_OSS` unsupported |
| VAT-ID | AT UID in CountryProfile seed | DE UID seed `^DE\d{9}$` | CHE-… seed | `EU_DEFAULT` regex; used by reverse-charge buyer path |
| E-invoicing | Not this hub | Stub: [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md) | Stub: [`FISCAL_SWITZERLAND.md`](FISCAL_SWITZERLAND.md) | Stub: [`EINVOICING_EU.md`](EINVOICING_EU.md) |
| Default flags | `Fiscal.RksvAt` on (lock) | DE fiscal/e-invoicing flags; `Fiscal.RksvAt` off | CH flags; `Fiscal.RksvAt` off | `EInvoicing.En16931`; ViDA report only |

EU_DEFAULT supports an EN 16931-oriented invoice builder and a **read-only** ViDA readiness report. It does not submit to a tax authority or Peppol network.

---

## 7. Formatting and VAT-ID

Admin UI **language** catalogs stay `de` / `en` / `tr`. Do not add `de-DE` / `de-CH` catalogs unless copy truly diverges. Tenant **formatting** (date, currency, separators) follows `CompanySettings.Country` via FA `useCountryFormatting` / `getCountryFormatProfile`. Unknown country → AT.

VAT-ID regexes belong **only** in CountryProfile seeds. Do not copy country regexes into controllers or FA form constants as a second source of truth.

### 7.1 One pattern, one literal (shipped)

`Models/Countries/VatIdPatterns` holds the per-country constants; the CountryProfile seeds and the two `[RegularExpression]` attributes (`PaymentDetails.Steuernummer`, `CreatePaymentRequest.Steuernummer`) reference the same constants. Attributes need a compile-time constant and therefore cannot read the registry — that is the reason the constants exist, not an invitation to add more literals. `VatIdPatternConsolidationTests` fails the build if a production file declares its own `^ATU\d{8}$` again.

Runtime consumers of the shared Austrian matcher: `PaymentService` (payment gate), `TenantSettingsService` (fiscal settings change), `FiskalyTseService`, `FiskalySetupService`, `FiskalyConnectionProbe`, `InvoiceController`.

**Matching is strict — normalization is the call site's job.** `VatIdPatterns.IsAustrianUid`, `VatIdPatterns.AustriaRegex`, `CountryProfile.MatchesVatIdShape`, and `ITaxStrategy.ValidateVatId` all match the value **as given**: no trimming, no case folding. Call sites that accept user-typed input keep their own `Trim().ToUpperInvariant()` (fiskaly and tenant-settings already do); the payment gate deliberately does not, because it never did.

**One behavior change shipped with the consolidation:** `InvoiceController` previously accepted any `CompanyTaxNumber` that started with `ATU` and was 11 characters long, so values like `ATU1234567X` passed. It now uses the shared pattern and returns HTTP 400 for them. This tightens invoice creation only; the payment and TSE paths were already strict and are unchanged.

The other validator that exists today is deliberately narrow: `billing_country` is checked for the two-letter ISO 3166-1 alpha-2 **shape** and upper-cased on write. It does not assert the code is an assigned country, and it does not validate VAT-ID.

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

Migration `20260921180000_AddFiscalDocumentCountryAtIssueSnapshots` is additive only: nullable `country_code_at_issue` and `vat_regime_at_issue` on `invoices`, `receipts`, and `payment_details`. **No backfill** — legacy rows stay null. Country change must not `UPDATE` these columns.

RKSV / TSE / FinanzOnline behavior is unchanged, and that claim is enforced by the regression suite in [§11](#11-testing-strategy) rather than asserted here.

### 9.2 Remaining

1. **Schema first, always additive.** Do not rewrite `country`. Do not drop or repurpose existing columns.
2. **Backfill in a separate step** from schema introduction, as above.
3. **Success criterion:** provisioning a new AT tenant produces today's result plus the new columns, and the AT baseline fixtures still match.
4. **If a backfill fails: fail closed.** Do not guess DE/CH/EU. Do not leave rows in a mixed or unknown regime in Production. The API must not serve fiscal operations for a mandant whose regime cannot be resolved — refuse rather than fall back to a guessed regime. Retry the backfill, and keep the AT payment path on the existing `country` / tax-number behavior until it reports complete.

Do not backfill `EU_DEFAULT` onto real mandants.

Production migration order, backfill SQL, and rollback: [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md).

---

## 10. Rollback

Rollback is per country and flag-driven, not schema-driven.

1. **AT rollback is a no-op.** Austria is the unchanged production path. Keep `Fiscal.RksvAt` on (or ship without that flag until the lock exists). Never use a global flag to disable Austrian TSE.
2. **DE rollback:** turn off `Fiscal.KassenSicherheitDe`, `EInvoicing.Zugferd`, `EInvoicing.XRechnung` in `tenant_settings`. Those modules must no-op when off.
3. **CH rollback:** turn off `Fiscal.MwstCh` and `EInvoicing.QrRechnung`.
4. **EU rollback:** turn off `EInvoicing.En16931` and `Vies.CheckEnabled`.
5. **Application rollback** if flags are not enough. **Do not drop** the country/billing columns as a first rollback step; additive columns can stay unused and dropping them destroys tenant configuration.
6. Austrian `Tse:` / `PaymentService` / special receipts must run **without** loading DE/CH/EU builders.

Ops sequence (app package vs schema, flag rollback, smoke): [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md).

---

## 11. Testing strategy

| Layer | Intent | State |
|-------|--------|-------|
| Unit — schema binding | New columns are additive; `country` not duplicated; `VatId` stays an unmapped alias | **Shipped** |
| Unit — migration | Every added column carries a default; backfill present; no altered column or index | **Shipped** |
| Unit — validators | `billing_country` shape and normalization | **Shipped** |
| Regression — AT fiscal chain | Austrian output unchanged by the country layer | **Shipped** |
| Unit — CountryProfile registry | Registry returns AT/DE/CH; `EU_DEFAULT` exists but is not selectable; unknown ISO code rejected; AT seed mirrors live defaults; no VAT rates on a profile | **Shipped** |
| Unit — CountryProfile seed sources | Pin every seed field; `// Source:` comment count ≥ 24 | **Shipped** (Paket 13 / 13-b) |
| Unit — Country tax type seeds | AT 20/10/13/0/4.9 pin live `TaxTypes`; DE 19/7; CH 8.1/2.6/3.8; unknown/`EU_DEFAULT` → empty (no AT fallback) | **Shipped** (Paket 13-c). DE/CH `CalculateTax` uses the registry (Paket 30-c); AT stays on live `TaxTypes`. |
| Unit — VAT-ID shape from seeds | AT/DE/CH valid and invalid cases resolved from `VatIdPattern` | **Shipped** |
| Unit — strategy resolution | AT + `AT_RKSV_STANDARD` → Austrian strategy; DE + `DE_USTG_STANDARD` → German; regime not allowed by the profile → `UNKNOWN_TAX_REGIME`; unregistered country → throws; DE/CH/EU skeletons throw and name their doc | **Shipped** |
| Unit — AT delegation | `CalculateTax` equals `CartMoneyHelper` output (decimal and serialized); `ProjectFiscalTaxSets` equals `RksvTaxSetMapper`; numbering and document calls land on the existing services; `tax_exempt` changes nothing | **Shipped** |
| Unit — VAT-ID pattern consolidation | One literal shared by seeds and attributes; strict semantics pinned; a file-scan test rejects a re-introduced local regex | **Shipped** |
| Unit — VIES lookup | Live registration check, VIES client mocked only | Planned |
| RTL — `CreateTenantWizard` country step | Country then form; AT/DE defaults; `EU_DEFAULT` omitted | **Shipped** (`CreateTenantWizard.countryStep.test.tsx`). Playwright E2E still open. |
| Unit — AT reverse-charge routing | AT + `EU_REVERSE_CHARGE` → EuDefault; `En16931` does not gate reverse-charge tax | **Shipped** (Paket 12-c) |
| Unit — offline BelegNr | `OfflineOrderService` → `IInvoiceStrategy.AllocateReceiptNumberAsync` | **Shipped** (Paket 30-b) |
| RTL — FA tenant country card | Super Admin edit + historical-preserved warning; Mandanten-Admin view-only | **Shipped** (Paket 14 / 16) |
| Unit — historical invoice preservation | AT invoice → country change to DE → new DE invoice; both keep original `CountryCodeAtIssue`; no rewrite of historical rows | **Shipped** (Paket 16) |

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
| [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md) | Production country-layer cutover (migrations, flags, FA/POS, blackout windows) |
| [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md) | **Austria only** — Soft TSE → production fiscal (do not reuse for DE/CH) |
| [`PRODUCTION_DEPLOYMENT_RUNBOOK.md`](PRODUCTION_DEPLOYMENT_RUNBOOK.md) | Linux API host deploy (`deploy-production.sh`) |

---

## 13. Do not

- **Do not hardcode VAT rates** (Austrian 20/10/13 or any other country's) in API controllers, Admin forms, or POS validators. Rates belong with tax types / CountryProfile / `VatRegime`.
- **Do not hardcode VAT-ID regexes** (`ATU…`, `DE…`, `CHE-…`) outside CountryProfile seeds. Seeds and a registry-backed validator own the pattern.
- **Do not hardcode country codes** outside seeds and the `company_settings.country` binding. No host-derived, JWT-derived, or constant-list country resolution.
- **Do not fork `PaymentService` or `TseService` internals** per country, and do not clone cart money logic. Select a strategy at the edge.
- **Do not enable RKSV special receipts or Austrian TSE for non-AT tenants.**
- **Do not implement ViDA submission, Peppol transport, or Swiss bank submission in this phase** (Pakets 22 / 21). EU support today is reverse-charge tax shape plus a read-only ViDA readiness report.
- Do not add a `CountryCode` column or any second country field on `company_settings`; `company_settings.country` is the binding. Issue-time snapshots live on invoices/receipts/payment_details as `CountryCodeAtIssue`, not on company settings.
- Do not treat `EU_DEFAULT` as a selectable country in the create-tenant wizard.
- Do not claim Germany, Switzerland, or EU e-invoicing is production-ready.
- Do not mix Austrian `Tse:` settings with planned `KassenSicherheit` settings.
- Do not invent a parallel feature-flag table or store flags on `company_settings`.
- Do not disable Austrian TSE through a country flag experiment.

---

## 14. Seed Sources

**Verified:** 2026-09-18 (Paket 13). **Citations on seeds:** 2026-09-18 (Paket 13-b). Production seed **values** were not changed.

This is an operational check of what the in-code registry currently stores. It is not a legal opinion and does not certify RKSV, KassenSichV, MWST, EN 16931, or ViDA compliance. CountryProfile **does not carry VAT rates**; rate rows below stay N/A on the profile. Country-scoped rates live in `ICountryTaxTypeRegistry` (Paket 13-c). DE/CH strategies use them in `CalculateTax` (Paket 30-c); `PaymentService` / `AustriaTaxStrategy` still use live `TaxTypes`.

`CountryProfileRegistry.cs` carries `// Source:` comments on sourced seed fields (`CountryProfileSourcesTests` requires at least 24). AT `DefaultLocale` stays `de-DE` (Paket 13-b decision A).

### 14.1 Verification table

| Profile | Field | Current value | Official source | Match? | Notes |
|---------|-------|---------------|-----------------|--------|-------|
| AT | Currency | `EUR` | ISO 4217; Austria uses the euro | Yes | Mirrors live `company_settings` default |
| AT | DefaultTimeZone | `Europe/Vienna` | IANA tzdb | Yes | |
| AT | DefaultLocale | `de-DE` | IETF BCP 47: Austrian German is `de-AT` | **DECISION (A)** | Keep production default `de-DE`. BCP-47 `de-AT` is documented on the seed comment; value unchanged |
| AT | FiscalSystem | `RKSV_AT` | RKSV, BGBl. II Nr. 410/2015; FinanzOnline | Yes | Only production fiscal module |
| AT | EInvoicingStandards | `[]` | RKSV Belege are cash-register receipts, not EN 16931 e-invoices | Yes | Empty until an AT e-invoicing builder is wired |
| AT | VatIdPattern | `^ATU\d{8}$` | BMF UID; Austrian UID is `ATU` + 8 digits; FinanzOnline | Yes | Same literal as the live fiscal path (`VatIdPatterns.Austria`) |
| AT | VAT rates | *(not a profile field)* | UStG §10 + live `TaxTypes` (20 / 10 / 13 / 0 / 4.9) | **N/A on profile** | `ICountryTaxTypeRegistry` AT seed (Paket 13-c) |
| DE | Currency | `EUR` | ISO 4217 | Yes | Shape-only until a DE module exists |
| DE | DefaultTimeZone | `Europe/Berlin` | IANA tzdb | Yes | |
| DE | DefaultLocale | `de-DE` | IETF BCP 47 | Yes | |
| DE | FiscalSystem | `KASSENSICHERHEIT_DE` | KassenSichV (Kassensicherungsverordnung) | Yes | Name/intent only; skeleton throws |
| DE | EInvoicingStandards | `ZUGFERD`, `XRECHNUNG` | FeRD ZUGFeRD; KoSIT XRechnung (EN 16931 profiles) | Yes | Declared standards; builders not implemented |
| DE | VatIdPattern | `^DE\d{9}$` | USt-IdNr.: `DE` + 9 digits; EU VIES | Yes | |
| DE | VAT rates | *(not a profile field)* | UStG §12 Abs. 1 (19 %); §12 Abs. 2 (7 %) | **N/A on profile** | `ICountryTaxTypeRegistry` DE seed (Paket 13-c) |
| CH | Currency | `CHF` | ISO 4217 | Yes | |
| CH | DefaultTimeZone | `Europe/Zurich` | IANA tzdb | Yes | |
| CH | DefaultLocale | `de-CH` | IETF BCP 47 | Yes | Default German-speaking CH; `fr-CH` / `it-CH` are not on the profile |
| CH | FiscalSystem | `MWST_CH` | MWSTG; ESTV | Yes | Name/intent only; skeleton throws |
| CH | EInvoicingStandards | `QR_RECHNUNG` | SIX Interbank Clearing QR-bill specification | Yes | Not an EN 16931 profile (enum comment) |
| CH | VatIdPattern | `^CHE-\d{3}\.\d{3}\.\d{3}( (MWST\|TVA\|IVA))?$` | ESTV / Zefix UID (`CHE-xxx.xxx.xxx` + optional MWST/TVA/IVA) | Yes | Suffix optional in the seed; ESTV VAT number usually includes a language suffix |
| CH | VAT rates | *(not a profile field)* | ESTV / MWSTG from 1 Jan 2024: 8.1 % / 2.6 % / lodging 3.8 % | **N/A on profile** | `ICountryTaxTypeRegistry` CH seed (Paket 13-c) |
| EU_DEFAULT | IsTenantSelectable | `false` | Not an ISO 3166-1 alpha-2 country | Yes | Registry-only sentinel |
| EU_DEFAULT | Currency | `EUR` | ISO 4217 (euro area default) | Yes | Never copied onto a tenant |
| EU_DEFAULT | DefaultTimeZone | `UTC` | IANA; no single EU zone | Yes | Never copied onto a tenant |
| EU_DEFAULT | DefaultLocale | `en` | IETF BCP 47 | Yes | Never copied onto a tenant |
| EU_DEFAULT | FiscalSystem | `NONE` | No cash-register fiscalisation at EU level | Yes | |
| EU_DEFAULT | EInvoicingStandards | `EN_16931` | CEN EN 16931-1; ViDA is a timeline, not a builder | Yes | Read-only readiness; no Peppol/ViDA submission |
| EU_DEFAULT | VatIdPattern | `^[A-Z]{2}[A-Z0-9]{8,12}$` | VIES country prefix + 8–12 alphanumeric | **Strict (13-d)** | Registry-only sentinel. Punctuation / lowercase rejected. Used by the EU reverse-charge buyer path via `IVatIdValidator` (Paket 12-b). VIES excludes `EU_DEFAULT` by design (`IsTenantSelectable`). |
| EU_DEFAULT | VAT rates | *(not a profile field)* | No single EU cash-register rate table | **N/A on profile** | `Get("EU_DEFAULT")` returns empty |

### 14.2 Official references (cited, not loaded as law)

| Area | Citation |
|------|----------|
| AT UID | BMF UID (`ATU` + 8 digits); FinanzOnline company/UID master data |
| AT fiscal | Registrierkassensicherheitsverordnung (RKSV), BGBl. II Nr. 410/2015; FinanzOnline submission docs |
| AT VAT rates (not on profile) | Austrian UStG §10 (live tax types, not CountryProfile) |
| DE USt-IdNr | German USt-IdNr. `DE` + 9 digits; EU VIES format list |
| DE VAT rates (not on profile) | UStG §12 Abs. 1 Satz 1 (19 %); §12 Abs. 2 (7 %) |
| DE fiscal | Kassensicherungsverordnung (KassenSichV) |
| DE e-invoicing | FeRD ZUGFeRD; KoSIT XRechnung (EN 16931 CIUS) |
| CH UID | ESTV / Zefix UID `CHE-xxx.xxx.xxx` with optional MWST / TVA / IVA |
| CH VAT rates (not on profile) | MWSTG; ESTV rates from 1 January 2024 (8.1 % / 2.6 % / 3.8 % lodging) |
| CH e-invoicing | SIX Interbank Clearing QR-Rechnung specification |
| EU e-invoicing | CEN EN 16931-1; European Commission ViDA (VAT in the Digital Age) timeline |
| Formatting | ISO 4217 (currency); IANA Time Zone Database; IETF BCP 47 (locale) |

### 14.3 DRIFT and follow-up packages

AT production seed values stay unchanged (`de-DE` kept — decision A).

| ID | Finding | Action |
|----|---------|--------|
| **Paket 13-b** | `// Source:` comments and AT locale decision | **Closed.** Comments on seeds (≥ 24). AT `DefaultLocale` remains `de-DE` (production default; BCP-47 `de-AT` noted on the comment) |
| **Paket 13-c** | VAT rates are not CountryProfile fields | **Closed.** `ICountryTaxTypeRegistry` in-code seeds (AT 20/10/13/0/4.9, DE 19/7, CH 8.1/2.6/3.8). Unknown/`EU_DEFAULT` → empty, no AT fallback. DE/CH calculation uses the registry (Paket 30-c); AT stays on live `TaxTypes`. |
| **Paket 13-c-bis** | Country tax types unused by AT `CalculateTax` / `PaymentService` | **Wired for DE/CH (Paket 30-c); AT stays on TaxTypes.** No AT cutover in this package. |
| **Paket 13-d** | `EU_DEFAULT` VatId regex is a generic placeholder, not a VIES member-state pattern | **Closed.** `VatIdPatterns.EuDefault` is `^[A-Z]{2}[A-Z0-9]{8,12}$`. AT/DE/CH patterns unchanged. EU reverse charge consumes the profile regex via `IVatIdValidator` (Paket 12-b). VIES excludes `EU_DEFAULT` by design. |
| **Paket 13-d-bis** | Tightened EU_DEFAULT regex vs VIES coverage | **Used by EU reverse charge path (Paket 12-b); VIES excludes EU_DEFAULT by design.** Validator stays profile-driven (`MatchesVatIdShape`); do not copy a second literal. |
| **Paket 30-d** | OSS destination-rate table (not AT `TaxTypes` stand-in) | **Done.** In-code `OssVatRates` seed: 14 countries, FI 25.5 (from 2024-09-01), Greek VAT-ID prefix `EL` → `GR`. `EuDefaultTaxStrategy.CalculateOss` uses `IOssVatRateRegistry.GetStandardRate`. No `TaxTypes.GetTaxRate` stand-in. See [`EINVOICING_EU.md`](EINVOICING_EU.md). |

---

## 15. Historical Invoice Preservation

A Super Admin country change (`PATCH /api/admin/tenants/{id}/country`) updates **only** `company_settings.country` / `vat_regime`. It does **not** rewrite historical fiscal documents.

### 15.1 Issue-time snapshots

Migration `20260921180000_AddFiscalDocumentCountryAtIssueSnapshots` adds nullable columns:

| Column | Type | Tables |
|--------|------|--------|
| `country_code_at_issue` | `varchar(2)` | `invoices`, `receipts`, `payment_details` |
| `vat_regime_at_issue` | `varchar(32)` | `invoices`, `receipts`, `payment_details` |

These are **not** a second operating-country field on `company_settings`. `CompanySettings.Country` remains the live binding; the snapshots freeze what was in effect when the document was issued.

Stamping happens at issue time from `CompanySettings` (via `ICountryStrategyContext` / `FiscalDocumentCountryStamp`). Receipts and invoices derived from a payment copy the payment's snapshot so the chain stays consistent. Legacy rows issued before this migration stay `NULL`.

### 15.2 Country change

A country change is **rejected** (`FISCAL_COUNTRY_CHANGE_INVALID`, HTTP 409) when the tenant already has signed fiscal documents issued under a different fiscal system (for example an Austrian TSE-signed receipt when moving to DE). Those rows are not rewritten.

When the change is allowed, historical invoices, receipts, and payments keep `CountryCodeAtIssue` / `VatRegimeAtIssue`. New receipts issued after the change stamp the **new** country. Moving off Austria also forces the tenant `Fiscal.RksvAt` override to `false`.

Audit on a successful change:

- `TENANT_COUNTRY_CHANGED` (`AuditEventType.TenantCountryChanged`) — old/new country and `correlationId`.
- `TENANT_COUNTRY_CHANGED_HISTORICAL_PRESERVED` (`AuditEventType.TenantCountryChangedHistoricalPreserved`) — `newValues.affectedRowCount` is the number of existing invoice + receipt + payment_details rows that were left untouched.

The Admin confirmation modal shows that affected-row count before the save. `COUNTRY_LOCKED_FISCAL` is no longer returned on this path. Do not backfill historical snapshots from the live country after a change.

---

## 16. Remaining gaps

Status below is what the code does today. A decision record or plan is not an implementation. Do not treat CountryProfile seeds, a Development fake signer, or the FA country UI as a production fiscal path for DE, CH, or EU.

Paket 30-d (OSS destination STANDARD rates) stays **Done**: `OssVatRates` / `IOssVatRateRegistry`, 14 countries, FI 25.5, Greek VAT-ID prefix `EL` → `GR`. Only `EuDefaultTaxStrategy.CalculateOss` calls it. AT + `EU_OSS` throws. See [`EINVOICING_EU.md`](EINVOICING_EU.md).

### 16.1 Paket 20, 21, and 22

| Package | Sub-feature | Status | File | Notes | Blocking gate |
|---------|-------------|--------|------|-------|----------------|
| 20 | `SignAsync` outbound HTTP | Implemented | `FiskalyDeKassenSicherheitService` | `Provider=fiskaly-de` calls `IKassenSicherheitHttpClient`. No invented signature. | `KassenSicherheit:Provider=fiskaly-de` plus a `Fiscal.KassenSicherheitDe` tenant override. Profile default is off. |
| 20 | `GetStatusAsync` outbound HTTP | Implemented | `FiskalyDeKassenSicherheitService` | `GET tss/{tenant}`. Ready only when the response state is `INITIALIZED`. | Same as `SignAsync`. |
| 20 | `GetCertificateChainAsync` outbound HTTP | Implemented | `FiskalyDeKassenSicherheitService` | Leaf from `certificate`, then `certificate_chain`. | Same as `SignAsync`. |
| 20 | Provider other than `fiskaly-de` on those three methods | Implemented | `KassenSicherheitNotConfiguredException` | Explicit error. No AT TSE fallback. | `KassenSicherheit:Provider` left at `not-configured`. |
| 20 | Start / finish / DSFinV-K export HTTP | Implemented | `FiskalyDeKassenSicherheitHttpClient` | `not-configured` is a no-op on this path. Any other provider throws `NotImplementedException`. `SignDeAsync` does not call these methods. | Same as `SignAsync`, plus `KassenSicherheit:PilotMode=true` (TEST host and `Environment=TEST` only). |
| 20 | DSFinV-K download | Not started | `ExportDsfinvkAsync` | PUT `/exports/{id}` only. No download method. | Not implemented; PUT-only. |
| 20 | Development signer | Implemented | `SoftKassenSicherheitService` | Registered only when the host is Development. Pseudo-JWS. No TSE HTTP. | `ApplicationHost` `IsDevelopment()` branch. Not registered in Production. |
| 20 | `SignAsync` reached from payment | Implemented | `PaymentService`, `FiscalSignatureRouter.SignDeAsync` | Payment calls the router. The DE branch calls `IKassenSicherheitService.SignAsync`. | `Fiscal.KassenSicherheitDe`. Profile default is off. A tenant override is required. Off → `DE_FLAG_OFF`. Missing TSS/client id → `DE_NOT_CONFIGURED`. |
| 20 | DE signature persistence separate from RKSV JWS | Implemented | `DeTseSignature`, `FiscalSignatureRouter.SignDeAsync` | Table `de_tse_signatures` (migration `20260929114500_AddDeTseSignaturePersistence`). `payment_details.TseSignature` stays null for DE. | Same as payment wiring: `Fiscal.KassenSicherheitDe` tenant override, plus `Provider=fiskaly-de`. |
| 20 | DE offline signing | Out of scope | [`FISCAL_ROUTER_PLAN.md`](FISCAL_ROUTER_PLAN.md) §13 | Unreachable middleware blocks the sale. No DE offline queue. | Out of scope (decision §13). |
| 21 | QR payload (SPC `0200`, address type S, mod-97) | Implemented | `QrRechnungBuilder`, `SwissQrEncoder` | `QRR` / `SCOR` / `NON`. No bank HTTP. | `EInvoicing.QrRechnung`. Profile default is on for CH when the seed lists `QR_RECHNUNG`. |
| 21 | PDF (Empfangsschein + Zahlteil + Swiss QR) | Implemented | `QrRechnungPdf` via `QrRechnungBuilder.BuildPdfAsync` | QuestPDF + QRCoder. Not a measured SIX print overlay. Bytes are not written to disk. | `EInvoicing.QrRechnung`. Profile default is on for CH. |
| 21 | Official Swiss cross | Not started | `ChQrKnownGaps.json` `official-swiss-cross` | `SwissCrossMatrix` paints modules. It is not the 7 mm cross with a white border. | `ChQrKnownGapsTests` fixture (`present: false`). |
| 21 | Font embedding | Not started | `ChQrKnownGaps.json` `font-embedding-liberation-arial` | Default QuestPDF font. No Liberation Sans or Arial embed. | Same fixture. |
| 21 | pain.001 / bank scan | Not started | `ChQrKnownGaps.json` `pain001`, `bank-scan` | No credit-transfer file and no scan of the printed bill. | Same fixture. |
| 21 | Audit `QrRechnungPayloadBuilt` / `QrRechnungPdfGenerated` | Implemented | `QrRechnungBuilder`, `AuditEventType` 111 and 112 | SHA-256 of the SPC text, invoice id, tenant id, `correlation_id`. The IBAN is not logged. | `EInvoicing.QrRechnung`. |
| 21 | Per-tenant known-gap acceptance | Implemented | `ChQrGapAcceptanceService`, `Fiscal.ChQrKnownGapsAccepted` | Records which `ChQrKnownGaps.json` ids the operator acknowledged. Does not change the PDF and does not enable bank submission. Open gaps that are not accepted log a warning and activity `ChQrKnownGapsOutstanding` (261). FA also shows that list on the invoice detail, the invoice preview, and the credit-note create dialog. The invoice is not blocked. | Audit `ChQrKnownGapsAccepted` (114). Activity `ChQrKnownGapsAccepted` (260). `POST/GET /api/admin/tenants/{tenantId}/ch-qr-gap-acceptance` (`system.critical`). |
| 21 | `qr_rechnung_documents` table | Not needed | — | No payload table. The audit hash is the record. | Not needed (decision: audit hash is enough). |
| 21 | Bank submission | Not started | `QrRechnungBankSubmitOptions` | Option D remains (print/PDF only; no bank client). Option A is the operator workaround: download the PDF and upload it in the bank portal (`QrRechnungPdfDownloaded` 116, `QrRechnungBankUploadConfirmed` 117). Option B (EBICS / pain.001) is out of scope ([decision](FISCAL_SWITZERLAND_QR_PLAN.md#option-b-decision-2026-09-30)). Not a SIX or MWST compliance claim. | `QrRechnung:BankSubmit:Enabled` stays false. `CountryFiscalLockEvaluator` rejects `true` in Production and Staging. |
| 21 | `ProjectFiscalTaxSets` | Implemented | `SwitzerlandTaxStrategy`, `ChTaxSetMapper` | Rates from `ICountryTaxTypeRegistry.Get("CH")`. Empty catalog throws. No AT fallback. | `Fiscal.MwstCh` off → `FeatureDisabledException`. |
| 21 | `AllocateReceiptNumberAsync` | Implemented | `SwitzerlandInvoiceStrategy`, `ChReceiptSequenceService` | `ch_receipt_sequences`. Format `CH-{slug}-{register}-{seq}`. Collision check on `payment_details`. | Missing `IChReceiptSequenceService` → `InvalidOperationException`. No AT sequence. |
| 22 | UBL 2.1 sketch for `EU_DEFAULT` | Implemented | `En16931UblXmlBuilder` | `CountryStrategyContext.SelectEn16931Builder` returns it only for `EU_DEFAULT`. | `EInvoicing.En16931`. Profile default is on for `EU_DEFAULT`. That code is not a selectable tenant country. |
| 22 | Schematron subset | Implemented | `En16931Schematron` | Embedded BR rules listed in [`EINVOICING_EU.md`](EINVOICING_EU.md). | Same as the UBL builder. `En16931InvoiceValidationService` returns null when the flag is off. |
| 22 | Persist validation result | Implemented | `En16931InvoiceValidationService` | Nullable `invoices.einvoice_validation_passed` and `einvoice_validation_rule_ids`. No historical backfill. | `EInvoicing.En16931`. |
| 22 | Audit `EinvoiceValidated` | Implemented | `AuditEventType` 108 | Fail stores rule ids, not the UBL document. | `EInvoicing.En16931`. |
| 22 | XRechnung XML | Stub-throws | `NotImplementedXrechnungXmlBuilder` | Flag on → `EInvoicingNotSupportedForCountryException`. Flag off → `FeatureDisabledException`. | `EInvoicing.XRechnung`. Off by default. Not a country-profile default. |
| 22 | ZUGFeRD XML | Stub-throws | `NotImplementedZugferdXmlBuilder` | Same flag pattern as XRechnung. | `EInvoicing.Zugferd`. Off by default. Not a country-profile default. |
| 22 | Peppol Access Point send | Not started as a default path | `PeppolSubmissionService`, `HostedPeppolAccessPointClient` | Reserved. A non-canary provider writes `einvoice_submissions` `Queued` / `peppol-reserved` and does not call the Access Point. | `EInvoicing.Peppol` (Reserved) plus `Peppol:ReservedExit:Enabled`. Not in `FeatureFlagNames.All`. |
| 22 | Storecove Access Point | Partial | `StorecovePeppolAccessPointClient`, `PeppolStorecoveOptionsValidator` | 22-b-2 calls it for the canary tenant only, and only when `Peppol:Storecove:Environment` is exactly `TEST`. Any other value throws `peppol-live-not-allowed` before HTTP. `LIVE` plus `Peppol:ReservedExit:Enabled=true` fails startup. HTTP 2xx is `Sent`, not an ACK. Not a Peppol or EN 16931 compliance claim. | `Peppol:Provider=storecove` is not the default. `EInvoicing.Peppol` stays Reserved. |
| 22-b | Peppol canary submission | 22-b-1 through 22-b-4 done; 22-b-5 not started | [`EINVOICING_EU_SUBMISSION_PLAN.md`](EINVOICING_EU_SUBMISSION_PLAN.md#peppol-canary-package-breakdown-22-b-1-through-22-b-5) | ACK is a TEST poll (`PeppolAckPollingService`). `Peppol:AckPollInterval` defaults to 0. `state=DELIVERED` with a matching `guid` sets `Ack`. Audit `EinvoiceAckReceived` is 115. Activity `EinvoiceAckReceived` is 262. Transient `state=ERROR` codes `STORE_INTERNAL`, `TIMEOUT`, and `RATE_LIMIT` retry on `Peppol:AckRetryIntervalsSeconds` (default 300s then 1800s) and increment `provider_attempt_count`. Audit `EinvoiceSubmissionRetry` is 118. Activity `EinvoiceSubmissionRetry` is 263 (Warning). A third transient error is `peppol-ack-retries-exhausted`. `REJECTED`, `INVALID`, and any other `ERROR` fail immediately. 22-b-4 is `PeppolCanaryEndToEndTests` (mocked Access Point, no Storecove socket) plus manual `npm run smoke:peppol-test`. The outbox is read-only at `GET /api/admin/peppol/submissions` and FA `/admin/peppol/submissions` (`system.critical`). Not a webhook. Not LIVE. Not a Peppol or EN 16931 compliance claim. | `EInvoicing.Peppol` stays Reserved until 22-b-5, and only after a verified ACK. |
| 22 | Peppol participant registry | Implemented, no send | `peppol_participants`, `AdminPeppolParticipantsController` | `POST/GET /api/admin/peppol/participants`. Optional `legal_entity_id`, `eidentifier_scheme`, `eidentifier_value` (`20260930060830_AddPeppolParticipantIdentity`, operator-supplied, not derived from a VAT id). No credential column. Cross-tenant GET is 404. Audit `PeppolParticipantRegistered` (113). | `system.critical`. |
| 22 | Full KoSIT / Peppol Schematron packs, CII | Not started | — | Those files are not in the repo. | No flag loads them. |

### What is production-ready

| Country | Readiness | What the code actually runs |
|---------|-----------|-----------------------------|
| **AT** | Production | Live RKSV / TSE / FinanzOnline path. `AustriaTaxStrategy` and `AustriaInvoiceStrategy` delegate to the existing services. |
| **DE** | Partial | Contract plus SIGN DE HTTP skeleton (`FiskalyDeKassenSicherheitService`) and the Development fake (`SoftKassenSicherheitService`). The payment router can call `SignAsync`. This is not a live TSE. |
| **CH** | Partial | MWST calculation, `ChTaxSetMapper`, `ch_receipt_sequences`, QR payload, and PDF. Bank submit stays off. |
| **EU** | Partial | UBL sketch plus the embedded validator. `EU_DEFAULT` is not a selectable tenant country. No submission. |

### Known gaps

**DE**

- `FiskalyDeKassenSicherheitService` start/finish/export: `Provider` other than `fiskaly-de` and `not-configured` throws `NotImplementedException`. Sign, status, and certificate use `KassenSicherheitNotConfiguredException` instead.
- `NotImplementedKassenSicherheitService` throws `NotImplementedException` for any provider other than the `not-configured` no-op. It is not the host registration (`Soft` in Development, `FiskalyDe` otherwise).
- `SoftKassenSicherheitService.SignAsync` returns a pseudo-JWS. It does not call a TSE.
- `NotImplementedZugferdXmlBuilder.BuildXmlAsync` throws `EInvoicingNotSupportedForCountryException` when `EInvoicing.Zugferd` is on.
- `NotImplementedXrechnungXmlBuilder.BuildXmlAsync` throws `EInvoicingNotSupportedForCountryException` when `EInvoicing.XRechnung` is on.
- `TseService` tax-set projection throws `NotSupportedException` when the profile is not Austria.
- `RksvSpecialReceiptService` throws `NotSupportedException` when the profile is not Austria.

**CH**

- No type submits a QR-Rechnung to a bank. `QrRechnungBuilder.BuildPdfAsync` stops at PDF bytes. `QrRechnung:BankSubmit:Enabled=true` fails startup outside Development.

**EU**

- `EuDefaultTaxStrategy.ProjectFiscalTaxSets` throws `NotImplementedException`.
- `EuDefaultInvoiceStrategy.AllocateReceiptNumberAsync` throws `NotImplementedException`.
- `NotImplementedEn16931XmlBuilder.BuildXmlAsync` throws `NotImplementedException`. DI selects `En16931UblXmlBuilder` instead.
- `NotImplementedXrechnungXmlBuilder` and `NotImplementedZugferdXmlBuilder` throw `EInvoicingNotSupportedForCountryException` when their flags are on.
- `PeppolSubmissionService.SubmitAsync` returns status `Validated` and detail `not-sent` when `EInvoicing.En16931` is off, and again when the provider is `not-configured`.
- `HostedPeppolAccessPointClient.SendAsync` throws `PeppolAccessPointNotConfiguredException` unless mode and provider are both `hosted`.

**AT (country-layer limits, not the live RKSV path)**

- `TaxStrategyResolver` and `InvoiceStrategyResolver` throw `ArgumentException` (`AT tenant + EU_OSS is not supported`) for Austria plus `EU_OSS`.

### 16.2 What OSS “done” means

`IOssVatRateRegistry` (`OssVatRates` seed, not a database table and not a second VAT-ID column) supplies the destination STANDARD percent only when `EuDefaultTaxStrategy.CalculateOss` runs for `VatRegime.EU_OSS`. Domestic AT (`AustriaTaxStrategy`), DE, and CH calculations do not call it. An Austrian mandant still cannot use `EU_OSS`.

Filing an OSS return, OSS registration, and a live VIES check are not in this paket. The seed is a rate lookup for the invoice calculation.

The mocked create-tenant country step has a Playwright spec (`frontend-admin/tests/e2e/tenant-create.spec.ts`). Mandanten-Admin sees the country card read-only on `/tenant/profile` (`GET /api/company/settings`, own ambient tenant). `/admin/tenants` stays `system.critical`. Live VIES network tests stay mocked; `Vies.CheckEnabled` defaults off.

### 16.3 What a Super Admin must NOT do

- Do not set `Fiscal.KassenSicherheitDe=true` for a DE tenant in Production. `CountryFeatureFlagDefaults.TryGet` returns false for that flag, including a DE profile. A real tenant stays off unless a tenant override is true. HTTP still requires `KassenSicherheit:Provider=fiskaly-de` (`FiskalyDeKassenSicherheitService.CallFiskalyAsync`).
- Do not set `QrRechnung:BankSubmit:Enabled=true` in Production. `CountryFiscalLockEvaluator` rejects that value in Production and Staging (`ReasonQrBankSubmit`). There is no bank client. Option B (EBICS / pain.001) is out of scope; the operator uploads the PDF in their bank portal (Option A, audit 116 and 117).
- Do not enable `EInvoicing.Peppol` as a resolved flag. It stays in `FeatureFlagNames.Reserved` and out of `FeatureFlagNames.All`. `Peppol:ReservedExit:Enabled=true` can mark one canary (`source=reserved_exit_canary`). `SubmitAsync` opens HTTP only for that tenant when `Peppol:Provider=storecove` and `Peppol:Storecove:Environment` is exactly `TEST`. Any other Storecove environment throws `peppol-live-not-allowed` before HTTP. `LIVE` with `ReservedExit:Enabled=true` fails startup. HTTP 2xx is `Sent`, not an ACK. See §16.4.
- Do not change a tenant's country to DE, CH, or `EU_DEFAULT` when signed fiscal documents were issued under a different fiscal system. `AdminTenantService.UpdateCountryAsync` returns HTTP 409 and `AdminTenantCountryErrorCodes.FiscalCountryChangeInvalid` (`FISCAL_COUNTRY_CHANGE_INVALID`).

This is not a legal opinion and does not certify RKSV, KassenSichV, MWST, EN 16931, Peppol, or ViDA compliance.

### 16.4 Reserved exit conditions

| Flag | Where it sits | Exit |
|------|----------------|------|
| `EInvoicing.Peppol` | `FeatureFlagNames.Reserved`. Not in `All`. | Exit when `Peppol:ReservedExit:Enabled=true` for a canary and that canary submission is ACKed. 22-b-2 may open TEST HTTP for that tenant only. HTTP 2xx is not an ACK. The name stays Reserved until 22-b-5. |

Reserved flags are enforced by `PeppolReservedFlagGuardTests` and `PeppolReservedExitTests`. Do not remove a Reserved name without a decision record. This section does not move `EInvoicing.Peppol` out of Reserved and does not set `Peppol:ReservedExit:Enabled`.

Operational sequence ([`EINVOICING_EU_SUBMISSION_PLAN.md`](EINVOICING_EU_SUBMISSION_PLAN.md#peppol-canary-operations)):

1. Ops sets `Peppol:ReservedExit:Enabled=true` in TEST only, with one real `CanaryTenantId`, `ApprovedBy=ops/<github-login>`, and `ApprovedAtUtc`. Any other tenant stays off and does not open HTTP.
2. **Paket 22-b** ([breakdown](EINVOICING_EU_SUBMISSION_PLAN.md#peppol-canary-package-breakdown-22-b-1-through-22-b-5)): 22-b-1 Storecove client is in code. 22-b-2 is the canary `SubmitAsync` in TEST. 22-b-3 is done: `PeppolAckPollingService` polls `GET /document_submissions/{guid}` when `Peppol:AckPollInterval` is greater than 0. `state=DELIVERED` plus a matching `guid` sets `Ack`. Audit `EinvoiceAckReceived` is 115. Activity `EinvoiceAckReceived` is 262. 22-b-4 is done: `PeppolCanaryEndToEndTests` covers submit, stored message id, and ACK with a mocked Access Point (no live Storecove in CI). `npm run smoke:peppol-test` is the manual TEST plan and does not call Storecove unless `PEPPOL_SMOKE_ALLOW=1` and `--confirm` are both set. An ACK is `einvoice_submissions.status=Ack` with `acked_at_utc` set, `failure_reason` null, and a provider message id on that row. HTTP 2xx alone is not an ACK. If the ACK is ambiguous, leave `Enabled=false` and `TestAckReceivedAtUtc` null. The name stays Reserved through 22-b-4. Operators read the outbox at `GET /api/admin/peppol/submissions` and FA `/admin/peppol/submissions`. Failure codes and hints are in the [operator guidance](EINVOICING_EU_SUBMISSION_PLAN.md#operator-guidance). That page does not send and does not open LIVE HTTP.
3. **Paket 22-b-5** is not started. The procedure is the [promotion runbook](EINVOICING_EU_SUBMISSION_PLAN.md#peppol-promotion-runbook-22-b-5). This section does not perform that move and does not take `EInvoicing.Peppol` out of `Reserved`. After a verified ACK, and only then: the backend lead opens the PR that moves the name from `FeatureFlagNames.Reserved` to `FeatureFlagNames.All` and updates `PeppolReservedFlagGuardTests` and `PeppolReservedExitTests`. Ops then sets `Peppol:ReservedExit:Enabled=false`, enables `EInvoicing.Peppol` for the canary with `PUT /api/admin/feature-flags`, and adds a second tenant and a third only after Compliance signs off on the first 24 hours of green canary traffic and each later tenant soaks for 24 hours. Ops updates this §16 to call Paket 22 production-ready only after that ladder, and keeps the disclaimer footer. No migration. No LIVE HTTP. `Peppol:Storecove:Environment` stays `TEST`.
4. Rollback before promotion: `ReservedExit:Enabled=false`; the outbox row stays. Rollback after promotion: revert the PR so the name returns to `Reserved` and leaves `All`; existing `einvoice_submissions` rows stay. While that revert is in place, ops may set `Peppol:ReservedExit:Enabled=true` again for the same canary in TEST. A document the Access Point already accepted cannot be unsent from Regkasse.

Swiss QR-Rechnung is not this exit ([`FISCAL_SWITZERLAND_QR_PLAN.md`](FISCAL_SWITZERLAND_QR_PLAN.md)).

---

*End of document. This hub describes the multi-country architecture. It is not a legal opinion and does not certify RKSV, KassenSichV, MWST, EN 16931, Peppol, or ViDA compliance.*
