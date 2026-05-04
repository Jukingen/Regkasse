# Admin legacy wrapper status

## Current status (2026-05-04)
- `frontend-admin/src/api/legacy/` klasörü repoda yok (kaldırılmış durumda).
- Admin tarafında ana tüketim yüzeyi generated client (`src/api/generated/**`) + `src/api/admin/**` helper katmanıdır.

## What still needs attention
- Generated surfaces içinde legacy path üreten bölümler (özellikle `generated/cart`) hala izlenmeli/migrate edilmelidir.
- Legacy path’lerin yok edilmesi için sadece transformer strip listesine güvenme; gerçek consumer migration yapılmalıdır.

## Validation
- `node scripts/verify-api-client.mjs`
- `cd frontend-admin && npm run test:contract`
- `cd frontend-admin && npm run build`
