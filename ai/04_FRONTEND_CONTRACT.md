# Frontend Contract

## Multi-Tenant Architecture

- **Üretim:** API ve admin/POS istemcileri kiracı alt alanına hizalanır (`{slug}.regkasse.at`).
- **POS:** `tenantStorage` + lisans aktivasyonu; üretimde `apiBaseUrl` bootstrap.
- **POS dev:** `EXPO_PUBLIC_DEV_TENANT_ID=test_cafe`, `DevTenantSwitcher`, otomatik `X-Tenant-Id` + `?tenant=` (`services/api/config.ts`).
- **Admin dev:** header’da `HeaderDevTenantSwitch` (dropdown); `dev` / `cafe` / `bar`.
- Sunucu izolasyonu nihai otoritedir; istemci yanlış slug ile başka kiracının verisini alamaz.

### Local multi-tenant testing (summary)

| Method | Example |
|--------|---------|
| Header | `curl -H "X-Tenant-Id: test_cafe" http://localhost:5184/api/health` |
| Query | `?tenant=test_cafe` (Development only) |
| FA dev | Header dropdown → `localStorage` `dev_tenant_id` |
| Hosts | `127.0.0.1 cafe.regkasse.local` → `http://cafe.regkasse.local:5184` |

## POS (`frontend/`)
- Stack: React Native + Expo Router.
- Navigation kaynakları: `app/_layout.tsx`, `app/(auth)/*`, `app/(tabs)/*`, `app/(screens)/*`.
- API çağrıları `frontend/services/api/*` üzerinden yapılır.
- Yeni POS çağrılarında canonical path tercih et: `/api/pos/*`.
- **Fiscal kural kaynağı:** RKSV/TSE/ödeme reddi ve kasa durumu için backend nihai otoritedir; POS yalnızca erken uyarı/blokaj sağlar (`REGKASSE_AI_ONBOARDING.md`).
- **Offline TSE intents (legacy):** `pendingPaymentQueue.ts`; `POST /api/offline-transactions/replay`; FA **`/admin/tse/offline-transactions`**. Voucher asla kuyruğa yazılmaz.
- **Offline orders (full snapshot):** `offlineOrderManager.ts` + `offlineStorage.ts`; POS `/api/pos/offline-orders/*`; FA **`/rksv/offline-orders`**. Ayrım: [`docs/release/OFFLINE_SYSTEMS_SEPARATION.md`](../docs/release/OFFLINE_SYSTEMS_SEPARATION.md).

## Admin (`frontend-admin/`)
- Stack: Next.js 14 App Router + Ant Design + TanStack Query.
- Route yapısı: `frontend-admin/src/app/**` (React Router/Vite değil).
- **Offline orders panel:** `/rksv/offline-orders` — Orval hooks from `@/api/generated/admin/admin`; i18n `rksvHub.offlineOrdersPage.*`
- API tüketimi: Orval generated client (`src/api/generated/**`) + admin boundary helper dosyaları.
- `src/api/generated/**` elle düzenlenmez.

## Ortak sınırlar
- POS koduna web-only admin pattern’leri taşınmamalı.
- Admin koduna React Native/Expo pattern’leri taşınmamalı.
- API path stringleri merkezileştirilmiş dosyalarda tutulmalı; ekran içinde dağınık hardcode azaltılmalı.
