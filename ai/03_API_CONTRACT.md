# API Contract

## Source of truth
- Contract kaynağı: `backend/swagger.json`.
- Backend implementation ve frontend tüketimi bu dosyayla hizalı olmalıdır.

## Boundary kuralı
- Admin: `/api/admin/*`
- POS: `/api/pos/*`
- Legacy prefix (`/api/Payment`, `/api/Cart`, `/api/Product`) yeni işlev için kullanılmaz.

## Contract değişikliği kuralı
- Endpoint/DTO/error shape değişiyorsa:
  1. backend kodunu güncelle,
  2. `backend/swagger.json` güncelle,
  3. gerekiyorsa admin generated client’i yenile (`frontend-admin`, Orval).

## Kontroller
- `node scripts/validate-critical-openapi-paths.mjs`
- `node scripts/verify-api-client.mjs`
