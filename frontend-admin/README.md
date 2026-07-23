# Regkasse Admin Panel

Next.js Admin Panel for Regkasse POS System.
Built with Ant Design 6, TanStack Query, Orval, and Playwright E2E.

## Tech Stack

Versions below match `package.json` / monorepo baselines (`AGENTS.md`). Installed minors may float within caret ranges after a fresh `npm install`.

| Component | Version / notes |
| --------- | --------------- |
| **Next.js** | `^16.2.10` (App Router, Turbopack in `next build` / `next dev`) |
| **React / React DOM** | `^19.2.7` — types via `@types/react` / `@types/react-dom` (React does not ship types) |
| **Ant Design** | `^6.4.3` (+ `@ant-design/nextjs-registry`, icons, cssinjs) |
| **TanStack Query** | `^5.101.3` (Orval-generated hooks) |
| **Zustand** | `^5.0.14` — **only** ephemeral UI prefs (`src/stores/`); never auth/API/fiscal state |
| **Recharts** | `^3.10.0` (page-local / dynamic import; `d3-*` are transitive — do not add as direct deps) |
| **Axios / SignalR** | `axios`, `@microsoft/signalr` |
| **Sentry** | `@sentry/nextjs` `^10.x` (optional DSN; disabled when unset) |
| **Web Vitals** | `web-vitals` + `@vercel/speed-insights` — see [`docs/PERFORMANCE_MONITORING.md`](docs/PERFORMANCE_MONITORING.md) |
| **TypeScript** | `^5.9.3` |
| **Vitest** | `^4.1.10` + Testing Library + jsdom |
| **Playwright** | `@playwright/test` `^1.61.1` — specs under `tests/e2e/` |
| **ESLint / Prettier** | ESLint 9 flat config (`eslint.config.mjs`) + Prettier 3.9 (`prettier.config.mjs`) |
| **Orval** | `^6.31.0` ← `../backend/swagger.json` |
| Backend (.NET) / EF Core | 10.0.8 (API consumed by FA) |
| Expo / React Native (POS) | SDK 56 / 0.85.3 (sibling package — not used in FA) |

### Related documentation

| Doc | Use when |
| --- | -------- |
| [`ROADMAP.md`](ROADMAP.md) | **Vision & 12-month roadmap** (quarterly review) |
| [`AGENTS.md`](../AGENTS.md) | Always-applied workspace rules (auth boundaries, API prefixes, fiscal guardrails) |
| [`GETTING_STARTED.md`](GETTING_STARTED.md) | **Day-1 checklist** for new FA developers |
| [`ONBOARDING.md`](ONBOARDING.md) | Full onboarding: setup, architecture, tests, deploy, weekly sessions, video script |
| [`REGKASSE_AI_ONBOARDING.md`](../REGKASSE_AI_ONBOARDING.md) | Project brief for medium/large FA changes |
| [`docs/`](../docs/) | Human reference (multi-tenant, backup, billing, DEP, …) |
| [`docs/MULTI_TENANT.md`](../docs/MULTI_TENANT.md) | Tenant resolution, FA switcher, production hosts |
| [`docs/POS_PRODUCTION_ARCHITECTURE.md`](../docs/POS_PRODUCTION_ARCHITECTURE.md) | Single POS UI (`pos.regkasse.at`) |
| [`docs/AUTH_TWO_FACTOR.md`](../docs/AUTH_TWO_FACTOR.md) | SuperAdmin TOTP / Dev bypass |
| [`docs/API_CONTRACTS.md`](../docs/API_CONTRACTS.md) | API contract notes |
| [`docs/CI_CD.md`](docs/CI_CD.md) | GitHub Actions CI/CD: quality gate, caching, staging/prod deploy, Slack |
| [`docs/DEPLOYMENT_BUILD_TIME_ENV.md`](docs/DEPLOYMENT_BUILD_TIME_ENV.md) | `NEXT_PUBLIC_*` at build time (Docker/CI pitfalls) |
| [`docs/ACCESS_AND_ROLES_HUB.md`](docs/ACCESS_AND_ROLES_HUB.md) | FA access & roles hub |
| [`docs/TESTING.md`](docs/TESTING.md) | Vitest / RTL / coverage gate (≥80% lines) / E2E strategy |
| [`docs/MONITORING.md`](docs/MONITORING.md) | Uptime, API error rate / latency, health, alerts, Grafana / Sentry dashboards |
| [`docs/LOGGING.md`](docs/LOGGING.md) | Structured logging (pino + browser), redaction, Sentry / Datadog / ELK |
| [`docs/PERFORMANCE_MONITORING.md`](docs/PERFORMANCE_MONITORING.md) | Core Web Vitals, Sentry/Vercel dashboards, alerts, monthly review |
| [`SECURITY_AUDIT.md`](SECURITY_AUDIT.md) | FA security audit findings, dependency vulns, improvement plan, quarterly cadence |
| [`docs/USER_FEEDBACK.md`](docs/USER_FEEDBACK.md) | Floating feedback widget, weekly triage, status loop |
| [`TECHNICAL_DEBT.md`](TECHNICAL_DEBT.md) | FA tech-debt process, prioritized backlog, sprint schedule |
| [`ROADMAP.md`](ROADMAP.md) | Vision, strategic goals, quarterly milestones |
| [`ai/`](../ai/) | Domain guardrails (do not invent parallel rules) |

## Onboarding (new developers)

Start here if you are new to `frontend-admin`:

| Step | Doc |
| ---- | --- |
| **Day 1** | [`GETTING_STARTED.md`](GETTING_STARTED.md) — install, `.env.local`, run API + FA, smoke login |
| **Deep dive** | [`ONBOARDING.md`](ONBOARDING.md) — architecture, conventions, tests, deploy, first-week path |
| **Rules** | [`AGENTS.md`](../AGENTS.md) — API boundaries, tenant isolation, Ant Design / i18n conventions |
| **Weekly sessions** | Calendar: **“FA Dev Onboarding (weekly)”** — agenda in [ONBOARDING §8](ONBOARDING.md#8-weekly-onboarding-sessions) |
| **Video (optional)** | Record using [ONBOARDING §9 script](ONBOARDING.md#9-video-walkthrough-script); catalog links in [`docs/onboarding-videos.md`](docs/onboarding-videos.md) |

### Day-1 commands (summary)

```bash
cd frontend-admin
cp .env.example .env.local   # set NEXT_PUBLIC_API_BASE_URL + NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST
npm install
npm run dev                  # http://localhost:3000 — API must be on :5184
```

From repo root: `npm run dev:admin`, `npm run test:admin`, `npm run build:admin`. See [`CONTRIBUTING.md`](../CONTRIBUTING.md).

Buddy / tech lead: walk auth layers (`proxy.ts` → `AuthGate` → `PermissionRouteGuard`) in the first weekly session. Prefer a small first PR (docs, test, i18n) before RKSV/payment/backup changes.

## Recent Improvements

High-level FA changes (see git history / PRs for full detail):

| Area | What changed |
| ---- | ------------ |
| **Ant Design 6 feedback** | Prefer **`useNotify()`** for toasts and **`useAntdApp()`** for `modal` — never static `message` / `notification` / `Modal.confirm` from `antd`. Props: `destroyOnHidden`, `popupRender`, `variant="borderless"` / `filled`. SSR via `@ant-design/nextjs-registry` (`AntdRegistry` in `src/app/layout.tsx`). |
| **Auth boundary (Next 16)** | `src/proxy.ts` (not `middleware.ts`): fail-closed JWT cookie/expiry → `/login`; RBAC stays in `PermissionRouteGuard` (inline 403). |
| **Login** | Single field: email **or** username → `loginIdentifier` on `POST /api/Auth/login`. |
| **RKSV environment** | `NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST\|PROD` is **build-time**; `/rksv` badge; production build fails if unset/invalid (`next.config.mjs`, `assertRksvPublicEnvironmentForBuild.mjs`). |
| **Security headers / CSP** | Pragmatic CSP + security headers in `next.config.mjs` (`connect-src` includes API + Sentry ingest). |
| **React Query** | Shared cache policy (`src/lib/query/queryCachePolicy.ts`): static / dynamic / volatile / realtime. |
| **API errors** | `translateApiError` maps backend `code` → i18n; no stack traces in toasts. |
| **Code splitting** | `next/dynamic` for large hubs (`/backup`, `/rksv`, users/tenants); keep `recharts` / `jszip` out of the shared chunk. |
| **Client state** | Server data → TanStack Query; auth → `AuthProvider` + `authStorage`; UI prefs → ThemeProvider / personalization **and** narrow Zustand stores (`uiPreferencesStore`, `siderWidthStore`). |
| **Form drafts** | `useAutoSave` + localStorage — **never** persists passwords. |
| **Tooling** | ESLint flat config (`eslint.config.mjs`), Prettier + import sort, `npm run analyze` (`@next/bundle-analyzer`), `npm run typecheck`, `npm run precommit`. |
| **E2E** | Playwright suite (`tests/e2e/`, `playwright.config.ts`); PR gate in `frontend-admin-ci.yml`; manual re-run via `frontend-admin-e2e.yml`. |
| **PWA / icons** | `public/manifest.json`, icons, `robots.txt`; App Router `manifest.webmanifest` / `robots.txt` routes. |
| **Observability** | Structured logging (`logger` / pino) + optional Sentry (`NEXT_PUBLIC_SENTRY_DSN`); see [`docs/LOGGING.md`](docs/LOGGING.md). |
| **Monitoring** | Uptime (`/health`), API error rate / latency, FA health, Super Admin dashboard `/admin/monitoring`, Grafana + Sentry alerts — [`docs/MONITORING.md`](docs/MONITORING.md). |
| **Performance monitoring** | Core Web Vitals via `web-vitals` → Sentry; optional Vercel Speed Insights; optional self-hosted beacon for Grafana/Datadog. Budgets: LCP ≤2.5s. See [`docs/PERFORMANCE_MONITORING.md`](docs/PERFORMANCE_MONITORING.md). |
| **Security audit** | [`SECURITY_AUDIT.md`](SECURITY_AUDIT.md) — `npm audit`, Dependabot, XSS/CSRF/auth findings, quarterly review schedule. |
| **User feedback** | Floating widget on protected pages; categories (ease of use / performance / feature / bug); Super Admin inbox `/admin/feedback`; weekly triage. See [`docs/USER_FEEDBACK.md`](docs/USER_FEEDBACK.md). |
| **Dependency hygiene** | Unused direct deps pruned (`d3` / `webpack` not direct — transitive only); keep `@types/react*` while React 19 ships no types; prefer `npm ci` in CI. |
| **Docker** | Multi-stage `Dockerfile` + `docker-compose.yml` (build-args for `NEXT_PUBLIC_*`, non-root `npm start` on :3000). |
| **Deploy configs** | `vercel.json` (Next.js framework + `npm run build`; do **not** override output to `.next`), `nginx.conf` reverse proxy for self-hosted `admin.regkasse.at`. |
| **Diagnostics** | `logger` / `technicalConsole` with redaction; ESLint `no-console`. |
| **Validations** | Shared Ant Design rules under `src/lib/validations/`. |

## Prerequisites

> New to FA? Use **[`GETTING_STARTED.md`](GETTING_STARTED.md)** first, then this section.

- **Node.js 22+** (align with CI / `@testing-library/jest-dom@7`; Node 20 may work for `dev`/`build` but is not the verified CI baseline)
- Backend API running locally when you need live data (`http://localhost:5184`)
- For E2E Chromium: `npx playwright install chromium` (CI installs with `--with-deps`)

## Setup

1. Install dependencies:

   ```bash
   npm install
   ```

2. Environment Setup:
   Copy `.env.example` to **`.env.local` in the `frontend-admin/` directory** (same folder as `package.json` and `next.config.mjs`). This monorepo has **no root-level Next.js app** — a `.env.local` at the repository root is **not** read when you run `npm run dev` here; each app (`frontend-admin`, `frontend`) uses its own package root.
   ```env
   NEXT_PUBLIC_API_BASE_URL=http://localhost:5184
   NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST
   ```
   (Backend API varsayılan port: 5184; `backend/appsettings.Development.json` içindeki `Urls` ile uyumlu olmalı)

### Next.js: `NEXT_PUBLIC_*` variables (build-time, not runtime-only)

**Diagnosis (confirmed):** Names starting with `NEXT_PUBLIC_` are **compiled into the client JavaScript** when you run `next dev` or `next build`. They are **not** read fresh from the server environment on every request like a secret `DATABASE_URL`.

| Broken flow                                                                                                                           | Why the UI stays `UNCONFIGURED`                                                                 |
| ------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------- |
| Image built **without** `NEXT_PUBLIC_RKSV_ENVIRONMENT`, then variable set only on `docker run` / Compose `environment:` / K8s pod env | The browser bundle was already built with `undefined` / empty. `next start` does not re-bundle. |
| CI builds artifact without the var, ops adds it only in production host env                                                           | Same: client chunk never saw the value at compile time.                                         |

**Correct flow:** Provide `NEXT_PUBLIC_RKSV_ENVIRONMENT` (and other `NEXT_PUBLIC_*`) **before** `next build` (or before the dev compiler picks up `.env.local`). For Docker: use `ARG`/`ENV` **before** `RUN npm run build`, or Compose `build.args`, or set env on the CI step that runs `npm run build`.

**This repo:** `frontend-admin/Dockerfile` + `docker-compose.yml` provide a multi-stage production image (`npm run build` → `npm start`, non-root). **CI:** `frontend-admin-ci.yml` runs lint/typecheck/test/build/e2e with build-time `NEXT_PUBLIC_*`; deploy via `frontend-admin-deploy.yml` (GHCR). Full notes: [`docs/CI_CD.md`](docs/CI_CD.md), [`docs/DEPLOYMENT_BUILD_TIME_ENV.md`](docs/DEPLOYMENT_BUILD_TIME_ENV.md), and [Docker](#docker).

### RKSV: `NEXT_PUBLIC_RKSV_ENVIRONMENT`

- **Allowed values:** `TEST`, `PROD` (any casing). **`NEXT_PUBLIC_*` variables must be present when `next dev` / `next build` runs** — not only at `next start` or container runtime. Restart dev or rebuild after changing them.
- **Why:** The RKSV hub (`/rksv`) shows an explicit FinanzOnline environment badge so operators don’t mix up test vs live FinanzOnline context (Austria Registrierkasse / RKSV).
- **If not set (local dev):** `next dev` still starts; `/rksv` shows **UNCONFIGURED** with fix hints and a one-time English `console.warn`.
- **Invalid value (e.g. `STAGING`):** UI shows **INVALID** with a sanitised display of what was read; `npm run build` also fails (`next.config.mjs`).

Code: `src/shared/config/rksvEnvironment.ts`.

3. Generate API Client (when the backend contract changes):

   `orval.config.ts` must point at `../backend/swagger.json`. After API route/DTO changes:

   ```bash
   # From repo root — refresh the committed OpenAPI document
   node scripts/generate-backend-openapi.mjs

   # From frontend-admin — regenerate React Query clients
   npm run generate:api

   # From repo root — fail if generated output drifts
   node scripts/verify-api-client.mjs
   ```

   Commit both `backend/swagger.json` and `frontend-admin/src/api/generated/**` together.
   CI enforces this via `.github/workflows/api-client-alignment.yml`.
   Optional local pre-commit: `node scripts/install-git-hooks.mjs` (runs verify when swagger/generated paths are staged).

## Scripts

| Script | Purpose |
| ------ | ------- |
| `npm run dev` | Dev server (`http://localhost:3000` or `http://admin.regkasse.local:3000`) |
| `npm run dev:clean` | Delete `.next` then `dev` — use after fixing `.env.local` if RKSV stays **UNCONFIGURED** |
| `npm run build` | Production build (**requires** `NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST` or `PROD`) |
| `npm run analyze` | Bundle analyzer (`ANALYZE=true` + RKSV env for build via `@next/bundle-analyzer`) |
| `npm start` | Production server (`next start`) |
| `npm run lint` | ESLint flat config (`eslint.config.mjs`) + OpenAPI critical-path check |
| `npm run lint:fix` | ESLint auto-fix |
| `npm run format` / `format:check` | Prettier write / check (`prettier.config.mjs` + trivago import sort) |
| `npm run lint:api-contract` | Critical OpenAPI paths + orval input (no full regenerate) |
| `npm run typecheck` | `tsc --noEmit` |
| `npm run precommit` / `check` | `lint` + `typecheck` (local gate before commit) |
| `npm run test` | Vitest unit/component suite |
| `npm run test:watch` | Vitest watch mode |
| `npm run test:coverage` | Vitest with coverage (`@vitest/coverage-v8`) |
| `npm run test:contract` | Focused contract / smoke package |
| `npm run test:truth-surfaces` | RKSV / FinanzOnline truth-surface tests |
| `npm run test:operator-copy-locale-parity` | de operator-copy parity |
| `npm run test:e2e` | Playwright E2E (mocked API by default; see [Testing](#testing)) |
| `npm run test:e2e:ui` | Playwright UI mode |
| `npm run test:e2e:headed` | Playwright headed Chromium |
| `npm run generate:api` | Orval client from `../backend/swagger.json` |
| `npm run verify:api-client` | Full Orval drift check (`git status` on `src/api/generated`) |
| `npm run i18n:validate` / `i18n:ci` | Translation validate / full CI gate (validate + boundary + usage + keys) |
| `npm run i18n:keys` / `i18n:keys:check` | Regenerate / assert `translationKeys.ts` |
| `npm run i18n:export-csv` / `i18n:import-csv` | Catalog CSV round-trip (repo `localization/` scripts) |

Repo root (when OpenAPI changes):

```bash
node scripts/generate-backend-openapi.mjs
cd frontend-admin && npm run generate:api
node scripts/verify-api-client.mjs
# optional: node scripts/install-git-hooks.mjs
```

## Project Structure

- `src/app` — Next.js App Router pages (protected admin routes under `(protected)/`)
- `src/api/generated` — Orval generated hooks and models
- `src/api/admin` / `src/api/manual` — hand-written admin clients and helpers
- `src/features` — Feature-specific components and domain hooks
- `src/lib` — Shared providers, query client, notifications, validations
- `src/stores` — Zustand UI-preference stores only (`uiPreferencesStore`, `siderWidthStore`)
- `src/theme` — Ant Design theme / palette tokens
- `src/proxy.ts` — Next.js 16 auth boundary (replaces `middleware.ts`)
- `tests/e2e` — Playwright specs + helpers
- `public/` — PWA icons, `manifest.json`, `robots.txt`
- `docs/` — FA-specific docs (deployment env, truth surfaces, access hub, …)
- `Dockerfile` / `docker-compose.yml` / `nginx.conf` / `vercel.json` — deploy surfaces (Docker, nginx, Vercel)

## Architecture

- **Auth**: `POST /api/Auth/login` with `loginIdentifier` (email or username) + JWT session; see [Authentication](#authentication).
- **Data Fetching**: TanStack Query via Orval generated hooks.
- **UI**: Ant Design v6 with App Router SSR style registry (`@ant-design/nextjs-registry` → `AntdRegistry` in `src/app/layout.tsx`).
- **Ant Design 6**: use `destroyOnHidden` (not `destroyOnClose`), `popupRender` (not `dropdownRender`); official v5→v6 codemod is not published — apply [migration guide](https://ant.design/docs/react/migration-v6) warnings as needed. Toasts: **`useNotify()`**; modals: **`useAntdApp().modal`** — never static antd APIs.
- **i18n**: Custom `I18nProvider` + JSON catalogs; runtime namespace’ler ve dosya adı eşlemesi için `src/i18n/README.md` kaynak kabul edilir.

### Client state (Zustand — UI prefs only)

Zustand **is** a dependency (`zustand` in `package.json`) and lives under `src/stores/`. Use it **only** for ephemeral UI preferences. Do **not** put auth tokens, user profiles, tenant secrets, payment/RKSV payloads, or React Query cache duplicates in Zustand.

| Concern | Where it lives | Notes |
| ------- | -------------- | ----- |
| Access / refresh tokens | `src/features/auth/services/authStorage.ts` | `localStorage` (+ cookie mirror for `proxy.ts`). Not Zustand. |
| Current user / permissions | `AuthProvider` + `useAuth` (React Query `/api/Auth/me`) | Never duplicate into a UI store. |
| Theme / density / a11y prefs | `ThemeProvider` + `src/lib/personalization/*` and/or `uiPreferencesStore` | Immediate `localStorage`; optional sync via `useUserPreferences` (React Query). |
| Sidebar width | `siderWidthStore` / `usePersistedAdminSiderWidth` | `localStorage` key `regkasse-admin-sidebar-width-v1`. |
| App language / format locale | `languageStorage` + `I18nProvider` | `localStorage` only for prefs. |
| Lists, forms, backups, RKSV | TanStack Query | Server state — invalidate/refetch. |

Repo-wide rule: `AGENTS.md` → Frontend-Admin conventions.

### Diagnostics logging

Full guide: [`docs/LOGGING.md`](docs/LOGGING.md).

- **Do not** use raw `console.warn` / `console.error` in feature code (`eslint` `no-console: error`).
- Use `logger` from `@/lib/logger` (or `technicalConsole`) — English technical messages only; UI copy stays in Ant Design via **`useNotify()`** / `notificationService`.
- Every emit is a **structured** record: `time`, `level`, `msg`, `service`, plus context (`component`, `userId`, `sessionId`, …).
- Args are **redacted** before emit (password/token/secret keys, JWT-shaped strings).
- Browser: `info`/`warn`/`debug` in development only; `error` in all environments → optional Sentry.
- Server Route Handlers: **`pino`** (`serverLogger`) → JSON stdout for Datadog / Loki / ELK / CloudWatch.
- Optional client beacon: `NEXT_PUBLIC_LOG_BEACON=true` → `POST /api/monitoring/logs`.
- API interceptor logs status + `code` / short message only (never full response bodies).

```ts
import { logger } from '@/lib/logger';

logger.child({ component: 'PaymentsPage' }).info('Filter applied', { status: 'open' });
logger.error(err, { code: 'PAYMENT_LIST_FAILED' });
```

### Auth boundary (Next.js 16 `proxy.ts`)

| Layer          | File                                           | Responsibility                                                                                                                                            |
| -------------- | ---------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Proxy (server) | `src/proxy.ts`                                 | JWT cookie/header present + structure + `exp`; redirect unauthenticated → `/login`; force `/force-password-change` when claim set. **No** full RBAC here. |
| Session gate   | `AuthGate`                                     | Client session via `/api/Auth/me`; protected layout redirects to `/login`.                                                                                |
| Permissions    | `PermissionRouteGuard` + `routePermissions.ts` | Fail-closed permission check; insufficient → inline **403** (`ForbiddenAccessView`).                                                                      |

- **Do not** add `middleware.ts` — Next.js 16 uses `proxy.ts` (named export `proxy`).
- Access token cookie name must stay aligned: `rk_admin_access_token` (`authStorage` ↔ `proxy.ts`).
- Signature verification remains on the API; the proxy only does optimistic expiry/shape checks.

### API error messages (user-facing)

Use **`translateApiError`** from `src/lib/api/errorTranslator.ts` (or `getUserFacingApiErrorMessage` / `useApiErrorHandler`) for mutation/query failures.

- Maps backend `code` values (e.g. `INVALID_CREDENTIALS`, `DUPLICATE_EMAIL`, `TENANT_NOT_FOUND`, `CASH_REGISTER_CLOSED`) to `common.apiErrors.*` i18n keys (de/en/tr).
- Technical dumps (stack traces, `System.*` exceptions) are logged via `technicalConsole` only — **never** shown in toasts/Alerts.
- Unknown errors → `common.messages.unknownError` (localized generic retry message).
- Extra codes: `registerApiErrorCodeTranslation(code, i18nKey)` (see `createRoleErrors.ts`).

## Authentication

Canonical login is **username or email** (not email-only).

### Login request

```http
POST /api/Auth/login
Content-Type: application/json

{
  "loginIdentifier": "manager1",
  "password": "***",
  "clientApp": "admin"
}
```

- `loginIdentifier` may be an **email** or a **username** (case-insensitive; username rules: `^[a-zA-Z0-9_-]{3,50}$`).
- FA `LoginForm` still sends legacy `email` mirrored to the same value for older OpenAPI shapes.
- Prefer `loginIdentifier` in new code. Do **not** invent a separate username-only endpoint.

### Session after login

1. Tokens stored via `authStorage` (`localStorage` + mirror cookie `rk_admin_access_token` for `proxy.ts`).
2. Session user/permissions from **`GET /api/Auth/me`** (`useAuth` / `AuthProvider`).
3. Forced password change → `/force-password-change` when `mustChangePasswordOnNextLogin` (JWT claim and/or `/me`).
4. **SuperAdmin 2FA** (TOTP) when enabled — see [`docs/AUTH_TWO_FACTOR.md`](../docs/AUTH_TWO_FACTOR.md); Dev may bypass.

### Route protection (layers)

See [Auth boundary](#auth-boundary-nextjs-16-proxyts) above. Short version:

| Unauthenticated                 | Authenticated, no permission            |
| ------------------------------- | --------------------------------------- |
| `proxy` / `AuthGate` → `/login` | `PermissionRouteGuard` → inline **403** |

FA must call **`/api/admin/*`** (and shared Auth) — never `/api/pos/*`. Details: [`AGENTS.md`](../AGENTS.md) → API Boundaries.

## Roles

| UI (de)                 | Backend      | Scope              |
| ----------------------- | ------------ | ------------------ |
| **Mandanten-Admin**     | `Manager`    | Tenant management  |
| **Kassierer**           | `Cashier`    | POS operations     |
| **Super-Administrator** | `SuperAdmin` | Full system access |

Backend role names remain `Manager`, `Cashier`, `SuperAdmin` in API/database; UI labels come from `src/i18n/locales/*/users.json`.

## Digital Services

Website / mobile app generation and **non-fiscal** online orders (separate from POS / TSE). Full guides: [`docs/DIGITAL_SERVICES.md`](../docs/DIGITAL_SERVICES.md), [`docs/ONLINE_ORDERS.md`](../docs/ONLINE_ORDERS.md), [`docs/PERMISSIONS_MATRIX.md`](../docs/PERMISSIONS_MATRIX.md).

### For Mandanten-Admin (Manager)

- View website/app status — `/settings/digital`
- Preview templates — `/tenant/{id}/website-preview`
- Request services (Super Admin reviews) — request actions on the digital portal
- Manage online orders (status only) — `/orders/online`

Permissions: `digital.view` / `preview` / `request`, `digital.orders.view` / `manage`. **No** create / publish / delete / POS cart bridge.

### For Super Admin

- Create websites for tenants — `/admin/digital`, `/tenant/{id}/digital`
- Create apps for tenants (PWA / Native package)
- Approve / reject requests — `/admin/digital/requests`
- Publish services — generators + publish actions (`digital.create` / `publish` / `manage`)

Optional POS cart bridge on an online order requires `digital.orders.approve` (not the Manager happy path).

## User Management

Users are created directly by administrators (Super Admin or **Mandanten-Admin**). **No email invitations** are sent.

### Creating a user

1. Navigate to **Users** → **Create User** (or tenant detail → **Benutzer** tab → **Benutzer anlegen**).
2. Fill in **E-Mail**, name, **Rolle**, and **Mandant** (Super Admin only, when not on a fixed tenant).
3. The system generates a secure password (shown **once** in a follow-up modal).
4. Copy **username**, **email**, and password from the success modal (`UserCreatedSuccessModal`) and share them securely (not via the product email channel).
5. The user must change the password on first login (`MustChangePasswordOnNextLogin`).

Optional **`userName`** on manual create: must be globally unique; when omitted, the backend auto-generates `{rolePrefix}{n}` (same rules as Quick Create).

### Quick Create User ("Schnell anlegen")

The Quick Create tab in `CreateUserModal` lets administrators create tenant users rapidly with auto-generated credentials.

**How it works:**

1. Open **Users** → **Create User** (or tenant detail → **Benutzer**) and switch to the **Schnell anlegen** tab.
2. Select **Rolle**: **Mandanten-Admin** (`Manager`), **Kassierer** (`Cashier`), or **Buchhaltung** (`Accountant`) — platform `Admin` / `SuperAdmin` are not available on this path.
3. Select **Mandant** when creating from the global users page (fixed when opened from tenant detail).
4. The system generates:
   - **Username:** `{rolePrefix}{nextAvailableNumber}` (e.g. `manager1`, `cashier2`, `user3` for Accountant)
   - **Email:** `{role}_{random6}@{tenantSlug}.regkasse.at` (e.g. `cashier_a3f9k2@dev.regkasse.at`)
   - **Password:** secure 12-character random password
5. `QuickUserSuccessModal` shows username, email, and password once (per-field copy + **copy all**).
6. User must change password on first login (`forcePasswordChangeOnNextLogin`).

**Username rules:**

- Minimum 3, maximum 50 characters.
- Allowed characters: letters (`a-z`), numbers (`0-9`), underscore (`_`), hyphen (`-`).
- **Case-insensitive:** the system does not distinguish uppercase and lowercase for login or uniqueness (`Manager1` and `manager1` are the same).
- Must be **globally unique** across all Identity users (not per tenant).
- Usable for login instead of email (`loginIdentifier` on `POST /api/Auth/login`).
- Auto-generated names: lowercase role prefix + digits; collision adds `_` + random suffix.
- Optional custom `userName` in the API body when creating (manual or quick); validation rejects case-insensitive duplicates.

**Login with username (FA):**

- Login form field accepts **email or username** (`LoginForm` → `loginIdentifier`, legacy `email` mirrored for compatibility).
- UI hint: tooltip and helper text state that username casing does not matter (`common.auth.loginIdentifierTooltip`, `loginIdentifierCaseHint`).
- POS uses the same backend contract; see `frontend/README.md` (Authentication).

**Rate limit:** up to 10 quick users per tenant per hour (server-side audit check).

**UI / API:**

- Hook: `createQuickUser` in `src/features/super-admin/api/quickUser.ts`
- Preview pattern: `getQuickUsernamePattern` in `src/features/super-admin/lib/quickUserPreview.ts`

**API example:**

```bash
# Quick Create (tenant-scoped) — role only; email, username, password generated server-side
POST /api/admin/tenants/{tenantId}/users/quick
Content-Type: application/json
Authorization: Bearer <token>

{
  "role": "Manager"
}

# Optional custom login name (must be unique)
{
  "role": "Cashier",
  "userName": "cashier_front"
}
```

**Response** (`CreateTenantUserResultDto`):

```json
{
  "userId": "...",
  "email": "manager_a3f9k2@dev.regkasse.at",
  "userName": "manager1",
  "generatedPassword": "...",
  "forcePasswordChangeOnNextLogin": true,
  "success": true,
  "tenantPortalUrl": "https://dev.regkasse.at",
  "role": "Manager"
}
```

Manual tenant user create (non-quick): `POST /api/admin/tenants/{tenantId}/users` with `email`, optional `userName`, `role`, etc. Platform users: `POST /api/admin/users` (no quick endpoint).

### Add existing user (tenant only)

On tenant detail (`/admin/tenants/{tenantId}`), **Bestehenden Benutzer hinzufügen** assigns an existing Identity account to the tenant (`AddExistingUserModal` → `POST …/users/assign`). This does **not** create a new login.

**API:** `createUser` in `src/features/users/api/users.ts` (tenant → `POST /api/admin/tenants/{id}/users`; platform → `POST /api/admin/users`). Hook: `useCreateUser`.

**Docs:** [`../docs/USER_MANAGEMENT.md`](../docs/USER_MANAGEMENT.md).

## Multi-Tenant Architecture

Regkasse uses a multi-tenant architecture: one API instance, many companies (tenants). Authoritative overview: [`docs/MULTI_TENANT.md`](../docs/MULTI_TENANT.md) · production hosts: [`docs/POS_PRODUCTION_ARCHITECTURE.md`](../docs/POS_PRODUCTION_ARCHITECTURE.md) · onboarding brief: [`REGKASSE_AI_ONBOARDING.md`](../REGKASSE_AI_ONBOARDING.md).

### Production hosts (Single POS UI)

| Surface | URL                         | Tenant source                                                |
| ------- | --------------------------- | ------------------------------------------------------------ |
| POS UI  | `https://pos.regkasse.at`   | JWT `tenant_id` after login (not Host slug)                  |
| FA UI   | `https://admin.regkasse.at` | JWT / impersonation; Super Admin platform host               |
| API     | `https://api.regkasse.at`   | JWT `tenant_id` (do **not** use `X-Tenant-Id` in Production) |

Reserved Host labels (never tenant slugs): `pos`, `api`, `admin`, `www`.

Optional **custom website domains** map Host → tenant via verified `TenantDomain` (FA `/settings/website`). Legacy `{slug}.regkasse.at` for POS is transition-only — not the production POS entry.

### Tenant identification (API)

| Environment     | Mechanism                                                      |
| --------------- | -------------------------------------------------------------- |
| **Production**  | JWT `tenant_id` after auth; Host reserved labels are not slugs |
| **Development** | `X-Tenant-Id: {slug}` and/or `?tenant={slug}` (slug, not UUID) |

### Data isolation

- Tenant-scoped tables implement `ITenantEntity` with non-null `tenant_id`
- EF global query filters apply via `ICurrentTenantAccessor`
- Tenants never see other tenants’ data
- Cross-tenant access → **HTTP 404** (not 403)
- Never use `IgnoreQueryFilters()` except Super Admin ops

### Development Mode (FA)

- Local FA: often `http://admin.regkasse.local:3000` with hosts-file entry
- Dev tenant selector in the header: `HeaderDevTenantSwitch` → `GET /api/tenants/switcher`
- Selection persists slug (`localStorage` / `dev_tenant_id`) and sets **`X-Tenant-Id`** on API calls, then reloads
- Backend must be `ASPNETCORE_ENVIRONMENT=Development` for header/query resolution

See [Tenant Switching](#tenant-switching) and [`docs/TENANT_MANAGEMENT.md`](../docs/TENANT_MANAGEMENT.md).

## Development Setup for Multi-Tenant Testing

**Option 1 — Header:**

```bash
curl -H "X-Tenant-Id: cafe" http://localhost:5184/api/health
```

**Option 2 — Query string:**

```bash
curl "http://localhost:5184/api/admin/payments?tenant=dev"
```

(Requires auth for payments; use `cafe` / `dev` only if matching `tenants.slug` in DB.)

**Option 3 — Hosts file (FA):**

```text
127.0.0.1 admin.regkasse.local
127.0.0.1 dev.regkasse.local
```

Access FA: `http://admin.regkasse.local:3000` · API health: `http://localhost:5184` (or slug host if configured).

**Option 4 — FA tenant switcher**

In **development**, FA shows a **searchable tenant dropdown in the header** (`HeaderDevTenantSwitch`). It loads tenants from **`GET /api/tenants/switcher`**: Super Admin sees all tenants; other users see active memberships only. Selection sets `X-Tenant-Id` via persisted slug and reloads.

See [Tenant Switching](#tenant-switching) and [`../docs/TENANT_MANAGEMENT.md`](../docs/TENANT_MANAGEMENT.md).

Backend must be `ASPNETCORE_ENVIRONMENT=Development`. See [`REGKASSE_AI_ONBOARDING.md`](../REGKASSE_AI_ONBOARDING.md).

**Backend note:** Singletons that touch EF must use `IServiceScopeFactory` (scoped `AppDbContext` / `ICurrentTenantAccessor`). Startup license warnings do not block the API.

## Tenant Switching

| Environment     | Mechanism                                                                    | Component / API                                                                     |
| --------------- | ---------------------------------------------------------------------------- | ----------------------------------------------------------------------------------- |
| **Production**  | JWT `tenant_id` on FA session; Super Admin impersonation for mandant support | `applyTenantImpersonationSession`, `POST /api/admin/tenants/{id}/impersonate`       |
| **Development** | Header switcher + `X-Tenant-Id`                                              | `HeaderDevTenantSwitch`, `GET /api/tenants/switcher`, `persistTenantSlugAndRefresh` |

**Dev switcher features:** search by name/slug/email; status icons (active + admin / no admin / suspended); mandant license tag per row; Super Admin warning when switching to tenant without owner admin (`TenantSwitcherNoAdminFlow`).

**Header context:** `TenantBadge` (active company or Super Admin mode), `LicenseStatusIndicator` (**Mandantenlizenz** only via `useHeaderTenantLicense` — four states: keine / abgelaufen / bald ab / lizenziert; never Server-Lizenz).

| Header badge (Mandanten-Admin) | Condition                      | Color  |
| ------------------------------ | ------------------------------ | ------ |
| Keine Mandantenlizenz          | `license_valid_until_utc` null | Red    |
| Lizenz abgelaufen              | past end date                  | Red    |
| Lizenz läuft bald ab           | ≤7 days                        | Orange |
| Lizenziert                     | >7 days                        | Green  |

**Server license page:** `/admin/license` — `license.page.title` = _Server-Lizenz (On-Premise)_; uses `GET /api/admin/license/deployment-status` — separate from header mandant badge.

Utilities: `src/features/super-admin/utils/tenantHeaderSwitcher.ts`, `src/features/tenancy/hooks/useTenantListForSwitcher.ts`.

![Dev tenant switcher](../docs/images/tenant-management/fa-header-tenant-switcher.png)

## Testing

Full strategy: [`docs/TESTING.md`](docs/TESTING.md).

### Unit / component (Vitest)

```bash
cd frontend-admin

npm run test                 # full Vitest suite
npm run test:watch           # watch mode while developing
npm run test -- path/to/file.test.ts   # focused file
npm run test:coverage        # Vitest + v8 coverage (enforces coverage gate)

npm run test:contract        # contract / smoke package
npm run test:truth-surfaces  # RKSV / FinanzOnline truth surfaces
npm run typecheck
npm run lint
```

#### Coverage gate (≥80% lines)

`npm run test:coverage` measures a **logic gate** (utils, logic helpers, logging/monitoring helpers, validations, key hooks) — not every App Router page. Thresholds live in `vitest.config.ts` (lines **80%**, statements **75%**, functions **70%**, branches **60%**).

| Focus | Examples |
| ----- | -------- |
| Utilities | Filter mappers, Vienna calendar, CSV, date formatting, Monatsbeleg helpers |
| Hooks | `useDebounce`, permissions / can-access |
| Interactions (RTL) | Modals (`ConfirmDialog` confirm/cancel/loading), forms |
| Edge / error | Empty filters, invalid dates, canceled HTTP, redacted log args |

HTML report: `coverage/index.html`. Run the **full** suite for an accurate gate score (path-filtered runs under-count).

From repo root (when touching OpenAPI / i18n budgets):

```bash
node scripts/verify-api-client.mjs
node localization/scripts/validate-translations.mjs --app frontend-admin --strictMissing true --orphanPolicy error
```

### E2E (Playwright)

Config: `playwright.config.ts` · Specs: `tests/e2e/` · CI: `frontend-admin-ci.yml` (PR gate) · manual: `frontend-admin-e2e.yml`.

| Mode | How | Notes |
| ---- | --- | ----- |
| **Default (mocked API)** | `npm run test:e2e` | Deterministic; no live backend. Playwright starts `npm run start` unless `E2E_SKIP_WEBSERVER=1`. |
| **UI / headed** | `npm run test:e2e:ui` / `test:e2e:headed` | Local debugging. |
| **Live API** | `E2E_LIVE=1 E2E_ADMIN_LOGIN=… E2E_ADMIN_PASSWORD=… npm run test:e2e` | Hits a real stack; see `.env.example` E2E section. |

First-time local Chromium:

```bash
npx playwright install chromium
```

Typical CI sequence: `npm ci` → `npx playwright install --with-deps chromium` → `npm run build` (with `NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST`) → `npm run test:e2e`.

Covered flows (examples): login, tenant create, user create, backup hub smoke, RKSV Sonderbelege.

### What to cover

| Priority | Examples |
| -------- | -------- |
| Auth / RBAC | `PermissionRouteGuard`, `proxy` unit tests, login `loginIdentifier` |
| Multi-tenant | switcher hooks, impersonation helpers, 404 semantics in UI |
| High-risk FA | backup permissions, RKSV truth badges, FinanzOnline outbox/queue presentation |
| Shared libs | `translateApiError`, `queryCachePolicy`, `useAutoSave` drafts (no password in storage) |
| i18n | missing keys / boundary (`npm run i18n:ci`) |
| E2E | critical operator paths under `tests/e2e/` (mocked by default) |

### Best practices

- Prefer **Vitest** + Testing Library for components; mock network with Orval hooks / axios mocks — do not hit a live API in unit tests.
- Keep tests **fail-closed** for permissions (missing route permission → 403 UI).
- When changing OpenAPI, regenerate client and run `verify:api-client` before committing.
- Do not assert on raw English backend dumps in UI tests — assert i18n keys / `translateApiError` outcomes.
- Truth-surface / operator-copy suites are CI-relevant for RKSV screens — run `test:truth-surfaces` when editing those areas.
- For component tests that use Ant Design App APIs, wrap with the same App / notify providers the product uses (`useNotify` / `useAntdApp`).

More: [`docs/`](../docs/), [`docs/TRUTH_SURFACE_AUTOMATED_TESTS.md`](docs/TRUTH_SURFACE_AUTOMATED_TESTS.md), [`AGENTS.md`](../AGENTS.md) → Validation Commands.

## User feedback

Operators can send structured feedback from any protected page via the floating **Feedback** button (bottom-right).

| | |
| - | - |
| **Categories** | Ease of use, Performance, Feature request, Bug |
| **My feedback** | Submitters see status: Under review → In progress → Implemented (or Declined / Duplicate) |
| **Inbox** | Super Admin: `/admin/feedback` (`system.critical`) |
| **Cadence** | Weekly triage (Monday) — see [`docs/USER_FEEDBACK.md`](docs/USER_FEEDBACK.md) |
| **Storage** | PostgreSQL `admin_user_feedback` via `POST/GET /api/admin/feedback` |

```bash
# After pulling: apply DB migration
cd backend && dotnet ef database update
```

## Monitoring

Full guide: [`docs/MONITORING.md`](docs/MONITORING.md). Alert recipes: [`monitoring/sentry-alert-recipes.md`](monitoring/sentry-alert-recipes.md). Grafana: [`monitoring/grafana-fa-dashboard.json`](monitoring/grafana-fa-dashboard.json).

| Signal | Mechanism | Alert |
| ------ | --------- | ----- |
| **Uptime** | `GET /health`, `GET /api/monitoring/health` | External probe (UptimeRobot / k8s) on HTTP ≠ 200 |
| **API error rate** | axios → rolling window + Sentry | **> 5%** (5 min, ≥20 samples) |
| **API response time** | axios duration | **> 1 s** per call |
| **Client errors** | Sentry (logger / unhandled / axios 5xx) | Issue volume |
| **FA process health** | `/api/monitoring/health` (uptime, memory, Sentry flag) | Probe + `/admin/monitoring` |
| **Web Vitals** | See [Performance monitoring](#performance-monitoring) | LCP > 2.5 s |

**In-app dashboard (Super Admin):** `/admin/monitoring` — live error rate, p50/p95/p99, health probe, recent sanitized API calls.

```bash
curl -sS http://localhost:3000/health
curl -sS http://localhost:3000/api/monitoring/health
```

## Performance monitoring

Real-user Core Web Vitals for FA (field data). Full ops guide: [`docs/PERFORMANCE_MONITORING.md`](docs/PERFORMANCE_MONITORING.md).

| Layer | What |
| ----- | ---- |
| **Collection** | `web-vitals` in `WebVitalsReporter` (FCP, LCP, CLS, TTFB, **INP**) — mounted from root `layout.tsx` via `PerformanceMonitoring` |
| **Vercel** | `@vercel/speed-insights` (on in production unless `NEXT_PUBLIC_SPEED_INSIGHTS=false`) |
| **Primary dashboard** | Sentry → Performance / Web Vitals (+ Issues tagged `source:web-vitals`) |
| **Optional self-hosted** | `NEXT_PUBLIC_WEB_VITALS_BEACON=true` → `POST /api/monitoring/web-vitals` → structured stdout for Grafana Loki / Datadog |
| **Primary alert** | LCP **> 2.5 s** → Sentry warning `Web vital degraded: LCP` (configure Issue Alert in Sentry) |
| **Monthly review** | Agenda + checklist in the performance doc (§6) |

**TTI:** not tracked — replaced by **INP** in modern Core Web Vitals.

```bash
# Budget helper tests
npm run test -- src/lib/monitoring/__tests__/webVitalsBudgets.test.ts
```

## Troubleshooting

| Symptom | Likely cause | Fix |
| ------- | ------------ | --- |
| `/rksv` shows **UNCONFIGURED** | `NEXT_PUBLIC_RKSV_ENVIRONMENT` missing at compile time | Set `TEST` or `PROD` in `frontend-admin/.env.local`, then `npm run dev:clean` (or rebuild). See [RKSV env](#rksv-next_public_rksv_environment) and [`docs/DEPLOYMENT_BUILD_TIME_ENV.md`](docs/DEPLOYMENT_BUILD_TIME_ENV.md). |
| `/rksv` shows **INVALID** | Value not `TEST`/`PROD` | Fix env and restart/rebuild; production `next build` fails on invalid values. |
| Env changes ignored | `.env.local` at **repo root** or only runtime env after image build | Place env in **`frontend-admin/.env.local`**; `NEXT_PUBLIC_*` must exist **before** `next build` / `next dev` compile. |
| **API client out of sync** / Orval types wrong | `swagger.json` or `src/api/generated` drifted | From repo root: `node scripts/generate-backend-openapi.mjs` → `cd frontend-admin && npm run generate:api` → `node scripts/verify-api-client.mjs`. CI: `api-client-alignment.yml`. |
| Redirect loop / always `/login` | Missing/expired JWT cookie | Clear site data; log in again; confirm `authStorage` writes `rk_admin_access_token` cookie for `proxy.ts`. |
| Inline **403** with sidebar visible | Authenticated but missing route permission | Expected (`PermissionRouteGuard`). Check role matrix / `ROUTE_PERMISSIONS`. |
| Wrong tenant data in **Development** | Stale `dev_tenant_id` / switcher | Use header tenant switcher; confirm `X-Tenant-Id` on requests; backend must be Development. |
| Production tenant header ignored | `X-Tenant-Id` not used in Production | Correct — use JWT / impersonation. See [`docs/MULTI_TENANT.md`](../docs/MULTI_TENANT.md). |
| Toast shows English stack / Exception | Should not happen after `translateApiError` | Prefer `useNotify` / `useApiErrorHandler` / `translateApiError`; file a bug if raw dumps appear. |
| `message is not a function` / theme-less toasts | Static `message` / `notification` from `antd` | Switch to `useNotify()` (or `notificationService` outside React). |
| Bundle / chart load on every page | Accidental static `recharts` import | Use `next/dynamic` / page-local import (see Recent Improvements). |
| Port **3000 already in use** | Another `next dev` / process | Stop the other process, or use the port Next prints (e.g. `3001`). For Docker: `ADMIN_PORT=3001 docker compose up`. |
| Flaky / broken `node_modules` | Corrupt install or lockfile drift | Delete `node_modules` (and optionally lockfile), then `npm install`; prefer **`npm ci`** in CI with committed `package-lock.json`. |
| Deleting `package-lock.json` then install | Caret ranges resolve newer minors (e.g. antd) | Prefer restoring lockfile from git and `npm ci` unless you intentionally refresh deps. |
| Docker `/rksv` **UNCONFIGURED** after `docker run -e …` | `NEXT_PUBLIC_*` set only at runtime | Rebuild with `--build-arg` / Compose `build.args` (see [Docker](#docker)). |
| Vercel “routes manifest” / empty deploy | `outputDirectory` overridden to `.next` | Remove Output Directory override; use Framework = Next.js (see [Vercel](#vercel)). |
| Playwright browsers missing | Chromium not installed | `npx playwright install chromium` (CI: `--with-deps`). |
| E2E fails to start webServer | No production build / wrong env | Run `npm run build` with RKSV env set, or point `E2E_BASE_URL` + `E2E_SKIP_WEBSERVER=1` at an already running server. |
| i18n CI fail | Missing/orphan keys | `npm run i18n:ci`; catalogs under `src/i18n/locales/{de,en,tr}/`. |
| Sentry inactive locally | DSN unset (expected) | Leave `NEXT_PUBLIC_SENTRY_DSN` empty in local/dev; set at **build** time for production. |
| No Speed Insights data | Not on Vercel, or insights disabled | Vercel-hosted only for that dashboard; use Sentry Web Vitals elsewhere. Set `NEXT_PUBLIC_SPEED_INSIGHTS=true` only if needed locally (still needs Vercel backend). |
| Web Vitals beacon 404 | `NEXT_PUBLIC_WEB_VITALS_BEACON` not `true` at build | Rebuild with the flag; see [`docs/PERFORMANCE_MONITORING.md`](docs/PERFORMANCE_MONITORING.md). |

## Super Admin Features

Access: **`admin.regkasse.at`** (or local dev on platform host). Role: **`SuperAdmin`** or `system.critical`.

| Feature                                        | Route                         | Key files                                                                                                                      |
| ---------------------------------------------- | ----------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| Tenant list / create / edit / suspend / delete | `/admin/tenants`              | `app/(protected)/admin/tenants/page.tsx`, `features/super-admin/api/adminTenants.ts`                                           |
| Tenant detail (users, license, registers)      | `/admin/tenants/[tenantId]`   | `TenantDetailUsersTab`, `LicenseManager`, `TenantDetailCashRegistersTab`                                                       |
| Impersonate (“Login as”)                       | list / detail / home selector | `impersonateAdminTenant`, `ImpersonationRedirectOverlay`                                                                       |
| Platform home (pick tenant)                    | `/admin`                      | `SuperAdminTenantSelector`                                                                                                     |
| Server license (On-Premise)                    | `/admin/license`              | `api/manual/adminLicense.ts` — **Server-Lizenz**; not Mandantenlizenz (see header badge)                                       |
| Billing tenant license (docs)                  | —                             | [`../docs/BILLING_TENANT_LICENSE.md`](../docs/BILLING_TENANT_LICENSE.md); Mandanten-Admin API `POST /api/admin/license/extend` |

**Create tenant** runs backend `TenantProvisioningService` (cash register, demo products, owner admin, optional 30-day trial). Success modal shows one-time credentials.

**Screenshots (add PNGs under `docs/images/tenant-management/`):**

| Image                                                                             | Description                                         |
| --------------------------------------------------------------------------------- | --------------------------------------------------- |
| ![Tenant list](../docs/images/tenant-management/fa-tenant-list.png)               | Mandantenverwaltung table                           |
| ![Tenant users](../docs/images/tenant-management/fa-tenant-detail-users.png)      | Create user / add existing / roles / reset password |
| ![Super Admin home](../docs/images/tenant-management/fa-super-admin-selector.png) | Tenant picker + impersonate                         |

**Customer onboarding:** `CreateTenantWizard` on tenant list — see [`../docs/CUSTOMER_ONBOARDING.md`](../docs/CUSTOMER_ONBOARDING.md).

**Impersonation (production):** redirect to `https://{tenantSlug}.regkasse.at/impersonate-callback#impersonate_token=…` — [`../docs/IMPERSONATION_FLOW.md`](../docs/IMPERSONATION_FLOW.md).

**Docs index:** [`../docs/TENANT_MANAGEMENT.md`](../docs/TENANT_MANAGEMENT.md), [`../docs/BILLING_TENANT_LICENSE.md`](../docs/BILLING_TENANT_LICENSE.md), [`../docs/CUSTOMER_ONBOARDING.md`](../docs/CUSTOMER_ONBOARDING.md), [`../docs/USER_MANAGEMENT.md`](../docs/USER_MANAGEMENT.md), [`../docs/CASH_REGISTER_LIFECYCLE.md`](../docs/CASH_REGISTER_LIFECYCLE.md), [`../docs/LICENSE_SYSTEM.md`](../docs/LICENSE_SYSTEM.md), [`../docs/MULTI_TENANT.md`](../docs/MULTI_TENANT.md), [`../docs/BACKUP_SYSTEM.md`](../docs/BACKUP_SYSTEM.md).

## Backup Management

Role-aware Backup & Disaster Recovery UI. Full guide: [`../docs/BACKUP_SYSTEM.md`](../docs/BACKUP_SYSTEM.md) · Permissions: [`../docs/BACKUP_PERMISSIONS.md`](../docs/BACKUP_PERMISSIONS.md) · RKSV restore: [`../docs/RKSV_COMPLIANCE.md`](../docs/RKSV_COMPLIANCE.md).

There is **no** `backup.view` permission. Read access uses **`settings.view`**. Manage uses **`backup.manage`** (Mandanten-Admin default); platform ops use **`settings.manage`** (implies `backup.manage`).

### Access

|                                          |                                                                               |
| ---------------------------------------- | ----------------------------------------------------------------------------- |
| **Hub URL**                              | `/backup` (role-aware overview)                                               |
| **Related routes**                       | `/backup/dashboard`, `/backup/runs`, `/backup/configuration`, `/backup/audit` |
| **View**                                 | `settings.view`                                                               |
| **Trigger / schedule / tenant download** | `backup.manage` (or `settings.manage`)                                        |
| **Execution mode / platform config**     | `settings.manage` only                                                        |
| **Restore / restore drills**             | **Super Admin only** (`useBackupPermissions().canRestore`)                    |

Hook: `src/features/backup/hooks/useBackupPermissions.ts`. Routes: `src/shared/backupAreaRoutes.ts`.

### Super Admin view (`SystemBackupView`)

- View backup status, System + all tenant runs, DR dashboard metrics.
- Create **System** manual backup.
- View backup history / list (strategy column when showing all).
- Configure schedule and **platform** execution mode.
- **Validation-only** restore from System `pg_dump` (`RestoreModal`, dual acknowledgement / dual Super Admin approval) — **not** production restore.
- Restore drills via DR surfaces (not Mandanten-Admin).

### Mandanten-Admin view (`TenantBackupView`)

- View **own tenant** backups only (`BackupStrategyKind.Tenant`).
- Create manual **Tenant** backup; configure tenant-scoped schedule/retention when permitted.
- Download / import own Tenant packages.
- **No** restore (API + UI).
- **No** access to other tenants’ backups or **System** dumps (Identity / all-tenants).

Hub page: `src/app/(protected)/backup/page.tsx` → `isSuperAdmin ? SystemBackupView : TenantBackupView`.

### Features (by capability)

| Feature                    | Mandanten-Admin    | Super Admin                   |
| -------------------------- | ------------------ | ----------------------------- |
| View backup status         | Own Tenant         | All + System                  |
| Create manual backup       | Tenant strategy    | System strategy               |
| View backup history / list | Own Tenant         | All                           |
| Configure schedule         | Own tenant binding | Yes (+ platform mode)         |
| Restore from backup        | No                 | Validation-only (System dump) |

## License display

| What you see                                                             | Meaning                                                                         |
| ------------------------------------------------------------------------ | ------------------------------------------------------------------------------- |
| **Super Admin Modus** (`TenantBadge`)                                    | Platform host; no mandant context                                               |
| Green/orange/red **Mandantenlizenz** tag (Mandanten-Admin on `{slug}.*`) | `tenants.license_valid_until_utc` / trial heuristic — **not** server On-Premise |
| **Lizenz (On-Premise)** `/admin/license`                                 | Deployment / machine license (`LicenseService`)                                 |
| Dev switcher license column                                              | Same mandant fields per tenant row                                              |

Expiry: header tag **orange** if ≤7 days; **red** if expired; content **banner** for Mandanten-Admins if ≤15 days or expired. Super Admin platform mode hides these (`suppressLicenseWarnings`).

![Manager license badge (placeholder)](../docs/images/tenant-management/fa-manager-license-badge.png)  
![Deployment license page (placeholder)](../docs/images/tenant-management/fa-deployment-license.png)

### Multi-Tenant Security (client)

- Production: tenant from JWT (and FA host `admin.regkasse.at`); do **not** rely on `X-Tenant-Id` in production builds.
- Cross-tenant IDs from API return 404; handle as “not found”, not permission denied.
- Dev only: header/query documented in [`REGKASSE_AI_ONBOARDING.md`](../REGKASSE_AI_ONBOARDING.md) and [`docs/MULTI_TENANT.md`](../docs/MULTI_TENANT.md).

## API Headers

### Tenant Identification

- **Production:** JWT `tenant_id` (no `X-Tenant-Id`).
- **Development:** `X-Tenant-Id: {slug}` or `?tenant={slug}` (slug, not UUID).

### Super Admin Endpoints

- `/api/admin/tenants/*` — `SuperAdmin` only; see `src/features/super-admin/`.
- Impersonation: `POST /api/admin/tenants/{tenantId}/impersonate` for tenant-scoped support.

## Deployment Requirements

### DNS Configuration

- Production entry points: `pos.regkasse.at`, `admin.regkasse.at`, `api.regkasse.at` (see [`docs/POS_PRODUCTION_ARCHITECTURE.md`](../docs/POS_PRODUCTION_ARCHITECTURE.md)).
- Optional wildcard / custom domains for tenant websites — not required for Single POS UI.
- SSL must cover `admin.regkasse.at` (and wildcards if used for websites).

### Build & ship checklist (frontend-admin)

1. **Set build-time env** before `next build` (at minimum):
   - `NEXT_PUBLIC_API_BASE_URL` → production API origin (e.g. `https://api.regkasse.at`)
   - `NEXT_PUBLIC_RKSV_ENVIRONMENT` → `PROD` (or `TEST` for non-prod builds)
   - Optional: `NEXT_PUBLIC_SENTRY_DSN`, `NEXT_PUBLIC_SENTRY_ENVIRONMENT`, `NEXT_PUBLIC_SENTRY_RELEASE`
2. Install with a locked tree: **`npm ci`** (commit `package-lock.json`).
3. `npm run build` → then serve with `npm start` (or Docker — see [Docker](#docker)).
4. Do **not** expect runtime-only injection of `NEXT_PUBLIC_*` to fix an already-built image — see [`docs/DEPLOYMENT_BUILD_TIME_ENV.md`](docs/DEPLOYMENT_BUILD_TIME_ENV.md).

### Docker

Files in `frontend-admin/`:

| File | Role |
| ---- | ---- |
| `Dockerfile` | Multi-stage (`deps` → `builder` → `runner`): `npm ci`, `npm run build`, non-root `nextjs` user, `npm start` on port 3000 |
| `docker-compose.yml` | Port publish + build-args for `NEXT_PUBLIC_*` |
| `.dockerignore` | Excludes `node_modules`, `.next`, env secrets, test reports |

```bash
cd frontend-admin

# Local / TEST image (API on host — Docker Desktop)
docker compose build
docker compose up -d
curl -sI http://localhost:3000/login

# Production-oriented build
NEXT_PUBLIC_API_BASE_URL=https://api.regkasse.at \
NEXT_PUBLIC_RKSV_ENVIRONMENT=PROD \
docker compose build --no-cache
```

Equivalent without Compose:

```bash
docker build -t regkasse-frontend-admin:local \
  --build-arg NEXT_PUBLIC_API_BASE_URL=https://api.regkasse.at \
  --build-arg NEXT_PUBLIC_RKSV_ENVIRONMENT=PROD \
  .
docker run --rm -p 3000:3000 --name regkasse-frontend-admin regkasse-frontend-admin:local
```

**Important:** Rebuild the image when `NEXT_PUBLIC_*` values change. Runtime `environment:` in Compose does not rewrite the client bundle.

### Vercel

Config: [`vercel.json`](vercel.json) in this package (set the Vercel project **Root Directory** to `frontend-admin` in a monorepo).

| Setting | Value | Notes |
| ------- | ----- | ----- |
| Framework | `nextjs` | Required — enables the Next.js builder / SSR |
| Install | `npm ci` | Locked install from `package-lock.json` |
| Build | `npm run build` | Same as local/CI |
| Output directory | *(do not override)* | Next.js writes `.next` internally. **Do not** set Project Settings / `outputDirectory` to `.next` — that breaks the routes manifest (Vercel treats it as a static folder). |
| Region | `fra1` (Frankfurt) | Closest default for `admin.regkasse.at` |

**Environment variables (Project → Settings → Environment Variables)** — must be available for **Build** (and Production):

| Variable | Example | Required |
| -------- | ------- | -------- |
| `NEXT_PUBLIC_API_BASE_URL` | `https://api.regkasse.at` | Yes |
| `NEXT_PUBLIC_RKSV_ENVIRONMENT` | `PROD` | Yes (`TEST` for Preview if desired) |
| `NEXT_PUBLIC_SENTRY_DSN` | (optional) | No |
| `SENTRY_AUTH_TOKEN` / `SENTRY_ORG` / `SENTRY_PROJECT` | (optional, build-only) | No |

```bash
cd frontend-admin
# Preview (requires `vercel login` + linked project)
npx vercel --yes
# Production
npx vercel --prod --yes
```

Validate config locally without deploy: `node -e "JSON.parse(require('fs').readFileSync('vercel.json','utf8'))"`.

### Nginx (self-hosted / Docker behind reverse proxy)

Files:

| File | Role |
| ---- | ---- |
| [`nginx.conf`](nginx.conf) | TLS vhost for `admin.regkasse.at` → `http://127.0.0.1:3000` (Next.js / Docker) |
| [`deploy/nginx-map-connection-upgrade.conf`](deploy/nginx-map-connection-upgrade.conf) | `map` for WebSocket `Upgrade` (include under `http {}`) |

```bash
# After Next.js is listening on :3000 (npm start or docker compose up):
sudo nginx -t
sudo systemctl reload nginx
curl -sI https://admin.regkasse.at/login
```

Proxy headers include `Host`, `X-Forwarded-*`, and WebSocket upgrade for SignalR/SSE. CSP remains owned by `next.config.mjs` (do not duplicate a conflicting CSP in nginx).

### Environment Variables

| Variable | When | Notes |
| -------- | ---- | ----- |
| `NEXT_PUBLIC_API_BASE_URL` | Build | Client + CSP `connect-src` API origin |
| `NEXT_PUBLIC_RKSV_ENVIRONMENT` | Build | `TEST` \| `PROD`; required for production `next build` |
| `NEXT_PUBLIC_SENTRY_DSN` | Build | Optional; omit locally |
| `SENTRY_AUTH_TOKEN` / `SENTRY_ORG` / `SENTRY_PROJECT` | Build (server) | Source-map upload only — never commit |
| Backend `ASPNETCORE_ENVIRONMENT` | API runtime | `Development` allows `X-Tenant-Id`; `Production` uses JWT tenant |

### CI/CD (GitHub Actions)

Full guide: [`docs/CI_CD.md`](docs/CI_CD.md). No GitLab CI in this repo.

#### Quality gate — `frontend-admin-ci.yml`

Runs on PRs and pushes to `main`/`master` when `frontend-admin/**` changes:

| Step | Command |
| ---- | ------- |
| Lint | `npm run lint` |
| Typecheck | `npm run typecheck` |
| Unit tests | `npm run test` |
| Production build | `npm run build` (`NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST`) |
| E2E | `npm run test:e2e` (Playwright, after quality job) |

**Caching:** npm download cache (`setup-node`), `node_modules` (lockfile-keyed), and `.next/cache` for faster builds.

**Failures:** optional Slack via repo secret `SLACK_WEBHOOK_URL`; otherwise rely on GitHub Actions email notifications for watchers.

#### Deploy — `frontend-admin-deploy.yml`

| Environment | When | Approval |
| ----------- | ---- | -------- |
| **Staging** (`frontend-admin-staging`) | After CI green on `main` (`workflow_run`), or manual `staging` | Optional |
| **Production** (`frontend-admin-production`) | Manual workflow only (`target=production`) | **Required reviewers** on the GitHub Environment |

Publishes `ghcr.io/<owner>/regkasse-frontend-admin:<tag>`. Optional deploy webhooks: `FA_STAGING_DEPLOY_WEBHOOK_URL` / `FA_PRODUCTION_DEPLOY_WEBHOOK_URL`. Bake `NEXT_PUBLIC_*` via repo Variables (`FA_*_API_BASE_URL`, `FA_*_RKSV_ENVIRONMENT`).

```text
PR → CI (lint → typecheck → test → build → e2e)
main CI green → Deploy staging image (+ webhook if configured)
Actions → Deploy production → Environment approval → prod image (+ webhook)
```

#### Other workflows

| Workflow | What it does |
| -------- | ------------ |
| `api-client-alignment.yml` | Orval drift + admin `npm run build` smoke |
| `frontend-admin-e2e.yml` | Manual / reusable Playwright (`workflow_dispatch`) — PR gate is in `frontend-admin-ci.yml` |
| Localization / contract workflows | Install FA deps; i18n / OpenAPI checks |
| [Dependabot](../.github/dependabot.yml) | Weekly npm (and monorepo) version updates |

Repo-wide detail: [`docs/MULTI_TENANT.md`](../docs/MULTI_TENANT.md), [`REGKASSE_AI_ONBOARDING.md`](../REGKASSE_AI_ONBOARDING.md), [`AGENTS.md`](../AGENTS.md), [`docs/ADMIN_FA_DEPLOY.md`](../docs/ADMIN_FA_DEPLOY.md).

## License

Proprietary — All rights reserved. See [`../LICENSE`](../LICENSE).
