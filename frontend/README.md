# Regkasse POS (Mobile)

Expo Router + React Native + TypeScript cashier client.

Historical dev notes: `DEVELOPMENT.md` (pointer) and `archive/DEVELOPMENT.md`.

## Setup

See `package.json` scripts and repo root `REGKASSE_AI_ONBOARDING.md` for stack and fiscal constraints.

## Multi-Tenant Architecture

Regkasse uses a multi-tenant architecture where a single backend instance serves multiple tenants (companies/customers).

### Tenant Identification

- Tenants are identified by subdomain: `{tenant}.regkasse.at`
- Examples: `cafe.regkasse.at`, `bar.regkasse.at`, `market.regkasse.at`
- Super Admin is an admin-panel concern (`admin.regkasse.at`), not the POS app

### Data Isolation

- All POS API data is scoped server-side by tenant; clients must target the correct tenant host or dev override
- Cross-tenant IDs are not visible to other tenants (backend returns 404)

### Development Mode

- Localhost API: send `X-Tenant-Id: <slug>` on requests (`services/tenant/tenantStorage.ts`, constant `TENANT_HTTP_HEADER`)
- Alternative: `?tenant=<slug>` on API URLs (Development backend only)

## Development Setup for Multi-Tenant Testing

### Option 1: Header-based (simplest)

```bash
curl -H "X-Tenant-Id: test_cafe" http://localhost:5184/api/health
```

### Option 2: Query string

```bash
curl "http://localhost:5184/api/health?tenant=test_cafe"
```

### Option 3: Hosts file (subdomain simulation)

```text
127.0.0.1 cafe.regkasse.local
127.0.0.1 bar.regkasse.local
127.0.0.1 test-cafe.localhost
```

Then open `http://cafe.regkasse.local:5184` or use header/query on `localhost:5184`.

### Dev tenant override (POS)

- `EXPO_PUBLIC_DEV_TENANT_ID` — default slug when unset in `__DEV__`
- `services/tenant/tenantStorage.ts` — sends `X-Tenant-Id` on loopback API calls in Development
- Effective slug order: dev switcher storage → env var → login/license bootstrap

Then: `http://test-cafe.localhost:5184` — host first label must match `tenants.slug` (see onboarding doc).

### Option 4: POS UI

`DevTenantSwitcher` in tab layout (`__DEV__` only). Presets: `dev`, `test_cafe`, `test_bar`.

## POS Tenant Configuration

### Production

- `tenant_id`, `tenant_slug`, and `api_base_url` from license activation → secure storage (`tenantStorage.ts`).
- Requests use bootstrap `apiBaseUrl` (typically `https://{tenant}.regkasse.at/api`).

### Development

```env
EXPO_PUBLIC_DEV_TENANT_ID=test_cafe
```

POS adds `X-Tenant-Id` and optional `?tenant=` on the API base URL automatically (`services/api/config.ts`, `services/tenant/devTenant.ts`).

Full guide: `REGKASSE_AI_ONBOARDING.md`.
- Login/license bootstrap persists `tenant_id` and `tenant_slug` for subsequent calls
- Optional hosts file: `cafe.regkasse.local` pointing at the API machine (same slug rules as production)

### POS responsibilities

- Use tenant-aware API base URL when deploying per customer subdomain
- Do not cache or replay offline payment payloads across tenants
- After login, respect `tenantId` / `tenantSlug` from auth and license bootstrap (`contexts/AuthContext.tsx`, `api/license.ts`)

## API Headers

### Tenant Identification

- **Production:** Subdomain on API base URL (automatic).
- **Development:** `X-Tenant-Id: {slug}` or `?tenant={slug}`.

## Deployment Requirements

- Production API base URL should use the tenant subdomain (e.g. `https://cafe.regkasse.at/api`).
- Wildcard DNS `*.regkasse.at` and TLS are required at infrastructure level — see `REGKASSE_AI_ONBOARDING.md` §16.

Repo-wide detail: `REGKASSE_AI_ONBOARDING.md` (Multi-Tenant Architecture, API Headers, Deployment).
