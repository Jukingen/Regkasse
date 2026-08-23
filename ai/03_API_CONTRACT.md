# API contract

## Source of truth

- Contract source: `backend/swagger.json`.
- Backend implementation and frontend consumption must stay aligned with this file.
- **Supplement (auth / username deltas):** [`docs/API_CONTRACTS.md`](../docs/API_CONTRACTS.md) — `loginIdentifier`, `userName`, Quick Create (`/users/quick`).

## API headers

### Tenant identification

- **Production (target):** Shared hosts `api.regkasse.at` / `pos.regkasse.at` / `admin.regkasse.at` — tenant is JWT `tenant_id` (`docs/POS_PRODUCTION_ARCHITECTURE.md`).
- **Host slug:** `{slug}.regkasse.at` is **not** the POS production entry (legacy / transition or `TenantDomain` customer site).
- **Development only:** `X-Tenant-Id: {slug}` or `?tenant={slug}` — value is the tenant **slug** (not a UUID); absent in Production.

JWT: after auth, `tenant_id` claim (Guid) + `TenantContextMiddleware` (authoritative for POS/API).

### Super Admin endpoints

- `/api/admin/tenants/*` → `SuperAdmin` role only.
- The `tenants` table is global; `ITenantEntity` filters do not cover this CRUD.
- For operational data: `POST /api/admin/tenants/{tenantId}/impersonate`.

## Multi-tenant architecture

- Foreign tenant resource IDs: **404** (anti-leak).
- Startup / singleton backend code: `IServiceScopeFactory` + scoped `AppDbContext` (`LicenseService`); do not use the root factory.

## Boundary rule

- Admin: `/api/admin/*`
- POS: `/api/pos/*`
- Sites / public (storefront, online order intake): `/api/public/*`, `/api/sites/*` — do not mix with the POS/FA boundary; working hours affect only this surface.
- RKSV special receipts: `/api/rksv/*` (canonical; high risk).
- Legacy prefixes (`/api/Payment`, `/api/Cart`, `/api/Product`) were **removed** (2026-08-13). Canonical: `/api/pos/payment`, `/api/pos/cart`, `/api/pos` + `/api/admin/products`.

## High risk (before a contract change)

- Payment: `/api/pos/payment*`, offline intent replay: `/api/offline-transactions/*`
- **Offline order snapshots:** `/api/pos/offline-orders/*`, `/api/admin/offline-orders/*` (see [`docs/release/OFFLINE_SYSTEMS_SEPARATION.md`](../docs/release/OFFLINE_SYSTEMS_SEPARATION.md))
- **Offline TSE intents (legacy):** `/api/offline-transactions/*`, `/api/admin/offline-transactions/*` — **not** the same as offline orders
- RKSV: `/api/rksv/special-receipts/*`
- TSE diagnostics and register-session related endpoints
- Fiscal export: `/api/admin/fiscal-export*`

## Contract change rule

If endpoint/DTO/error shape changes:

1. Update backend code.
2. Update `backend/swagger.json`.
3. Regenerate Orval for admin (`frontend-admin/src/api/generated/**`).
4. Run `node scripts/verify-api-client.mjs` and critical-path scripts.

## Development setup for multi-tenant testing

Requires `ASPNETCORE_ENVIRONMENT=Development`. Slug must match `tenants.slug` in the DB (for example `dev`, `cafe`).

### Option 1: Header (simplest)

```bash
curl -H "X-Tenant-Id: dev" http://localhost:5184/api/health
```

### Option 2: Query string (Dev only)

```bash
curl "http://localhost:5184/api/health?tenant=dev"
```

### Option 3: Hosts file

```text
127.0.0.1 admin.regkasse.local
127.0.0.1 dev.regkasse.local
```

Example: FA `http://admin.regkasse.local:3000`; optional API slug host `http://dev.regkasse.local:5184`.

### Option 4: FA tenant switcher

In Development, header dropdown (`HeaderDevTenantSwitch`); `X-Tenant-Id` + reload.

POS: `EXPO_PUBLIC_DEV_TENANT_ID=dev`, `DevTenantSwitcher`. Detail: `REGKASSE_AI_ONBOARDING.md`.

## Checks

- `node scripts/validate-critical-openapi-paths.mjs`
- `node scripts/verify-api-client.mjs`
