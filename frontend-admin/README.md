# Regkasse Admin Panel

Next.js Admin Panel for Regkasse POS System.
Built with Ant Design 6, TanStack Query, and Orval.

### Updated Stack Versions

| Component | Version |
|-----------|---------|
| Backend (.NET) | 10.0.8 |
| EF Core | 10.0.8 |
| Next.js | 16.2.6 |
| React | 19.2.6 |
| Ant Design | 6.4.3 |
| Expo (POS) | SDK 56 |
| React Native (POS) | 0.85.3 |

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
- **UI**: Ant Design v6 with CSS-in-JS registry for SSR (`@ant-design/nextjs-registry`).
- **Ant Design 6**: use `destroyOnHidden` (not `destroyOnClose`), `popupRender` (not `dropdownRender`); official v5→v6 codemod is not published — apply [migration guide](https://ant.design/docs/react/migration-v6) warnings as needed.
- **i18n**: Custom `I18nProvider` + JSON catalogs; runtime namespace’ler ve dosya adı eşlemesi için `src/i18n/README.md` kaynak kabul edilir.

## Roles

| UI (de) | Backend | Scope |
|---------|---------|-------|
| **Mandanten-Admin** | `Manager` | Tenant management |
| **Kassierer** | `Cashier` | POS operations |
| **Super-Administrator** | `SuperAdmin` | Full system access |

Backend role names remain `Manager`, `Cashier`, `SuperAdmin` in API/database; UI labels come from `src/i18n/locales/*/users.json`.

## User Management

Users are created directly by administrators (Super Admin or **Mandanten-Admin**). **No email invitations** are sent.

### Creating a user

1. Navigate to **Users** → **Create User** (or tenant detail → **Benutzer** tab → **Benutzer anlegen**).
2. Fill in **E-Mail**, name, **Rolle**, and **Mandant** (Super Admin only, when not on a fixed tenant).
3. The system generates a secure password (shown **once** in a follow-up modal).
4. Copy **username**, **email**, and password from the success modal (`UserCreatedSuccessModal`) and share them securely (not via the product email channel).
5. The user must change the password on first login (`MustChangePasswordOnNextLogin`).

Optional **`userName`** on manual create: must be globally unique; when omitted, the backend auto-generates `{rolePrefix}{n}` (same rules as Quick Create).

### Quick Create User ("Schnell anlegen")

The Quick Create tab in `CreateUserModal` lets administrators create tenant users rapidly with auto-generated credentials.

**How it works:**

1. Open **Users** → **Create User** (or tenant detail → **Benutzer**) and switch to the **Schnell anlegen** tab.
2. Select **Rolle**: **Mandanten-Admin** (`Manager`), **Kassierer** (`Cashier`), or **Buchhaltung** (`Accountant`) — platform `Admin` / `SuperAdmin` are not available on this path.
3. Select **Mandant** when creating from the global users page (fixed when opened from tenant detail).
4. The system generates:
   - **Username:** `{rolePrefix}{nextAvailableNumber}` (e.g. `manager1`, `cashier2`, `user3` for Accountant)
   - **Email:** `{role}_{random6}@{tenantSlug}.regkasse.at` (e.g. `cashier_a3f9k2@dev.regkasse.at`)
   - **Password:** secure 12-character random password
5. `QuickUserSuccessModal` shows username, email, and password once (per-field copy + **copy all**).
6. User must change password on first login (`forcePasswordChangeOnNextLogin`).

**Username rules:**

- Minimum 3, maximum 50 characters.
- Allowed characters: letters (`a-z`), numbers (`0-9`), underscore (`_`), hyphen (`-`).
- **Case-insensitive:** the system does not distinguish uppercase and lowercase for login or uniqueness (`Manager1` and `manager1` are the same).
- Must be **globally unique** across all Identity users (not per tenant).
- Usable for login instead of email (`loginIdentifier` on `POST /api/Auth/login`).
- Auto-generated names: lowercase role prefix + digits; collision adds `_` + random suffix.
- Optional custom `userName` in the API body when creating (manual or quick); validation rejects case-insensitive duplicates.

**Login with username (FA):**

- Login form field accepts **email or username** (`LoginForm` → `loginIdentifier`, legacy `email` mirrored for compatibility).
- UI hint: tooltip and helper text state that username casing does not matter (`common.auth.loginIdentifierTooltip`, `loginIdentifierCaseHint`).
- POS uses the same backend contract; see `frontend/README.md` (Authentication).

**Rate limit:** up to 10 quick users per tenant per hour (server-side audit check).

**UI / API:**

- Hook: `createQuickUser` in `src/features/super-admin/api/quickUser.ts`
- Preview pattern: `getQuickUsernamePattern` in `src/features/super-admin/lib/quickUserPreview.ts`

**API example:**

```bash
# Quick Create (tenant-scoped) — role only; email, username, password generated server-side
POST /api/admin/tenants/{tenantId}/users/quick
Content-Type: application/json
Authorization: Bearer <token>

{
  "role": "Manager"
}

# Optional custom login name (must be unique)
{
  "role": "Cashier",
  "userName": "cashier_front"
}
```

**Response** (`CreateTenantUserResultDto`):

```json
{
  "userId": "...",
  "email": "manager_a3f9k2@dev.regkasse.at",
  "userName": "manager1",
  "generatedPassword": "...",
  "forcePasswordChangeOnNextLogin": true,
  "success": true,
  "tenantPortalUrl": "https://dev.regkasse.at",
  "role": "Manager"
}
```

Manual tenant user create (non-quick): `POST /api/admin/tenants/{tenantId}/users` with `email`, optional `userName`, `role`, etc. Platform users: `POST /api/admin/users` (no quick endpoint).

### Add existing user (tenant only)

On tenant detail (`/admin/tenants/{tenantId}`), **Bestehenden Benutzer hinzufügen** assigns an existing Identity account to the tenant (`AddExistingUserModal` → `POST …/users/assign`). This does **not** create a new login.

**API:** `createUser` in `src/features/users/api/users.ts` (tenant → `POST /api/admin/tenants/{id}/users`; platform → `POST /api/admin/users`). Hook: `useCreateUser`.

**Docs:** [`../docs/USER_MANAGEMENT.md`](../docs/USER_MANAGEMENT.md).

## Multi-Tenant Architecture

Regkasse uses a multi-tenant architecture where a single backend instance serves multiple tenants (companies/customers).

### Tenant Identification

- Tenants are identified by subdomain: `{tenant}.regkasse.at`
- Examples: `dev.regkasse.at`, `prod.regkasse.at`, `market.regkasse.at`
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
curl "http://localhost:5184/api/admin/payments?tenant=dev"
```

(Requires auth for payments; use `cafe` / `dev` only if matching `tenants.slug` in DB.)

**Option 3 — Hosts file:**

```text
127.0.0.1 dev.localhost
127.0.0.1 prod.localhost
```

Access API: `http://dev.localhost:5184`

**Option 4 — FA tenant switcher**

In **development** mode, FA shows a **searchable tenant dropdown in the header** (`HeaderDevTenantSwitch`). It loads tenants from **`GET /api/tenants/switcher`** (database-backed): Super Admin sees all tenants; other users see active memberships only. Selection sets `X-Tenant-Id` via `localStorage.dev_tenant_id` and reloads.

See [Tenant Switching](#tenant-switching) and [`../docs/TENANT_MANAGEMENT.md`](../docs/TENANT_MANAGEMENT.md).

Backend must be `ASPNETCORE_ENVIRONMENT=Development`. See `REGKASSE_AI_ONBOARDING.md`.

**Backend note:** `LicenseService` is a singleton and uses `IServiceScopeFactory` for database access (scoped `AppDbContext` / `ICurrentTenantAccessor`). Startup license warnings do not block the API.

- Hosts file: e.g. `dev.regkasse.local` → same slug resolution as production subdomains

## Tenant Switching

| Environment | Mechanism | Component / API |
|-------------|-----------|-----------------|
| **Production** | Subdomain + JWT `tenant_id`; Super Admin uses impersonation | `applyTenantImpersonationSession` |
| **Development** | `HeaderDevTenantSwitch` | `GET /api/tenants/switcher`, `persistTenantSlugAndRefresh` |

**Dev switcher features:** search by name/slug/email; status icons (active + admin / no admin / suspended); mandant license tag per row; Super Admin warning when switching to tenant without owner admin (`TenantSwitcherNoAdminFlow`).

**Header context:** `TenantBadge` (active company or Super Admin mode), `LicenseStatusIndicator` (**Mandantenlizenz** only via `useHeaderTenantLicense` — four states: keine / abgelaufen / bald ab / lizenziert; never Server-Lizenz).

| Header badge (Mandanten-Admin) | Condition | Color |
|------------------------|-----------|-------|
| Keine Mandantenlizenz | `license_valid_until_utc` null | Red |
| Lizenz abgelaufen | past end date | Red |
| Lizenz läuft bald ab | ≤7 days | Orange |
| Lizenziert | >7 days | Green |

**Server license page:** `/admin/license` — `license.page.title` = *Server-Lizenz (On-Premise)*; uses `GET /api/admin/license/deployment-status` — separate from header mandant badge.

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
| Server license (On-Premise) | `/admin/license` | `api/manual/adminLicense.ts` — **Server-Lizenz**; not Mandantenlizenz (see header badge) |
| Billing tenant license (docs) | — | [`../docs/BILLING_TENANT_LICENSE.md`](../docs/BILLING_TENANT_LICENSE.md); Mandanten-Admin API `POST /api/admin/license/extend` |

**Create tenant** runs backend `TenantProvisioningService` (cash register, demo products, owner admin, optional 30-day trial). Success modal shows one-time credentials.

**Screenshots (add PNGs under `docs/images/tenant-management/`):**

| Image | Description |
|-------|-------------|
| ![Tenant list](../docs/images/tenant-management/fa-tenant-list.png) | Mandantenverwaltung table |
| ![Tenant users](../docs/images/tenant-management/fa-tenant-detail-users.png) | Create user / add existing / roles / reset password |
| ![Super Admin home](../docs/images/tenant-management/fa-super-admin-selector.png) | Tenant picker + impersonate |

**Customer onboarding:** `CreateTenantWizard` on tenant list — see [`../docs/CUSTOMER_ONBOARDING.md`](../docs/CUSTOMER_ONBOARDING.md).

**Impersonation (production):** redirect to `https://{tenantSlug}.regkasse.at/impersonate-callback#impersonate_token=…` — [`../docs/IMPERSONATION_FLOW.md`](../docs/IMPERSONATION_FLOW.md).

**Docs index:** [`../docs/TENANT_MANAGEMENT.md`](../docs/TENANT_MANAGEMENT.md), [`../docs/BILLING_TENANT_LICENSE.md`](../docs/BILLING_TENANT_LICENSE.md), [`../docs/CUSTOMER_ONBOARDING.md`](../docs/CUSTOMER_ONBOARDING.md), [`../docs/USER_MANAGEMENT.md`](../docs/USER_MANAGEMENT.md), [`../docs/CASH_REGISTER_LIFECYCLE.md`](../docs/CASH_REGISTER_LIFECYCLE.md), [`../docs/LICENSE_SYSTEM.md`](../docs/LICENSE_SYSTEM.md), [`../docs/MULTI_TENANT.md`](../docs/MULTI_TENANT.md).

## License display

| What you see | Meaning |
|--------------|---------|
| **Super Admin Modus** (`TenantBadge`) | Platform host; no mandant context |
| Green/orange/red **Mandantenlizenz** tag (Mandanten-Admin on `{slug}.*`) | `tenants.license_valid_until_utc` / trial heuristic — **not** server On-Premise |
| **Lizenz (On-Premise)** `/admin/license` | Deployment / machine license (`LicenseService`) |
| Dev switcher license column | Same mandant fields per tenant row |

Expiry: header tag **orange** if ≤7 days; **red** if expired; content **banner** for Mandanten-Admins if ≤15 days or expired. Super Admin platform mode hides these (`suppressLicenseWarnings`).

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
