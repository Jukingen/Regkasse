# Frontend contract

## Multi-tenant architecture

- **Production (target):** Single POS UI — `https://pos.regkasse.at` → API `https://api.regkasse.at`; tenant is **JWT `tenant_id`**. FA: `https://admin.regkasse.at`. Detail: `docs/POS_PRODUCTION_ARCHITECTURE.md`.
- **POS:** JWT after login; fixed API base in production (no per-tenant Host).
- **POS dev:** `EXPO_PUBLIC_DEV_TENANT_ID=dev`, `DevTenantSwitcher`, automatic `X-Tenant-Id` + `?tenant=` (`services/api/config.ts`).
- **Admin dev:** `HeaderDevTenantSwitch` dropdown in the header; `dev` / `cafe` / `bar`.
- Server isolation is the final authority; a wrong slug on the client cannot read another tenant’s data.

### Local multi-tenant testing (summary)

| Method | Example |
|--------|---------|
| Header | `curl -H "X-Tenant-Id: dev" http://localhost:5184/api/health` |
| Query | `?tenant=dev` (Development only) |
| FA dev | Header dropdown → `localStorage` `dev_tenant_id` |
| POS | `localhost:8081` + `EXPO_PUBLIC_DEV_TENANT_ID` / switcher |
| Hosts | `127.0.0.1 admin.regkasse.local` → FA; optional `dev.regkasse.local` → API |

## POS (`frontend/`)

- Stack: React Native + Expo Router (Expo SDK **56**).
- Navigation sources: `app/_layout.tsx`, `app/(auth)/*`, `app/(tabs)/*`, `app/(screens)/*`.
- API calls go through `frontend/services/api/*`.
- Prefer canonical paths for new POS calls: `/api/pos/*`.
- **Fiscal rule source:** Backend is authoritative for RKSV/TSE/payment rejection and register state; POS only provides early warning/block (`REGKASSE_AI_ONBOARDING.md`).
- **Offline TSE intents (legacy):** `pendingPaymentQueue.ts`; `POST /api/offline-transactions/replay`; FA **`/admin/tse/offline-transactions`**. Never enqueue vouchers.
- **Offline orders (full snapshot):** `offlineOrderManager.ts` + `offlineStorage.ts`; POS `/api/pos/offline-orders/*`; FA **`/rksv/offline-orders`**. Split: [`docs/release/OFFLINE_SYSTEMS_SEPARATION.md`](../docs/release/OFFLINE_SYSTEMS_SEPARATION.md).
- Working hours / `posOperationsAllowed`: display + reminder only — **never** blocks POS operations (`docs/WORKING_HOURS.md`).

## Admin (`frontend-admin/`)

- Stack: Next.js **16** App Router + Ant Design **6** + TanStack Query.
- Auth route gate: `frontend-admin/src/proxy.ts` (Next.js 16; replaces deprecated `middleware.ts`). Permission RBAC is client-side (`PermissionRouteGuard`), not in `proxy.ts`.
- Route tree: `frontend-admin/src/app/**` (not React Router/Vite).
- **Offline orders panel:** `/rksv/offline-orders` — Orval hooks from `@/api/generated/admin/admin`; i18n `rksvHub.offlineOrdersPage.*`
- **Backup hub:** `/backup` (+ `/backup/costs`, `/backup/compliance`); legacy `/settings/backup-dr` redirects.
- API consumption: Orval generated client (`src/api/generated/**`) + admin boundary helper files.
- Do not hand-edit `src/api/generated/**`.
- Toasts: `useNotify()` / `NotificationService` — **never** static `message` / `notification` / `Modal.confirm` from `antd` (`useAntdApp()` for modal).

## Sites (`frontend-sites/`)

- Shared Next.js storefront: `/[slug]` + public catalog / online-order APIs (`/api/public/*`, `/api/sites/*`).
- Optional custom Host via verified `TenantDomain`. Not fiscal POS — `docs/DIGITAL_SERVICES.md`, `frontend-sites/README.md`.

## Shared limits

- Do not carry web-only admin patterns into POS code.
- Do not carry React Native/Expo patterns into admin code.
- Do not attach Sites to fiscal payment / RKSV chain.
- Keep API path strings in centralized files; reduce scattered hardcoded paths in screens.
