# Frontend Contract

## Multi-Tenant Architecture

- **Üretim (hedef):** Tek POS UI — `https://pos.regkasse.at` → API `https://api.regkasse.at`; kiracı **JWT `tenant_id`**. FA: `https://admin.regkasse.at`. Ayrıntı: `docs/POS_PRODUCTION_ARCHITECTURE.md`.
- **POS:** login sonrası JWT; üretimde sabit API base (per-tenant Host yok).
- **POS dev:** `EXPO_PUBLIC_DEV_TENANT_ID=dev`, `DevTenantSwitcher`, otomatik `X-Tenant-Id` + `?tenant=` (`services/api/config.ts`).
- **Admin dev:** header’da `HeaderDevTenantSwitch` (dropdown); `dev` / `cafe` / `bar`.
- Sunucu izolasyonu nihai otoritedir; istemci yanlış slug ile başka kiracının verisini alamaz.

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
- Navigation kaynakları: `app/_layout.tsx`, `app/(auth)/*`, `app/(tabs)/*`, `app/(screens)/*`.
- API çağrıları `frontend/services/api/*` üzerinden yapılır.
- Yeni POS çağrılarında canonical path tercih et: `/api/pos/*`.
- **Fiscal kural kaynağı:** RKSV/TSE/ödeme reddi ve kasa durumu için backend nihai otoritedir; POS yalnızca erken uyarı/blokaj sağlar (`REGKASSE_AI_ONBOARDING.md`).
- **Offline TSE intents (legacy):** `pendingPaymentQueue.ts`; `POST /api/offline-transactions/replay`; FA **`/admin/tse/offline-transactions`**. Voucher asla kuyruğa yazılmaz.
- **Offline orders (full snapshot):** `offlineOrderManager.ts` + `offlineStorage.ts`; POS `/api/pos/offline-orders/*`; FA **`/rksv/offline-orders`**. Ayrım: [`docs/release/OFFLINE_SYSTEMS_SEPARATION.md`](../docs/release/OFFLINE_SYSTEMS_SEPARATION.md).
- Working hours / `posOperationsAllowed`: display + reminder only — **never** blocks POS operations (`docs/WORKING_HOURS.md`).

## Admin (`frontend-admin/`)
- Stack: Next.js **16** App Router + Ant Design **6** + TanStack Query.
- Auth route gate: `frontend-admin/src/proxy.ts` (Next.js 16; replaces deprecated `middleware.ts`). Permission RBAC is client-side (`PermissionRouteGuard`), not in `proxy.ts`.
- Route yapısı: `frontend-admin/src/app/**` (React Router/Vite değil).
- **Offline orders panel:** `/rksv/offline-orders` — Orval hooks from `@/api/generated/admin/admin`; i18n `rksvHub.offlineOrdersPage.*`
- **Backup hub:** `/backup` (+ `/backup/costs`, `/backup/compliance`); legacy `/settings/backup-dr` redirects.
- API tüketimi: Orval generated client (`src/api/generated/**`) + admin boundary helper dosyaları.
- `src/api/generated/**` elle düzenlenmez.
- Toasts: `useNotify()` / `NotificationService` — **never** static `message` / `notification` / `Modal.confirm` from `antd` (`useAntdApp()` for modal).

## Sites (`frontend-sites/`)
- Shared Next.js storefront: `/[slug]` + public catalog / online-order APIs (`/api/public/*`, `/api/sites/*`).
- Optional custom Host via verified `TenantDomain`. Not fiscal POS — `docs/DIGITAL_SERVICES.md`, `frontend-sites/README.md`.

## Ortak sınırlar
- POS koduna web-only admin pattern’leri taşınmamalı.
- Admin koduna React Native/Expo pattern’leri taşınmamalı.
- Sites fiscal ödeme / RKSV zincirine bağlanmamalı.
- API path stringleri merkezileştirilmiş dosyalarda tutulmalı; ekran içinde dağınık hardcode azaltılmalı.
