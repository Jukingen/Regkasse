> **Status:** Partial. Paket 20 outbound HTTP is implemented. `PaymentService` reaches it through `IFiscalSignatureRouter.SignAsync` → `FiscalSignatureRouter.SignDeAsync`. Not production-ready. This is not a KassenSichV compliance claim.

# Fiscal Germany (KassenSichV)

**Last updated:** 2026-09-29  
**Hub:** [`COUNTRIES.md`](COUNTRIES.md) · **Rules:** [`../AGENTS.md`](../AGENTS.md)

This page describes the **shape** of the German cash-register and e-invoicing module. It is not a legal opinion and does not claim KassenSichV, DSFinV-K, ZUGFeRD, or XRechnung compliance.

---

## Purpose

Describe how **DE** mandants should eventually differ from the live **Austria** RKSV/TSE path: a separate KassenSicherheit module, pluggable technical security equipment (TSE) providers, and German e-invoicing options (ZUGFeRD and/or XRechnung).

Do not use this document to change Austrian payment, TSE, or FinanzOnline behavior.

---

## Status

**NOT production-ready.** Tax and invoice strategies are wired (Paket 30-c). DE checkout signing goes through the router. That is not a live TSE.

| Item | State |
|------|--------|
| `CountryProfile` DE seed | Shipped (`^DE\d{9}$`, `KASSENSICHERHEIT_DE`, ZUGFeRD + XRechnung declared) |
| `GermanyTaxStrategy.CalculateTax` | Shape: CountryTaxType 19/7 + `CartMoneyHelper` line math; AT buckets not used |
| `GermanyInvoiceStrategy` disclosures / `InvoiceDocumentDto` | Shape (UStG §14 keys) |
| `GermanyTaxStrategy.ProjectFiscalTaxSets` | Implemented. Delegates to `DeTaxSetMapper.MapFromTaxDetailsJson`. Flag off → `FeatureDisabledException` |
| `GermanyInvoiceStrategy.AllocateReceiptNumberAsync` | Implemented. `DeReceiptSequenceService.FormatDeBelegNr` on `de_receipt_sequences`. Missing sequence service → `InvalidOperationException` |
| `IKassenSicherheitService` sign / status / certificate | Partial. Development DI registers `SoftKassenSicherheitService` (pseudo-JWS, no HTTP). Every other environment registers `FiskalyDeKassenSicherheitService`. `SignAsync`, `GetStatusAsync`, and `GetCertificateChainAsync` call `IKassenSicherheitHttpClient` only when `Fiscal.KassenSicherheitDe` is on and `Provider=fiskaly-de`. Any other provider throws `KassenSicherheitNotConfiguredException` (no AT fallback). Flag off → `FeatureDisabledException`. `StartTransactionAsync`, `FinishTransactionAsync`, and `ExportDsfinvkAsync` are HTTP methods on the concrete service, not on the interface |
| ZUGFeRD / XRechnung XML | Flag on → `EInvoicingNotSupportedForCountryException` (`NotImplementedZugferdXmlBuilder.BuildXmlAsync`, `NotImplementedXrechnungXmlBuilder.BuildXmlAsync`). Flag off → `FeatureDisabledException` |
| Wiring into `PaymentService` / `InvoiceService` | Tax/invoice domain wired (Paket 30-c). The DE branch calls `IFiscalSignatureRouter.SignAsync`. `SignDeAsync` stores the signature on `de_tse_signatures` when `Provider=fiskaly-de`. It does not write `payment.TseSignature` |
| DE signature persistence | **Implemented (Option A).** Table `de_tse_signatures`. Not included in RKSV DEP export |
| **Paket 20 — DE TSE provider** | **Partial — outbound HTTP implemented and reached from payment through the router. Not a live TSE** |

Feature-flag gate: `Fiscal.KassenSicherheitDe` (country-profile default **off**, including DE; a pilot sets a tenant override). ZUGFeRD XML: `EInvoicing.Zugferd` (not country-derived; stays **off** until override). `Fiscal.RksvAt` stays **off** for DE tenants.

Simulated or fake DE signing is rejected at host startup (`CountryFiscalLockEvaluator`). This document does not name a required vendor.

Super Admin may **create** a DE tenant (wizard country step). That does not enable Austrian TSE or a German TSE. Production cutover: [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md).

---

## DE signature persistence

A DE TSS signature must not be stored in `payment_details.tse_signature`. That column is the Austrian RKSV compact JWS and is what `RksvDepExportService` selects (`TseSignature` not null and not empty).

| Option | Shape | Why it was considered |
|--------|--------|------------------------|
| A | New table `de_tse_signatures` (`payment_details_id` FK, `tss_id`, `transaction_id`, `signature`, `signature_algorithm`, `signed_at_utc`, `certificate_serial`, `tenant_id`) | Receipt lookup is one row per payment. Tenant backup can include the table without mixing it into the RKSV chain. DEP export never reads it. |
| B | Nullable columns on `payment_details` | Fewer joins, but the RKSV row and the DE signature share a table that DEP already scans. |

**Chosen: Option A.** `FiscalSignatureRouter.SignDeAsync` inserts the row only when `Fiscal.KassenSicherheitDe` is on and `KassenSicherheit:Provider=fiskaly-de`. `PaymentService` does not copy the signature onto `TseSignature`, `PrevSignatureValueUsed`, or `CertificateThumbprint`. `signature_algorithm` is `signature.algorithm` from the transaction response. `certificate_serial` is the serial of the TSS leaf `certificate` (Base64 DER). If fiskaly does not return the field, the column is null and a Warning is emitted; do not backfill. This is not a KassenSichV compliance claim. Do not enable the flag for a real tenant.

---

## Planned Architecture

- Resolve DE from `CompanySettings.Country` and a CountryProfile. Do not enable RKSV special receipts or Austrian TSE for DE.
- A KassenSicherheit facade with extension points for device provisioning and signing. Paket **20** HTTP is partial (`FiskalyDeKassenSicherheitHttpClient`). The router calls `SignAsync` only.
- Signature-chain state for DE must not reuse the Austrian RKSV chain table as if it were the same legal instrument. The DE signature lives on `de_tse_signatures`.
- E-invoicing: ZUGFeRD and/or XRechnung as profile-driven options. Shared EN 16931 semantics belong in [`EINVOICING_EU.md`](EINVOICING_EU.md); this page does not pick a single syntax.
- Do not extend DE signing past the current `SignAsync` wiring until [`FISCAL_ROUTER_PLAN.md`](FISCAL_ROUTER_PLAN.md) §6 gates are met. Tax calculation already selects `GermanyTaxStrategy` at the resolver edge (Paket 30-c).

---

## What is production-ready today

Nothing in this table is a KassenSichV sign-off.

| Piece | State | Where |
|-------|--------|--------|
| Soft signer | Development only | `ApplicationHost` registers `SoftKassenSicherheitService` when `IsDevelopment()`. `SignAsync` returns a pseudo-JWS. No HTTP |
| FiskalyDe outbound HTTP | Implemented, gated | `FiskalyDeKassenSicherheitService.CallFiskalyAsync` calls the HTTP client only when `Fiscal.KassenSicherheitDe` is on and `Provider=fiskaly-de` |
| PaymentService DE branch | Implemented via the router | `PaymentService` `KASSENSICHERHEIT_DE` branch → `IFiscalSignatureRouter.SignAsync` → `FiscalSignatureRouter.SignDeAsync` |
| DSFinV-K export | Not production-ready | `ExportDsfinvkAsync` sends `PUT /exports/{id}` only. No download method. `SignDeAsync` does not call it |
| DE signature table | Implemented, not a live TSE | `de_tse_signatures`. `FiscalSignatureRouter.SignDeAsync` writes it when the flag is on and `Provider=fiskaly-de` |
| DE signature columns on payment_details | Not used | DE signatures go to `de_tse_signatures`. RKSV columns (`TseSignature`, `PrevSignatureValueUsed`, `CertificateThumbprint`) stay null for DE |
| AT-style BelegNr for DE | Not used | DE numbers come from `DeReceiptSequenceService.FormatDeBelegNr` (`PaymentService.AllocateCountryBelegNrAsync`). That is not the Austrian RKSV sequence |

## Known gaps

- `FiskalyDeKassenSicherheitService.CallFiskalyAsync`: provider other than `fiskaly-de` → `KassenSicherheitNotConfiguredException` (`SignAsync`, `GetStatusAsync`, `GetCertificateChainAsync`).
- `FiskalyDeKassenSicherheitService.DispatchAsync`: provider other than `fiskaly-de` and `not-configured` → `NotImplementedException` (`StartTransactionAsync`, `FinishTransactionAsync`, `ExportDsfinvkAsync`). `not-configured` is a no-op on that path.
- Flag off on either path → `FeatureDisabledException` (`FeatureFlagNames.FiscalKassenSicherheitDe`).
- `FiscalSignatureRouter.SignDeAsync` calls `IKassenSicherheitService.SignAsync` only. It does not call `StartTransactionAsync` or `FinishTransactionAsync`. Missing TSS/client id → `FiscalSigningNotAvailableException` (`DeNotConfigured`). Flag off → `DeFlagOff`.
- `SoftKassenSicherheitService.SignAsync` does not call a TSE.
- `NotImplementedZugferdXmlBuilder.BuildXmlAsync` and `NotImplementedXrechnungXmlBuilder.BuildXmlAsync` throw `EInvoicingNotSupportedForCountryException` when their flags are on.
- `FiskalyDeKassenSicherheitHttpClient.ExportDsfinvkAsync` does not download the archive.

See also [`COUNTRIES.md`](COUNTRIES.md) §16.

## Remaining gaps

ZUGFeRD/XRechnung XML builders stay off until a dedicated e-invoicing package. Do not extend DE signing past the current router wiring until [`FISCAL_ROUTER_PLAN.md`](FISCAL_ROUTER_PLAN.md) §6.

---

## Open Questions

Answered for v1 in [`FISCAL_GERMANY_PROVIDER_DECISION.md`](FISCAL_GERMANY_PROVIDER_DECISION.md): **fiskaly SIGN DE** (cloud TSS). Epson/Swissbit USB deferred. `KassenSicherheit:` stays isolated from `Tse:`.

Still open:

- Is ZUGFeRD, XRechnung, or both the default for DE **e-invoicing**? See [`EINVOICING_EU_SUBMISSION_PLAN.md`](EINVOICING_EU_SUBMISSION_PLAN.md) (UBL / Peppol first; ZUGFeRD later).
- Which turnover or legal thresholds change what is mandatory for a given mandant?

---

## Staging setup (Faz 2)

Obtain a fiskaly TEST API key and secret from a SIGN DE organization.

Set these environment variables on the staging host. Do not commit the values.

    KassenSicherheit__ApiKey=<test-key>
    KassenSicherheit__ApiSecret=<test-secret>
    KassenSicherheit__AdminPin=<test-pin>

Deployed `appsettings.Staging.json` uses `Provider=fiskaly-de`, `PilotMode=true`, `Environment=TEST`, and `ApiBaseUrl=https://kassensichv-middleware.fiskaly.com/api/v2`. `KassenSicherheitHostOptionsValidator` rejects startup when `PilotMode=true` and `Environment` is `LIVE`, or when `ApiBaseUrl` is the SIGN DE LIVE host (`kassensichv.fiskaly.com`). `PilotMode=false` keeps the existing lock (`fake`, `AllowSimulatedTse`, `rksv.fiskaly.com`) and does not turn DE signing on. The key fields stay empty in git and are filled from the environment.

`Fiscal.KassenSicherheitDe` stays off from the country profile. A pilot sets that flag `true` as a tenant override for exactly one DE tenant. The override is per tenant (`tenant_settings`), not a profile default, and `PilotMode` does not set it.

SIGN DE and DSFinV-K are different hosts. Transactions use the SIGN DE middleware. A DSFinV-K export is `PUT https://dsfinvk.fiskaly.com/api/v1/exports/{export_id}` with `start_date`, `end_date`, and `format` (`tar` or `zip`). The response is JSON (`state=PENDING`, `_id`, `format`, `error.code`), not the archive. Download is a later `GET /exports/{export_id}/download`.

Feature flag: in Super Admin FA, set a tenant override `Fiscal.KassenSicherheitDe=true` for exactly one DE tenant. Do not turn the flag on as a profile default or a Production default.

Verify: TSS created via `POST /tss` and `PATCH` to `INITIALIZED`, client created, transaction `ACTIVE` then `FINISHED`.

---

## Staging smoke test

Environment names (values stay out of git): `KassenSicherheit__ApiKey`, `KassenSicherheit__ApiSecret`, `KassenSicherheit__AdminPin`, `KASSENSICHERHEIT_SMOKE_ALLOW`.

`npm run smoke:kassensicherheit-test` is a dry-run. It prints the seven HTTP steps and does not call fiskaly.

Real HTTP requires both flags:

```bash
set KASSENSICHERHEIT_SMOKE_ALLOW=1
node scripts/smoke/kassensicherheit-test-smoke.mjs --confirm
```

`--dry-run` is an explicit alias of the default. `--confirm` without the three credentials exits 1 and prints only the variable names. `--confirm` without `KASSENSICHERHEIT_SMOKE_ALLOW=1` exits 1 and does not send HTTP. Success output is the TSS id, client id, transaction id, and export `state`. Failures print the HTTP status and provider `code` only.

---

## Related Docs

- [`COUNTRIES.md`](COUNTRIES.md) — multi-country hub
- [`FISCAL_GERMANY_PROVIDER_DECISION.md`](FISCAL_GERMANY_PROVIDER_DECISION.md) — Paket 20 provider decision record (that file still says implementation is not started; the HTTP client and router wiring are in code)
- [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md) — production country-layer apply order
- [`../AGENTS.md`](../AGENTS.md) — Country & Fiscal Regimes
- [`EINVOICING_EU.md`](EINVOICING_EU.md) — EN 16931 / ViDA stub (no submission)
- `RKSV_*.md` — **Austria only**; do not treat as German fiscal law

---

This is not a legal opinion and does not certify RKSV, KassenSichV, MWST, EN 16931, Peppol, or ViDA compliance.
