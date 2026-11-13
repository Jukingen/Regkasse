# API contract stabilization plan

**Status:** Active, incremental (no big-bang rewrite).

**Context:** `REGKASSE_AI_ONBOARDING.md` is the top-level behavior and RKSV/voucher summary. This file is the route/OpenAPI stabilization work queue.

## Multi-tenant architecture

- New admin endpoints must not break tenant context; the Super Admin surface stays under `/api/admin/tenants`.
- On OpenAPI/swagger changes, review `tenant_id` claims and admin tenant DTOs in the diff.

## Current repository facts

- Canonical boundaries exist: `/api/admin/*` and `/api/pos/*`.
- Legacy aliases for `Payment`, `Cart`, `Product` were **hard-removed** (2026-08-13). See `docs/API_LEGACY_DEPRECATION.md`.
- OpenAPI contract checks run via `scripts/validate-critical-openapi-paths.mjs` and `scripts/verify-api-client.mjs`.

## Stabilization goals

1. Stop legacy expansion.
2. Keep OpenAPI and implementation aligned.
3. Move consumers to canonical paths with minimal risk.
4. Preserve fiscal/compliance behavior during migration.

## Practical rules

- New endpoint: canonical route only.
- Do not reintroduce `/api/Payment`, `/api/Cart`, or `/api/Product`.
- Contract change: update `backend/swagger.json` and the related consumer in the same change set.

## Near-term work queue

1. **Payment contract hardening:** Track v2 envelope usage; shrink legacy parse branches with metrics.
2. **OpenAPI governance:** Keep critical-path scripts green in CI; block new retired prefixes.
3. **Route inventory upkeep:** Keep `ai/09_LEGACY_CANONICAL_ROUTE_INVENTORY.md` current.

## Validation baseline

- `node scripts/validate-critical-openapi-paths.mjs`
- `node scripts/verify-api-client.mjs`
- `dotnet test backend/KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj --filter "FullyQualifiedName~PaymentApiContractTests|FullyQualifiedName~OpenApiCriticalPathsContractTests"`
- `cd frontend-admin && npm run test:contract`
- `cd frontend && npm run test:contract`
