# RKSV prioritized action plan

**Date:** 2026-07-29  
**Phase:** **Simulation-first** (intentional)  
**Environment assumption:** `TseMode=Demo` / Soft TSE · `RKSV:Mode=Demo` · `FinanzOnline:UseSimulation=true`  
**Source:** [`RKSV_COMPLIANCE_ASSESSMENT.md`](RKSV_COMPLIANCE_ASSESSMENT.md)  
**Cutover:** [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md)  
**Sign-off (production):** [`RKSV_FINAL_VALIDATION_CHECKLIST.md`](RKSV_FINAL_VALIDATION_CHECKLIST.md)

> This plan is not a legal-certificate claim. **Simulation = development/test**; a “green” UI is not BMF production acceptance.  
> Estimates are rough **person-days (PD)**.

---

## 1. Context: why priorities changed

The current working environment is **intentional simulation**. In this phase:

| Do | Do not (until cutover) |
|----|------------------------|
| Soft TSE / Demo signatures, simulated FON responses | Force a Production Soft-TSE ban “now” |
| Mock/simulated SOAP client (configurable success/fail) | Require a live BMF `belegpruefung` close-out |
| Ausfall episode + FA UI + outbox mechanics (submit off) | Real Ausfall SOAP submit |
| DEP export + Prüftool CI | Claim “production ready” |
| May 2027 tracking (independent of simulation) | — |

Former **P0-1 / P0-2 / P0-3** (production-blocking packages) are reclassified in this phase as **P1/P2 or cutover gates**. Surfaces already in code (SOAP Real client, TSE prod validator, Ausfall episodes) **can be used in simulation / opened at cutover**; live BMF/prod lock stays on the cutover checklist.

---

## 2. Priority matrix (simulation-first)

### P0 — Immediate (gate for this phase)

| ID | Action | Estimate (PD) | Owner | Note |
|----|--------|---------------|-------|------|
| **P0-S1** | **Simulation Mode indicator** — clear “Simulation / Demo — not fiscal” signal on FA + POS + backend log + selected API responses | **3–5** | Backend, Frontend (FA+POS) | Stops a false “Verified / Production” reading. Existing TSE/RKSV demo flags should bind to one `isSimulation` read model. |
| **P0-S2** | **Production Cutover Checklist** — Soft TSE off, real SCU, `UseSimulation=false`, Real SOAP, Ausfall submit on, FON registration, smoke tests | **2–3** | Ops, Compliance, Backend | ✅ Doc: [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md). FON-specific extra: [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md). |

**P0 subtotal (simulation phase):** ~**5–8 PD**.

### P1 — High (progress in simulation; live submit at cutover)

| ID | Action | Estimate (PD) | Owner | Note |
|----|--------|---------------|-------|------|
| **P1-3** | **May 2027 Signaturkarte** program (banner, list, milestone) | **5–8** | Compliance, Ops, Backend, Frontend | **Independent** of simulation — time-boxed. If code surface exists, verify + close gaps. [`MAI_2027_SIGNATURKARTE_PLAN.md`](MAI_2027_SIGNATURKARTE_PLAN.md) |
| **P1-A** | **Ausfall episode + FA UI + outbox skeleton** — real FON submit **off** (`AutoEnqueue=false` / simulation gate) | **8–12** | Backend, Frontend, Compliance | Simulation slice of former P0-3. Live Ausfall → cutover. [`AUSFALL_BENACHRICHTIGUNG_PLAN.md`](AUSFALL_BENACHRICHTIGUNG_PLAN.md) |
| **P1-F** | **Simulated / mock Sonderbeleg FON client** — configurable success/fail; Fake-in-prod ban design at cutover | **4–8** | Backend | Simulation slice of former P0-1. Real SOAP is a **cutover package** (P1-C / P2 design + cutover apply). |
| **P1-1** | Monatsbeleg FON **NotRequired** decision + FA note | **1–2** (remaining) | Compliance, Frontend | ✅ Decision doc exists; verify UI. [`MONATSBELEG_FINANZONLINE_DECISION.md`](MONATSBELEG_FINANZONLINE_DECISION.md) |
| **P1-2** | Outbox Mode ambient/config (no TEST constant) | **1–2** | Backend, Ops | Correct label in simulation too. |
| **P1-4** | Signaturkarte runbook + fleet / i18n | **4–6** | Ops, Backend, Frontend | After P1-3 |

**Former production P0s (reclassified):**

| Old ID | New place | Rationale |
|--------|-----------|-----------|
| P0-1 Real SOAP + BMF E2E | **Cutover (P1-C) + P2 design** | No real SOAP in simulation |
| P0-2 TSE Production Lock | **P2-L + cutover** | Soft TSE is accepted in this phase; lock at banner + cutover |
| P0-3 Live Ausfall submit | **Cutover** | Episode/UI = P1-A; SOAP submit = cutover |

### P2 — Medium (quality + cutover prep)

| ID | Action | Estimate (PD) | Owner | Note |
|----|--------|---------------|-------|------|
| **P2-1** | DEP Prüftool CI (`-UseFixtures` + seeded smoke) | **3–5** | Backend, Ops | DEP works in simulation; CI is required. |
| **P2-2** | Pre-F5 legacy JWS warning | **2–4** | Backend, Frontend | Important for Soft TSE legacy payload risk. |
| **P2-3** | Empty `Signaturzertifikat` hard-fail | **1–2** | Backend | Simulation export quality. |
| **P2-L** | TSE Production Lock **design + cutover step** (validator / health) — Soft TSE stays on in simulation | **3–5** | Backend, Ops | Former P0-2 demoted. Soft TSE off = cutover. [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) |
| **P2-S** | Real SOAP client **design / skeleton** — deploy and BMF E2E **at cutover** | **6–10** (design) | Backend | Early design of former P0-1; no live submit. |
| **P2-4** | Post-cutover assessment / AI docs sync | **1–2** | Compliance, Backend | After cutover signature. |

---

## 3. Suggested sequence (simulation-first)

```text
Phase S0 (Week 1) — visibility + cutover document
  P0-S1  Simulation Mode indicator (FA + POS + API/logs)
  P0-S2  Production Cutover Checklist (this repo doc + Ops ownership)

Phase S1 (Weeks 1–4) — compliance in simulation
  P1-3   May 2027 (parallel, independent)
  P1-A   Ausfall episode + FA (submit off)
  P1-F   Mock/simulated FON Sonderbeleg client
  P1-1 / P1-2  remaining verification

Phase S2 (Weeks 3–6) — DEP quality
  P2-1   Prüftool CI
  P2-2 / P2-3  legacy JWS + empty cert
  P2-S   Real SOAP design notes (implementation at cutover)

Phase C — Production Cutover (separate gate; S0–S2 green + Compliance)
  Soft TSE / Demo / UseSimulation off
  Real SCU + Real SOAP + Ausfall submit on
  P2-L lock ValidateOnStart in Production
  FINANZONLINE + RKSV cutover checklist signatures
```

---

## 4. Role matrix

| Role | Simulation phase | Cutover |
|------|------------------|---------|
| **Backend** | Indicator API, mock FON, Ausfall episode (no send), DEP CI | Real SOAP, prod lock, Ausfall send |
| **Frontend FA/POS** | Simulation banner; Ausfall/2027 UI | Prod banners; no Soft TSE |
| **Ops** | Cutover checklist ownership; sim env documentation | Credentials, SCU, `UseSimulation=false` |
| **Compliance** | May 2027; Monatsbeleg NotRequired; Ausfall “submit off” policy | Live FON / Ausfall approval |

---

## 5. Progress tracking

### P0 (simulation-first)

- [ ] P0-S1 Simulation Mode indicator (FA + POS + backend/API)
- [x] P0-S2 Production Cutover Checklist document ([`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md))

### P1

- [ ] P1-3 May 2027 (verify / gaps; checklist if code exists)
- [ ] P1-A Ausfall episode + FA, submission disabled
- [ ] P1-F Mock/simulated Sonderbeleg FON client
- [x] P1-1 Monatsbeleg NotRequired decision
- [ ] P1-2 Mode ambient (verify)
- [ ] P1-4 Signaturkarte runbook + fleet/i18n

### P2

- [ ] P2-1 DEP Prüftool CI
- [ ] P2-2 Legacy JWS warning
- [ ] P2-3 Empty Signaturzertifikat hard-fail
- [ ] P2-L TSE Production Lock (active at cutover)
- [ ] P2-S Real SOAP design (deploy at cutover)
- [ ] P2-4 Post-cutover docs

### Cutover gate (production)

- [ ] [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md) signed by Ops+Compliance
- [ ] [`RKSV_FINAL_VALIDATION_CHECKLIST.md`](RKSV_FINAL_VALIDATION_CHECKLIST.md) required items

> Note: The repo may already contain production-priority P0–P2 code surfaces. This plan defines **simulation-phase priorities**; until cutover, Soft TSE and `UseSimulation=true` are an **intentional accept**. Existing Real SOAP / prod lock / Ausfall code counts as “early delivery” — **live BMF/prod opening** still depends on cutover.

---

**Related:** [`RKSV_IMPLEMENTATION_READINESS.md`](RKSV_IMPLEMENTATION_READINESS.md) · [`RKSV_COMPLIANCE_ASSESSMENT.md`](RKSV_COMPLIANCE_ASSESSMENT.md) · [`TSE_PRODUCTION_CONFIG_LOCK.md`](TSE_PRODUCTION_CONFIG_LOCK.md) · [`FINANZONLINE_PROD_CUTOVER_CHECKLIST.md`](FINANZONLINE_PROD_CUTOVER_CHECKLIST.md)

**Last updated:** 2026-07-29 — **simulation-first** re-prioritization (P0-S1/S2; former P0-1/2/3 demoted).
