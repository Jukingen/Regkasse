# RKSV final validation checklist (sign-off)

**Date:** 2026-07-29  
**Purpose:** Operational / BMF validation and production sign-off after P0–P2 code improvements.  
**Sources:** [`RKSV_COMPLIANCE_ASSESSMENT.md`](RKSV_COMPLIANCE_ASSESSMENT.md) · [`RKSV_ACTION_PLAN.md`](RKSV_ACTION_PLAN.md)

> This checklist is **not** a legal certificate. Attach **evidence** for every item (screenshot, log, ticket, CI run URL, cutover form). Sign-off: Ops + Compliance (+ Backend lead optional).

---

## 1. Code surface (reference — 2026-07-29)

| Package | Code status | Detail doc |
|---------|-------------|------------|
| P0-1 Sonderbeleg SOAP | ✅ | `FINANZONLINE_SOAP_IMPLEMENTATION_PLAN.md` |
| P0-2 TSE Production Lock | ✅ | `TSE_PRODUCTION_CONFIG_LOCK.md` |
| P0-3 Ausfallmeldung | ✅ | `AUSFALL_BENACHRICHTIGUNG_PLAN.md` |
| P1-1 Monatsbeleg NotRequired | ✅ | `MONATSBELEG_FINANZONLINE_DECISION.md` |
| P1-2 Enqueue Mode | ✅ | Action plan |
| P1-3 May 2027 | ✅ | `MAI_2027_SIGNATURKARTE_PLAN.md` |
| P1-4 Signaturkarte runbook/fleet | ⬜ Open | — |
| P2-1 DEP Prüftool CI | ✅ | `DEP_EXPORT_DEVELOPMENT.md`, `dep-prueftool.yml` |
| P2-2 Legacy JWS warning | ✅ | `DEP_EXPORT_DEVELOPMENT.md` § Legacy JWS |
| P2-3 Empty Signaturzertifikat | ✅ | `RKSV_DEP_EXPORT_MISSING_CERTIFICATE` |

---

## 2. Validation checklist (to check)

### P0 — Production safety and FON

- [ ] **TSE Production Lock:** Production (or prod-like Staging) blocks Soft TSE / `TseMode=Off` / fake signing; `/health/tse/mode` returns the expected result; FA “demo fiscal” banner appears only in an unsuitable mode.
  - **Evidence:** health JSON + config snippet (secrets masked) + FA screenshot
  - **Owner:** Ops + Backend

- [ ] **SOAP Sonderbeleg (BMF TEST):** Startbeleg and Jahresbeleg go to the real **BMF TEST** `belegpruefung` path; outbox → Verified (or an accepted terminal state); Fake client is not used in Production.
  - **Evidence:** outbox row IDs, FO response summary, signed TEST section of `FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`
  - **Owner:** Ops + Compliance (+ Backend)

- [ ] **Ausfallmeldung:** TSE failover / down scenario shows an Ausfall episode or FA `/admin/tse/ausfall` **suggestion**; (per policy) outbox enqueue or manual submit path verified.
  - **Evidence:** failover drill note + FA screenshot / episode ID
  - **Owner:** Ops + Compliance

### P1 — Compliance policy

- [ ] **May 2027:** Super Admin banner / program page (`/admin/tse/signaturkarte-program`) is visible; milestone reminder (activity/email) can fire in test.
  - **Evidence:** FA screenshot + reminder log/activity
  - **Owner:** Compliance + Ops

- [ ] **Monatsbeleg:** No separate FON outbox (**NotRequired**); FA Sonderbelege shows `MonatsbelegInfoCard` + [`MONATSBELEG_FINANZONLINE_DECISION.md`](MONATSBELEG_FINANZONLINE_DECISION.md); December → Jahresbeleg path works.
  - **Evidence:** FA screenshot + (optional) December Jahresbeleg FO row
  - **Owner:** Compliance + Frontend spot-check

### P2 — DEP quality

- [ ] **DEP Prüftool CI:** Latest `main`/`PR` run of `.github/workflows/dep-prueftool.yml` is **green** (fixture `-UseFixtures` + `Category=DepPrueftool`).
  - **Evidence:** GitHub Actions run URL
  - **Owner:** Backend / Ops

- [ ] **Legacy JWS:** Export that contains a pre-F5 (JSON payload) signature shows the FA warning Alert and/or envelope `legacyJwsCount` > 0; history “Prüftool-kompatibel: Nein”.
  - **Evidence:** FA screenshot or API envelope JSON
  - **Owner:** Backend + Frontend spot-check

- [ ] **Empty certificate hard-fail:** Known missing-thumbprint group → DEP export **HTTP 500** `RKSV_DEP_EXPORT_MISSING_CERTIFICATE` (or service exception); empty `Signaturzertifikat` is **absent** from JSON.
  - **Evidence:** API error body or green unit-test CI + manual negative-test note
  - **Owner:** Backend

### Optional / remaining

- [ ] **P1-4** Signaturkarte renewal runbook + fleet “X days” + i18n
- [ ] **BMF PROD** Start/Jahres cutover (`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md` PROD section)
- [ ] **`ai/05_SECURITY_COMPLIANCE.md`** row update after cutover

---

## 3. Go / No-Go recommendation (2026-07-29)

### Software / platform (P0–P2 code)

| Decision | **CONDITIONAL GO** |
|----------|---------------------|
| Meaning | Product code is a **merge/release candidate** for the RKSV core + P0–P2 improvements; ready for staging validation. |
| Conditions | Soft TSE must stay off in prod; FON ClientKind=Real + credentials; Ausfall policy communicated to operators. |

### Full RKSV production / official “production ready” claim

| Decision | **NO-GO** |
|----------|-----------|
| Meaning | Do not sign a **Betriebsprüfung / “full FON production compliant”** claim today. |
| Blockers | (1) BMF TEST Start/Jahres E2E evidence missing or unchecked here; (2) live Ausfall drill missing; (3) prod TSE lock not signed by Ops; (4) P1-4 runbook open; (5) BMF PROD cutover is separate. |

### When is full **GO**?

When every **required** item in section 2 (TSE Lock, SOAP BMF TEST, Ausfall drill, May 2027, Monatsbeleg, DEP CI, Legacy JWS, empty cert) is **checked + evidenced** and signed by Compliance + Ops:

1. Write **GO — Production candidate (FON TEST validated)** on this document.
2. Sign `FINANZONLINE_PROD_CUTOVER_CHECKLIST.md` separately for PROD cutover → **GO — Production FON**.
3. Update [`RKSV_COMPLIANCE_ASSESSMENT.md`](RKSV_COMPLIANCE_ASSESSMENT.md) §5 Result table.

---

## 4. Sign-off

| Role | Name | Date | Signature / approval |
|------|------|------|----------------------|
| **Ops** | | | ☐ |
| **Compliance** | | | ☐ |
| **Backend lead** (optional) | | | ☐ |
| **Product / Super Admin** (optional) | | | ☐ |

**Decision box (check one):**

- [ ] **NO-GO** — no production claim; code/staging only
- [ ] **CONDITIONAL GO** — software OK; BMF TEST + drill done; not PROD FON yet
- [ ] **GO — Production candidate** — section 2 required items + Ops/Compliance signed
- [ ] **GO — Production FON** — plus PROD cutover checklist signed

---

**Last updated:** 2026-07-29 — first final validation checklist (after P0–P2 code close).
