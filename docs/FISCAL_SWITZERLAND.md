> **Status:** Stub. Not production-ready. No code implemented yet.

# Fiscal Switzerland (MWST / QR-Rechnung)

**Last updated:** 2026-09-16  
**Hub:** [`COUNTRIES.md`](COUNTRIES.md) · **Rules:** [`../AGENTS.md`](../AGENTS.md)

This page is a placeholder for future Switzerland VAT and QR-bill work. It is not a legal opinion and does not claim MWST or SIX QR-Rechnung compliance.

---

## Purpose

Describe how **CH** mandants should eventually differ from the live **Austria** RKSV/TSE path: MWST calculation and labels, Swiss UID validation, and QR-Rechnung (Swiss QR-bill) **payload** generation for IBAN accounts in Switzerland or Liechtenstein.

This stub does **not** include bank submission or PDF QR rendering.

---

## Status

**NOT production-ready. No code implemented yet.**

| Item | State |
|------|--------|
| `CountryProfile` CH seed | Planned — see [`COUNTRIES.md`](COUNTRIES.md) |
| MWST calculator / CHE UID validator | Planned |
| QR-Rechnung payload (SIX) | Planned; no bank API |
| Wiring into `InvoiceService` / `PaymentService` | Out of scope for this stub |

Planned feature-flag gates (not in code): `Fiscal.MwstCh`, `EInvoicing.QrRechnung`. `Fiscal.RksvAt` must stay **off** for CH tenants.

---

## Planned Architecture

- Resolve CH from company country fields and a CountryProfile. Do not enable RKSV or Austrian TSE for CH.
- MWST label and rate selection (standard, reduced, Kleinunternehmer exemption) come from CountryProfile / `VatRegime`. This stub does **not** freeze numeric rates.
- VAT-ID format and optional MWST suffix live in CountryProfile seeds, not in scattered form regexes.
- QR-Rechnung: produce the Swiss QR-bill payload for CH/LI IBANs and a reference type (QRR, SCOR, or NON). Reference default is an open question. Persist payload metadata when a table exists; do not call a bank.
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
