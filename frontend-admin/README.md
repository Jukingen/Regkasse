# Regkasse Admin Panel

Next.js Admin Panel for Regkasse POS System.
Built with Ant Design, TanStack Query, and Orval.

## Prerequisites

- Node.js 18+
- Backend API running (KasseAPI)

## Setup

1. Install dependencies:
   ```bash
   npm install
   ```

2. Environment Setup:
   Copy `.env.example` to **`.env.local` in the `frontend-admin/` directory** (same folder as `package.json` and `next.config.mjs`). This monorepo has **no root-level Next.js app** — a `.env.local` at the repository root is **not** read when you run `npm run dev` here; each app (`frontend-admin`, `frontend`) uses its own package root.
   ```env
   NEXT_PUBLIC_API_BASE_URL=http://localhost:5184
   NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST
   ```
   (Backend API varsayılan port: 5184; `backend/appsettings.Development.json` içindeki `Urls` ile uyumlu olmalı)

### Next.js: `NEXT_PUBLIC_*` variables (build-time, not runtime-only)

**Diagnosis (confirmed):** Names starting with `NEXT_PUBLIC_` are **compiled into the client JavaScript** when you run `next dev` or `next build`. They are **not** read fresh from the server environment on every request like a secret `DATABASE_URL`.

| Broken flow | Why the UI stays `UNCONFIGURED` |
|-------------|--------------------------------|
| Image built **without** `NEXT_PUBLIC_RKSV_ENVIRONMENT`, then variable set only on `docker run` / Compose `environment:` / K8s pod env | The browser bundle was already built with `undefined` / empty. `next start` does not re-bundle. |
| CI builds artifact without the var, ops adds it only in production host env | Same: client chunk never saw the value at compile time. |

**Correct flow:** Provide `NEXT_PUBLIC_RKSV_ENVIRONMENT` (and other `NEXT_PUBLIC_*`) **before** `next build` (or before the dev compiler picks up `.env.local`). For Docker: use `ARG`/`ENV` **before** `RUN npm run build`, or Compose `build.args`, or set env on the CI step that runs `npm run build`.

**This repo:** There is **no** `Dockerfile` or `docker-compose` for `frontend-admin` in-tree. **CI:** `.github/workflows/api-client-alignment.yml` runs `npm run build` with `NEXT_PUBLIC_RKSV_ENVIRONMENT` and `NEXT_PUBLIC_API_BASE_URL` set on that step (`.env.local` is gitignored). Full deployment notes and Docker examples: [`docs/DEPLOYMENT_BUILD_TIME_ENV.md`](docs/DEPLOYMENT_BUILD_TIME_ENV.md).

### RKSV: `NEXT_PUBLIC_RKSV_ENVIRONMENT`

- **Allowed values:** `TEST`, `PROD` (any casing). **`NEXT_PUBLIC_*` variables must be present when `next dev` / `next build` runs** — not only at `next start` or container runtime. Restart dev or rebuild after changing them.
- **Why:** The RKSV hub (`/rksv`) shows an explicit FinanzOnline environment badge so operators don’t mix up test vs live FinanzOnline context (Austria Registrierkasse / RKSV).
- **If not set (local dev):** `next dev` still starts; `/rksv` shows **UNCONFIGURED** with fix hints and a one-time English `console.warn`.
- **Invalid value (e.g. `STAGING`):** UI shows **INVALID** with a sanitised display of what was read; `npm run build` also fails (`next.config.mjs`).

Code: `src/shared/config/rksvEnvironment.ts`.

3. Generate API Client:
   If Backend Swagger changes, ensure `orval.config.ts` points to `../backend/swagger.json` and run:
   ```bash
   npm run generate:api
   ```

## Scripts

- `npm run dev`: Start development server (localhost:3000)
- `npm run dev:clean`: Deletes `.next` then starts dev — use if `/rksv` stays **UNCONFIGURED** after fixing `.env.local` (stale inlined `NEXT_PUBLIC_*`).
- `npm run build`: Build for production (**requires** `NEXT_PUBLIC_RKSV_ENVIRONMENT=TEST` or `PROD`; see above)
- `npm start`: Start production server
- `npm run lint`: Run ESLint
- `npm run generate:api`: Generate TypeScript client from Swagger

## Project Structure

- `src/app`: Next.js App Router pages
- `src/api/generated`: Orval generated hooks and models
- `src/features`: Feature-specific components
- `src/lib`: Shared providers (AntD, QueryClient)
- `src/theme`: Ant Design theme configuration

## Architecture

- **Auth**: Uses `/api/Auth/login` (Cookie-based or Token-based).
- **Data Fetching**: TanStack Query via Orval generated hooks.
- **UI**: Ant Design v5 with CSS-in-JS registry for SSR.
- **i18n**: Custom `I18nProvider` + JSON catalogs; runtime namespace’ler ve dosya adı eşlemesi için `src/i18n/README.md` kaynak kabul edilir.

## User Management

Users are created directly by administrators (Super Admin or Manager). **No email invitations** are sent.

### Creating a user

1. Navigate to **Users** → **Create User** (or tenant detail → **Benutzer** tab → **Benutzer anlegen**).
2. Fill in **E-Mail**, name, **Rolle**, and **Mandant** (Super Admin only, when not on a fixed tenant).
3. The system generates a secure password (shown **once** in a follow-up modal).
4. Copy the password and share it securely with the user (not via the product email channel).
5. The user must change the password on first login (`MustChangePasswordOnNextLogin`).

### Add existing user (tenant only)

On tenant detail (`/admin/tenants/{tenantId}`), **Bestehenden Benutzer hinzufügen** assigns an existing Identity account to the tenant (`AddExistingUserModal` → `POST …/users/assign`). This does **not** create a new login.

**API:** `createUser` in `src/features/users/api/users.ts` (tenant → `POST /api/admin/tenants/{id}/users`; platform → `POST /api/admin/users`). Hook: `useCreateUser`.

**Docs:** [`../docs/USER_MANAGEMENT.md`](../docs/USER_MANAGEMENT.md).

## Multi-Tenant Architecture

Regkasse uses a multi-tenant architecture where a single backend instance serves multiple tenants (companies/customers).

### Tenant Identification

- Tenants are identified by subdomain: `{tenant}.regkasse.at`
- Examples: `cafe.regkasse.at`, `bar.regkasse.at`, `market.regkasse.at`
- Super Admin accesses `admin.regkasse.at`

### Data Isolation

- Every tenant-scoped database row has a `TenantId` column (non-nullable on `ITenantEntity` types)
- Backend EF global query filters scope data per request tenant
- Tenants can NEVER see other tenants' data
- Cross-tenant access attempts return HTTP 404

### Development Mode

- Localhost API: set `X-Tenant-Id` to a tenant **slug** (matches backend `SubdomainTenantProvider`)
- Optional `?tenant={slug}` on API requests in Development
- Dev-only tenant selector in the admin shell (`src/features/auth/` — presets in `devTenantPresets.ts`)

## Development Setup for Multi-Tenant Testing

**Option 1 — Header:**

```bash
curl -H "X-Tenant-Id: cafe" http://localhost:5184/api/health
```

**Option 2 — Query string:**

```bash
curl "http://localhost:5184/api/admin/payments?tenant=test_cafe"
```

(Requires auth for payments; use `cafe` / `test_cafe` only if matching `tenants.slug` in DB.)

**Option 3 — Hosts file:**

```text
127.0.0.1 test-cafe.localhost
127.0.0.1 test-bar.localhost
```

Access API: `http://test-cafe.localhost:5184`

**Option 4 — FA tenant switcher**

In **development** mode, FA shows a **searchable tenant dropdown in the header** (`HeaderDevTenantSwitch`). It loads tenants from **`GET /api/tenants/switcher`** (database-backed): Super Admin sees all tenants; other users see active memberships only. Selection sets `X-Tenant-Id` via `localStorage.dev_tenant_id` and reloads.

See [Tenant Switching](#tenant-switching) and [`../docs/TENANT_MANAGEMENT.md`](../docs/TENANT_MANAGEMENT.md).

Backend must be `ASPNETCORE_ENVIRONMENT=Development`. See `REGKASSE_AI_ONBOARDING.md`.

**Backend note:** `LicenseService` is a singleton and uses `IServiceScopeFactory` for database access (scoped `AppDbContext` / `ICurrentTenantAccessor`). Startup license warnings do not block the API.

- Hosts file: e.g. `cafe.regkasse.local` → same slug resolution as production subdomains

## Tenant Switching

| Environment | Mechanism | Component / API |
|-------------|-----------|-----------------|
| **Production** | Subdomain + JWT `tenant_id`; Super Admin uses impersonation | `applyTenantImpersonationSession` |
| **Development** | `HeaderDevTenantSwitch` | `GET /api/tenants/switcher`, `persistTenantSlugAndRefresh` |

**Dev switcher features:** search by name/slug/email; status icons (active + admin / no admin / suspended); mandant license tag per row; Super Admin warning when switching to tenant without owner admin (`TenantSwitcherNoAdminFlow`).

**Header context:** `TenantBadge` (active company or Super Admin mode), `LicenseStatusIndicator` (Manager mandant license only — not server On-Premise license).

Utilities: `src/features/super-admin/utils/tenantHeaderSwitcher.ts`, `src/features/tenancy/hooks/useTenantListForSwitcher.ts`.

![Dev tenant switcher](../docs/images/tenant-management/fa-header-tenant-switcher.png)

## Super Admin Features

Access: **`admin.regkasse.at`** (or local dev on platform host). Role: **`SuperAdmin`** or `system.critical`.

| Feature | Route | Key files |
|---------|-------|-----------|
| Tenant list / create / edit / suspend / delete | `/admin/tenants` | `app/(protected)/admin/tenants/page.tsx`, `features/super-admin/api/adminTenants.ts` |
| Tenant detail (users, license, registers) | `/admin/tenants/[tenantId]` | `TenantDetailUsersTab`, `LicenseManager`, `TenantDetailCashRegistersTab` |
| Impersonate (“Login as”) | list / detail / home selector | `impersonateAdminTenant`, `ImpersonationRedirectOverlay` |
| Platform home (pick tenant) | `/admin` | `SuperAdminTenantSelector` |
| Server license (On-Premise) | `/admin/license` | `api/manual/adminLicense.ts` — **not** the same as Mandantenlizenz |

**Create tenant** runs backend `TenantProvisioningService` (cash register, demo products, owner admin, optional 30-day trial). Success modal shows one-time credentials.

**Screenshots (add PNGs under `docs/images/tenant-management/`):**

| Image | Description |
|-------|-------------|
| ![Tenant list](../docs/images/tenant-management/fa-tenant-list.png) | Mandantenverwaltung table |
| ![Tenant users](../docs/images/tenant-management/fa-tenant-detail-users.png) | Create user / add existing / roles / reset password |
| ![Super Admin home](../docs/images/tenant-management/fa-super-admin-selector.png) | Tenant picker + impersonate |

**Customer onboarding:** `CreateTenantWizard` on tenant list — see [`../docs/CUSTOMER_ONBOARDING.md`](../docs/CUSTOMER_ONBOARDING.md).

**Impersonation (production):** redirect to `https://{tenantSlug}.regkasse.at/impersonate-callback#impersonate_token=…` — [`../docs/IMPERSONATION_FLOW.md`](../docs/IMPERSONATION_FLOW.md).

**Docs index:** [`../docs/TENANT_MANAGEMENT.md`](../docs/TENANT_MANAGEMENT.md), [`../docs/CUSTOMER_ONBOARDING.md`](../docs/CUSTOMER_ONBOARDING.md), [`../docs/USER_MANAGEMENT.md`](../docs/USER_MANAGEMENT.md), [`../docs/CASH_REGISTER_LIFECYCLE.md`](../docs/CASH_REGISTER_LIFECYCLE.md), [`../docs/LICENSE_SYSTEM.md`](../docs/LICENSE_SYSTEM.md), [`../docs/MULTI_TENANT.md`](../docs/MULTI_TENANT.md).

## License display

| What you see | Meaning |
|--------------|---------|
| **Super Admin Modus** (`TenantBadge`) | Platform host; no mandant context |
| Green/orange/red **Mandantenlizenz** tag (Manager on `{slug}.*`) | `tenants.license_valid_until_utc` / trial heuristic — **not** server On-Premise |
| **Lizenz (On-Premise)** `/admin/license` | Deployment / machine license (`LicenseService`) |
| Dev switcher license column | Same mandant fields per tenant row |

Expiry: header tag **orange** if ≤7 days; **red** if expired; content **banner** for Managers if ≤15 days or expired. Super Admin platform mode hides these (`suppressLicenseWarnings`).

![Manager license badge (placeholder)](../docs/images/tenant-management/fa-manager-license-badge.png)  
![Deployment license page (placeholder)](../docs/images/tenant-management/fa-deployment-license.png)

### Multi-Tenant Security (client)

- Production: tenant from subdomain; do not rely on `X-Tenant-Id` in production builds.
- Cross-tenant IDs from API return 404; handle as “not found”, not permission denied.
- Dev only: header/query documented in `REGKASSE_AI_ONBOARDING.md`.

## API Headers

### Tenant Identification

- **Production:** Tenant from subdomain (no header required).
- **Development:** `X-Tenant-Id: {slug}` or `?tenant={slug}` (slug, not UUID).

### Super Admin Endpoints

- `/api/admin/tenants/*` — `SuperAdmin` only; see `src/features/super-admin/`.
- Impersonation: `POST /api/admin/tenants/{tenantId}/impersonate` for tenant-scoped support.

## Deployment Requirements

### DNS Configuration

- Wildcard A record: `*.regkasse.at` → server IP (per-tenant and `admin` subdomains).
- Wildcard SSL certificate required in production.

### Environment Variables

- Backend `ASPNETCORE_ENVIRONMENT`: `Development` allows dev tenant header/query; `Production` uses subdomain only.
- Admin build: see [`docs/DEPLOYMENT_BUILD_TIME_ENV.md`](docs/DEPLOYMENT_BUILD_TIME_ENV.md) for `NEXT_PUBLIC_*` at build time.

Repo-wide detail: `docs/MULTI_TENANT.md`, `REGKASSE_AI_ONBOARDING.md` (Multi-Tenant Architecture, API Headers, §16 Deployment).
