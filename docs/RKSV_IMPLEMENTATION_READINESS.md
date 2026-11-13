# RKSV Implementation Readiness — Go / No-Go (Simulation-first)

**Date:** 2026-07-29  
**Phase:** **Simulation-first**  
**Environment assumption:** Soft TSE / `TseMode=Demo` · `RKSV:Mode=Demo` · `FinanzOnline:UseSimulation=true` (intentional)

**Plan:** [`RKSV_ACTION_PLAN.md`](RKSV_ACTION_PLAN.md)  
**Cutover:** [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md)  
**Production sign-off:** [`RKSV_FINAL_VALIDATION_CHECKLIST.md`](RKSV_FINAL_VALIDATION_CHECKLIST.md)  
**Assessment:** [`RKSV_COMPLIANCE_ASSESSMENT.md`](RKSV_COMPLIANCE_ASSESSMENT.md)

> This document is decision support. It is not a BMF certificate or legal approval.  
> Soft TSE is **accepted** in simulation; Production Soft TSE is **banned at cutover**.

---

## Executive decision

### **Decision: GO — Simulation phase** · **NO-GO — Production / BMF live**

| Package (new ID) | Go/No-Go | Rationale |
|-----------------|----------|---------|
| **P0-S1** Simulation indicator | **GO — now** | Cuts operator false-confidence risk; compatible with Soft TSE |
| **P0-S2** Production Cutover Checklist | **GO — now** | Collects Soft TSE shutdown + Real SOAP + Ausfall enablement behind one gate |
| **P1-3** May 2027 | **GO — parallel** | Independent of simulation; time-critical |
| **P1-A** Ausfall episode + FA (no send) | **GO — in simulation** | Learn the mechanics; live FON at cutover |
| **P1-F** Mock/simulated FON Sonderbeleg | **GO — in simulation** | Real SOAP at cutover |
| **P2-1** DEP Prüftool CI | **GO — in simulation** | DEP can be tested with Soft TSE |
| **P2-L** TSE Production Lock | **GO — design; NO-GO force prod now** | Soft TSE is intentional in this phase; lock is a cutover step |
| **P2-S / Cutover Real SOAP** | **GO — design; NO-GO live send now** | Real BMF is not required while `UseSimulation=true` |
| **Live Ausfall SOAP** | **NO-GO until cutover** | Episode OK; send = cutover |

**One-shot “Production fiscal ready” announcement:** **No-Go** — the environment is intentionally simulation.

**Minimum this week:** P0-S1 kickoff + P0-S2 checklist ownership (Ops) + continue P1-3.

---

## 1. Technical feasibility

### 1.1 Simulation-first principles

1. **Visibility over early prod lock** — A clear “Simulation” banner is mandatory while Soft TSE is on.  
2. **Mechanics in simulation, wire at cutover** — outbox/episode/UI yes; real BMF network call no (gate).  
3. **DEP quality in simulation** — Prüftool CI is meaningful with Soft TSE fixtures.  
4. **Single cutover gate** — Turn Soft TSE off + Real SCU + `UseSimulation=false` + Real SOAP + Ausfall send.

### 1.2 Package feasibility

| Package | Feasibility | Difficulty | Note |
|-------|------------|--------|-----|
| P0-S1 Indicator | High | Low–Medium | Combined `RKSV:Mode` / `TseMode` / `UseSimulation` flag; FA + POS i18n |
| P0-S2 Cutover doc | High | Low | Merge existing FON cutover + TSE lock docs |
| P1-3 Mai 2027 | High | Low–Medium | Independent |
| P1-A Ausfall (no send) | High | Medium | Episode + FA; `AutoEnqueue`/send gate |
| P1-F Mock FON | High | Low–Medium | Fake client already exists; clarify configurable fail path |
| P2-1 DEP CI | High | Medium | JDK 17 + JAR ensure |
| P2-L Prod lock | High | Low | Code/docs exist; **Production ValidateOnStart** at cutover |
| Real SOAP (cutover) | Medium–High | High | External dependency: BMF TEST credentials |

### 1.3 Known alignment notes (repo)

- Soft TSE / Demo / `UseSimulation=true` is **currently intentional** — mark it on every surface via P0-S1.  
- Real SOAP client / TSE prod validator / Ausfall episodes **exist in code**; they do not mean “live production fiscal” in this phase.  
- At cutover, verify P2-L + Real SOAP + Ausfall send + Soft TSE shutdown together.

---

## 2. Time and staffing

### 2.1 Person-days (simulation-phase focused)

| ID | PD (approx.) |
|----|----------------|
| P0-S1 Simulation indicator | 3–5 |
| P0-S2 Cutover checklist (document + Ops walkthrough) | 2–3 |
| P1-3 May 2027 | 5–8 |
| P1-A Ausfall (no send) | 8–12 |
| P1-F Mock FON | 4–8 |
| P2-1 DEP CI | 3–5 |
| P2-2 / P2-3 | 3–6 |
| P2-S Real SOAP design | 6–10 |
| **Simulation-phase subtotal** | **~34–57 PD** |

**Cutover package** (separate; after BMF credentials): Soft TSE shutdown + Real SOAP E2E + Ausfall send + prod lock — roughly **+15–30 PD** (Ops/Compliance heavy).

### 2.2 Staffing

| Role | Simulation phase |
|-----|-----------------|
| Backend 1 | Indicator, mock FON, Ausfall episode, DEP CI |
| FA 0.5 + POS 0.25 | Simulation banner |
| Ops 0.25 | Cutover checklist ownership |
| Compliance 0.25 | 2027 + “send closed” policy |

Calendar (parallel): **~6–10 weeks** simulation phase; cutover is a separate window.

---

## 3. Risks

| Risk | Impact | Mitigation |
|------|------|---------|
| Soft TSE perceived as “production-like” | Legal / customer false confidence | **P0-S1** mandatory banner + API `isSimulation` |
| Early Real SOAP / Fake Verified | False confidence | Simulation client until cutover; Fake prod ban at cutover |
| Accidental send even in simulation Auto-Ausfall | FON pollution | Send gate / `UseSimulation` / `AutoEnqueue=false` |
| Incomplete cutover checklist | Soft TSE forgotten in prod | **P0-S2** + Ops signature |
| 2027 delay | Operational crisis | P1-3 in parallel, independent of simulation |
| No DEP CI | Format regression | P2-1 |

**Top three risks (this phase):** (1) missing visible simulation signal, (2) Soft TSE leaking into prod without cutover, (3) accidental live FON send.

---

## 4. Priority order

```text
Phase S0:  P0-S1 (banner) + P0-S2 (cutover doc)
Phase S1:  P1-3 ∥ P1-A ∥ P1-F
Phase S2:  P2-1 ∥ P2-2 ∥ P2-3 ∥ P2-S (design)
Phase C:   Production Cutover Checklist signature
         → Soft TSE off, Real SCU, UseSimulation=false,
           Real SOAP, Ausfall send, P2-L ValidateOnStart
```

### Intentional No-Go

| Condition | Effect |
|-------|------|
| Declare Soft TSE “prod ready” without a simulation banner | **No-Go production claim** |
| Market “BMF Verified production” while `UseSimulation=true` | **No-Go** |
| Deploy Soft TSE to Production without cutover | **No-Go leave P2-L forced open** — either Demo env or lock |
| Auto-send Ausfall before Compliance approves send | **No-Go Ausfall send** |

---

## 5. Go / No-Go summary (decision form)

| Question | Answer |
|------|--------|
| Start the simulation phase? | **GO** |
| Is Soft TSE OK in this phase? | **Yes (intentional)** + **P0-S1 mandatory** |
| Real BMF SOAP now? | **No — cutover** |
| Live Ausfall now? | **No — episode/UI yes, send at cutover** |
| May 2027 now? | **GO (P1-3)** |
| DEP CI now? | **GO (P2-1)** |
| Production fiscal ready? | **NO-GO** until cutover is signed |

### Sign-off / approval

| Role | Name | Date | Decision |
|-----|-----|-------|-------|
| Engineering lead | | | Simulation GO / NO-GO |
| Ops | | | Cutover checklist owner: Y / N |
| Compliance | | | Simulation policy + 2027: Y / N |
| Product | | | Staffing: Y / N |

---

## 6. First 10 working days (simulation backlog)

1. Unified `isSimulation` / demo flag contract (Backend)  
2. FA Simulation Mode banner (all protected surfaces)  
3. POS Simulation Mode indicator (cash register UI)  
4. `simulationMode` on API/health or `/me`-like response (optional but preferred)  
5. Structured log: `FiscalMode=Simulation`  
6. Ops: [`RKSV_PRODUCTION_CUTOVER_CHECKLIST.md`](RKSV_PRODUCTION_CUTOVER_CHECKLIST.md) walkthrough  
7. P1-3 May 2027 MVP validation / gap  
8. P1-A Ausfall episode list + FA, send disabled  
9. P1-F mock FON success/fail config  
10. P2-1 DEP Prüftool CI skeleton (JDK 17 + `-UseFixtures`)

---

**Last updated:** 2026-07-29 — **simulation-first** Go/No-Go; production lock and live FON are **cutover gates**.
