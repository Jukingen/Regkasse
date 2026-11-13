# Frontend Admin — Vision & Roadmap

**Package:** `frontend-admin/` (Regkasse Admin Panel / FA)  
**Audience:** Product, engineering, Super Admin ops  
**Last updated:** 2026-07-21  
**Next roadmap review:** **2026-10-21** (quarterly — see §6)  
**Horizon:** 12 months (2026-Q3 → 2027-Q2)

This document defines **why FA exists**, **where it is going**, and **how we prioritize** the next year.  
Implementation detail lives in linked docs (`TECHNICAL_DEBT.md`, `SECURITY_AUDIT.md`, `docs/*`); this file stays strategic.

---

## 1. Long-term vision

### 1.1 What problems FA solves

Regkasse is a multi-tenant, RKSV-aware POS platform for Austrian businesses. The POS app (`frontend/`) must stay fast and fiscal-safe at the till. Operators still need a **back office** that:

| Problem | Without FA | With FA |
| ------- | ---------- | ------- |
| **Tenant & license lifecycle** | Manual DB / support tickets | Super Admin provisions, licenses, soft-deletes, impersonates with audit |
| **People & access** | Spreadsheet roles, unclear permissions | Users, Mandanten-Admin vs Cashier matrix, Access & Roles hub |
| **Catalog & commercial config** | Ad-hoc API / POS-only edits | Products, categories, pricing, vouchers, payment methods |
| **Fiscal & compliance ops** | Opaque TSE/RKSV status | RKSV truth surfaces, DEP export, FinanzOnline ops, offline queues, Tagesabschluss visibility |
| **Backup & DR** | Ops-only scripts | Role-aware backup hub, Fake vs Real mode honesty, restore boundary UI |
| **Digital / GDPR / billing** | Fragmented tools | Website/app requests, online orders inbox, data-management rights, license sales |
| **Trust under pressure** | Guesswork during incidents | Truth badges, investigation navigation, monitoring & feedback loops |

FA is the **operator control plane**: configure, observe, export, and govern — **without** crossing into POS cart → payment → TSE signing.

### 1.2 Target users

| Persona | Backend role | Primary FA jobs |
| ------- | ------------ | --------------- |
| **Platform operator** | `SuperAdmin` | Tenants, licenses/billing, digital publish, cross-tenant DR, feedback inbox, monitoring, ELMAH |
| **Mandanten-Admin** | `Manager` | Own-tenant users, catalog, settings, reports, tenant backup, GDPR export/deletion request, digital *request* (not publish) |
| **Accountant / Report viewer** | `Accountant` / `ReportViewer` | Read-heavy fiscal & commercial reports (permission-gated) |
| **Support engineer** (internal) | Often Super Admin / impersonation | Diagnose RKSV/FO, read-only forensics, guided recovery |

**Not primary FA users:** Cashiers / Waiters at the till (POS), end customers (tenant websites).

Languages: Admin UI **de / en / tr**; explanations for developers follow repo language rules (`AGENTS.md`).

### 1.3 Key value proposition

> **One trusted admin surface** where Austrian POS operators and Regkasse platform staff can run multi-tenant business and **RKSV-critical operations** safely — with clear truth, tenant isolation (404 semantics), and no accidental POS/fiscal side effects.

Pillars:

1. **Compliance-aware UX** — RKSV / FinanzOnline / backup wording must not create false confidence.  
2. **Strict API boundaries** — FA ↔ `/api/admin/*` (+ Auth); never `/api/pos/*`.  
3. **Role-true navigation** — Mandanten-Admin sees tenant scope; Super Admin sees platform scope.  
4. **Operability** — CI/CD, monitoring, logging, onboarding so the panel stays shippable.

### 1.4 Non-goals (keep explicit)

- Replacing the POS UI or signing receipts in the browser.  
- Becoming a generic “SaaS admin template” disconnected from Austrian fiscal reality.  
- Parallel state stores for API data (TanStack Query remains source of truth for server state).

---

## 2. Strategic goals (next 12 months)

Aligned to four themes. Each goal maps to quarterly milestones in §3.

### G1 — Feature improvements

| ID | Goal | Success signal |
| -- | ---- | -------------- |
| G1.1 | **RKSV / FO operator clarity** — complete truth-surface consistency, Monatsbeleg/reminder UX, offline systems separation in UI | Fewer support tickets on “is this real?”, checklist green in `docs/RKSV_*` |
| G1.2 | **Access & identity** — Access hub maturity, Mandanten vs Super Admin flows, 2FA UX polish for Super Admin | Role matrix docs match UI; impersonation hardened |
| G1.3 | **Digital + data rights** — Mandanten digital portal + Super Admin generators; GDPR request UX reliable | Happy path E2E for export; Delete approval visible |
| G1.4 | **Feedback → backlog** — Weekly triage of in-app feedback drives FA-TD / roadmap | Feedback statuses move; themes logged each quarter |

### G2 — Performance

| ID | Goal | Success signal |
| -- | ---- | -------------- |
| G2.1 | **Core Web Vitals** — hold LCP p75 ≤ 2.5s on key routes; INP/CLS budgets | Monthly review in `docs/PERFORMANCE_MONITORING.md` |
| G2.2 | **API & list UX** — fewer waterfall calls; virtualized heavy tables where needed | Dashboard overview endpoints used; FA-TD perf items closed |
| G2.3 | **Build & CI speed** — cache-friendly CI, leaner client bundles on critical paths | CI wall-clock trend down; `npm run analyze` regressions caught |

### G3 — Security

| ID | Goal | Success signal |
| -- | ---- | -------------- |
| G3.1 | **Session & token hygiene** — address FA-SEC-001/002 (storage / impersonation fragment) with backend partnership | Audit items closed or explicitly deferred with controls |
| G3.2 | **Proxy & CSP hardening** — fail-closed route lists; reduce `unsafe-inline`/`eval` where feasible | `SECURITY_AUDIT.md` quarterly score improves |
| G3.3 | **Dependency & supply chain** — Dependabot discipline; Orval upgrade path without force-audit | Critical runtime vulns = 0; generate-time debt tracked |

### G4 — User experience

| ID | Goal | Success signal |
| -- | ---- | -------------- |
| G4.1 | **Ant Design 6 consistency** — finish static `message`/`Modal.confirm` migration (`useNotify` / `useAntdApp`) | FA-TD-001 Done |
| G4.2 | **i18n completeness** — de/en/tr parity for operator-critical copy; no English-only fiscal warnings | Localization CI green; parity suites expand |
| G4.3 | **Onboarding & discoverability** — Getting Started, weekly sessions, in-app monitoring/feedback | Time-to-first-PR ↓ for new FA devs |
| G4.4 | **Honesty in Backup/DR & license UI** — Fake vs Real, grace/locked states never oversold | Backup DR honesty regressions stay green |

---

## 3. Quarterly milestones

Calendar assumes reviews on ~**21st of Jan / Apr / Jul / Oct**. Adjust if release trains shift.

### Q3 2026 (Jul–Sep) — *Stabilize & observe*

**Theme:** Lock in July modernization; make quality visible.

| Milestone | Goals | Priority | Feasibility | Notes |
| --------- | ----- | -------- | ----------- | ----- |
| **M1** Ship CI quality gate + deploy pipeline as default path | G2.3, G3.3 | P0 | High | `frontend-admin-ci.yml` / deploy docs |
| **M2** Monitoring + logging + Web Vitals alerts live in prod | G2.1, G4.3 | P0 | High | Sentry + `/health` uptime |
| **M3** Security audit follow-ups: plan FA-SEC-001/002; close quick wins | G3.1, G3.2 | P0 | Medium | Needs backend for full token model |
| **M4** Coverage gate ≥80% lines on logic; document testing strategy | G2.3 | P1 | High | `docs/TESTING.md` |
| **M5** Feedback widget weekly triage ritual | G1.4 | P1 | High | `docs/USER_FEEDBACK.md` |
| **M6** Start FA-TD-001 (Ant static API migration) on hottest routes | G4.1 | P1 | Medium | Large effort — bite-sized PRs |

### Q4 2026 (Oct–Dec) — *Trust & access*

**Theme:** Operator trust surfaces + identity hardening.

| Milestone | Goals | Priority | Feasibility | Notes |
| --------- | ----- | -------- | ----------- | ----- |
| **M7** RKSV truth-surface QA checklist green; offline systems clearly separated in nav/copy | G1.1 | P0 | Medium | Coupled with API when needed |
| **M8** Access & Roles hub polish + permission matrix UX | G1.2 | P0 | Medium | |
| **M9** Impersonation / session storage security iteration (FA-SEC) | G3.1 | P0 | Medium–Low | Backend coordination |
| **M10** LCP budget held on `/dashboard`, `/payments`, `/admin/tenants` | G2.1, G2.2 | P1 | Medium | |
| **M11** Complete ≥70% of FA-TD-001 call sites | G4.1 | P1 | Medium | |
| **M12** Quarterly security audit #2 | G3.2, G3.3 | P1 | High | Update `SECURITY_AUDIT.md` |

### Q1 2027 (Jan–Mar) — *Depth & digital*

**Theme:** Digital services + data rights + DR honesty.

| Milestone | Goals | Priority | Feasibility | Notes |
| --------- | ----- | -------- | ----------- | ----- |
| **M13** Digital services FA UX complete for Mandanten request + Super Admin publish flows | G1.3 | P0 | Medium | Non-fiscal; still RBAC-critical |
| **M14** GDPR data-management UX (View/Export/Delete) hardened + E2E | G1.3 | P0 | Medium | RKSV retention copy verified |
| **M15** Backup/DR honesty + Fake/Real operator paths fully regression-locked | G4.4 | P0 | Medium | High support value |
| **M16** Virtualize remaining heavy tables; close FA-TD perf cluster | G2.2 | P1 | Medium | |
| **M17** i18n operator-critical parity pass (de/en/tr) | G4.2 | P1 | High | |
| **M18** FA-TD-001 Done (or Won't-fix leftovers documented) | G4.1 | P1 | Medium | |

### Q2 2027 (Apr–Jun) — *Scale & polish*

**Theme:** Platform maturity; prepare next annual vision.

| Milestone | Goals | Priority | Feasibility | Notes |
| --------- | ----- | -------- | ----------- | ----- |
| **M19** Orval major upgrade path executed (or deferred with rationale) | G3.3 | P0 | Low–Medium | Breaking codegen |
| **M20** CSP / proxy protect-list hardening wave | G3.2 | P1 | Medium | |
| **M21** E2E critical-path pack expanded (login → RKSV → backup → tenants) | G2.3, G1.1 | P1 | Medium | |
| **M22** Onboarding video + quarterly “FA roadmap” session with stakeholders | G4.3 | P2 | High | |
| **M23** Annual vision refresh → draft next 12-month roadmap | All | P0 | High | Feeds Jul 2027 review |

---

## 4. Prioritization (business value × feasibility)

### Scoring (used for ordering above)

| Priority | Meaning |
| -------- | ------- |
| **P0** | Revenue, compliance, or security risk if slipped; do this quarter |
| **P1** | Strong leverage / debt interest; schedule firmly |
| **P2** | Valuable but deferrable without immediate harm |

### Priority matrix (summary)

```text
                    High feasibility          Lower feasibility
High business value │ M1 M2 M5 M7 M8 M14     │ M3 M9 M13 M15 M19
                    │ M4 M12 M17             │ M10 M11 M16 M20
────────────────────┼────────────────────────┼──────────────────
Lower urgency       │ M6 M22                 │ M21 M23 (planning)
```

**Always prefer:** compliance honesty + tenant isolation + security over cosmetic UI.  
**Never trade:** fiscal truth wording or API boundary rules for velocity.

### Dependencies outside FA

| Dependency | Affects |
| ---------- | ------- |
| Backend OpenAPI / auth cookie model | M3, M9, Orval (M19) |
| Coupled FA+API deploys | RKSV overview, digital, data-management (`docs/ADMIN_FA_DEPLOY.md`) |
| Ops (Sentry, uptime, GHCR, Environments) | M1, M2 |

---

## 5. How this connects to day-to-day work

| Artifact | Role vs this roadmap |
| -------- | -------------------- |
| [`TECHNICAL_DEBT.md`](TECHNICAL_DEBT.md) | Sprint-level debt; pull P0/P1 items that advance G2–G4 |
| [`SECURITY_AUDIT.md`](SECURITY_AUDIT.md) | Quarterly security milestones (G3) |
| [`docs/MONITORING.md`](docs/MONITORING.md) / [`docs/PERFORMANCE_MONITORING.md`](docs/PERFORMANCE_MONITORING.md) | G2.1 evidence |
| [`docs/USER_FEEDBACK.md`](docs/USER_FEEDBACK.md) | G1.4 intake |
| [`docs/TESTING.md`](docs/TESTING.md) / CI docs | Quality bar for every milestone |
| [`ONBOARDING.md`](ONBOARDING.md) | G4.3 enablement |

New epics: add a row under the **current quarter** (or next) with goal IDs, priority, and feasibility — do not invent a parallel roadmap in chat.

---

## 6. Review cadence

| Cadence | Action | Owner |
| ------- | ------ | ----- |
| **Weekly** | Feedback triage; note themes for roadmap | FA maintainer |
| **Sprint** | Map 1–3 FA-TD / SEC items to active milestones | Tech lead |
| **Monthly** | Web Vitals / error-rate glance (`docs/PERFORMANCE_MONITORING.md`) | FA + ops |
| **Quarterly** | **Update this file:** status of milestones, reprioritize, bump `Last updated` / `Next roadmap review` | Product + engineering |

### Quarterly review checklist

1. Mark milestones **Done / Partial / Deferred** with one-line evidence.  
2. Re-score P0–P2 from support load + compliance calendar (Monatsbeleg / Jahresbeleg seasons).  
3. Pull closed FA-TD/SEC ids into a short “Shipped” note; move leftovers.  
4. Confirm next review date (±1 week of the 21st).  
5. Announce summary in team channel / weekly onboarding session.

### Review log

| Date | Reviewer | Outcome |
| ---- | -------- | ------- |
| 2026-07-21 | FA maintainers | Initial vision & 12-month roadmap established |
| 2026-10-21 | _pending_ | |
| 2027-01-21 | _pending_ | |
| 2027-04-21 | _pending_ | |
| 2027-07-21 | _pending_ | Annual refresh |

---

## 7. North-star (beyond 12 months)

FA remains the **single back-office** for Regkasse: multi-tenant, RKSV-honest, i18n-complete, observable, and boringly reliable — so POS can stay focused on selling, and operators can prove compliance without tribal knowledge.
