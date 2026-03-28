# OpenAPI as the Source of Truth — Contract Governance

**Status:** Normative for API and client changes.  
**Related:** `ai/08_API_CONTRACT_STABILIZATION_PLAN.md`, `ai/10_API_BOUNDARY_POLICY.md`, `ai/09_LEGACY_CANONICAL_ROUTE_INVENTORY.md`  
**Last updated:** 2026-03-25

---

## 1. Single source of truth

| Artifact | Role |
|----------|------|
| **`backend/swagger.json`** | Committed OpenAPI document produced by **Swashbuckle** (`Program.cs` → `AddSwaggerGen`). This is the **contract** consumers and reviewers trust. |
| **Backend C# types** | **Implementation** of the contract: DTOs, controllers, `[ProducesResponseType]`, schema filters. They must **match** what Swashbuckle emits. |
| **`frontend-admin` generated client** | **Derived** from `swagger.json` via **Orval** (`frontend-admin/orval.config.ts`). Not hand-edited. |

**Rule:** If the **documented** request/response shape changes, **`swagger.json` must change in the same PR** (or the immediately following “regenerate spec” commit in the same merge train). Reviewers treat **OpenAPI diff** as the primary contract review surface.

---

## 2. OpenAPI-first workflow (end-to-end)

1. **Design / change** the HTTP contract (paths, methods, bodies, error shapes).
2. **Implement** in backend:
   - Named request/response DTOs (avoid anonymous `object` in public responses where Orval/schema quality matters).
   - `[ProducesResponseType(typeof(...), StatusCodes.StatusXXX)]` on actions for critical endpoints.
   - Existing `SchemaFilter` / `OperationFilter` patterns (`backend/Swagger/`) when enums or required fields need documentation.
3. **Regenerate** `swagger.json`:
   - Run the project’s standard export (e.g. build + script that writes `swagger.json`, or the repo’s documented command). **Commit the updated `swagger.json`.**
4. **Admin frontend:** from `frontend-admin`, run `npm run generate:api` (Orval). **Commit** `src/api/generated/**` changes.
5. **POS:** update `frontend/services/api/*` paths/types to match the spec; prefer centralized path constants (`apiPaths.ts`, `posPaymentPaths.ts`).

**PR description must include:** “Contract:” bullet — what changed, whether **breaking**, and link to **swagger diff** for reviewers.

---

## 3. Contract review (schema-first)

- **Required:** Reviewers **open `swagger.json` diff** (or Swagger UI / Redoc) for any PR that adds or changes public API behavior.
- **Breaking:** If a field is removed, renamed, or type-changed, label the PR **breaking** and coordinate Admin regenerate + POS consumers.
- **Legacy:** New operations **must not** be added under legacy-only prefixes (`/api/Payment`, `/api/Cart`, `/api/Product` without allowlist). See `ai/10_API_BOUNDARY_POLICY.md`.

---

## 4. Backend alignment with OpenAPI (targeted endpoints)

These areas are **explicitly** aligned with documented schemas (DTOs + attributes + filters):

| Area | Mechanism |
|------|-----------|
| **Payment POST (v2)** | `PaymentApiEnvelope<T>`, `PaymentApiErrorBody`, `PaymentApiErrorCodes`; header `X-Regkasse-Payment-Contract: v2`; `ProducesResponseType` on `PaymentController.CreatePayment`. |
| **Swagger metadata** | `PosAdminTagsAndDeprecationFilter` (POS/Admin tags, legacy `deprecated`); `TaxTypeSchemaFilter`, etc. |
| **General rule** | Prefer **named types** in `ProducesResponseType` / return types so Swashbuckle generates stable `components/schemas`. |

**Ongoing work:** Extend the same pattern to additional endpoints (e.g. admin payment actions, cart completion) — **track per PR**, not big-bang.

---

## 5. Orval and Admin client consistency

**Input:** `../backend/swagger.json`  
**Config:** `frontend-admin/orval.config.ts`  
**Transformer:** `frontend-admin/scripts/orval-strip-legacy-paths.cjs` — removes selected legacy **paths** from the spec so generated clients do not expose `/api/Product`, `/api/Categories`, `/api/Payment`.

**Rules:**

- Do **not** edit files under `src/api/generated/` manually.
- If Orval output changes unexpectedly, **fix the spec or controller**, not the generated files.
- Transformer changes (strip list) require **review + migration** — they hide endpoints from the client.

---

## 6. Frontend teams — what to rely on

| App | Source of truth |
|-----|-----------------|
| **Admin** | `openapi.json` / `swagger.json` → Orval types and hooks. **Boundary:** `ai/10_API_BOUNDARY_POLICY.md`. |
| **POS** | Same spec for **paths and shapes**; implementation is manual services — **must** match committed `swagger.json` (path strings and DTOs). Reduce drift via shared path modules and payment v2 envelope adoption. |

---

## 7. Acceptance criteria mapping

| Criterion | Where addressed |
|-----------|-----------------|
| OpenAPI-first workflow documented | §1–2 |
| Backend schema aligned with spec (targeted) | §4 (Payment v2 + general rules) |
| Contract changes reviewed via schema | §3 |
| Frontend teams can rely on spec | §5–6 |

---

## 8. CI / automation

| Check | Purpose | Where |
|-------|---------|--------|
| **Orval drift** | Regenerate client; fail if `frontend-admin/src/api/generated/` differs from git | Workflow **`.github/workflows/api-client-alignment.yml`**; script **`scripts/verify-api-client.mjs`** |
| **OpenAPI parse** | `backend/swagger.json` must be valid JSON | Same script (before Orval) |
| **Admin smoke** | `npm run build` after successful drift check (step sets `NEXT_PUBLIC_RKSV_ENVIRONMENT` / `NEXT_PUBLIC_API_BASE_URL` — build-time inlining) | Same workflow |
| Local | `cd frontend-admin && npm run verify:api-client` | `frontend-admin/package.json` |

Optional later: Spectral / swagger-cli validation for OpenAPI semantics (beyond JSON parse).
