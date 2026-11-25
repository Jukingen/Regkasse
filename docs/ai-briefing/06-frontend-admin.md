# 06 — Admin Panel ("FA", `frontend-admin/`, package `registrierkasse-admin`)

> Part 6 of the Regkasse AI briefing. Index: [`../AI_PROJECT_BRIEFING.md`](../AI_PROJECT_BRIEFING.md).
> Verified at commit `e2be4d5b` (2026-11-24).
> `frontend-admin/AGENTS.md` warns that this Next.js version has breaking changes. Read the relevant guide in `node_modules/next/dist/docs/` before writing Next.js code, and heed deprecation notices.

## 1. Overview

- Next.js `^16.4.6` (App Router), React `^19.3.1`, Ant Design `^6.4.3`, TanStack Query 5, Orval 8 (generated API client), axios, Zustand 5 (UI preferences only), dayjs, recharts, `@dnd-kit`, `jszip`, `fuse.js`, pino, `@sentry/nextjs`, `@microsoft/signalr`. Tests: vitest 4 (about 620 test files) and Playwright (about 33 specs). TypeScript ~5.9.
- Users: Manager ("Mandanten-Admin") for a single tenant, SuperAdmin platform-wide, plus Accountant, ReportViewer, and ComplianceOfficer where permitted.
- API boundary: calls `/api/admin/*` and `/api/Auth/*`. **Never** `/api/pos/*`.
- Dev URL: `http://admin.regkasse.local:3000` (hosts-file entry; `localhost:3000` also works). Dev server uses `next dev --webpack`.
- UI language: i18n with **de / en / tr** catalogs under `frontend-admin/src/i18n/locales/{de,en,tr}`. Hardcoded user-facing strings are forbidden.

Commands:

```bash
npm run dev:admin                       # from the repo root
cd frontend-admin && npm run typecheck
cd frontend-admin && npm run lint       # eslint + OpenAPI contract check
cd frontend-admin && npm run test       # vitest
cd frontend-admin && npm run test:e2e   # Playwright
cd frontend-admin && npm run generate:api   # Orval (after backend/swagger.json changes)
cd frontend-admin && npm run i18n:ci    # validate + boundary + usage + key-union check
```

## 2. Source layout (`frontend-admin/src/`)

| Folder | Role |
|---|---|
| `app/` | Next.js routes. `(public)/` (`force-password-change`, `impersonate-callback`), `(protected)/` (all authenticated pages), `login/`, `health/`, `api/` (monitoring sinks) |
| `proxy.ts` | **Auth boundary** (Next.js 16 replacement for `middleware.ts`). Fail-closed |
| `features/<domain>/` | Domain code: `components/`, `hooks/`, `api/`, `types/`, `utils/`, `__tests__/` (about 100 domains, see §4) |
| `api/generated/` | Orval output (about 2,100 files). **Do not hand-edit.** Regenerated from `backend/swagger.json` |
| `api/admin/`, `api/admin-rksv/`, `api/manual/` | Hand-written clients for endpoints that Orval strips or that need custom handling (for example legacy product/category paths) |
| `lib/axios.ts` | The axios instance used as the Orval mutator (`customInstance`) |
| `lib/queryClient.ts`, `lib/queryKeys.ts` | React Query setup and keys |
| `lib/notificationService.ts`, `lib/antdAppBridge.ts` | Toasts/modals for non-React code |
| `shared/auth/` | Permissions, route guard, menu-permission mapping, `usePermissions`, `PermissionGate` |
| `shared/` (other) | `components`, `layout`, `config`, `constants`, `contract`, `errors`, `hospitality`, `pagination`, `reporting`, `utils`, `backendLocale` |
| `providers/` | `AppProviders` (provider order: `I18nProvider` → `TenantSwitchProvider` → `QueryClientProvider` → `AuthProvider` → `ThemeProvider` → `PersonalizationProvider`), theme, density, devtools |
| `stores/` | Zustand stores for **ephemeral UI prefs only** (`uiPreferencesStore`, `siderWidthStore`) |
| `i18n/` | `locales/de|en|tr`, `generated/translationKeys.ts` (typed key union, checked in CI) |
| `hooks/` | `useNotify`, `useAntdApp`, shared hooks |
| `theme/`, `styles/`, `types/`, `constants/`, `services/` | Theme tokens and misc |

## 3. Authentication and permissions in FA

- `proxy.ts`: public paths (`/login`, `/login/forgot-username`, `/health`, `/impersonate-callback`, robots/sitemap/manifest, change-password path) are open. Protected prefixes (`/admin/`, `/dashboard/`, `/rksv/`, `/settings/`, `/staff/`, `/users/`, `/403`) require a non-expired JWT in the HttpOnly cookie (`rk_admin_access_token`, legacy `access_token`, or the compact `rk_admin_edge_session` presence cookie) or a header; otherwise redirect to `/login`. Permission RBAC is **client-side**: `PermissionRouteGuard` renders an inline 403; denied attempts must be audited.
- Auth state: `AuthProvider` + `useAuth` + `authStorage`. Tokens are handled only through `authStorage`, never in Zustand. Tenant state: `TenantProvider` / `TenantSwitchContext` + React Query. Idle timeout and session warning modal exist (`IdleTimeoutProvider`).
- `lib/axios.ts` adds: `Authorization`/cookies handling, `X-Tenant-Id` (development slug only; it never auto-injects `?tenant=`), `X-App-Context: admin`, CSRF token handling for mutations, language header, retry, 401/403 handling, license-lockdown handling, Sentry reporting, API latency metrics.
- Dev tenant switcher: `HeaderDevTenantSwitch` (development only; presets in `features/auth/constants/devTenantPresets.ts` and `features/tenancy/devTenantCatalog.ts`; persists `dev_tenant_id` in `localStorage`).
- Permission keys are mirrored from the backend (`shared/auth/permissions.ts`, `permissionsCatalog.ts`, `menuPermissionRegistry.ts`, `routePermissions.ts`). CI scripts `verify-permission-keys.mjs` and `verify-menu-permissions.mjs` check alignment. When adding a permission or menu item, update backend and FA together.

## 4. Routes and feature domains

Top-level `(protected)` routes: `dashboard`, `products`, `categories`, `modifier-groups`, `inventory`, `suppliers`, `price-rules`, `pricing-rules`, `orders`, `online-orders`, `payments`, `storno`, `receipts`, `receipt-generate`, `receipt-templates`, `invoices`, `sales`, `shifts`, `tagesabschluss`, `tables`, `customers`, `vouchers`, `benefit-definitions`, `benefit-assignments`, `staff`, `users`, `reports`, `reporting`, `billing`, `license`, `kassenverwaltung`, `operations-center`, `audit-logs`, `backup`, `digital`, `settings`, `tenant`, `rksv`, `admin`, `profile`, `403`.

- `/rksv/*`: `status`, `compliance`, `signature-chain`, `sonderbelege`, `monatsbeleg`, `monatsbelege`, `jahresbeleg`, `dep-export`, `dep-export-compliance`, `cmc-certificate`, `belegcheck`, `finanz-online-operations`, `finanz-online-outbox`, `finanz-online-queue`, `fiscal-export-diagnostics`, `integrity`, `incident`, `offline-orders`, `offline-intent-coverage`, `operations`, `payload-hash-conflicts`, `replay-batch`, `verifications`.
- `/settings/*`: `company`, `tse`, `finanzonline`, `payment`, `payment-methods`, `receipt`, `tax-groups`, `tax-history`, `working-hours`, `website`, `digital`, `offline`, `backup`, `backup-dr`, `backup-retention`, `data-management`, `development-mode`, `account`, `appearance`, `personalization`, `preferences`, `password`, `session`, `sessions`.
- `/admin/*` (mostly SuperAdmin or platform): `tenants`, `users`, `license`, `licenses`, `license-management`, `trials`, `limits`, `sessions`, `support`, `billing`, `backup`, `restore-verification`, `monitoring`, `deployments`, `database`, `feature-flags`, `vertical-profiles`, `online-order-integrations`, `online-payments`, `peppol`, `kassensicherheit`, `fiskaly`, `tse`, `tse-management`, `risk-dashboard`, `maintenance`, `approvals`, `access`, `demo-requests`, and vertical areas (`rooms`, `folios`, `tickets`, `taxi-trips`, `patients`, `imeis`, `appointments`, `kitchen`).

Feature domains under `features/` (grouped): catalog (`products`, `categories`, `imeis`, `mwst`, `tax`), sales (`orders`, `payments`, `invoices`, `receipts`, `receipt-templates`, `vouchers`, `shifts`, `tagesabschluss`, `online-orders`, `online-payments`), RKSV and fiscal (`rksv`, `rksv-operations`, `rksv-runtime-config`, `fiscal-export-audit`, `fiskaly`, `kassenSicherheit`, `peppol`, `signaturkarte-program`, about 25 `tse-*` domains), tenant/SaaS (`tenants`, `tenant`, `tenant-portal`, `tenant-invoices`, `tenancy`, `license`, `billing`, `trial`, `extensions`, `feature-flags`, `vertical-profiles`, `super-admin`, `support-tickets`), users and security (`auth`, `users`, `staff`, `access`, `sessions`, `audit`, `audit-logs`, `critical-actions`, `risk`), operations (`backup`, `backup-dr`, `backup-management`, `offline`, `operations-center`, `activity-notifications`, `alerts`, `errors`, `metrics`, `dev-limits`, `development-mode`), data (`data-export`, `data-management`, `exports`, `export-email`, `download-history`, `reports`, `reporting`, `analytics`), digital (`digital`, `digital-services`, `website-generator`), verticals (`lodging`, `kitchen`, `patients`, `appointments`, `taxi-trips`, `tickets`).

## 5. API client rules (Orval)

- Source of truth: `backend/swagger.json` (OpenAPI 3.0.4, about 1,245 paths). `orval.config.ts` uses the `tags-split` mode, `react-query` client, axios mutator `customInstance` from `src/lib/axios.ts`, and a transformer that strips some legacy paths.
- After any backend endpoint or DTO change:
  1. `node scripts/generate-backend-openapi.mjs` (refresh `swagger.json`).
  2. `cd frontend-admin && npm run generate:api`.
  3. `node scripts/verify-api-client.mjs` (also run by Husky pre-commit and CI `api-client-alignment.yml`).
  4. Commit `swagger.json` and the generated output together. Do not regenerate the huge client when the API did not change.
  CI may auto-commit generated output when `backend/swagger.json` lands on `main`/`master` (`api-client-auto-generate.yml`).
- Prefer generated hooks. Fall back to `src/api/manual` or `src/api/admin` only when the spec does not cover the endpoint.
- `staleTime` should respect backend cache TTLs (for example product lists about 15 min) to avoid refetch storms.

## 6. UI rules (Ant Design 6)

- **Never** import static `message` / `notification` or call `Modal.confirm` from `antd`. Use:
  - `useNotify()` (`@/hooks/useNotify`) for toasts: `notify.successKey('key')`, `notify.apiError(err, { logContext, fallbackKey })`, `notify.error(msg, { mode: 'notification', description })`.
  - `useAntdApp()` for `modal.confirm(...)`.
  - Non-React code: `notifyError` / `showAntdError` from `@/lib/notificationService`.
  - Keep `import { Modal } from 'antd'` only for JSX `<Modal>`.
- Ant Design 6 props: `destroyOnHidden` (Modal/Drawer/Tabs), `popupRender` (Dropdown/Select), `titlePlacement` (Divider), `variant="borderless"` for Card, `variant="filled"` for Tag.
- Client state: server data in TanStack Query only; auth in `AuthProvider`/`authStorage`; UI prefs in Zustand or context (`ThemeProvider`, `PersonalizationProvider`); never duplicate API payloads in global stores; never put tokens, tenant secrets, or payment/RKSV payloads in Zustand.
- i18n: add keys to **all** locales (de, en, tr). Run `npm run i18n:ci`. The typed key union (`i18n/generated/translationKeys.ts`) must be regenerated (`npm run i18n:keys`). Permission codes and backend constants are not translated. Operators see German copy that must match `operatorCopyDeLocaleParity` tests.
- Do not expose raw voucher codes. Show masked codes and ledger history only.
- Route and menu changes need permission mapping updates (`menuPermissionRegistry`, `routePermissions`) and a pass of `verify:menu-permissions`.

## 7. Tests and gates

- Unit/component tests: vitest + Testing Library (`src/**/__tests__`). Contract subsets: `npm run test:contract`, `test:truth-surfaces`, `test:operator-copy-locale-parity`.
- E2E: Playwright (`frontend-admin/tests`, `playwright.config.ts`), also `scripts/fa-full-menu-e2e.mjs` and `fa-sections-smoke-e2e.mjs`.
- CI: `frontend-admin-ci.yml` (lint, typecheck, test, build, Playwright E2E on pull requests; typecheck only on push to `main`), `frontend-admin-e2e.yml`, `frontend-admin-deploy.yml`, `api-client-alignment.yml`, `localization-validation.yml`.

## 8. Checklist for an FA change

1. Backend API involved? Make the backend change first, regenerate the client, then build the UI.
2. Which permission gates the page, menu entry, and each action? Reuse existing keys.
3. All text through i18n in de/en/tr. No hardcoded dates or strings (`npm run find:hardcoded-dates` exists).
4. Use `useNotify` and `useAntdApp`. No static Ant Design feedback APIs.
5. Add or update vitest tests; add Playwright coverage for a critical flow.
6. Respect tenant behavior: Manager sees only their tenant; SuperAdmin platform pages work without an ambient tenant only for the listed platform prefixes (part 04).
