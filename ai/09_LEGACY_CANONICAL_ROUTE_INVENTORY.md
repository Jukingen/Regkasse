# Legacy vs canonical API route inventory

**Last reviewed:** 2026-08-13  
**Removal note:** [`docs/API_LEGACY_DEPRECATION.md`](../docs/API_LEGACY_DEPRECATION.md) (hard-removed **2026-08-13**)

## Definitions

- **Canonical:** Admin `/api/admin/*`, POS `/api/pos/*`.
- **Removed legacy alias:** Former second prefix on the same handler (`/api/Payment` + `/api/pos/payment`). Dual `[Route]` dropped; handlers remain on canonical prefixes only.
- **Policy gap:** Single route families not yet moved under `/api/admin/*` or `/api/pos/*`.

## A) Removed aliases (do not reintroduce)

| Family | Removed | Canonical | Backend source | Notes |
|--------|---------|-----------|----------------|-------|
| Payment | `/api/Payment/*` | `/api/pos/payment/*` | `PaymentController` | Canonical route only. |
| Cart | `/api/Cart/*` | `/api/pos/cart/*` | `CartController` | Canonical route only. Unused FA generated `/api/Cart` client deleted. |
| Product | `/api/Product/*` | `/api/pos/*` | `ProductController` | Canonical route only. Admin CRUD: `/api/admin/products`. |

## B) Consumer reality snapshot

- POS services use canonical `/api/pos/*` (`frontend/services/api/*`).
- Admin products → `/api/admin/products`.
- Orval transformer strips `/api/Product`, `/api/Categories`, `/api/Payment`, `/api/Cart`.
- OpenAPI (`backend/swagger.json`) does not publish the removed aliases.

## C) Policy-gap route families (single-surface, not alias)

- Examples: `/api/UserManagement/*`, `/api/Tse/*`, `/api/Tagesabschluss/*`, `/api/Settings/*`, `/api/Orders/*`, `/api/Receipts/*`, `/api/Invoice/*`.
- **Multi-tenant (canonical):** `/api/admin/tenants` — Super Admin only; impersonation `POST /api/admin/tenants/{tenantId}/impersonate`.
- **SaaS trials:** `/api/admin/trials` — Super Admin; ambient-tenant exempt.
- **Support:** Mandanten `/api/admin/support/tickets`; Super Admin inbox `/api/admin/support/admin/tickets`.
- These are not alias-removal work; they are controlled boundary-migration work.

## D) Known risks

1. Out-of-repo clients still on a legacy path get HTTP 404 (rollback: dual `[Route]` hotfix).
2. TSE/FinanzOnline/receipt route families carry high compatibility risk if names/paths change.
3. `/api/rksv/*` special-receipt endpoints are fiscal high risk; review separately during boundary migration.

## E) Maintenance rule

Update this file when:

- A controller route attribute changes
- The Orval transformer legacy list changes
- Timeline changes → also `docs/API_LEGACY_DEPRECATION.md`
