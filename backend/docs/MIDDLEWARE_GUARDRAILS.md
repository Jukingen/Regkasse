# Middleware guardrails

## Canonical order (`ApplicationHost`)

```text
… → RateLimiting
  → CsrfMiddleware
  → TenantResolutionMiddleware
  → (static files)
  → TokenValidationMiddleware
  → UseAuthentication
  → TenantContextMiddleware
  → TenantValidationMiddleware
  → SessionActivityMiddleware
  → TenantOperationalGateMiddleware
  → LicenseMiddleware
  → UseAuthorization
  → MustChangePassword / PaymentSecurity
  → controllers
```

Expected core chain: **Resolution → Auth → Context → License** (with CSRF *before* Resolution).

## Why CSRF stays before Resolution

CSRF does not read tenant context. Rejecting invalid mutations before host/DB resolution is cheaper and safer.

## Tenant binding

| Stage | Behavior |
|-------|----------|
| Resolution (pre-auth) | Dev override / mandant host bind; Production `api`/`pos`/`admin`/`www`/loopback → **clear** ambient |
| Context (post-auth) | Production: JWT `tenant_id` only (fail-closed clear); Dev: header/query wins over JWT |
| Validation | 404 when ambient null, except public/ops allowlist (`/api/auth/*` login/refresh/2fa, `/api/csrf`, `/api/health`, `/health`, `/metrics`, …) |

## CSRF exemptions

Login, refresh, verify-2fa, health, swagger, metrics, webhooks, `GET/POST /api/csrf/token`.

## License

Runs after Context so mandant lockdown sees JWT-scoped `TenantId`. Deployment lockdown allows `/api/health*` and license activate (not general login).

## Tests

```bash
dotnet test --filter "FullyQualifiedName~TenantResolutionMiddlewareTests|FullyQualifiedName~TenantContextMiddlewareTests|FullyQualifiedName~CsrfMiddlewareTests|FullyQualifiedName~LicenseMiddlewareTests|FullyQualifiedName~TenantValidationMiddlewareTests"
```
