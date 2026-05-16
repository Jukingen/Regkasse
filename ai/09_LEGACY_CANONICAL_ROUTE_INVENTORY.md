# Legacy vs Canonical API Route Inventory

**Last reviewed:** 2026-05-04 (inventory only; re-validate controller routes after major API changes)

## Definitions
- **Canonical:** Admin `/api/admin/*`, POS `/api/pos/*`.
- **Legacy alias:** Aynı handler’a ikinci prefix (örn. `/api/Payment` + `/api/pos/payment`).
- **Policy gap:** Henüz `/api/admin/*` veya `/api/pos/*` altına taşınmamış tekil route aileleri.

## A) Legacy aliases (confirmed in backend)

| Family | Legacy | Canonical | Backend source | Notes |
|---|---|---|---|---|
| Payment | `/api/Payment/*` | `/api/pos/payment/*` | `PaymentController` | `LegacyRouteDeprecationFilter` aktif. |
| Cart | `/api/Cart/*` | `/api/pos/cart/*` | `CartController` | `LegacyRouteDeprecationFilter` aktif. |
| Product | `/api/Product/*` | `/api/pos/*` | `ProductController` | `LegacyRouteDeprecationFilter` aktif. |

## B) Consumer reality snapshot
- POS servisleri çoğunlukla canonical `/api/pos/*` kullanıyor (`frontend/services/api/*`).
- Admin’de eski `src/api/legacy/` klasörü artık yok.
- Buna rağmen Orval-generated yüzeyde legacy cart path’leri görülebiliyor (`frontend-admin/src/api/generated/cart/cart.ts`).
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
- Legacy deprecation/filter davranışı değişimi
