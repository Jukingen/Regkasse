# Frontend Contract

## POS (`frontend/`)
- Stack: React Native + Expo Router.
- Navigation kaynakları: `app/_layout.tsx`, `app/(auth)/*`, `app/(tabs)/*`, `app/(screens)/*`.
- API çağrıları `frontend/services/api/*` üzerinden yapılır.
- Yeni POS çağrılarında canonical path tercih et: `/api/pos/*`.

## Admin (`frontend-admin/`)
- Stack: Next.js 14 App Router + Ant Design + TanStack Query.
- Route yapısı: `frontend-admin/src/app/**` (React Router/Vite değil).
- API tüketimi: Orval generated client (`src/api/generated/**`) + admin boundary helper dosyaları.
- `src/api/generated/**` elle düzenlenmez.

## Ortak sınırlar
- POS koduna web-only admin pattern’leri taşınmamalı.
- Admin koduna React Native/Expo pattern’leri taşınmamalı.
- API path stringleri merkezileştirilmiş dosyalarda tutulmalı; ekran içinde dağınık hardcode azaltılmalı.
