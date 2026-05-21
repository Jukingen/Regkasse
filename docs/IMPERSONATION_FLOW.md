# Super Admin tenant impersonation (Frontend Admin)

Support staff on `admin.regkasse.at` can open a tenant’s operational FA context via **impersonation**. The backend issues a short-lived JWT scoped to the target tenant; the admin UI hands that token to the tenant subdomain without storing it on the admin host in production.

## API

| Method | Path | Permission |
|--------|------|------------|
| `POST` | `/api/admin/tenants/{tenantId}/impersonate` | `SuperAdmin` role |

**Response (`TenantImpersonationResponseDto`):** `token`, `expiresIn`, `refreshToken`, `refreshTokenExpiresAtUtc`, `tenantId`, `tenantSlug`, `tenantDisplayName`, `impersonation` (always `true`).

JWT claims include `tenant_id` and `tenant_impersonation=true`.

**Restrictions:** deleted, suspended, or inactive tenants return 400/404.

## Environment behaviour

| Host | Behaviour |
|------|-----------|
| `localhost`, `127.0.0.1`, `*.regkasse.local` | **Development:** token + refresh stored in `localStorage` / cookies on **same origin**; `dev_tenant_id` set to `tenantSlug`; full page reload. |
| `admin.regkasse.at`, `{slug}.regkasse.at` (production) | **Production:** browser navigates to tenant FA with JWT in **URL fragment** (not query string). |

Detection: `shouldUseProductionImpersonationRedirect()` in `frontend-admin/src/lib/auth/impersonationHandoff.ts` (hostname-based; not `NODE_ENV` alone).

Configurable base domain: `NEXT_PUBLIC_TENANT_APP_BASE_DOMAIN` (default `regkasse.at`).

## Production handoff (fragment — Option A)

1. Super Admin clicks **Als Mandant anmelden** on `/admin/tenants`.
2. FA calls `POST …/impersonate` and receives the JWT.
3. `applyTenantImpersonationSession()` assigns:

   `https://{tenantSlug}.{baseDomain}/impersonate-callback#impersonate_token=…&refresh_token=…&tenant={slug}`

   The fragment is **not** sent to the server on HTTP navigation.

4. Tenant FA route `/impersonate-callback` runs `applyImpersonationHandoffFromFragment()` (`tokenHandler.ts`):
   - Validates `tenant` matches subdomain slug.
   - Decodes JWT payload client-side; requires `tenant_impersonation` and non-expired `exp`.
   - Persists access/refresh via `authStorage`; tenant bootstrap via `tenantStorage`.
   - Strips the hash with `history.replaceState`.
   - Redirects to `/dashboard`.

5. Subsequent API calls use the impersonation JWT; EF filters use `tenant_id` from the token.

**Why not query `?impersonation_token=`:** query strings appear in server access logs, Referer headers, and browser history more often than fragments.

## Development handoff

Same API; `applyTenantImpersonationSession()` stores tokens on the admin/dev origin and sets `dev_tenant_id` for `X-Tenant-Id`-style dev resolution, then reloads. No cross-subdomain redirect.

## Code map

| File | Role |
|------|------|
| `frontend-admin/src/features/super-admin/api/adminTenants.ts` | `impersonateAdminTenant`, `applyTenantImpersonationSession` |
| `frontend-admin/src/lib/auth/impersonationHandoff.ts` | URL building, fragment parse, env detection |
| `frontend-admin/src/lib/auth/tokenHandler.ts` | Public API: `buildImpersonationRedirectUrl`, `applyImpersonationHandoffFromFragment` |
| `frontend-admin/src/features/auth/components/ImpersonateCallback.tsx` | Tenant-side callback UI |
| `frontend-admin/src/app/(public)/impersonate-callback/page.tsx` | Public route (middleware allow-list) |
| `frontend-admin/src/features/super-admin/components/ImpersonationRedirectOverlay.tsx` | Loading overlay on admin host before navigation |
| `frontend-admin/src/components/admin-layout/ImpersonationBanner.tsx` | Global banner on all protected pages (tenant scope + exit + expiry hint) |
| `frontend-admin/src/lib/auth/exitImpersonation.ts` | Clears session and returns to `admin.{base}/admin/tenants` |
| `backend/Services/ImpersonationAuditContext.cs` | Reads JWT `tenant_impersonation` + `tenant_id` for audit columns |
| `backend/Services/AuditLogService.cs` | Applies impersonation fields on every audit insert |

## Security notes

- Use HTTPS on all `*.regkasse.at` hosts in production.
- Impersonation tokens are short-lived; re-issue from admin if expired.
- Fragment handoff avoids leaving the JWT on `admin.regkasse.at` storage.
- **Not yet enforced:** JWT `tenant_id` ↔ request host subdomain binding (see `docs/MULTI_TENANT.md`).
- **Audit:** `audit_logs.impersonated_by` (Super Admin user id) and `audit_logs.impersonated_tenant` (target tenant uuid) are set on all rows written via `IAuditLogService` when JWT has `tenant_impersonation=true`, plus an explicit `TENANT_IMPERSONATION_STARTED` row when the session is issued.

## POS

The mobile POS does not consume this handoff. Operators use tenant-scoped login on the device. Impersonation targets **Frontend Admin** on the tenant subdomain.

## Related

- [`docs/IMPERSONATION.md`](./IMPERSONATION.md) — operator guide (DE) for Super Admin support staff
- `docs/MULTI_TENANT.md` — tenancy model and Super Admin overview
- `AGENTS.md` — impersonation summary in repo rules
