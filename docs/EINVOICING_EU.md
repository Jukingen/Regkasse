> **Status:** Shape only (Paket 10). Domain wired (Paket 30-c); TSE/RKSV paths remain AT-only. AT reverse charge uses this layer (Paket 12-c). OSS destination STANDARD rates are seeded (Paket 30-d). Not production-ready. **Paket 22 (Peppol) is NOT STARTED.**

# EU e-invoicing (EN 16931) and ViDA readiness

**Last updated:** 2026-09-21  
**Hub:** [`COUNTRIES.md`](COUNTRIES.md) · **Rules:** [`../AGENTS.md`](../AGENTS.md)

This page describes the **shape** of the generic EU e-invoicing layer used when a mandant has no country-specific invoice strategy, and when an AT mandant invoices under `EU_REVERSE_CHARGE`. It is not a legal opinion, not Peppol onboarding, and **not submission-ready**.

---

## Purpose

Describe a default for EU-oriented mandants: EN 16931 semantic invoicing, reverse charge / OSS / NON_EU tax shape, optional Peppol as a later transport/CIUS choice, VIES as an optional B2B VAT-ID check, and a **read-only** ViDA readiness report.

This document does **not** implement tax-authority or network submission (Paket **22**).

---

## Status

**NOT production-ready. Domain wired (Paket 30-c); TSE/RKSV paths remain AT-only. Not submission-ready.**

| Item | State |
|------|--------|
| `EU_DEFAULT` CountryProfile | Shipped; **registry-only**, not selectable as a country in the wizard — [`COUNTRIES.md`](COUNTRIES.md) |
| `EuDefaultTaxStrategy.CalculateTax` | Shape: reverse charge (valid buyer VAT-ID → 0%) / OSS (destination STANDARD rate) / NON_EU export (0%); AT buckets not used |
| AT + `EU_REVERSE_CHARGE` | **Shipped** (Paket 12-c): both resolvers route to EuDefault regardless of country code. `EInvoicing.En16931` does **not** gate reverse-charge tax or disclosures. |
| `EuDefaultInvoiceStrategy` disclosures / `InvoiceDocumentDto` | Shape (EN 16931 keys) |
| EN 16931 XML | UBL 2.1 invoice (`En16931UblXmlBuilder`) + core BR Schematron. No Peppol send |
| XRechnung XML (DE CIUS) | Stub throws `NotImplementedException` (`IXrechnungXmlBuilder`); see [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md) |
| Peppol Access Point | Hosted client + mock. `Provider=not-configured` does not send. Own AP is rejected (Paket 83) |
| OSS destination rate table (Paket 30-d) | **Shipped** in-code seed (`IOssVatRateRegistry`). Greek VAT-ID prefix `EL` aliases to `GR`. No AT `TaxTypes` fallback |
| VIES client | Shipped as optional (`Vies.CheckEnabled`, default **off**); no live VIES in tests |
| ViDA | Read-only checklist only; no timeline committed |
| Wiring into `InvoiceService` / `PaymentService` | `EU_DEFAULT` sales use the fiscal router (`EN_16931` + Peppol). AT TSE is unchanged. Flag off → `EU_FLAG_OFF` |

Feature-flag gates: `EInvoicing.En16931`, `EInvoicing.XRechnung` (DE CIUS), `Vies.CheckEnabled` (default **off**). `Fiscal.RksvAt` stays **off** for `EU_DEFAULT`. Reverse-charge **tax** does not require `EInvoicing.En16931`.

Austria RKSV receipts and German ZUGFeRD are **not** defined here. See `RKSV_*.md` and [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md). XRechnung is a German CIUS of EN 16931; the DE-specific doc is the reference for that builder.

---

## Planned Architecture

- `EU_DEFAULT` is a fallback profile, not an ISO country code. Real mandants keep an ISO alpha-2 code (or AT backfill).
- Tax and invoice strategies use `VatRegime` (including OSS and reverse charge) without forking `PaymentService`.
- Reverse charge requires a buyer VAT-ID. Prefix `AT` / `DE` / `CH` uses that seeded profile regex; otherwise the context profile (`EU_DEFAULT` `^[A-Z]{2}[A-Z0-9]{8,12}$`). Missing or invalid shape → `VAT_ID_SHAPE_INVALID`. Never `GetOrDefault` (that would fall back to AT).
- AT tenant + `VatRegime=EU_REVERSE_CHARGE` is supported since Paket 12-c. Both resolvers route reverse charge to `EuDefaultTaxStrategy` / `EuDefaultInvoiceStrategy` regardless of country code (VAT regime, not fiscal system). Valid buyer VAT-ID → 0% + reverse-charge disclosure; missing/invalid → `VAT_ID_SHAPE_INVALID`. AT + `EU_OSS` remains unsupported (`ArgumentException`: `AT tenant + EU_OSS is not supported`).
- `EInvoicing.En16931` flag does NOT gate the reverse-charge VAT calculation or disclosure. It still gates `EU_OSS` / `NON_EU` tax, `BuildInvoiceDocumentAsync`, `ValidateVatId`, `DetermineInvoiceFields`, and the EN 16931 / XRechnung / ZUGFeRD XML builders.
- EN 16931 is the semantic target. UBL vs CII is an open question; this stub does not require Peppol until Paket 22.
- VIES: mockable client behind `Vies.CheckEnabled`. Never call the live network from unit tests.
- ViDA: a read-only readiness object (planned fields: `en16931Ready`, `viesEnabled`, `ossRegistered`, `eInvoicingCapable`). No submission API. This document does not assign a go-live date.

---

## OSS destination rate table (Paket 30-d)

`OssVatRates` / `IOssVatRateRegistry` is an in-code STANDARD rate seed (no database table). `CountryPaymentTaxLineMapper.ResolveOssDestinationCountry` sets `TaxCalculationContext.DestinationCountry` from the first two letters of the buyer VAT-ID when `VatRegime` is `EU_OSS`. `EuDefaultTaxStrategy.CalculateOss` then uses `GetStandardRate`. Missing destination throws `OSS requires BuyerVatId to determine destination country`. An unknown destination throws `OSS destination rate missing: {country}`. There is no Austrian `TaxTypes` fallback. AT + `EU_OSS` remains unsupported.

`GetStandardRate` maps the Greek VAT-ID prefix `EL` to ISO `GR`. The seed contains `GR` only, not a separate `EL` row.

Seeded STANDARD rates. `EffectiveFrom` is 2024-01-01 except Finland.

| Country | Rate |
|---------|------|
| AT | 20 |
| DE | 19 |
| FR | 20 |
| IT | 22 |
| NL | 21 |
| ES | 21 |
| PL | 23 |
| BE | 21 |
| IE | 23 |
| PT | 23 |
| SE | 25 |
| DK | 25 |
| FI | 25.5 (effective 2024-09-01) |
| GR | 24 |

---

## Remaining gaps

See [`COUNTRIES.md`](COUNTRIES.md) §16.

| Paket | Scope | Status |
|-------|--------|--------|
| **22** | Peppol Access Point / transport; tax-authority / ViDA submission | **NOT STARTED** |
| **30-d** | OSS destination rate table | **Shipped** (in-code seed; `EL` → `GR`) |

---

## Open Questions

Answered for v1 in [`EINVOICING_EU_SUBMISSION_PLAN.md`](EINVOICING_EU_SUBMISSION_PLAN.md): **UBL 2.1** / Peppol BIS 3.0; **hosted AP** (not own AP); validator-first; no ViDA submission.

Still open:

- Where does “OSS registered” come from (tenant flag vs external register)? The destination rate table does not answer this.
- Which mandants fall under which ViDA duties — **unknown**; not decided here.

---

## Related Docs

- [`COUNTRIES.md`](COUNTRIES.md) — multi-country hub
- [`EINVOICING_EU_SUBMISSION_PLAN.md`](EINVOICING_EU_SUBMISSION_PLAN.md) — Paket 22 submission plan (not implemented)
- [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md) — production country-layer apply order
- [`../AGENTS.md`](../AGENTS.md) — Country & Fiscal Regimes
- [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md) — DE ZUGFeRD / XRechnung stub
- [`FISCAL_SWITZERLAND.md`](FISCAL_SWITZERLAND.md) — CH QR-Rechnung stub (not EN 16931)
- `RKSV_*.md` — **Austria only**
