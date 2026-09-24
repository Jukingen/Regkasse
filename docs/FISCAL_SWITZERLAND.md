> **Status:** Shape only (Paket 9). Domain wired (Paket 30-c); TSE/RKSV paths remain AT-only. Not production-ready. **Paket 21 (CH QR bank submit) is NOT STARTED.** No PDF/QR image.

# Fiscal Switzerland (MWST / QR-Rechnung)

**Last updated:** 2026-09-21  
**Hub:** [`COUNTRIES.md`](COUNTRIES.md) · **Rules:** [`../AGENTS.md`](../AGENTS.md)

This page describes the **shape** of the Swiss VAT and QR-bill module. It is not a legal opinion and does not claim MWST or SIX QR-Rechnung compliance.

---

## Purpose

Describe how **CH** mandants should eventually differ from the live **Austria** RKSV/TSE path: MWST calculation and labels, Swiss UID validation, and QR-Rechnung (Swiss QR-bill) **payload** generation for IBAN accounts in Switzerland or Liechtenstein.

This document does **not** include bank submission or PDF QR rendering (Paket **21**).

---

## Status

**NOT production-ready. Domain wired (Paket 30-c); TSE/RKSV paths remain AT-only.**

| Item | State |
|------|--------|
| `CountryProfile` CH seed | Shipped (CHE-… UID, `MWST_CH`, `QR_RECHNUNG`) |
| `SwitzerlandTaxStrategy.CalculateTax` | 8.1 / 2.6 / 3.8 via CountryTaxType + `CartMoneyHelper`. `CH_KLEINUNTERNEHMER` / `TaxExempt` → 0%. Reverse charge and OSS throw. No VIES |
| `SwitzerlandInvoiceStrategy` disclosures / `InvoiceDocumentDto` | Shape (MWSTG keys) |
| `ProjectFiscalTaxSets` / `AllocateReceiptNumberAsync` | `NotImplementedException` |
| QR-Rechnung payload (SIX) | Shape: `IQrRechnungBuilder` returns IBAN / creditor / debtor / amount / currency / reference. CH/LI prefix only. No bank API |
| QR-Rechnung PDF / QR image | Not implemented (`BuildPdfAsync` throws) |
| Wiring into `InvoiceService` / `PaymentService` | Tax/invoice domain wired (Paket 30-c); TSE/RKSV paths remain AT-only |
| **Paket 21 — CH QR bank submit** | **NOT STARTED** |

Feature-flag gates: `Fiscal.MwstCh` (country-profile default **on** for CH), `EInvoicing.QrRechnung` (on when the profile lists `QR_RECHNUNG`). `Fiscal.RksvAt` stays **off** for CH tenants.

Simulated or fake CH fiscal modes are rejected at host startup (`CountryFiscalLockEvaluator`).

Super Admin may **create** a CH tenant. Do not take Production CH POS sales. Cutover: [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md).

---

## Planned Architecture

- Resolve CH from `CompanySettings.Country` and a CountryProfile. Do not enable RKSV or Austrian TSE for CH.
- MWST label and rate selection (standard, reduced, lodging) come from `CountryTaxType` seeds, not from AT `TaxTypes`.
- VAT-ID format and optional MWST suffix live in CountryProfile seeds (`IVatIdValidator`).
- QR-Rechnung: produce the Swiss QR-bill payload for CH/LI IBANs. Persist payload metadata when a table exists; do not call a bank until Paket 21. PDF/QR image generation is not implemented.
- Switzerland is not the EN 16931 EU default; see [`EINVOICING_EU.md`](EINVOICING_EU.md) only for contrast.

---

## Remaining gaps

See [`COUNTRIES.md`](COUNTRIES.md) §16. This stub owns **Paket 21** (QR bank submission + PDF/QR image).

---

## Open Questions

Answered for v1 in [`FISCAL_SWITZERLAND_QR_PLAN.md`](FISCAL_SWITZERLAND_QR_PLAN.md): spec **IG 2.3**; default reference **SCOR**; PDF via **QuestPDF + QRCoder**; no bank HTTP API.

Still open:

- Is the accommodation special rate in product scope? (CountryTaxType lodging 3.8% is seeded; POS catalog mapping is a later product choice.)
- Are language-specific UID suffixes in scope, or only the MWST suffix?

---

## Related Docs

- [`COUNTRIES.md`](COUNTRIES.md) — multi-country hub
- [`FISCAL_SWITZERLAND_QR_PLAN.md`](FISCAL_SWITZERLAND_QR_PLAN.md) — Paket 21 QR-bill plan (not implemented)
- [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md) — production country-layer apply order
- [`../AGENTS.md`](../AGENTS.md) — Country & Fiscal Regimes
- [`EINVOICING_EU.md`](EINVOICING_EU.md) — EU e-invoicing stub (not CH QR-Rechnung)
- `RKSV_*.md` — **Austria only**
