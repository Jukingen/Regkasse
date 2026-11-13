# OpenAPI contract governance

**Related:** Project behavior summary `REGKASSE_AI_ONBOARDING.md` (API lists and guardrails). This file focuses on generation and sync.

## Source of truth

- `backend/swagger.json` is the API contract source.
- Backend controller/DTO implementation must stay aligned with this file.
- The admin generated client (`frontend-admin/src/api/generated/**`) is derived from this file.

## Required workflow

1. Update API behavior in the backend.
2. Update `backend/swagger.json`.
3. Refresh Orval generation for admin.
4. Run contract scripts.

**Tenancy / DI (backend-only):** Singleton services access EF through `IServiceScopeFactory` — `LicenseService`; this is not a contract change. See the architecture note in `REGKASSE_AI_ONBOARDING.md`.

## Required checks

- `node scripts/validate-critical-openapi-paths.mjs`
- `node scripts/verify-api-client.mjs`
- CI: `.github/workflows/api-client-alignment.yml`
- CI: `.github/workflows/api-contract-tests.yml`

## Multi-tenant architecture

- Admin tenant and auth `tenant_id` fields in Swagger are part of the contract; label breaking changes.
- After Orval, admin Super Admin types (`adminTenants`, tenant DTOs) must stay in sync.

## Review rules

- Swagger diff review is required on contract-affecting PRs.
- Label breaking changes explicitly (which consumer is affected).
- Adding a new operation under a legacy prefix must be rejected (unless an exception is documented).

## Admin Orval notes

- Input: `../backend/swagger.json`
- Config: `frontend-admin/orval.config.ts`
- Transformer: `frontend-admin/scripts/orval-strip-legacy-paths.cjs`
- Do not hand-edit generated files.
