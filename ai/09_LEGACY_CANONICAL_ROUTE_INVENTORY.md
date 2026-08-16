# Legacy vs Canonical API Route Inventory

**Last reviewed:** 2026-08-13  
**Removal note:** [`docs/API_LEGACY_DEPRECATION.md`](../docs/API_LEGACY_DEPRECATION.md) (hard-removed **2026-08-13**)

## Definitions
- **Canonical:** Admin `/api/admin/*`, POS `/api/pos/*`.
- **Removed legacy alias:** Former second prefix on the same handler (`/api/Payment` + `/api/pos/payment`). Dual `[Route]` dropped; handlers remain on canonical prefixes only.
- **Policy gap:** Henüz `/api/admin/*` veya `/api/pos/*` altına taşınmamış tekil route aileleri.

## A) Removed aliases (do not reintroduce)

| Family | Removed | Canonical | Backend source | Notes |
|---|---|---|---|---|
| Payment | `/api/Payment/*` | `/api/pos/payment/*` | `PaymentController` | Canonical route only. |
| Cart | `/api/Cart/*` | `/api/pos/cart/*` | `CartController` | Canonical route only. Unused FA generated `/api/Cart` client deleted. |
| Product | `/api/Product/*` | `/api/pos/*` | `ProductController` | Canonical route only. Admin CRUD: `/api/admin/products`. |

## B) Consumer reality snapshot
- POS servisleri canonical `/api/pos/*` kullanır (`frontend/services/api/*`).
- Admin products → `/api/admin/products`.
- Orval transformer strips `/api/Product`, `/api/Categories`, `/api/Payment`, `/api/Cart`.
- OpenAPI (`backend/swagger.json`) does not publish the removed aliases.

## C) Policy-gap route families (single-surface, not alias)
- Örnekler: `/api/UserManagement/*`, `/api/Tse/*`, `/api/Tagesabschluss/*`, `/api/Settings/*`, `/api/Orders/*`, `/api/Receipts/*`, `/api/Invoice/*`.
- **Multi-tenant (canonical):** `/api/admin/tenants` — Super Admin only; impersonation `POST /api/admin/tenants/{tenantId}/impersonate`.
- **SaaS trials:** `/api/admin/trials` — Super Admin; ambient-tenant exempt.
- **Support:** Mandanten `/api/admin/support/tickets`; Super Admin inbox `/api/admin/support/admin/tickets`.
- Bunlar alias kaldırma işi değil; kontrollü boundary migration işidir.

## D) Known risks
1. Repo dışı istemciler hâlâ legacy path kullanıyorsa HTTP 404 alır (rollback: dual `[Route]` hotfix).
2. TSE/FinanzOnline/receipt ilişkili route ailelerinde isim/path değişikliği yüksek uyumluluk riski taşır.
3. `/api/rksv/*` özel fiş uçları fiscal yüksek risk; boundary migration’da ayrı gözden geçirilmelidir.

## E) Maintenance rule
Bu dosyayı şu değişikliklerde güncelle:
- Controller route attribute değişimi
- Orval transformer legacy listesi değişimi
- Timeline değişiklikleri → ayrıca `docs/API_LEGACY_DEPRECATION.md`
