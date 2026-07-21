# Frontend Admin — Developer Onboarding

**Audience:** New engineers joining `frontend-admin` (FA)  
**Last updated:** 2026-07-21  
**Companion:** [Getting Started (Day 1)](GETTING_STARTED.md) · [README](README.md) · [AGENTS.md](../AGENTS.md)

This guide is the long-form onboarding path. Use [GETTING_STARTED.md](GETTING_STARTED.md) for a one-page Day-1 checklist, then return here for architecture, tests, deploy, and weekly sessions.

---

## Table of contents

1. [Project overview](#1-project-overview)
2. [Setup instructions](#2-setup-instructions)
3. [Architecture overview](#3-architecture-overview)
4. [Key concepts and conventions](#4-key-concepts-and-conventions)
5. [How to run tests](#5-how-to-run-tests)
6. [How to deploy](#6-how-to-deploy)
7. [First week learning path](#7-first-week-learning-path)
8. [Weekly onboarding sessions](#8-weekly-onboarding-sessions)
9. [Video walkthrough (script)](#9-video-walkthrough-script)
10. [Where to get help](#10-where-to-get-help)

---

## 1. Project overview

**Regkasse Admin Panel (FA)** is the web back-office for the Austrian RKSV-compatible POS monorepo.

| Surface | URL (prod) | Package |
| ------- | ---------- | ------- |
| Admin (FA) | `https://admin.regkasse.at` | `frontend-admin/` |
| POS | `https://pos.regkasse.at` | `frontend/` (Expo — not this package) |
| API | `https://api.regkasse.at` | `backend/` |

### What FA owns

- Mandanten-Admin and Super Admin workflows: users/roles, catalog, payments browse, RKSV/FinanzOnline ops UI, backup & DR, billing/licenses, digital services, GDPR data management, reporting.
- **Not** POS cart/payment signing — that stays in `frontend/` + `/api/pos/*`.

### Stack (at a glance)

Next.js 16 App Router · React 19 · Ant Design 6 · TanStack Query · Orval · Vitest · Playwright · optional Sentry / Speed Insights.

Repo rules that apply on every change: root [`AGENTS.md`](../AGENTS.md) (API boundaries, tenant isolation, fiscal guardrails, FA conventions).

---

## 2. Setup instructions

### 2.1 Prerequisites

| Tool | Version / notes |
| ---- | --------------- |
| Node.js | **22+** (CI baseline) |
| npm | Comes with Node; prefer `npm ci` when lockfile is trusted |
| Git | Access to this monorepo |
| Backend | Local API on `http://localhost:5184` for live data |
| PostgreSQL | Via Docker or local — required for backend |
| Optional | JDK 17+ only if you run RKSV DEP verify scripts |

### 2.2 Clone and install

```bash
# From monorepo root
cd frontend-admin
npm install
# or: npm ci
```

### 2.3 Environment

Copy env **inside this package** (not repo root):

```bash
cp .env.example .env.local
```

Minimum for local UI:

```env
NEXT_PUBLIC_API_BASE_URL=http://localhost:5184
NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST
```

**Critical:** `NEXT_PUBLIC_*` is **build-time**. Changing only container/runtime env after `next build` will not update the client bundle. Details: [`docs/DEPLOYMENT_BUILD_TIME_ENV.md`](docs/DEPLOYMENT_BUILD_TIME_ENV.md).

### 2.4 Start the stack

```bash
# Terminal A — API (from monorepo)
cd backend
dotnet run

# Terminal B — FA
cd frontend-admin
npm run dev
```

- FA: `http://localhost:3000` or `http://admin.regkasse.local:3000`
- Optional hosts: `127.0.0.1 admin.regkasse.local`

### 2.5 First login (dev)

Use a seeded Super Admin or Mandanten-Admin account from your team’s local secrets (never commit passwords). Login field accepts **email or username** (`loginIdentifier`).

Development tenant switching: header switcher → sends `X-Tenant-Id` (Development API only). Production uses JWT `tenant_id` only.

### 2.6 API client (when backend contracts change)

```bash
# Repo root
node scripts/generate-backend-openapi.mjs
cd frontend-admin && npm run generate:api
# Repo root
node scripts/verify-api-client.mjs
```

Commit `backend/swagger.json` + `frontend-admin/src/api/generated/**` together.

### 2.7 Verify setup

```bash
cd frontend-admin
npm run typecheck
npm run test -- --reporter=dot
npm run lint
```

Open `/login` → log in → confirm dashboard and header tenant badge (dev).

---

## 3. Architecture overview

```text
Browser (admin.regkasse.at)
  ├─ src/proxy.ts          Edge: JWT cookie present + exp (not full RBAC)
  ├─ AuthGate + /me        Session
  ├─ PermissionRouteGuard  Route RBAC (inline 403)
  ├─ features/*            Domain UI + hooks
  ├─ api/generated         Orval React Query clients
  ├─ api/admin | manual    Hand-written admin clients
  └─ axios + CSRF          Bearer + X-XSRF-TOKEN on mutations
         │
         ▼
  backend /api/admin/* , /api/Auth/* , shared /api/rksv/* …
```

### Directory map

| Path | Role |
| ---- | ---- |
| `src/app/(protected)/` | Authenticated App Router pages |
| `src/app/(public)/` | Login, force password change, etc. |
| `src/features/{domain}/` | Feature UI, hooks, local helpers |
| `src/api/generated/` | **Do not hand-edit** — Orval output |
| `src/api/admin/`, `src/api/manual/` | Manual clients / unwrap helpers |
| `src/shared/auth/` | `ROUTE_PERMISSIONS`, guards, permission constants |
| `src/i18n/` | de / en / tr catalogs + `I18nProvider` |
| `src/lib/` | Query client, notify, validations, personalization |
| `src/stores/` | Zustand **UI prefs only** |
| `src/proxy.ts` | Next.js 16 auth edge boundary |
| `tests/e2e/` | Playwright |
| `docs/` | FA-specific ops and process docs |

### Request boundaries (do not cross)

| Client | Allowed | Forbidden |
| ------ | ------- | --------- |
| FA | `/api/admin/*`, `/api/Auth/*`, shared user/rksv as documented | `/api/pos/*` |
| POS | `/api/pos/*`, Auth, Receipts | `/api/admin/*` |

Cross-tenant resource access must surface as **404** in the UI (not “forbidden”).

---

## 4. Key concepts and conventions

### Multi-tenant

- Production FA: platform host `admin.regkasse.at`; tenant from JWT / impersonation.
- Development: `X-Tenant-Id` / `?tenant=` allowed on API.
- Never invent a second tenant source of truth in the client store.

### Auth & RBAC

- Tokens: `authStorage` (localStorage + cookie mirror for `proxy.ts`).
- UI permissions: `user.permissions` from `/me` + `ROUTE_PERMISSIONS` / `PermissionRouteGuard`.
- Roles display: backend `Manager` → UI **Mandanten-Admin**; see `AGENTS.md`.

### Ant Design 6

- Toasts: **`useNotify()`** — never static `message` / `notification` from `antd`.
- Confirms: **`useAntdApp().modal`** — never `Modal.confirm` static.
- Prefer `destroyOnHidden`, `popupRender`, `variant="borderless"` / `filled`.

### i18n

- Admin UI: **de / en / tr** under `src/i18n/locales/`.
- No hardcoded operator strings in new UI.
- After key changes: `npm run i18n:keys` and `npm run i18n:ci` when touching catalogs.

### Data & state

| Kind | Tool |
| ---- | ---- |
| Server/API | TanStack Query (Orval) |
| Auth | `AuthProvider` + `authStorage` |
| UI prefs | Theme / personalization + narrow Zustand stores |
| Never in Zustand | Tokens, profiles, fiscal/payment payloads, RQ cache copies |

### High-risk areas (read before editing)

Payments presentation, RKSV truth surfaces, FinanzOnline outbox UI, backup/restore permissions, tenant impersonation, voucher ledgers (read-only FA), GDPR data management.

See `AGENTS.md` → High-Risk Flows and `ai/07_DO_NOT_TOUCH.md` when relevant.

### Language rules (reminder)

| Context | Language |
| ------- | -------- |
| Code identifiers / most comments | English |
| POS UI | German only (not FA) |
| Admin UI | i18n de/en/tr |
| IDE explanations to teammates | Turkish is OK per project rules |
| Git commits | English |

---

## 5. How to run tests

### Unit / component (Vitest)

```bash
cd frontend-admin
npm run test                 # full suite
npm run test:watch
npm run test -- path/to/file.test.ts
npm run test:contract
npm run test:truth-surfaces  # when editing RKSV / FO truth UI
npm run typecheck
npm run lint
```

### E2E (Playwright)

```bash
npx playwright install chromium   # once
npm run build                     # with NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST
npm run test:e2e                  # mocked API by default
npm run test:e2e:ui
```

Live API (optional): see `.env.example` E2E section (`E2E_LIVE=1`, credentials).

### Repo-root gates (when touching OpenAPI / i18n)

```bash
node scripts/verify-api-client.mjs
node localization/scripts/validate-translations.mjs --app frontend-admin --strictMissing true --orphanPolicy error
```

### What “good” looks like before a PR

- `npm run precommit` (lint + typecheck) green locally
- Relevant Vitest files green
- No new static antd `message` / `Modal.confirm`
- i18n keys present for new copy
- OpenAPI regen + verify if API changed

---

## 6. How to deploy

### Build-time env (always)

Before `next build` / Docker image build, set at least:

- `NEXT_PUBLIC_API_BASE_URL`
- `NEXT_PUBLIC_RKSV_ENVIRONMENT` (`TEST` or `PROD`)
- Optional: `NEXT_PUBLIC_SENTRY_DSN`, release/environment labels

### Options used in this repo

| Path | Entry |
| ---- | ----- |
| Docker | `Dockerfile` + `docker-compose.yml` (build-args for `NEXT_PUBLIC_*`) |
| Vercel | `vercel.json` — Framework = Next.js; **do not** set Output Directory to `.next` |
| Nginx + Node | `nginx.conf` reverse proxy → `npm start` on :3000 |

Coupled FA + API releases: [`../docs/ADMIN_FA_DEPLOY.md`](../docs/ADMIN_FA_DEPLOY.md).

CI/CD: see [`docs/CI_CD.md`](docs/CI_CD.md) and README **CI/CD**.

| Workflow | Role |
| -------- | ---- |
| `frontend-admin-ci.yml` | `lint` → `typecheck` → `test` → `build` → `test:e2e` |
| `frontend-admin-deploy.yml` | GHCR image; staging on `main`; production = manual + Environment approval |
| `api-client-alignment.yml` | Orval / OpenAPI drift |
| `frontend-admin-e2e.yml` | Manual Playwright re-run |

Smoke after deploy: `/login`, `/rksv` badge (TEST/PROD), one Super Admin and one Mandanten-Admin path.

---

## 7. First week learning path

| Day | Focus |
| --- | ----- |
| **1** | [GETTING_STARTED.md](GETTING_STARTED.md) — env, `npm run dev`, login, one PR-sized doc fix or test |
| **2** | Read `AGENTS.md` FA sections; walk `src/proxy.ts`, `AuthGate`, `PermissionRouteGuard`, `ROUTE_PERMISSIONS` |
| **3** | Pick one feature under `src/features/` (e.g. products or users); follow data: Orval hook → page → mutations + `useNotify` |
| **4** | Run Vitest + one Playwright smoke; read `docs/ACCESS_AND_ROLES_HUB.md` |
| **5** | Shadow a weekly onboarding session (§8); skim `TECHNICAL_DEBT.md`, `SECURITY_AUDIT.md`, `docs/USER_FEEDBACK.md` |

Suggested first code touch (low risk): i18n typo, test assertion, or docs — avoid payment/RKSV/backup until pair-reviewed.

---

## 8. Weekly onboarding sessions

**Cadence:** weekly, 45–60 minutes  
**Owner:** FA maintainer / tech lead  
**Audience:** new joiners in their first 1–3 weeks (and anyone refreshing)

### Agenda template

1. **Monorepo map** (5 min) — FA vs POS vs API hosts  
2. **Live setup check** (10 min) — `.env.local`, `npm run dev`, tenant switcher  
3. **Auth & permissions walkthrough** (15 min) — login → `/me` → sidebar filter → 403  
4. **Feature deep-dive** (15 min) — rotate weekly: catalog, RKSV hub, backup, digital, billing  
5. **PR hygiene** (10 min) — Orval, i18n, `useNotify`, test commands  
6. **Q&A** + assign a buddy for the week  

### Facilitation notes

- Record the session when possible; link recording under [§9](#9-video-walkthrough-script) once available.
- Keep a shared calendar invite: **“FA Dev Onboarding (weekly)”**.
- New hire checklist: completed Day-1 Getting Started before session 1.

### Session log (fill as you go)

| Date | Host | Attendees | Topic focus | Recording / notes |
| ---- | ---- | --------- | ----------- | ----------------- |
| 2026-07-__ | | | Kickoff / setup | |
| | | | | |

---

## 9. Video walkthrough (script)

A recorded walkthrough is **optional** but recommended. Use this script (~12–15 minutes) when recording Loom / Teams / YouTube (unlisted).

| Segment | Time | Talk track / actions |
| ------- | ---- | -------------------- |
| 1. Intro | 0:00 | What FA is; link to this doc |
| 2. Repo open | 0:45 | Show `frontend-admin/` vs `backend/` vs `frontend/` |
| 3. Env | 2:00 | Copy `.env.example` → `.env.local`; explain build-time `NEXT_PUBLIC_*` |
| 4. Run | 3:30 | `dotnet run` + `npm run dev`; open `admin.regkasse.local:3000` |
| 5. Login | 5:00 | `loginIdentifier`; header tenant switcher in Development |
| 6. Auth layers | 6:30 | Brief: `proxy.ts` → AuthGate → PermissionRouteGuard |
| 7. Feature tour | 8:00 | Sidebar: users, products, `/rksv` badge, `/backup` role-aware hub |
| 8. Feedback widget | 10:00 | Floating Feedback → My feedback statuses |
| 9. Tests | 11:00 | `npm run test`, mention Playwright |
| 10. Deploy caveat | 12:30 | Docker/Vercel must bake `NEXT_PUBLIC_*` at build |
| 11. Close | 13:30 | Point to GETTING_STARTED + weekly session |

**Artifact location (when recorded):** store link here or in the team wiki — e.g. `docs/onboarding-videos.md` (create when first recording exists).

---

## 10. Where to get help

| Topic | Doc / channel |
| ----- | ------------- |
| Always-on rules | [`AGENTS.md`](../AGENTS.md) |
| Broader product brief | [`REGKASSE_AI_ONBOARDING.md`](../REGKASSE_AI_ONBOARDING.md) |
| Day-1 checklist | [`GETTING_STARTED.md`](GETTING_STARTED.md) |
| Package README | [`README.md`](README.md) |
| Tech debt / security / perf / feedback | `TECHNICAL_DEBT.md`, `SECURITY_AUDIT.md`, `docs/PERFORMANCE_MONITORING.md`, `docs/USER_FEEDBACK.md` |
| Domain guardrails | [`ai/`](../ai/) |
| Stuck on env / RKSV badge | README Troubleshooting |

Welcome — prefer small, reversible PRs and ask early on fiscal or tenant-isolation questions.
