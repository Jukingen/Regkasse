> **Status:** Stub. Not production-ready. No code implemented yet.

# Fiscal Germany (KassenSichV)

**Last updated:** 2026-09-16  
**Hub:** [`COUNTRIES.md`](COUNTRIES.md) · **Rules:** [`../AGENTS.md`](../AGENTS.md)

This page is a placeholder for future Germany cash-register and e-invoicing work. It is not a legal opinion and does not claim KassenSichV, DSFinV-K, ZUGFeRD, or XRechnung compliance.

---

## Purpose

Describe how **DE** mandants should eventually differ from the live **Austria** RKSV/TSE path: a separate KassenSicherheit module, pluggable technical security equipment (TSE) providers, and German e-invoicing options (ZUGFeRD and/or XRechnung).

Do not use this document to change Austrian payment, TSE, or FinanzOnline behavior.

---

## Status

**NOT production-ready. No code implemented yet.**

| Item | State |
|------|--------|
| `CountryProfile` DE seed | Planned — see [`COUNTRIES.md`](COUNTRIES.md) |
| KassenSicherheit / DE TSE providers | Planned skeleton only |
| ZUGFeRD / XRechnung | Planned; EU EN 16931 relationship in [`EINVOICING_EU.md`](EINVOICING_EU.md) (stub) |
| Wiring into `PaymentService` | Out of scope for this stub |

Planned feature-flag gates (not in code): `Fiscal.KassenSicherheitDe`, `EInvoicing.Zugferd`, `EInvoicing.XRechnung`. `Fiscal.RksvAt` must stay **off** for DE tenants.

Simulated or fake DE signing, when it exists, must fail closed outside Development, in the same spirit as the Austrian TSE production lock. This document does not name a required vendor.

---

## Planned Architecture

- Resolve DE from `CompanySettings` country fields and a CountryProfile. Do not enable RKSV special receipts or Austrian TSE for DE.
- A KassenSicherheit facade with extension points for device provisioning and signing. Provider choice is a later product decision.
- Signature-chain state for DE must not reuse the Austrian RKSV chain table as if it were the same legal instrument.
- E-invoicing: ZUGFeRD and/or XRechnung as profile-driven options. Shared EN 16931 semantics belong in [`EINVOICING_EU.md`](EINVOICING_EU.md); this stub does not pick a single syntax.
- Do not attach DE signing to `PaymentService` until a dedicated implementation package is approved.

---

## Open Questions

- Which TSE provider(s) and certification path will the product support?
- Is ZUGFeRD, XRechnung, or both the default for DE mandants?
- How should Austrian `Tse:` configuration stay isolated from DE KassenSicherheit configuration?
- Which turnover or legal thresholds change what is mandatory for a given mandant?

---

## Related Docs

- [`COUNTRIES.md`](COUNTRIES.md) — multi-country hub
- [`../AGENTS.md`](../AGENTS.md) — Country & Fiscal Regimes
- [`EINVOICING_EU.md`](EINVOICING_EU.md) — EN 16931 / ViDA stub (no submission)
- `RKSV_*.md` — **Austria only**; do not treat as German fiscal law
