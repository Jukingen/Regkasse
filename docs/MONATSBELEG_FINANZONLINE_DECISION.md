# Monatsbeleg → FinanzOnline — Decision (P1-1)

**Date:** 2026-07-29  
**Action:** [`RKSV_ACTION_PLAN.md`](RKSV_ACTION_PLAN.md) → **P1-1**  
**Status:** ✅ **NotRequired** — no separate FON `belegpruefung` / outbox for January–November Monatsbeleg  
**Warning:** This is a **product decision** based on Compliance plus BMF primary sources; it is not legal advice. If official texts conflict, they win.

---

## 1. Question

Must a Monatsbeleg be submitted to FinanzOnline (rkdb `belegpruefung` / Belegcheck) **in addition** to Startbeleg/Jahresbeleg, or is the December Monatsbeleg = Jahresbeleg path enough?

---

## 2. Research summary (BMF / WKO / RKSV)

| Source | Relevant point |
|--------|----------------|
| **RKSV § 8 Abs. 3** (WKO summary) | At the end of each calendar year, a **Monatsbeleg (Jahresbeleg)** that includes the year-end counter must be printed, **checked**, and retained per § 132 BAO. |
| [WKO — Prüfung des Jahresbelegs](https://www.wko.at/steuern/pruefung-jahresbeleg-registrierkasse) | FON Belegcheck is mandatory for **Startbeleg** and **Jahresbeleg**; the deadline is typically **15 February of the following year**. The cash-register system may submit automatically via webservice. |
| [BMF Handbuch Registrierkassen](https://www.bmf.gv.at/dam/jcr:0af97a40-da60-4c81-8e1e-22c3ecca52a4/BMF_Handbuch_Registrierkassen.pdf) | At year end a Monatsbeleg (= Jahresbeleg) is created and checked. Belegcheck App / webservice can be used for general Belegprüfung; **mandatory FON submit of every monthly Monatsbeleg** is not stated as binding as Jahresbeleg/Startbeleg. |
| Operator summaries (for example branch/register guides) | Monatsbeleg: **create + store in DEP**; FON check is **recommended**, **mandatory for Jahresbeleg**. |

**Technical equivalence:** In Regkasse, a December Monatsbeleg request is already routed to the **Jahresbeleg** production path (`RksvSpecialReceiptService`); Jahresbeleg is tracked in the FON outbox.

---

## 3. Decision (Compliance product policy)

| Kind | In-register production (TSE signature + DEP) | FinanzOnline Belegcheck / rkdb `belegpruefung` outbox |
|------|----------------------------------------------|------------------------------------------------------|
| **Monatsbeleg** (January–November) | ✅ Required (RKSV monthly check) | ❌ **NotRequired** — no separate automatic submit |
| **Jahresbeleg** (= December Monatsbeleg) | ✅ | ✅ Same path as Startbeleg (`RksvJahresbelegSubmission`) |
| **Startbeleg** | ✅ | ✅ |

**Rationale:** Binding FON Belegprüfung is, in practice, clear for **Startbeleg** and **Jahresbeleg**. Monthly Monatsbeleg receipts are produced for DEP integrity and business control; submitting every month to FON is **not implemented as an extra Regkasse obligation**. December is covered via Jahresbeleg.

**Manual option:** An operator may check any Monatsbeleg QR with the BMF Belegcheck App; that check is not written to the Regkasse outbox.

**Re-evaluation triggers:** BMF/RKSV text change; a mandant tax advisor request; a written Compliance “submit all months” policy → then reverse P1-1: `SubmitMonatsbelegAsync` + outbox.

---

## 4. Implementation impact

| Layer | Behavior |
|-------|----------|
| Backend | Creating a Monatsbeleg **does not enqueue** the outbox (current). `SubmitMonatsbelegAsync` → `RKS_MONATSBELEG_NOT_REQUIRED`. |
| FA Sonderbelege | `MonatsbelegInfoCard` — NotRequired + BMF/WKO links |
| FA receipt detail | Monatsbeleg is not FO “tracked”; info note only |
| Assessment | P1-1 closed; risk row updated to “NotRequired” |

---

## 5. References

- [`docs/RKSV_OFFICIAL_SOURCES.md`](RKSV_OFFICIAL_SOURCES.md)  
- [WKO Jahresbelegprüfung](https://www.wko.at/steuern/pruefung-jahresbeleg-registrierkasse)  
- [BMF Handbuch Registrierkassen (PDF)](https://www.bmf.gv.at/dam/jcr:0af97a40-da60-4c81-8e1e-22c3ecca52a4/BMF_Handbuch_Registrierkassen.pdf)  
- [`docs/RKSV_CASH_REGISTER_OPERATIONS.md`](RKSV_CASH_REGISTER_OPERATIONS.md) §4.3  
- [`docs/RKSV_BMF_BELEGCHECK_WORKFLOW.md`](RKSV_BMF_BELEGCHECK_WORKFLOW.md)

**Last updated:** 2026-07-29 — P1-1 decision: **NotRequired** (no separate Monatsbeleg FON outbox).
