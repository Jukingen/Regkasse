# API Boundary Policy

## Canonical boundary
- Admin client (`frontend-admin`) → `/api/admin/*`
- POS client (`frontend`) → `/api/pos/*`
- Customer sites (`frontend-sites`) → `/api/public/*`, `/api/sites/*` (storefront / online-order intake; **not** POS or admin)

## API Headers

### Tenant Identification

- Production (hedef): JWT `tenant_id` on `api.regkasse.at` / `pos.regkasse.at` (`docs/POS_PRODUCTION_ARCHITECTURE.md`); Development: `X-Tenant-Id: {slug}` veya `?tenant={slug}`.
- Custom website Host → slug: verified `TenantDomain` (`website.manage`).

### Super Admin Endpoints

- `/api/admin/tenants/*` — `SuperAdmin`; global `tenants` CRUD; iş verisi için impersonation.

## Multi-Tenant Architecture

- POS: tek UI (`pos.regkasse.at`); FA: `admin.regkasse.at`; API: `api.regkasse.at`.
- Yeni “global” admin uçları eklemeden önce kiracı filtresi gereksinimini değerlendir.
- Backend singleton’lar EF için `IServiceScopeFactory` kullanır (`LicenseService`); bkz. `REGKASSE_AI_ONBOARDING.md`.
- **Working hours:** yalnızca `/api/public/*` / sites online-order intake; authenticated `/api/pos/*` ve `/api/admin/*` asla saat ile kapatılmaz (`docs/WORKING_HOURS.md`).

## Hard rules
1. Yeni endpointler canonical boundary altında açılır (POS / Admin / Sites ayrı aileler).
2. Legacy alias (`/api/Payment`, `/api/Cart`, `/api/Product`) genişletilmez.
3. Legacy route sadece geçiş uyumluluğu içindir; yeni feature eklenmez.
4. Contract değişikliği OpenAPI diff ile review edilir.
5. `offline_transactions` ile `offline_orders` birleştirilmez / tek UI’ya karıştırılmaz.

**Timeline / Sunset:** [`docs/API_LEGACY_DEPRECATION.md`](../docs/API_LEGACY_DEPRECATION.md) (soft Sunset **2026-09-30**).

## Explicit exceptions (shared surfaces)
Aşağıdaki yüzeyler boundary dışı ama bilinçli şekilde paylaşılıyor veya geçişte:
- `/api/Auth/*`
- `/api/user/*` (tercihen settings/profile; yeni özellik için `/api/admin` veya `/api/pos` tercih et)
- `/api/Receipts/*` (POS-allowed migration debt — yeni özellik için `/api/pos` tercih)
- `/api/Invoice/*`, `/api/Orders/*` (migration debt; yeni özellik için canonical prefix)
- `/api/rksv/*` (RKSV özel fişler; admin/POS yetkisine göre; yüksek risk)
- `/api/offline-transactions` (legacy payment-intent replay)
- `/api/pos/offline-orders` (POS full order snapshots — save/replay)
- `/api/admin/offline-orders` (admin list/manual replay)
- PascalCase tarihi aileler (`/api/Tse/*`, `/api/UserManagement/*`, `/api/Tagesabschluss/*`, vb.)

## Admin guidance
- `src/api/generated/**` primary client surface.
- `src/api/generated/**` elle düzenlenmez.
- Legacy kullanımını gizlemek için transformer’a körü körüne yeni strip eklemek yerine istemciyi canonical endpointlere taşı.

## POS guidance
- API erişimini `frontend/services/api/*` içinde tut.
- Yeni kodda `/api/Payment`, `/api/Cart`, `/api/Product` doğrudan kullanılmaz.
