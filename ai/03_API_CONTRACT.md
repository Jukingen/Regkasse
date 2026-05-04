# API Contract

## Source of truth
- Contract kaynağı: `backend/swagger.json`.
- Backend implementation ve frontend tüketimi bu dosyayla hizalı olmalıdır.

## Boundary kuralı
- Admin: `/api/admin/*`
- POS: `/api/pos/*`
- RKSV özel fişler: `/api/rksv/*` (canonical; yüksek risk).
- Legacy prefix (`/api/Payment`, `/api/Cart`, `/api/Product`) yeni işlev için kullanılmaz.

## Yüksek risk (contract değişikliği öncesi)
- Ödeme: `/api/pos/payment*`, offline replay: `/api/offline-transactions/*`
- RKSV: `/api/rksv/special-receipts/*`
- TSE tanılama ve kasa oturumu ile ilişkili uçlar
- Fiscal export: `/api/admin/fiscal-export*`

## Contract değişikliği kuralı
- Endpoint/DTO/error shape değişiyorsa:
  1. backend kodunu güncelle,
  2. `backend/swagger.json` güncelle,
  3. admin tarafında Orval ile `frontend-admin/src/api/generated/**` yenile,
  4. `node scripts/verify-api-client.mjs` ve kritik path scriptlerini çalıştır.

## Kontroller
- `node scripts/validate-critical-openapi-paths.mjs`
- `node scripts/verify-api-client.mjs`
