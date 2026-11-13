# FA Menu IA Redesign — Internal QA, Feedback & Rollout

**Owner:** FA maintainers / Super Admin product owner  
**Scope:** Sidebar group IA + breadcrumbs (2026-08)  
**Status:** Ready for **internal** testing; production only after checklist sign-off  

**Related**

| Area | Path |
| ---- | ---- |
| Sidebar IA | `frontend-admin/src/shared/adminSidebarRegistry.ts` |
| Open keys / filter | `frontend-admin/src/shared/adminSidebarNavigation.ts` |
| Platform breadcrumbs | `frontend-admin/src/shared/adminPlatformBreadcrumbs.ts` |
| Auto path crumbs | `frontend-admin/src/shared/buildPathBreadcrumbs.ts` |
| i18n group labels | `frontend-admin/src/i18n/locales/{de,en,tr}/nav.json` (+ `admin-shell.json`) |
| In-app feedback loop | [`USER_FEEDBACK.md`](./USER_FEEDBACK.md) · FA widget + `/admin/feedback` |

---

## New menu groups (reference)

| Group (de) | Typical contents | Primary hub |
| ---------- | ---------------- | ----------- |
| **Verwaltung** | Zugriff & Rollen, Mandanten, Datenverwaltung | `/admin/access` |
| **Sicherheit & TSE** | TSE-* ops, Kritische Freigaben | `/admin/tse-management` |
| **Deployment & System** | Deployments, Feature Flags, DB-Migrationen, Systemwartung | `/admin/deployments` |
| **Monitoring & Logs** | Monitoring, Risiko-Dashboard, Error Logs | `/admin/monitoring` |

Unchanged top-level areas: Dashboard, Lizenzverwaltung, Betrieb, RKSV, Sortiment, Kunden, Berichte, Backup, Einstellungen.

**Contract:** routes and permissions are unchanged — display grouping and breadcrumbs only.

---

## 1. Internal testing checklist

### Test instructions

1. Use three sessions (or three browsers / profiles):
   - **Super Admin** (`system.critical`)
   - **Manager** (Mandanten-Admin)
   - **Cashier** with FA access (if your env grants any admin session)
2. Prefer **de** locale first (POS/ops language), then spot-check **en** / **tr** group labels.
3. Record issues in the table at the bottom (severity if useful).

### Pre-flight (engineers)

```bash
cd frontend-admin
npm run test -- --run src/shared/__tests__/adminPlatformBreadcrumbs.test.ts \
  src/shared/__tests__/adminSidebarNavigation.test.ts \
  src/shared/__tests__/adminRoleMenuVisibility.test.ts \
  src/shared/__tests__/sidebarRegistryCatalog.test.ts
```

Optional: `npm run verify:menu-permissions` from repo root.

### Navigation

| # | Check | Super Admin | Manager | Cashier |
| - | ----- | ----------- | ------- | ------- |
| N1 | New groups visible when permitted (Verwaltung / Sicherheit & TSE / Deployment & System / Monitoring & Logs) | ☐ | ☐ (usually **no** Super Admin-only groups) | ☐ |
| N2 | Can open: Zugriff & Rollen, Mandanten (SA), Deployments (SA), TSE-Verwaltung (SA), Monitoring (SA) | ☐ | ☐ | ☐ |
| N3 | Groups feel logical (TSE under Sicherheit; deploys under Deployment; Elmah under Monitoring) | ☐ | ☐ | ☐ |
| N4 | Collapsible groups expand/collapse without stuck open state | ☐ | ☐ | ☐ |
| N5 | Global search / command palette still finds the same pages | ☐ | ☐ | ☐ |
| N6 | Deep link (e.g. `/admin/tse/failover`) auto-opens the correct sidebar group | ☐ | ☐ | ☐ |

### Breadcrumb

| # | Check | Pass |
| - | ----- | ---- |
| B1 | `/admin/tse-management` → Overview / **Sicherheit & TSE** / TSE-Verwaltung | ☐ |
| B2 | `/admin/tse/failover` → includes **Sicherheit & TSE** (and optional TSE-Verwaltung hub) | ☐ |
| B3 | `/admin/deployments` → Overview / **Deployment & System** / Deployments | ☐ |
| B4 | `/admin/monitoring` → Overview / **Monitoring & Logs** / … | ☐ |
| B5 | `/admin/access` → Overview / **Verwaltung** / Zugriff & Rollen | ☐ |
| B6 | Clicking group / hub crumbs navigates (no 404) | ☐ |

### Permissions (must not regress)

| # | Check | Pass |
| - | ----- | ---- |
| P1 | Super Admin sees platform groups + TSE ops leaves | ☐ |
| P2 | Manager does **not** see `system.critical` TSE ops / deployments / monitoring / Mandanten platform list | ☐ |
| P3 | Manager still sees: Zugriff hub (as permitted), RKSV, Betrieb, Einstellungen, Backup (tenant) | ☐ |
| P4 | Cashier FA session stays limited (no platform Verwaltung/TSE/Deployment/Monitoring) | ☐ |
| P5 | Direct URL to forbidden page still blocked (403 / guard), not only hidden in menu | ☐ |

### Performance / UX

| # | Check | Pass |
| - | ----- | ---- |
| X1 | Sidebar first paint feels unchanged | ☐ |
| X2 | Expanding a large group (Sicherheit & TSE) has no noticeable lag | ☐ |
| X3 | No duplicate menu keys / blank labels in de/en/tr | ☐ |

### Issues found

| Issue | Severity (S0–S3) | Role | Screenshot / URL | Notes |
| ----- | ---------------- | ---- | ---------------- | ----- |
| | | | | |

**Sign-off**

| Role | Name | Date | OK to ship internally? |
| ---- | ---- | ---- | ---------------------- |
| QA / engineer | | | ☐ |
| Product / Super Admin | | | ☐ |

---

## 2. User feedback form

### A) Prefer in-app (production / staging)

Use the existing FA **Feedback** widget ([`USER_FEEDBACK.md`](./USER_FEEDBACK.md)):

| Field | Suggested value |
| ----- | --------------- |
| Category | **Ease of use** |
| Title | `Menu IA 2026-08: …` |
| Rating | 1–5 |
| Message | What you liked / what was hard to find |

Super Admin triage: `/admin/feedback` (filter **Under review**). Tag duplicates with note `Menu IA 2026-08`.

### B) Structured interview / printed form

Copy for 1:1 sessions or Slack threads:

```markdown
# FA Menu Redesign — User Feedback

Date: ________  Environment: ☐ Staging  ☐ Production

## Your role
- [ ] Super Admin
- [ ] Manager (Mandanten-Admin)
- [ ] Cashier (FA access)
- [ ] Other: ________

## How do you find the new menu?
- [ ] Much better
- [ ] Better
- [ ] Same
- [ ] Worse
- [ ] Much worse

## What do you like most?
____________________________________________________________

## What could be improved?
____________________________________________________________

## Did you find what you were looking for?
- [ ] Yes, easily
- [ ] Yes, with some effort
- [ ] No — I was looking for: ________

## Breadcrumb clarity
- [ ] Clear
- [ ] Confusing — example URL: ________

## Any other feedback?
____________________________________________________________

## Optional: top 3 features you use daily
1. ________  2. ________  3. ________
```

**Target:** ≥5 Super Admin + ≥5 Manager responses before treating labels as final.

---

## 3. Metrics (1–2 weeks after release)

| Metric | How | Goal / note |
| ------ | --- | ----------- |
| Feedback volume & rating | `/admin/feedback` EaseOfUse + title prefix `Menu IA 2026-08` | Median rating ≥ 4, or improving week-over-week |
| “Couldn’t find” reports | Feedback message text / support tickets | Zero S1 “missing menu” after week 1 |
| Time-to-feature (manual) | Ask testers: seconds to open TSE-Failover / Deployments / Monitoring | Faster or equal vs old flat list |
| Sidebar expand lag | Subjective + optional Performance panel | No new complaints |
| Guard / 403 spikes | API / FA logs around `/admin/tse/*`, `/admin/deployments` | No increase (permissions unchanged) |

Heatmaps / click analytics: only if you already have a privacy-approved tool; not required for this IA change.

---

## 4. Next iterations (after feedback)

Prioritize only what feedback repeats (≥3 users):

| Idea | Do when |
| ---- | ------- |
| Rename a group label (i18n only) | Label confuses ≥3 testers |
| Move a leaf between groups | Wrong mental model (e.g. “TSE-Logs under Monitoring”) |
| Nested subgroup under Sicherheit & TSE | List still feels too long for SA |
| Pinned / custom order | Explicit product ask — **not** in this release |
| Extra shortcuts (command palette) | High-frequency leaves still hard to reach |

Keep **routes and permissions** stable unless a separate security ticket says otherwise.

---

## 5. Rollback plan

Permissions and URLs stay valid even if UI reverts. Prefer reverting the **IA/layout + breadcrumb helpers + i18n labels**, not unrelated WIP.

### Files touched by this redesign (primary)

```text
frontend-admin/src/shared/adminSidebarRegistry.ts
frontend-admin/src/shared/adminSidebarNavigation.ts
frontend-admin/src/shared/adminPlatformBreadcrumbs.ts
frontend-admin/src/shared/buildPathBreadcrumbs.ts
frontend-admin/src/shared/adminShellLabels.ts
frontend-admin/src/shared/auth/permissionGroupRegistry.ts
frontend-admin/src/components/admin-layout/AdminPageHeader.tsx
frontend-admin/src/i18n/locales/{de,en,tr}/nav.json
frontend-admin/src/i18n/locales/{de,en,tr}/admin-shell.json
+ page breadcrumb call sites under admin/* / users / tenants
+ tests under src/shared/__tests__/
```

### Git revert (if this work is a single commit)

```bash
# Identify the commit, then:
git revert <menu-ia-commit-sha>
# Rebuild FA and redeploy per your usual pipeline
```

### Surgical checkout (only if you must unstick production fast)

```bash
# WARNING: restores those paths to the given revision — review diff first
git checkout <known-good-sha> -- \
  frontend-admin/src/shared/adminSidebarRegistry.ts \
  frontend-admin/src/shared/adminSidebarNavigation.ts \
  frontend-admin/src/shared/adminPlatformBreadcrumbs.ts \
  frontend-admin/src/shared/buildPathBreadcrumbs.ts \
  frontend-admin/src/i18n/locales/de/nav.json \
  frontend-admin/src/i18n/locales/en/nav.json \
  frontend-admin/src/i18n/locales/tr/nav.json

cd frontend-admin && npm run build
```

Page-level breadcrumb call sites may then reference a missing helper — prefer **full commit revert** over partial file restore when possible.

---

## 6. Production readiness

| Gate | Status |
| ---- | ------ |
| Unit tests for sidebar IA + breadcrumbs | ✅ Present |
| Permission contract (Manager vs Super Admin) | ✅ Updated fixtures |
| Internal QA checklist signed | ☐ Required before prod |
| Staging soak (≥1 day Super Admin ops) | ☐ Recommended |
| Feedback channel communicated | ☐ Widget + this form |

### Ready to deploy to production?

**Not yet as a blind “yes”.** Ship to **staging / internal Super Admin** first, run §1 checklist, collect a few §2 responses, then promote.

When gates are green:

1. Merge the menu IA branch (no permission/API contract changes expected).  
2. Deploy FA only (backend not required for this UI-only change).  
3. Announce in ops channel: new groups + link to this doc.  
4. Triage `/admin/feedback` for 1–2 weeks; adjust labels only if needed.

---

**Last updated:** 2026-08-07
