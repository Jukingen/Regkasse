# Legacy API removal (`/api/Payment`, `/api/Cart`, `/api/Product`)

**Status:** Hard-removed (2026-08-13). Canonical POS routes remain.  
**Last updated:** 2026-08-13  
**Related:** [`ai/09_LEGACY_CANONICAL_ROUTE_INVENTORY.md`](../ai/09_LEGACY_CANONICAL_ROUTE_INVENTORY.md), [`ai/10_API_BOUNDARY_POLICY.md`](../ai/10_API_BOUNDARY_POLICY.md)

## Canonical mapping (current)

| Removed alias | Canonical (kept) | Controller |
|---------------|------------------|------------|
| `/api/Payment/*` | `/api/pos/payment/*` | `PaymentController` |
| `/api/Cart/*` | `/api/pos/cart/*` | `CartController` |
| `/api/Product/*` | `/api/pos/*` (product actions) | `ProductController` |

Admin product CRUD: **`/api/admin/products`** (`AdminProductsController`).

The three controller types still host POS handlers. Only the dual `[Route]` legacy prefixes were dropped. DTOs and payment/cart/product services stay — they are used by canonical routes.

Requests to `/api/Payment`, `/api/Cart`, or `/api/Product` now return **HTTP 404**.

## Why early removal

Soft sunset was **2026-09-30**. Aliases were unused by POS (`frontend/services/api/*` canonical helpers) and FA (admin products + no live `/api/Cart` callers). OpenAPI already omitted the aliases (`LegacySwaggerPathExclusions`). They were removed before production so clients cannot re-adopt them.

## Rollback

Re-add `[Route("api/Payment")]` / `[Route("api/Cart")]` / `[Route("api/Product")]` on the same controller types (hotfix). Do **not** recreate separate legacy business logic.

## Safety nets still in the repo

- `LegacySwaggerPathExclusions` — if a dual route is reintroduced, OpenAPI still hides it.
- Orval transformer `frontend-admin/scripts/orval-strip-legacy-paths.cjs` strips `/api/Cart|/api/Payment|/api/Product`.
- Contract tests: `LegacyAliasRemovalContractTests`, `OpenApiCriticalPathsContractTests`, `LegacySwaggerPathExclusionsTests`.

## Compatibility tests

```powershell
cd backend
dotnet test KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj -c Release --filter "FullyQualifiedName~LegacyAliasRemovalContractTests|FullyQualifiedName~PaymentControllerAuthorizationTests|FullyQualifiedName~CartControllerForceCleanupAuthorizationTests|FullyQualifiedName~LegacySwaggerPathExclusionsTests|FullyQualifiedName~OpenApiCriticalPathsContractTests"
```

## Rules for contributors

1. Never reintroduce `/api/Payment`, `/api/Cart`, or `/api/Product`.
2. Admin-only features → `/api/admin/*`.
3. POS features → `/api/pos/*`.
4. Update this file + `ai/09_LEGACY_CANONICAL_ROUTE_INVENTORY.md` if route attributes change.
