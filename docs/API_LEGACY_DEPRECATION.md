# Legacy API deprecation timeline (`/api/Payment`, `/api/Cart`, `/api/Product`)

**Status:** Soft-deprecated (dual-route + headers + metrics). Hard removal not started.  
**Last updated:** 2026-07-21  
**Related:** [`ai/09_LEGACY_CANONICAL_ROUTE_INVENTORY.md`](../ai/09_LEGACY_CANONICAL_ROUTE_INVENTORY.md), [`ai/10_API_BOUNDARY_POLICY.md`](../ai/10_API_BOUNDARY_POLICY.md), `LegacyRouteDeprecationFilter`

## Canonical mapping

| Legacy alias | Canonical | Controller (shared handlers) |
|--------------|-----------|------------------------------|
| `/api/Payment/*` | `/api/pos/payment/*` | `PaymentController` |
| `/api/Cart/*` | `/api/pos/cart/*` | `CartController` |
| `/api/Product/*` | `/api/pos/*` (product actions) | `ProductController` |

Admin product CRUD: **`/api/admin/products`** (`AdminProductsController`) — not the legacy Product alias.

These three controllers use **dual `[Route]`** attributes: legacy and canonical hit the **same** action methods (no separate legacy business logic). Runtime deprecation applies **only** when the request path uses the legacy prefix (`Deprecation`, `Sunset`, `Link`, metrics, warning logs).

## Sunset

| Item | Value |
|------|--------|
| Soft sunset (headers) | **2026-09-30** (`Wed, 30 Sep 2026 23:59:59 GMT` in `LegacyRouteDeprecationFilter`) |
| Hard remove (drop legacy `[Route]`) | Earliest **2026-10-15**, only after gates below |

## Phased plan

### Phase 0 — Now (completed / ongoing)

- Dual routes keep backward compatibility.
- `LegacyRouteDeprecationFilter` on the three controllers.
- Controllers marked `[Obsolete(..., error: false)]` for IDE/compiler guidance (type still hosts canonical routes).
- Prometheus: `RecordLegacyRouteHit`.
- OpenAPI: legacy paths excluded from published contract where configured (`LegacySwaggerPathExclusions`).
- Policy: **no new features** on legacy aliases; new work under `/api/pos/*` or `/api/admin/*` only.

### Phase 1 — Client migration (target: before 2026-09-01)

1. POS: confirm zero direct `/Payment|/Cart|/Product` usage (paths via `frontend/services/api/*` canonical helpers).
2. FA: stop generating/using `/api/Cart/*` in Orval output; prefer `/api/pos/cart` only if FA still needs cart (otherwise remove unused generated cart client).
3. FA products: keep `/api/admin/products` only (`frontend-admin/src/api/admin/products.ts`).
4. Repo-external / partner clients: notify with Sunset header + this doc; collect traffic via metrics.

**Exit criteria:** 14 consecutive days of **near-zero** legacy metric hits in Staging + Production (define threshold with ops; default: &lt; 10 hits/day excluding health probes).

### Phase 2 — Soft enforcement (optional, ≥ Sunset)

- Optionally return **HTTP 410 Gone** for legacy prefixes with body pointing to canonical `Link` successor (feature flag).
- Keep dual routes behind flag for emergency rollback 7–14 days.

### Phase 3 — Hard remove (≥ 2026-10-15, metrics green)

1. Remove `[Route("api/Payment")]`, `[Route("api/Cart")]`, `[Route("api/Product")]` (keep only `/api/pos/...`).
2. Narrow or remove `LegacyRouteDeprecationFilter` path matching for those families.
3. Update inventory doc + OpenAPI strip lists.
4. Do **not** delete the controller types until POS cart/payment/product are optionally split into dedicated `Pos*` controllers (optional follow-up; not required for alias removal).

## Gates (hard remove blocked unless all true)

- [ ] Legacy route metrics at agreed near-zero for 14 days in Production
- [ ] FA Orval/OpenAPI no longer emits `/api/Cart|/api/Payment|/api/Product` for active features
- [ ] POS smoke: payment + cart + product list on `/api/pos/*` only
- [ ] Release notes + partner notice published ≥ 30 days before hard remove
- [ ] Rollback plan: re-add legacy `[Route]` in a hotfix commit

## Compatibility tests

Run when changing routes or the filter:

```powershell
cd backend
dotnet test KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj -c Release --filter "FullyQualifiedName~LegacyRouteDeprecationFilterTests|FullyQualifiedName~PaymentControllerAuthorizationTests|FullyQualifiedName~CartControllerForceCleanupAuthorizationTests|FullyQualifiedName~LegacySwaggerPathExclusionsTests"
```

## Rules for contributors

1. Never add a new action that is reachable **only** via `/api/Payment|/api/Cart|/api/Product`.
2. Admin-only features → `/api/admin/*` (e.g. products → `AdminProductsController`).
3. POS features → `/api/pos/*` (same dual-routed controller is fine until a Pos* split).
4. Update this file + `ai/09_LEGACY_CANONICAL_ROUTE_INVENTORY.md` when Sunset or route attributes change.
