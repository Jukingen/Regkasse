# API Contract

## Source of truth
- Contract kaynağı: `backend/swagger.json`.
- Backend implementation ve frontend tüketimi bu dosyayla hizalı olmalıdır.
- **Supplement (auth / username deltas):** [`docs/API_CONTRACTS.md`](../docs/API_CONTRACTS.md) — `loginIdentifier`, `userName`, Quick Create (`/users/quick`).

## API Headers

### Tenant Identification

- **Production:** Kiracı `Host` alt alanından otomatik (`{slug}.regkasse.at`).
- **Development:** `X-Tenant-Id: {slug}` — değer kiracı **slug**’ıdır (UUID değil).
- **Development:** `?tenant={slug}` query parametresi (header ile aynı anlam).

JWT: auth sonrası `tenant_id` claim (Guid) + `TenantContextMiddleware`.

### Super Admin Endpoints

- `/api/admin/tenants/*` → yalnızca `SuperAdmin` rolü.
- `tenants` tablosu global; `ITenantEntity` filtreleri bu CRUD’u kapsamaz.
- Operasyonel veri için: `POST /api/admin/tenants/{tenantId}/impersonate`.

## Multi-Tenant Architecture

- Kiracı dışı kaynak ID’leri: **404** (sızıntı önleme).
- Startup / singleton backend kodu: `IServiceScopeFactory` + scoped `AppDbContext` (`LicenseService`); root factory kullanma.

## Boundary kuralı
- Admin: `/api/admin/*`
- POS: `/api/pos/*`
- RKSV özel fişler: `/api/rksv/*` (canonical; yüksek risk).
- Legacy prefix (`/api/Payment`, `/api/Cart`, `/api/Product`) yeni işlev için kullanılmaz.

## Yüksek risk (contract değişikliği öncesi)
- Ödeme: `/api/pos/payment*`, offline intent replay: `/api/offline-transactions/*`
- **Offline order snapshots:** `/api/pos/offline-orders/*`, `/api/admin/offline-orders/*` (bkz. [`docs/release/OFFLINE_SYSTEMS_SEPARATION.md`](../docs/release/OFFLINE_SYSTEMS_SEPARATION.md))
- **Offline TSE intents (legacy):** `/api/offline-transactions/*`, `/api/admin/offline-transactions/*` — **not** the same as offline orders
- RKSV: `/api/rksv/special-receipts/*`
- TSE tanılama ve kasa oturumu ile ilişkili uçlar
- Fiscal export: `/api/admin/fiscal-export*`

## Contract değişikliği kuralı
- Endpoint/DTO/error shape değişiyorsa:
  1. backend kodunu güncelle,
  2. `backend/swagger.json` güncelle,
  3. admin tarafında Orval ile `frontend-admin/src/api/generated/**` yenile,
  4. `node scripts/verify-api-client.mjs` ve kritik path scriptlerini çalıştır.

## Development Setup for Multi-Tenant Testing

`ASPNETCORE_ENVIRONMENT=Development` gerekir. Slug, DB’de `tenants.slug` ile eşleşmeli (ör. `dev`, `cafe`, `dev`).

### Option 1: Header-based (simplest)

```bash
curl -H "X-Tenant-Id: dev" http://localhost:5184/api/health
```

### Option 2: Query string

```bash
curl "http://localhost:5184/api/admin/payments?tenant=dev"
```

### Option 3: Hosts file

`127.0.0.1 dev.localhost` → `http://dev.localhost:5184` (slug: `dev`).

### Option 4: FA tenant switcher

Development’ta header dropdown (`HeaderDevTenantSwitch`); `X-Tenant-Id` + reload.

POS: `EXPO_PUBLIC_DEV_TENANT_ID=dev`, `DevTenantSwitcher`. Ayrıntı: `REGKASSE_AI_ONBOARDING.md`.

## Kontroller
- `node scripts/validate-critical-openapi-paths.mjs`
- `node scripts/verify-api-client.mjs`
