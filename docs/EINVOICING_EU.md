> **Status:** Stub. Not production-ready. No code implemented yet.

# EU e-invoicing (EN 16931) and ViDA readiness

**Last updated:** 2026-09-16  
**Hub:** [`COUNTRIES.md`](COUNTRIES.md) · **Rules:** [`../AGENTS.md`](../AGENTS.md)

This page is a placeholder for a generic EU e-invoicing layer used when a mandant has no country-specific invoice strategy. It is not a legal opinion, not Peppol onboarding, and **not submission-ready**.

---

## Purpose

Describe a default for EU-oriented mandants: EN 16931 semantic invoicing, optional Peppol as a later transport/CIUS choice, VIES as an optional B2B VAT-ID check, and a **read-only** ViDA readiness report.

This stub does **not** implement tax-authority or network submission.

---

## Status

**NOT production-ready. No code implemented yet. Not submission-ready.**

| Item | State |
|------|--------|
| `EU_DEFAULT` CountryProfile | Planned; **registry-only**, not selectable as a country in the wizard — [`COUNTRIES.md`](COUNTRIES.md) |
| EN 16931 invoice builder | Planned; syntax (UBL vs CII) not locked here |
| Peppol Access Point | Not in scope |
| VIES client | Planned; default off; no live VIES in tests |
| ViDA | Read-only checklist only; no timeline committed |

Planned gates (not in code): `EInvoicing.En16931`, `Vies.CheckEnabled` (default **off**).

Austria RKSV receipts and German ZUGFeRD/XRechnung are **not** defined here. See `RKSV_*.md` and [`FISCAL_GERMANY.md`](FISCAL_GERMANY.md).

---

## Planned Architecture

- `EU_DEFAULT` is a fallback profile, not an ISO country code. Real mandants keep an ISO alpha-2 code (or AT backfill).
- Tax and invoice strategies use `VatRegime` (including OSS and reverse charge) without forking `PaymentService`.
- EN 16931 is the semantic target. UBL vs CII is an open question; this stub does not require Peppol.
- VIES: mockable client behind `Vies.CheckEnabled`. Never call the live network from unit tests.
- ViDA: a read-only readiness object (planned fields: `en16931Ready`, `viesEnabled`, `ossRegistered`, `eInvoicingCapable`). No submission API. This document does not assign a go-live date.

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
