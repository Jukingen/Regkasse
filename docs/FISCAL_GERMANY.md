> **Status:** Shape only. Domain wired (Paket 30-c); TSE/RKSV paths remain AT-only. Not production-ready. **Paket 20 (DE TSE provider) is NOT STARTED.**

# Fiscal Germany (KassenSichV)

**Last updated:** 2026-09-21  
**Hub:** [`COUNTRIES.md`](COUNTRIES.md) · **Rules:** [`../AGENTS.md`](../AGENTS.md)

This page describes the **shape** of the German cash-register and e-invoicing module. It is not a legal opinion and does not claim KassenSichV, DSFinV-K, ZUGFeRD, or XRechnung compliance.

---

## Purpose

Describe how **DE** mandants should eventually differ from the live **Austria** RKSV/TSE path: a separate KassenSicherheit module, pluggable technical security equipment (TSE) providers, and German e-invoicing options (ZUGFeRD and/or XRechnung).

Do not use this document to change Austrian payment, TSE, or FinanzOnline behavior.

---

## Status

**NOT production-ready. Domain wired (Paket 30-c); TSE/RKSV paths remain AT-only.**

| Item | State |
|------|--------|
| `CountryProfile` DE seed | Shipped (`^DE\d{9}$`, `KASSENSICHERHEIT_DE`, ZUGFeRD + XRechnung declared) |
| `GermanyTaxStrategy.CalculateTax` | Shape: CountryTaxType 19/7 + `CartMoneyHelper` line math; AT buckets not used |
| `GermanyInvoiceStrategy` disclosures / `InvoiceDocumentDto` | Shape (UStG §14 keys) |
| `ProjectFiscalTaxSets` / `AllocateReceiptNumberAsync` | `NotImplementedException` |
| `IKassenSicherheitService` | Stub: flag off → `FEATURE_DISABLED`; `Provider=not-configured` → no-op; other provider → `NotImplementedException` |
| ZUGFeRD / XRechnung XML | Stub throws `NotImplementedException` |
| Wiring into `PaymentService` / `InvoiceService` | Tax/invoice domain wired (Paket 30-c); TSE/RKSV paths remain AT-only |
| **Paket 20 — DE TSE provider** | **NOT STARTED** |

Feature-flag gate: `Fiscal.KassenSicherheitDe` (country-profile default **on** for DE). ZUGFeRD XML: `EInvoicing.Zugferd` (not country-derived; stays **off** until override). `Fiscal.RksvAt` stays **off** for DE tenants.

Simulated or fake DE signing is rejected at host startup (`CountryFiscalLockEvaluator`). This document does not name a required vendor.

Super Admin may **create** a DE tenant (wizard country step). That does not enable Austrian TSE or a German TSE. Production cutover: [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md).

---

## Planned Architecture

- Resolve DE from `CompanySettings.Country` and a CountryProfile. Do not enable RKSV special receipts or Austrian TSE for DE.
- A KassenSicherheit facade with extension points for device provisioning and signing. Provider choice is Paket **20** — not started.
- Signature-chain state for DE must not reuse the Austrian RKSV chain table as if it were the same legal instrument.
- E-invoicing: ZUGFeRD and/or XRechnung as profile-driven options. Shared EN 16931 semantics belong in [`EINVOICING_EU.md`](EINVOICING_EU.md); this stub does not pick a single syntax.
- Do not attach DE **signing** to `PaymentService` until Paket 20. Tax calculation already selects `GermanyTaxStrategy` at the resolver edge (Paket 30-c).

---

## Remaining gaps

See [`COUNTRIES.md`](COUNTRIES.md) §16. This stub owns **Paket 20** (DE TSE provider). ZUGFeRD/XRechnung XML builders stay off until a dedicated e-invoicing package.

---

## Open Questions

- Which TSE provider(s) and certification path will the product support?
- Is ZUGFeRD, XRechnung, or both the default for DE mandants?
- How should Austrian `Tse:` configuration stay isolated from DE KassenSicherheit configuration?
- Which turnover or legal thresholds change what is mandatory for a given mandant?

---

## Related Docs

- [`COUNTRIES.md`](COUNTRIES.md) — multi-country hub
- [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md) — production country-layer apply order
- [`../AGENTS.md`](../AGENTS.md) — Country & Fiscal Regimes
- [`EINVOICING_EU.md`](EINVOICING_EU.md) — EN 16931 / ViDA stub (no submission)
- `RKSV_*.md` — **Austria only**; do not treat as German fiscal law
