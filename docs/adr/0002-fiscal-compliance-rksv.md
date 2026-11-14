# ADR 0002: Fiscal compliance (RKSV / TSE)

- **Status:** Accepted
- **Date:** 2026-11-14

## Context

Austrian cash registers that fall under the Registrierkassensicherheitsverordnung (RKSV) must sign each receipt with a technical security system (TSE) and keep a signature journal for tax audit. Regkasse sells that cash-register product in Austria. This note is an engineering decision, not a legal opinion. Country packs for Germany, Switzerland, and EU e-invoicing stay separate and are not a substitute for the Austrian chain.

## Decision

AT production receipts go through the existing signature chain. Each new signature is linked to the previous value for that cash register so a gap is detectable. Receipt numbers stay monotonic per register. Special receipts (Startbeleg, Nullbeleg, Monatsbeleg, Jahresbeleg, Schlussbeleg) and Tagesabschluss use that same chain. They are not a second ledger.

The audit artifact is the DEP §7 export (Datenerfassungsprotokoll): a BMF JSON journal of receipts and signatures for one register and a UTC period. That export is not the operational CSV/Excel download in [`../EXPORT_GUIDE.md`](../EXPORT_GUIDE.md). The operational `tse_signatures` file lists receipt number, time, and TSE device id only. It does not contain the JWS.

Signing, receipt numbering, and DEP generation stay in the current fiscal services. This ADR does not change them.

## Consequences

- Payments, receipts, and TSE rows are kept for the retention window even when other tenant data is purged.
- A change to the chain, receipt numbers, or DEP shape needs the fiscal review path. Do not add a parallel signer.
- Offline TSE intents and offline order snapshots are two queues. Replaying either must not fork the chain.
- Demo versus production RKSV mode is runtime configuration. A demo label is not a signed production receipt.

See [`../DEP_EXPORT_DEVELOPMENT.md`](../DEP_EXPORT_DEVELOPMENT.md) and [`../RKSV_CASH_REGISTER_OPERATIONS.md`](../RKSV_CASH_REGISTER_OPERATIONS.md).
