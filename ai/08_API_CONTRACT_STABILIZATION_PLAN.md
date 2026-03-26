# API Contract Stabilization Plan (Incremental, OpenAPI-first)

**See also:** [`ai/11_OPENAPI_CONTRACT_GOVERNANCE.md`](11_OPENAPI_CONTRACT_GOVERNANCE.md) — OpenAPI-first workflow, PR review rules, Orval alignment. [`ai/12_ADMIN_LEGACY_WRAPPER_REMOVAL.md`](12_ADMIN_LEGACY_WRAPPER_REMOVAL.md) — removed unused admin `src/api/legacy/*`. [`ai/13_POS_RESPONSE_NORMALIZATION_INVENTORY.md`](13_POS_RESPONSE_NORMALIZATION_INVENTORY.md) — POS normalization inventory & reduction plan.

## Scope and Current State
- Canonical boundary direction already exists and is valid:
  - Admin: `/api/admin/*`
  - POS: `/api/pos/*`
- Legacy and canonical routes currently coexist for some domains (especially cart/product/payment).
- Payment already has a partial v2 envelope mechanism (header-gated) and a legacy deprecation filter.
- `frontend-admin` is generated from `backend/swagger.json` via Orval with a transformer that removes selected legacy paths.

Inspected areas:
- `backend/Controllers/*`
- `backend/DTOs/PaymentApiContractDtos.cs`
- `backend/DTOs/PaymentApiContractMapper.cs`
- `backend/Services/PaymentLegacyRouteDeprecationFilter.cs`
- `backend/Swagger/PosAdminTagsAndDeprecationFilter.cs`
- `backend/swagger.json`
- `frontend-admin/orval.config.ts`
- `frontend-admin/scripts/orval-strip-legacy-paths.cjs`
- `frontend-admin/src/api/legacy/*`
- `frontend/services/api/*` (POS normalization and compatibility usage)

---

## Route Migration Matrix (Legacy vs Canonical)

| Domain | Legacy | Canonical | Status | Action |
|---|---|---|---|---|
| Payment (POS create/read/action lane) | `/api/Payment/*` | `/api/pos/payment/*` | Coexist | Keep alias temporarily, enforce deprecation + telemetry + staged removal |
| Payment (Admin operational views/actions) | Mixed legacy consumption in FE wrappers | `/api/admin/payments/*` | Canonical available | Migrate admin pages/wrappers to admin endpoints where semantics are admin-specific |
| Cart (POS) | `/api/Cart/*` | `/api/pos/cart/*` | Coexist | Freeze legacy usage, migrate generated consumers, then remove alias |
| Product (POS catalog lane) | `/api/Product/*` | `/api/pos/*` | Coexist | Migrate POS callers to `/api/pos/*`, then remove `/api/Product/*` |
| Product (Admin management lane) | `/api/Product/*` consumers still exist | `/api/admin/products/*` | Canonical available | Migrate admin CRUD/search/stock consumers to admin endpoints |
| User Management (Admin) | `/api/UserManagement/*` | `/api/admin/users/*` | Partial overlap coexistence | Keep both until parity matrix is complete, then switch generated client surface |

Notes:
- `Categories` legacy route is already removed in backend (`api/admin/categories` only).
- Swagger deprecation tagging currently focuses on Product/Cart/Payment prefixes; this should be expanded as migration proceeds.

---

## Strict API Boundary Policy

### Policy
1. Admin application may only consume `/api/admin/*` contracts.
2. POS application may only consume `/api/pos/*` contracts.
3. Any new endpoint outside those boundaries requires an explicit exception record and planned sunset.
4. Legacy aliases are read-only migration shims; no feature expansion on legacy routes.
5. OpenAPI is the single source of truth for request/response contract.

### Enforcements
- Backend:
  - Keep Swagger operation tagging (`Admin` / `POS`) and mark legacy routes deprecated.
  - Add CI check on `swagger.json` that fails when new legacy route prefixes are introduced without allowlist update.
- Frontend-admin:
  - Keep Orval transformer filtering legacy endpoints.
  - Add lint guard forbidding direct imports from legacy modules outside explicit compatibility layer.
- Frontend-pos:
  - Keep service boundary (`services/api/*`) and disallow direct fetch to non-canonical domains.

---

## Proposed Standard Response Envelope

Applies to critical write/read endpoints first (Payment, Cart completion/reset after payment, Admin payment actions).

### Success payload
```json
{
  "apiVersion": "v2",
  "success": true,
  "message": "Payment created successfully",
  "correlationId": "string",
  "requestId": "string",
  "data": {},
  "timestamp": "2026-03-25T10:15:30Z"
}
```

### Validation error payload (HTTP 400)
```json
{
  "apiVersion": "v2",
  "success": false,
  "code": "VALIDATION_FAILED",
  "message": "Validation failed",
  "correlationId": "string",
  "requestId": "string",
  "fieldErrors": {
    "payment.method": ["Required"]
  },
  "timestamp": "2026-03-25T10:15:30Z"
}
```

### Domain/business error payload (HTTP 409/422/400 as mapped)
```json
{
  "apiVersion": "v2",
  "success": false,
  "code": "PAYMENT_TSE_NOT_READY",
  "message": "TSE device not ready",
  "correlationId": "string",
  "requestId": "string",
  "detail": "Optional technical detail",
  "context": {
    "diagnosticCode": "TSE_NOT_READY"
  },
  "timestamp": "2026-03-25T10:15:30Z"
}
```

### Correlation/request id support
- `correlationId`: propagated from `X-Correlation-Id` middleware value.
- `requestId`: `HttpContext.TraceIdentifier` (or a dedicated request id header if introduced).
- Return both in envelope and in response headers for traceability.

---

## Concrete Backend Tasks by Module/File Area

### 1) Route policy + deprecation coverage
- `backend/Swagger/PosAdminTagsAndDeprecationFilter.cs`
  - Expand deprecation detection beyond current hardcoded prefixes where legacy remains.
  - Ensure deprecation description includes canonical successor path family.
- `backend/Services/*LegacyRouteDeprecationFilter.cs`
  - Generalize from payment-only to reusable filter/middleware for legacy aliases (`Cart`, `Product`, later `UserManagement` when mapped).
  - Emit headers: `Deprecation`, `Sunset`, `Link`, `X-Regkasse-Canonical-Route`.
- `backend/Services/CoreMetrics.cs` + interface
  - Add per-route-family counters:
    - `legacy_route_hits_total{family,method,route}`
    - `contract_v2_opt_in_total{domain,endpoint}`

### 2) Response contract standardization (critical endpoints first)
- `backend/DTOs/`
  - Introduce generic envelope + error DTOs shared across domains (keep payment-specific codes as-is, add domain namespaces incrementally).
- `backend/Controllers/PaymentController.cs`
  - Keep current v2 header path.
  - Add `requestId` in v2 responses.
  - Ensure all error exits map to stable `code`.
- `backend/Controllers/AdminPaymentsController.cs`
  - Align action responses (`cancel`, `refund`) to standard envelope.
- `backend/Controllers/CartController.cs`
  - Prioritize `complete` and `reset-after-payment` response normalization to envelope.

### 3) OpenAPI-first hardening
- `backend/swagger.json` generation pipeline (via Swashbuckle in `Program.cs`)
  - Ensure canonical endpoints are fully represented and examples are present for envelope success/error.
  - Mark legacy operations with `deprecated: true` and migration note.
  - Add reusable `components/schemas` for envelope and error structures.

### 4) Backward compatibility adapters
- Keep legacy route aliases active temporarily.
- Inside controllers, route to canonical handler logic only (single behavior source).
- For old response consumers, preserve legacy shape unless v2 header is requested (short-term dual contract).

---

## Concrete Frontend-Admin Tasks

1. Keep canonical generated clients as primary (`src/api/generated/admin/*`, `src/api/generated/pos/*` where intentionally needed).
2. Narrow `src/api/legacy/*` to compatibility-only wrappers and document removal criteria per wrapper.
3. Replace old payment usages with:
   - Admin pages: `/api/admin/payments/*`
   - POS-only payment APIs in admin only when functionally intended (forensics/receipt views).
4. Add contract-safe normalizers only at edge modules; avoid page-level shape branching.
5. Regeneration workflow:
   - Update backend OpenAPI
   - Run `npm run generate:api` in `frontend-admin`
   - Smoke test key pages (`payments`, `receipts`, RKSV incident/replay views)

---

## Concrete Frontend-POS Tasks

1. Keep payment lane fixed on `services/api/paymentService.ts` with canonical `/api/pos/payment/*`.
2. Remove duplicated normalization logic from scattered hooks/components; centralize in dedicated normalizers.
3. Ensure cart and register-readiness flows consume only `/api/pos/*`.
4. Add strict lint rule for forbidden API prefixes in POS (`/api/Payment`, `/api/Cart`, `/api/Product`).
5. Preserve offline queue payload compatibility while gradually adopting standard envelope parser.

---

## Test Plan

### Contract tests (backend)
- Extend `backend/KasseAPI_Final.Tests/PaymentApiContractTests.cs` patterns to:
  - Admin payment action envelope tests
  - Cart critical endpoint envelope tests
- Add tests for stable error code mapping and required fields (`code`, `correlationId`, `timestamp`).

### Schema validation tests
- Add CI step validating generated OpenAPI against schema and custom assertions:
  - no unapproved legacy prefix additions
  - deprecated=true on known legacy paths
  - presence of envelope/error schema refs on targeted endpoints

### Generated client smoke tests
- `frontend-admin`: run `generate:api` and compile/typecheck smoke.
- Add minimal runtime smoke for critical generated hooks (payments list/detail/cancel/refund).

### Critical payment flow integration tests
- Keep existing payment integration coverage.
- Add/extend tests for:
  - v2 header on/off behavior
  - deprecation headers on legacy payment alias
  - correlation/request id propagation in responses

---

## Rollout Plan (Phased, Non-blocking)

### Phase 0: Freeze legacy expansion
- No new legacy routes.
- CI warning/fail for new legacy prefixes.

### Phase 1: Standardize critical contracts
- Payment + admin payment actions + cart payment-adjacent endpoints get envelope support.
- OpenAPI updated with explicit schemas/examples.

### Phase 2: Consumer migration
- Admin pages migrate from legacy wrappers to canonical generated clients.
- POS normalization hacks consolidated to one compatibility layer.

### Phase 3: Observe and enforce
- Dashboard alerts on legacy route usage trend.
- Exit criterion: legacy route traffic near-zero for agreed window.

### Phase 4: Removal
- Remove legacy route aliases and compatibility wrappers in small PRs.
- Keep changelog + migration notes for rollback clarity.

---

## Risk Notes
- Biggest risk is hidden shape dependency in admin/POS normalization code.
- Dual-contract period can prolong complexity; keep explicit sunset date and metrics-based gate.
- Avoid touching fiscal-critical internals (TSE chaining, receipt numbering, closing logic) unless explicitly required by contract surface changes.
- Prefer additive envelope rollout with header opt-in first; flip default only after client migration readiness.

---

## Suggested PR Breakdown (Small, Safe)

1. **PR-1 Policy + Observability**
   - Extend deprecation headers/metrics coverage for all known legacy aliases.
   - Add CI guard for legacy route expansion.

2. **PR-2 Payment Contract Completion**
   - Finalize payment v2 envelope (`requestId`, complete error mapping).
   - Add payment contract + deprecation integration tests.

3. **PR-3 Admin Payment Actions Contract**
   - Align `/api/admin/payments/{id}/cancel|refund` responses to envelope.
   - Update OpenAPI and regenerate admin client.

4. **PR-4 Cart Critical Endpoints Contract**
   - Envelope for `complete` and `reset-after-payment` (with compatibility fallback).
   - Add integration tests.

5. **PR-5 Frontend-admin migration**
   - Replace legacy wrapper usages in payments/receipts/investigation surfaces.
   - Keep temporary adapters where needed.

6. **PR-6 Frontend-pos cleanup**
   - Consolidate normalization hacks and enforce canonical prefix lint.
   - Add smoke tests for payment/cart path assumptions.

7. **PR-7 Legacy removal**
   - Remove legacy aliases with zero-traffic evidence.
   - Remove obsolete compatibility wrappers and dead tests.

