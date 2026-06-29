# API Boundary Policy

## Canonical boundary
- Admin client (`frontend-admin`) → `/api/admin/*`
- POS client (`frontend`) → `/api/pos/*`

## API Headers

### Tenant Identification

- Production: subdomain; Development: `X-Tenant-Id: {slug}` veya `?tenant={slug}`.

### Super Admin Endpoints

- `/api/admin/tenants/*` — `SuperAdmin`; global `tenants` CRUD; iş verisi için impersonation.

## Multi-Tenant Architecture

- Admin ve POS istemcileri kiracı host/slug ile hizalanır.
- Yeni “global” admin uçları eklemeden önce kiracı filtresi gereksinimini değerlendir.
- Backend singleton’lar EF için `IServiceScopeFactory` kullanır (`LicenseService`); bkz. `REGKASSE_AI_ONBOARDING.md`.

## Hard rules
1. Yeni endpointler canonical boundary altında açılır.
2. Legacy alias (`/api/Payment`, `/api/Cart`, `/api/Product`) genişletilmez.
3. Legacy route sadece geçiş uyumluluğu içindir; yeni feature eklenmez.
4. Contract değişikliği OpenAPI diff ile review edilir.

## Explicit exceptions (shared surfaces)
Aşağıdaki yüzeyler boundary dışı ama bilinçli şekilde paylaşılıyor veya geçişte:
- `/api/Auth/*`
- `/api/user/settings/*`
- `/api/Receipts/*`
- `/api/Invoice/*`
- `/api/Orders/*`
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
