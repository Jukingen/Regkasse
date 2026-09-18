> **Status:** Shape only (Paket 9). Not production-ready. No bank submission. No PDF/QR image.

# Fiscal Switzerland (MWST / QR-Rechnung)

**Last updated:** 2026-09-18  
**Hub:** [`COUNTRIES.md`](COUNTRIES.md) · **Rules:** [`../AGENTS.md`](../AGENTS.md)

This page describes the **shape** of the Swiss VAT and QR-bill module. It is not a legal opinion and does not claim MWST or SIX QR-Rechnung compliance.

---

## Purpose

Describe how **CH** mandants should eventually differ from the live **Austria** RKSV/TSE path: MWST calculation and labels, Swiss UID validation, and QR-Rechnung (Swiss QR-bill) **payload** generation for IBAN accounts in Switzerland or Liechtenstein.

This document does **not** include bank submission or PDF QR rendering.

---

## Status

**NOT production-ready. Not wired into `PaymentService` / `TseService`.**

| Item | State |
|------|--------|
| `CountryProfile` CH seed | Shipped |
| `SwitzerlandTaxStrategy.CalculateTax` | Shape: CountryTaxType 8.1 / 2.6 / 3.8 + `CartMoneyHelper` line math; AT buckets not used |
| `SwitzerlandInvoiceStrategy` disclosures / placeholder document | Shape (MWSTG keys + `InvoiceDocumentDto`) |
| QR-Rechnung payload (SIX) | Shape: `IQrRechnungBuilder` returns IBAN / creditor / debtor / amount / currency / reference. CH/LI prefix only. No bank API |
| QR-Rechnung PDF / QR image | Not implemented (`BuildPdfAsync` throws) |
| Wiring into `InvoiceService` / `PaymentService` | Out of scope (Paket 30-c); non-AT tenants still fail closed via `CountryCallSiteGuard` |

Feature-flag gates: `Fiscal.MwstCh`, `EInvoicing.QrRechnung`. `Fiscal.RksvAt` stays **off** for CH tenants.

Simulated or fake CH fiscal modes are rejected at host startup (`CountryFiscalLockEvaluator`).

---

## Planned Architecture

- Resolve CH from company country fields and a CountryProfile. Do not enable RKSV or Austrian TSE for CH.
- MWST label and rate selection (standard, reduced, lodging) come from `CountryTaxType` seeds, not from AT `TaxTypes`.
- VAT-ID format and optional MWST suffix live in CountryProfile seeds (`IVatIdValidator`).
- QR-Rechnung: produce the Swiss QR-bill payload for CH/LI IBANs. Persist payload metadata when a table exists; do not call a bank. PDF/QR image generation is not implemented.
- Switzerland is not the EN 16931 EU default; see [`EINVOICING_EU.md`](EINVOICING_EU.md) only for contrast.

---

## Open Questions

- Default QR reference type (QRR vs SCOR vs NON)?
- Is the accommodation special rate in product scope?
- When is QR currency CHF vs EUR?
- Are language-specific UID suffixes in scope, or only the MWST suffix?

---

## Related Docs

- [`COUNTRIES.md`](COUNTRIES.md) — multi-country hub
- [`../AGENTS.md`](../AGENTS.md) — Country & Fiscal Regimes
- [`EINVOICING_EU.md`](EINVOICING_EU.md) — EU e-invoicing stub (not CH QR-Rechnung)
- `RKSV_*.md` — **Austria only**
