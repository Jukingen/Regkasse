# Legacy vs Canonical API Route Inventory

**Last reviewed:** 2026-07-21  
**Deprecation timeline:** [`docs/API_LEGACY_DEPRECATION.md`](../docs/API_LEGACY_DEPRECATION.md) (Sunset **2026-09-30**)

## Definitions
- **Canonical:** Admin `/api/admin/*`, POS `/api/pos/*`.
- **Legacy alias:** Aynı handler’a ikinci prefix (örn. `/api/Payment` + `/api/pos/payment`). Dual `[Route]` — ayrı business logic yok.
- **Policy gap:** Henüz `/api/admin/*` veya `/api/pos/*` altına taşınmamış tekil route aileleri.

## A) Legacy aliases (confirmed in backend)

| Family | Legacy | Canonical | Backend source | Notes |
|---|---|---|---|---|
| Payment | `/api/Payment/*` | `/api/pos/payment/*` | `PaymentController` | `[Obsolete]` + `LegacyRouteDeprecationFilter`. |
| Cart | `/api/Cart/*` | `/api/pos/cart/*` | `CartController` | `[Obsolete]` + filter. FA generated `cart.ts` may still reference legacy — migrate before hard remove. |
| Product | `/api/Product/*` | `/api/pos/*` | `ProductController` | `[Obsolete]` + filter. Admin CRUD: `/api/admin/products`. |

## B) Consumer reality snapshot
- POS servisleri çoğunlukla canonical `/api/pos/*` kullanıyor (`frontend/services/api/*`).
- Admin’de eski `src/api/legacy/` klasörü yok; products → `/api/admin/products`.
- Orval-generated yüzeyde legacy cart path’leri görülebiliyor (`frontend-admin/src/api/generated/cart/cart.ts`).
- Orval transformer şu an `/api/Product`, `/api/Categories`, `/api/Payment` pathlerini strip ediyor.

## C) Policy-gap route families (single-surface, not alias)
- Örnekler: `/api/UserManagement/*`, `/api/Tse/*`, `/api/Tagesabschluss/*`, `/api/Settings/*`, `/api/Orders/*`, `/api/Receipts/*`, `/api/Invoice/*`.
- **Multi-tenant (canonical):** `/api/admin/tenants` — Super Admin only; impersonation `POST /api/admin/tenants/{tenantId}/impersonate`.
- Bunlar alias kaldırma işi değil; kontrollü boundary migration işidir.

## D) Known risks
1. Generated client içinde kalan legacy path’ler yanlışlıkla yeni kullanım üretebilir.
2. Repo dışı istemciler legacy path kullanıyor olabilir (sadece server log/metrics ile doğrulanır).
3. TSE/FinanzOnline/receipt ilişkili route ailelerinde isim/path değişikliği yüksek uyumluluk riski taşır.
4. `/api/rksv/*` özel fiş uçları fiscal yüksek risk; boundary migration’da ayrı gözden geçirilmelidir.

## E) Maintenance rule
Bu dosyayı şu değişikliklerde güncelle:
- Controller route attribute değişimi
- Orval transformer legacy listesi değişimi
- Legacy deprecation/filter / Sunset davranışı değişimi
- Timeline değişiklikleri → ayrıca `docs/API_LEGACY_DEPRECATION.md`
