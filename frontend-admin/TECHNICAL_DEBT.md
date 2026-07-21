# Frontend Admin — Technical Debt Management

**Owner:** FA maintainers  
**Scope:** `frontend-admin/` only (backend/POS debt stays in their own trackers)  
**Last reviewed:** 2026-07-21  
**Next review:** every sprint planning (or at least every 2 weeks)

This file is the **single source of truth** for known FA technical debt.  
It absorbs residual work from the July 2026 FA modernization campaign (“~90 improvements”), plus other repo-documented issues.

---

## 1. Process

### Goals

- Keep debt **visible** (no silent TODOs in chat history only).
- Pay down **high severity / low effort** items first.
- Defer intentionally — deferred ≠ forgotten.

### How to add an item

1. Assign an id: `FA-TD-XXX` (next free number in the Active backlog).
2. Fill **Severity**, **Effort**, **Impact**, **Status**, **Notes / evidence**.
3. Add to **Prioritized backlog** (re-sort by priority score).
4. If work is postponed >1 quarter → move to **Deferred** with a reason.

### Status values

| Status | Meaning |
| ------ | ------- |
| `Open` | Not started |
| `In progress` | Actively being worked |
| `Blocked` | Waiting on backend/OpenAPI/ops |
| `Done` | Resolved — move to Completed log (keep id) |
| `Won't fix` | Explicitly rejected (document why) |

### Team review ritual

| When | What |
| ---- | ---- |
| Sprint planning | Pick 1–3 `Open` items into the sprint (prefer P0/P1) |
| Mid-sprint | Update status / notes on touched items |
| Sprint retro | Mark `Done`, re-score anything that grew, prune stale Deferred |
| After OpenAPI regen | Re-check contract-mismatch rows (`FA-TD-010`+) |

### Priority score (sort key)

```text
score = severityWeight × 10 − effortWeight
```

| Severity | Weight | Effort | Weight |
| -------- | ------ | ------ | ------ |
| Critical | 4 | Small | 1 |
| High | 3 | Medium | 2 |
| Low | 1 | Large | 3 |
| Medium | 2 | | |

Higher score → do sooner. Tie-break: Security > Performance > Maintainability > UX.

---

## 2. Taxonomy

| Field | Values |
| ----- | ------ |
| **Severity** | Critical · High · Medium · Low |
| **Effort** | Small · Medium · Large |
| **Impact** | Performance · Security · Maintainability · UX (multi-label allowed; primary listed first) |

---

## 3. Prioritized active backlog

Sorted by priority score (highest first). IDs are stable.

| ID | Item | Sev | Effort | Impact | Score | Status | Evidence / notes |
| -- | ---- | --- | ------ | ------ | ----- | ------ | ---------------- |
| FA-TD-001 | Finish migration off static Ant Design feedback (`message` / `notification` / `Modal.confirm`) → `useNotify()` / `useAntdApp()` | High | Large | Maintainability, UX | 27 | Open | Still many call sites under `features/website-generator`, `data-management`, catalog pages, vouchers, etc. AGENTS.md forbids static APIs. |
| FA-TD-002 | Complete route-body lazy load for remaining heavy lists (payments, users, invoices, receipts, audit-logs) + shared `createLazyAdminPage` helper | High | Medium | Performance | 28 | Open | Products/tenants shells use `next/dynamic`; plan todos still open (`fa_performance_improvements`). No `createLazyAdminPage` yet. |
| FA-TD-003 | Migrate remaining large tables to `VirtualTable` + `adminTablePagination` defaults (users, cash registers, orders, backup runs, products) | High | Medium | Performance, UX | 28 | Open | `VirtualTable.tsx` / `adminTablePagination.ts` exist; adoption incomplete (~11 VirtualTable refs). |
| FA-TD-004 | Wire `OptimizedImage` for product/template previews + Header logo (`priority` above-the-fold) | Medium | Small | Performance, UX | 19 | Open | Component exists; few production call sites. |
| FA-TD-005 | Expand Playwright E2E beyond smoke (backup RBAC, RKSV truth badges, FinanzOnline outbox, impersonation happy-path mocked) | High | Large | Security, UX | 27 | Open | Suite + CI exist; coverage still thin for high-risk FA flows. |
| FA-TD-006 | Remove `@/lib/auth/*` compatibility shims after callers use `@/features/auth/lib/*` | Medium | Small | Maintainability | 19 | Open | Deprecated re-exports after auth folder move. |
| FA-TD-007 | Align OpenAPI / generated types: product `taxType` (int vs string), category `vatRate` on create/update | Medium | Medium | Maintainability | 18 | Blocked | Needs backend swagger + `generate:api`. See `docs/CLEANUP_AND_CONSISTENCY_REPORT.md`. |
| FA-TD-008 | Standardize StatusBadge / EmptyState / ConfirmDialog / FieldTooltip across list pages (stop one-off Tag/Empty/Modal patterns) | Medium | Large | UX, Maintainability | 17 | Open | Components + some tests exist; sparse adoption. |
| FA-TD-009 | Finish skeleton loading coverage for remaining protected routes (replace ad-hoc Spin where Skeleton fits) | Medium | Medium | UX | 18 | Open | Skeleton kit exists; not universal. |
| FA-TD-010 | Modifier-groups: shared query-key factory + typed assignment response/errors | Low | Small | Maintainability | 9 | Open | Keys still local to page (`CLEANUP_AND_CONSISTENCY_REPORT.md`). |
| FA-TD-011 | Tagesbericht detail: replace `TODO(adapter)` raw API enums/warnings with locale maps | Medium | Medium | UX, Maintainability | 18 | Open | `reporting/tagesbericht/[id]/page.tsx` adapter TODOs. |
| FA-TD-012 | Drop legacy login `email` mirror once OpenAPI / older clients no longer need it | Low | Small | Maintainability | 9 | Deferred→watch | `LoginForm` still mirrors `email` for compatibility (README). |
| FA-TD-013 | Purge deprecated feature re-exports (command-palette, audit FilterBar, IdleTimeoutProvider, currency helper, sidebar open-keys aliases, …) | Low | Medium | Maintainability | 8 | Open | Many `@deprecated` shims under `src/features` / `src/shared`. |
| FA-TD-014 | Loading/error-state audit for `useQuery` consumers (consistent Skeleton + `translateApiError` / `notify.apiError`) | Medium | Large | UX, Maintainability | 17 | Open | Campaign item; partial. |
| FA-TD-015 | Sentry: production-only init hardening, axios error bridging, ignore noisy 404/network noise | Medium | Medium | Security, Maintainability | 18 | Open | Package present; config can still be tightened. |
| FA-TD-016 | CSP / security headers: tighten after deploy validation (move from pragmatic allowlist toward stricter policy) | Medium | Medium | Security | 18 | Deferred | Do not break admin.regkasse.at connect-src (API + Sentry). |
| FA-TD-017 | Page transition animations (`PageTransition`) | Low | Medium | UX | 8 | Deferred | Nice-to-have; risk of layout shift with Ant Layout. |
| FA-TD-018 | Broad `src/` folder reorganization beyond auth | Low | Large | Maintainability | 7 | Deferred | Auth moved; further moves high churn / low urgency. |
| FA-TD-019 | Replace `process.env` with `import.meta.env` in FA | Low | Medium | Maintainability | 8 | Won't fix | **Rejected:** Next.js App Router uses `process.env.NEXT_PUBLIC_*` at build time. Vite-style `import.meta.env` is the wrong model here. |
| FA-TD-020 | Legacy modifier bulk migration: optional bulk deactivate + admin migrate-to-product route alignment | Low | Medium | Maintainability, UX | 8 | Deferred | Documented in `LEGACY_MODIFIER_MIGRATION_DELIVERABLE.md`; ops workaround = single migrate. |

---

## 4. Sprint schedule (proposed)

Assumes ~2-week sprints and capacity for **debt alongside features** (not a pure debt freeze). Adjust dates in review.

### Sprint A (current / next) — P0 quick wins + safety

| ID | Focus |
| -- | ----- |
| FA-TD-006 | Delete auth shims / update remaining imports |
| FA-TD-004 | OptimizedImage wiring (Header + 1–2 previews) |
| FA-TD-010 | Modifier-groups query keys (opportunistic) |
| FA-TD-001 | Start static-antd sweep on **website-generator + data-management** only |

**Exit criteria:** no `@/lib/auth` imports left; Header uses OptimizedImage; ≥2 high-traffic panels on `useNotify`.

### Sprint B — Performance completion

| ID | Focus |
| -- | ----- |
| FA-TD-002 | Lazy shells for payments / users / invoices / receipts / audit-logs |
| FA-TD-003 | VirtualTable + pagination defaults on remaining heavy lists |
| FA-TD-009 | Skeleton gaps on those same routes |

**Exit criteria:** DevTools shows separate chunks for those route bodies; virtual on ≥30-row pages; checklist from performance plan passes.

### Sprint C — Consistency + observability

| ID | Focus |
| -- | ----- |
| FA-TD-001 | Continue static-antd migration (catalog, vouchers, digital) |
| FA-TD-008 | StatusBadge / EmptyState / ConfirmDialog on top 5 list pages |
| FA-TD-015 | Sentry hardening |
| FA-TD-014 | Loading/error audit for those pages |

### Sprint D — Contract + E2E depth

| ID | Focus |
| -- | ----- |
| FA-TD-007 | Backend OpenAPI align → regen → remove casts |
| FA-TD-005 | Playwright scenarios for backup RBAC + RKSV truth surfaces |
| FA-TD-011 | Tagesbericht adapter i18n |

### Later / backlog grooming

FA-TD-012, FA-TD-013, FA-TD-016, FA-TD-017, FA-TD-018, FA-TD-020 — see Deferred.

---

## 5. Deferred

Items we **intentionally** postpone. Revisit quarterly or when touching the area.

| ID | Why deferred | Revisit when |
| -- | ------------ | ------------ |
| FA-TD-012 | Compatibility with older OpenAPI login shape | After auth OpenAPI cleanup |
| FA-TD-016 | Strict CSP can break prod admin if mis-tuned | Post-deploy monitoring window |
| FA-TD-017 | Animation polish vs operator task focus | Explicit UX polish sprint |
| FA-TD-018 | Wide move = merge conflict tax | Only with a dedicated modularization epic |
| FA-TD-020 | Legacy modifiers shrinking; bulk deactivate is backend+ops | When migration counts stay non-zero |
| FA-TD-019 | Marked **Won't fix** (wrong Next.js model) | N/A |

---

## 6. Campaign reference — “~90 improvements” (Jul 2026)

The FA modernization campaign produced a large set of prompts (performance, UX consistency, Ant Design 6, tooling, Docker/E2E, dependency hygiene, keyboard shortcuts, skeletons, etc.). Most themes are **done or largely done**; residuals are tracked above as `FA-TD-*`.

### Completed themes (do not re-open without new evidence)

| Theme | Outcome |
| ----- | ------- |
| Ant Design 6 App feedback pattern (`useNotify` / `useAntdApp`) | Established; residual call sites → FA-TD-001 |
| Auth boundary `proxy.ts`, loginIdentifier, SuperAdmin 2FA UI | Done |
| RKSV build-time env + badge | Done |
| React Query cache policy | Done |
| `translateApiError` / no stack traces in toasts | Done |
| Code-splitting for backup/RKSV hubs; recharts isolation | Done (extend → FA-TD-002) |
| Form auto-save (no passwords) | Done |
| ESLint flat + Prettier + typecheck + analyze | Done |
| Playwright E2E + CI workflow | Done (deepen → FA-TD-005) |
| PWA icons / manifest / robots | Done |
| Sentry optional integration | Done (harden → FA-TD-015) |
| Docker / Compose / vercel.json / nginx / Dependabot | Done |
| Dependency prune (d3/webpack direct, RN/Expo absent, @types audit) | Done |
| Logger + `no-console` | Done |
| Shared validations | Done |
| VirtualTable / OptimizedImage / pagination helpers | Introduced; adoption → FA-TD-003/004 |
| Keyboard shortcuts + help | Done |
| StatusBadge / EmptyState / ConfirmDialog / FieldTooltip | Introduced; adoption → FA-TD-008 |
| Skeleton components | Introduced; coverage → FA-TD-009 |
| Axios retry / rowKey standardization / useEffect cleanup | Done (re-audit if regressions) |
| Auth lib → `features/auth/lib` | Done; shim removal → FA-TD-006 |
| README Recent Improvements / Tech Stack / Troubleshooting | Done |

### Rejected / superseded campaign ideas

| Idea | Decision |
| ---- | -------- |
| `react-virtual` sliced `dataSource` for Ant Table | Rejected — use Ant native `virtual` (`VirtualTable`) |
| `import.meta.env` instead of `process.env` | Won't fix (FA-TD-019) |
| Redis / Prometheus (backend-centric prompts in same era) | Out of FA debt scope — track under backend if needed |

If you still have the original numbered 1–90 prompt list outside the repo, paste any **missing Open** rows into this file as new `FA-TD-*` entries during the next review.

---

## 7. Completed debt log

| ID | Resolved | Notes |
| -- | -------- | ----- |
| — | — | Move finished `FA-TD-*` rows here with date + PR/commit. |

---

## 8. Related docs

| Doc | Role |
| --- | ---- |
| [`README.md`](README.md) | Stack, scripts, Recent Improvements |
| [`docs/CLEANUP_AND_CONSISTENCY_REPORT.md`](docs/CLEANUP_AND_CONSISTENCY_REPORT.md) | Catalog/OpenAPI leftovers |
| [`docs/LEGACY_MODIFIER_MIGRATION_DELIVERABLE.md`](docs/LEGACY_MODIFIER_MIGRATION_DELIVERABLE.md) | Legacy modifier migration risks |
| [`docs/DEPLOYMENT_BUILD_TIME_ENV.md`](docs/DEPLOYMENT_BUILD_TIME_ENV.md) | `NEXT_PUBLIC_*` build-time pitfalls |
| [`AGENTS.md`](../AGENTS.md) | FA conventions & Ant Design 6 rules |

---

## 9. Changelog

| Date | Change |
| ---- | ------ |
| 2026-07-21 | Initial process + backlog created from campaign residuals and cleanup reports. |
