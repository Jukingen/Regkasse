# Legacy API Boundary (Admin)

This folder contains **containment** for deprecated backend namespaces used by the admin app. The backend is moving to `/api/pos/*` (POS) and `/api/admin/*` (Admin). Legacy paths are not to be extended; new features should use admin or pos endpoints.

## Scope

- **Payment**: All admin usage of `/api/Payment/*` goes through `./payment.ts`. Do not import from `@/api/generated/payment/payment` in pages or features; use this module instead.
- **Cart**: Generated client at `@/api/generated/cart/cart` uses `/api/Cart/*`. No app code currently imports it. If you add Cart usage in admin, add a wrapper here (e.g. `./cart.ts`) and route all usage through it.
- **Product/Categories**: Admin uses **stable** `/api/admin/*` only; see `../admin/README.md`. Legacy Product/Categories paths are not used by admin.
- **Signature-debug**: `GET /api/Payment/{id}/signature-debug` is used by `src/features/receipts` for RKSV diagnostics. It remains a debug endpoint; do not auto-migrate. See "High-risk endpoints" in the deliverable.

## Rules

- Do not auto-migrate fiscal or debug endpoints (e.g. receipt, TSE, signature-debug). Document them and require manual review.
- Prefer containment: new legacy usage must go through this boundary so we can track and eventually replace it.
- Query keys and hooks in this folder are the single place to invalidate or refetch legacy payment/cart data.
