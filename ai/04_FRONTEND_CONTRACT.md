# Frontend Contract

## Multi-Tenant Architecture

- **Üretim:** API ve admin/POS istemcileri kiracı alt alanına hizalanır (`{slug}.regkasse.at`).
- **POS:** `tenantStorage` + lisans aktivasyonu; üretimde `apiBaseUrl` bootstrap.
- **POS dev:** `EXPO_PUBLIC_DEV_TENANT_ID=test_cafe`, `DevTenantSwitcher`, otomatik `X-Tenant-Id` + `?tenant=` (`services/api/config.ts`).
- **Admin dev:** header’da `HeaderDevTenantSwitch` (dropdown); `dev` / `cafe` / `bar`.
- Sunucu izolasyonu nihai otoritedir; istemci yanlış slug ile başka kiracının verisini alamaz.

## POS (`frontend/`)
- Stack: React Native + Expo Router.
- Navigation kaynakları: `app/_layout.tsx`, `app/(auth)/*`, `app/(tabs)/*`, `app/(screens)/*`.
- API çağrıları `frontend/services/api/*` üzerinden yapılır.
- Yeni POS çağrılarında canonical path tercih et: `/api/pos/*`.
- **Fiscal kural kaynağı:** RKSV/TSE/ödeme reddi ve kasa durumu için backend nihai otoritedir; POS yalnızca erken uyarı/blokaj sağlar (`REGKASSE_AI_ONBOARDING.md`).
- **Offline kuyruk:** `NON_FISCAL_PENDING` ödemeler `frontend/services/payment/pendingPaymentQueue.ts` ile yerel kuyrukta tutulur; **düz metin voucher kodu asla kuyruğa yazılmaz**. Senkron: `POST /api/offline-transactions/replay` (bağlantı/health toparlanınca tetiklenir).

## Admin (`frontend-admin/`)
- Stack: Next.js 14 App Router + Ant Design + TanStack Query.
- Route yapısı: `frontend-admin/src/app/**` (React Router/Vite değil).
- API tüketimi: Orval generated client (`src/api/generated/**`) + admin boundary helper dosyaları.
- `src/api/generated/**` elle düzenlenmez.

## Ortak sınırlar
- POS koduna web-only admin pattern’leri taşınmamalı.
- Admin koduna React Native/Expo pattern’leri taşınmamalı.
- API path stringleri merkezileştirilmiş dosyalarda tutulmalı; ekran içinde dağınık hardcode azaltılmalı.
