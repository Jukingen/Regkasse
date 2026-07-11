# Regkasse POS (Mobile)

Expo Router + React Native + TypeScript cashier client.

Historical dev notes: `DEVELOPMENT.md` (pointer) and `archive/DEVELOPMENT.md`.

## Setup

See `package.json` scripts and repo root `REGKASSE_AI_ONBOARDING.md` for stack and fiscal constraints.

## Authentication

### POS Login Credentials

Users can log in using either:

- **Username** (short identifier like `cashier1`, `manager2`)
- **Email address** (full email)

The login field accepts both formats. Usernames are generated automatically when users are created via **Schnell anlegen** in the admin panel.

### POS Login (technical)

**Login flow:**

1. Cashier enters email **or** username in the login field (`frontend/app/(auth)/login.tsx`).
2. App calls `POST /api/Auth/login` with `loginIdentifier` (and legacy `email` for compatibility) plus `clientApp: "pos"` (`frontend/services/api/authService.ts`, `contexts/AuthContext.tsx`).
3. Backend resolves the user by email, then by username, validates password and POS role policy.
4. On success, JWT (+ optional refresh token) is returned; session stores token, user, and tenant bootstrap.

**Examples:**

- Username: `cashier1` + password
- Email: `cashier@dev.regkasse.at` + password

**Username generation (Admin only):**

- When operators create users via FA **Schnell anlegen**, usernames are auto-generated (`manager1`, `cashier2`, …).
- Pattern: `{rolePrefix}{incrementalNumber}` — see `REGKASSE_AI_ONBOARDING.md` § Authentication.
- Custom usernames can be set on manual admin user create (`userName` on `POST /api/admin/tenants/{tenantId}/users`).

**Case-insensitive usernames:** `Mustafa`, `mustafa`, and `MUSTAFA` are the same account at login (backend `NormalizedUserName`). See `REGKASSE_AI_ONBOARDING.md` (Authentication).

**Persistence:** last login identifier saved as `lastUsername` and `savedLoginIdentifier` in device storage (legacy `savedUsername` still read once). On next open, the password field is focused when a saved identifier exists.

**Verified in code (no POS change required):**

| Layer | Behavior |
|-------|----------|
| UI | `login.tsx` — single field `loginIdentifier` (email or username) |
| Context | `AuthContext.login()` → `buildLoginPayload(..., 'pos')` |
| API client | `POST /api/Auth/login` body: `loginIdentifier`, mirrored `email`, `clientApp: "pos"` |
| Backend | `FindByEmailAsync` then `FindByNameAsync` on `LoginModel.ResolveLoginIdentifier()` |

**Automated tests:**

- POS payload: `frontend/__tests__/authService.buildLoginPayload.test.ts` (`services/api/loginPayload.ts`)
- Backend username: `AuthControllerTests.Login_WithLoginIdentifierUsername_Succeeds`, `Login_WithLoginIdentifierUsername_ClientAppPos_Succeeds`

**Manual smoke test (dev):**

1. In FA, create or quick-create a tenant user with username `cashier1` (Cashier role, active).
2. Set `EXPO_PUBLIC_DEV_TENANT_ID` / API base URL so POS hits the same tenant as the user.
3. Open POS login, enter `cashier1` + password (not the full email).
4. Expect successful login and cashier home; wrong password → German error via `auth` i18n.

Contract detail: [`docs/API_CONTRACTS.md`](../docs/API_CONTRACTS.md) § `POST /api/Auth/login`.

Repo-wide auth detail: `REGKASSE_AI_ONBOARDING.md` (Authentication).

## Multi-Tenant Architecture

Regkasse uses a multi-tenant architecture where a single backend instance serves multiple tenants (companies/customers).

### Tenant Identification

- Tenants are identified by subdomain: `{tenant}.regkasse.at`
- Examples: `dev.regkasse.at`, `prod.regkasse.at`, `market.regkasse.at`
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
curl -H "X-Tenant-Id: dev" http://localhost:5184/api/health
```

### Option 2: Query string

```bash
curl "http://localhost:5184/api/health?tenant=dev"
```

### Option 3: Hosts file (subdomain simulation)

```text
127.0.0.1 dev.regkasse.local
127.0.0.1 prod.regkasse.local
127.0.0.1 dev.localhost
```

Then open `http://dev.regkasse.local:5184` or use header/query on `localhost:5184`.

### Dev tenant override (POS)

- `EXPO_PUBLIC_DEV_TENANT_ID` — default slug when unset in `__DEV__`
- `services/tenant/tenantStorage.ts` — sends `X-Tenant-Id` on loopback API calls in Development
- Effective slug order: dev switcher storage → env var → login/license bootstrap

Then: `http://dev.localhost:5184` — host first label must match `tenants.slug` (see onboarding doc).

### Option 4: POS UI

`DevTenantSwitcher` in tab layout (`__DEV__` only). Loads tenants from `GET /api/tenants/switcher` when authenticated (same as FA dev header switcher).

## POS Tenant Configuration

### Production

- `tenant_id`, `tenant_slug`, and `api_base_url` from license activation → secure storage (`tenantStorage.ts`).
- Requests use bootstrap `apiBaseUrl` (typically `https://{tenant}.regkasse.at/api`).

### Development

```env
EXPO_PUBLIC_DEV_TENANT_ID=dev
```

POS adds `X-Tenant-Id` and optional `?tenant=` on the API base URL automatically (`services/api/config.ts`, `services/tenant/devTenant.ts`).

Full guide: `REGKASSE_AI_ONBOARDING.md`.
- Login/license bootstrap persists `tenant_id` and `tenant_slug` for subsequent calls
- Optional hosts file: `dev.regkasse.local` pointing at the API machine (same slug rules as production)

### POS responsibilities

- Use tenant-aware API base URL when deploying per customer subdomain
- Do not cache or replay offline payment payloads across tenants
- After login, respect `tenantId` / `tenantSlug` from auth and license bootstrap (`contexts/AuthContext.tsx`, `api/license.ts`)

## API Headers

### Tenant Identification

- **Production:** Subdomain on API base URL (automatic).
- **Development:** `X-Tenant-Id: {slug}` or `?tenant={slug}`.

## Native Android / iOS workflow (CNG)

This project uses **Continuous Native Generation (prebuild-only)**. The `android/` and `ios/` folders are **not** committed — they are generated from `app.json` when needed.

| Task | Command |
|------|---------|
| Generate native projects | `cd frontend && npx expo prebuild` |
| Clean regenerate | `cd frontend && npx expo prebuild --clean` |
| Run on Android (Expo Go / dev client) | `npm run android` |

After changing `plugins`, `android`, `ios`, splash, or icon entries in `app.json`, run **`npx expo prebuild --clean`** locally before native builds or EAS Build.

Native folders are listed in `frontend/.gitignore`.

## Deployment Requirements

- Production API base URL should use the tenant subdomain (e.g. `https://dev.regkasse.at/api`).
- Wildcard DNS `*.regkasse.at` and TLS are required at infrastructure level — see `REGKASSE_AI_ONBOARDING.md` §16.

Repo-wide detail: `REGKASSE_AI_ONBOARDING.md` (Multi-Tenant Architecture, API Headers, Deployment).
