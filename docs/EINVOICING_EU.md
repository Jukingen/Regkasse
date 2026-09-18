> **Status:** Shape only (Paket 10). Domain wired (Paket 30-c); TSE/RKSV paths remain AT-only. Not production-ready. No Peppol / ViDA / tax-authority submission.

# EU e-invoicing (EN 16931) and ViDA readiness

**Last updated:** 2026-09-18  
**Hub:** [`COUNTRIES.md`](COUNTRIES.md) · **Rules:** [`../AGENTS.md`](../AGENTS.md)

This page describes the **shape** of the generic EU e-invoicing layer used when a mandant has no country-specific invoice strategy. It is not a legal opinion, not Peppol onboarding, and **not submission-ready**.

---

## Purpose

Describe a default for EU-oriented mandants: EN 16931 semantic invoicing, reverse charge / OSS / NON_EU tax shape, optional Peppol as a later transport/CIUS choice, VIES as an optional B2B VAT-ID check, and a **read-only** ViDA readiness report.

This document does **not** implement tax-authority or network submission.

---

## Status

**NOT production-ready. Domain wired (Paket 30-c); TSE/RKSV paths remain AT-only. Not submission-ready.**

| Item | State |
|------|--------|
| `EU_DEFAULT` CountryProfile | Shipped; **registry-only**, not selectable as a country in the wizard — [`COUNTRIES.md`](COUNTRIES.md) |
| `EuDefaultTaxStrategy.CalculateTax` | Shape: reverse charge (valid buyer VAT-ID → 0%) / OSS (line `VatRatePercent`) / NON_EU export (0%); AT buckets not used |
| `EuDefaultInvoiceStrategy` disclosures / placeholder document | Shape (EN 16931 keys + `InvoiceDocumentDto`) |
| EN 16931 XML | Stub throws `NotImplementedException` (`IEn16931XmlBuilder`) |
| XRechnung XML (DE CIUS) | Stub throws `NotImplementedException` (`IXrechnungXmlBuilder`); see [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md) |
| Peppol Access Point | Not in scope |
| VIES client | Shipped as optional (`Vies.CheckEnabled`, default **off**); no live VIES in tests |
| ViDA | Read-only checklist only; no timeline committed |
| Wiring into `InvoiceService` / `PaymentService` | Domain wired (Paket 30-c); TSE/RKSV paths remain AT-only |

Feature-flag gates: `EInvoicing.En16931`, `EInvoicing.XRechnung` (DE CIUS), `Vies.CheckEnabled` (default **off**). `Fiscal.RksvAt` stays **off** for `EU_DEFAULT`.

Austria RKSV receipts and German ZUGFeRD are **not** defined here. See `RKSV_*.md` and [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md). XRechnung is a German CIUS of EN 16931; the DE-specific doc is the reference for that builder.

---

## Planned Architecture

- `EU_DEFAULT` is a fallback profile, not an ISO country code. Real mandants keep an ISO alpha-2 code (or AT backfill).
- Tax and invoice strategies use `VatRegime` (including OSS and reverse charge) without forking `PaymentService`.
- Reverse charge requires a buyer VAT-ID. Prefix `AT` / `DE` / `CH` uses that seeded profile regex; otherwise the context profile (`EU_DEFAULT` `^[A-Z]{2}[A-Z0-9]{8,12}$`). Missing or invalid shape → `VAT_ID_SHAPE_INVALID`. Never `GetOrDefault` (that would fall back to AT).
- **Limitation:** an AT tenant with `VatRegime=EU_REVERSE_CHARGE` is **not** supported today. The resolver picks `AustriaTaxStrategy` by country code, so reverse charge does not apply. Future package (Paket 12-c or 30-e) may add cross-regime support. Do not change the resolver for this gap.
- EN 16931 is the semantic target. UBL vs CII is an open question; this stub does not require Peppol.
- VIES: mockable client behind `Vies.CheckEnabled`. Never call the live network from unit tests.
- ViDA: a read-only readiness object (planned fields: `en16931Ready`, `viesEnabled`, `ossRegistered`, `eInvoicingCapable`). No submission API. This document does not assign a go-live date.

---

## OSS placeholder (Paket 30-c)

OSS is **wired but not destination-rated**. `CountryPaymentTaxLineMapper` (and therefore `PaymentService`) maps EU_DEFAULT product `TaxType` ints through `TaxTypes.GetTaxRate` — the Austrian 20 / 10 / 13 / 0 / 4.9 stand-in. `EuDefaultTaxStrategy.CalculateOss` then uses the line `VatRatePercent` as-is.

This is a **temporary placeholder**. Paket 30-d will add the real OSS destination-rate table. Tests pin the stand-in (`EuOss_CurrentlyUsesAtRates_TemporaryUntilPaket30d`) so the swap is visible.

---

## Open Questions

- Canonical syntax: UBL 2.1 vs CII?
- Is Peppol required, or is EN 16931 XML enough for the first slice?
- Where does “OSS registered” come from (tenant flag vs external register)?
- Which mandants fall under which ViDA duties — **unknown**; not decided in this stub.

---

## Related Docs

- [`COUNTRIES.md`](COUNTRIES.md) — multi-country hub
- [`../AGENTS.md`](../AGENTS.md) — Country & Fiscal Regimes
- [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md) — DE ZUGFeRD / XRechnung stub
- [`FISCAL_SWITZERLAND.md`](FISCAL_SWITZERLAND.md) — CH QR-Rechnung stub (not EN 16931)
- `RKSV_*.md` — **Austria only**
