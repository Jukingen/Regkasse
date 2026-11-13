# RKSV production cutover checklist

**Date:** 2026-07-29  
**Purpose:** Steps to leave a deliberate **simulation** environment (`Soft TSE` / `RKSV:Mode=Demo` / `FinanzOnline:UseSimulation=true`) and enter **production fiscal** mode.  
**Plan:** [`RKSV_ACTION_PLAN.md`](RKSV_ACTION_PLAN.md) · **Readiness:** [`RKSV_IMPLEMENTATION_READINESS.md`](RKSV_IMPLEMENTATION_READINESS.md)  
**FON extra detail:** [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md)  
**TSE lock:** [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md)  
**Country layer (schema / flags / FA — not this AT fiscal switch):** [`COUNTRY_LAYER_CUTOVER.md`](COUNTRY_LAYER_CUTOVER.md)

> Do **not** claim “RKSV production compliant” or “BMF Verified production” until cutover is complete.  
> Do not write secrets into this file.

---

## 0. Prerequisites (simulation phase green)

- [ ] **P0-S1** Simulation Mode indicator live on FA + POS (+ API/logs) and understood
- [ ] DEP export + (where possible) Prüftool CI green
- [ ] May 2027 program surface (P1-3) at least banner/report
- [ ] Ausfall episode/FA (P1-A) verified in simulation; **send closed** documented
- [ ] Compliance + Ops approved the cutover window

---

## 1. TSE / SCU (Soft TSE → real)

- [ ] Soft TSE / Demo device inventory (which tenant/register)
- [ ] Real **Signaturerstellungseinheit** (for example Fiskaly) credential + `SignatureCreationUnitId`
- [ ] `TseMode` Production value: Device / vendor (Demo/Off/Fake **forbidden**)
- [ ] `TseProductionOptionsValidator` / `/health/tse/mode` returns the expected **fail-closed** result
- [ ] No escape hatch, or Compliance written approval exists
- [ ] Smoke: one test payment with a **real** compact JWS + thumbprint stamp

**Reference:** [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) · [`FISKALY_PRODUCTION_CUTOVER.md`](FISKALY_PRODUCTION_CUTOVER.md)

---

## 2. RKSV application mode

- [ ] `RKSV:Mode` (or equivalent) Demo → Production/Test per policy
- [ ] Simulation banner **closes** in Production or switches to a “Production” signal
- [ ] Legal notice / DEP `IsDemo` flags switch to production text

---

## 3. FinanzOnline

- [ ] `FinanzOnline:UseSimulation=false` (target environment)
- [ ] Webservice user (tid/benid/pin) — **TEST** first, then **PROD** (separate gates)
- [ ] Cash register + SCU registered in FON; AES / Benutzerschlüssel correct
- [ ] `RksvSubmission` **ClientKind=Real** (Fake forbidden in Production)
- [ ] Outbox Mode ambient = target environment (no TEST constant)
- [ ] Startbeleg + Jahresbeleg **belegpruefung** E2E (TEST first)

**Reference:** [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md)

---

## 4. Ausfallmeldung

- [ ] Episode + FA path verified in simulation
- [ ] Compliance: auto-enqueue policy (on/off) in writing
- [ ] Live Ausfall / Wiederinbetriebnahme submit **opened** (gate removed)
- [ ] Staging failover drill → episode + (per policy) outbox

**Reference:** [`AUSFALL_BENACHRICHTIGUNG_PLAN.md`](AUSFALL_BENACHRICHTIGUNG_PLAN.md)

---

## 5. DEP / Prüftool

- [ ] Production crypto material / per-register key process is known
- [ ] Leaf `Signaturzertifikat` hard-fail behavior smoked with prod SCU
- [ ] Legacy JWS inventory (if any) is known; Prüftool expectation managed
- [ ] CI `dep-prueftool` green (regression gate)

---

## 6. Operations / communication

- [ ] Simulation banner removed / Production message
- [ ] Cutover announcement to mandanten / internal team
- [ ] Rollback plan (`UseSimulation` back on; Soft TSE only on non-prod)
- [ ] [`RKSV_FINAL_VALIDATION_CHECKLIST.md`](RKSV_FINAL_VALIDATION_CHECKLIST.md) required items checked
- [ ] [`RKSV_COMPLIANCE_ASSESSMENT.md`](RKSV_COMPLIANCE_ASSESSMENT.md) §5 Result updated
- [ ] `ai/05_SECURITY_COMPLIANCE.md` cutover rows (P2-4)

---

## 7. Sign-off

| Role | Name | Date | Approval |
|------|------|------|----------|
| **Ops** | | | ☐ Soft TSE off + FON + SCU |
| **Compliance** | | | ☐ Policies + Ausfall send |
| **Backend lead** | | | ☐ Config + health |
| **Product** | | | ☐ Go-live |

**Environment:** ☐ BMF TEST cutover complete · ☐ BMF PROD cutover complete

---

**Last updated:** 2026-07-29 — simulation-first P0-S2 delivery.
