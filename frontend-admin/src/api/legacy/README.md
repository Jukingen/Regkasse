# Legacy API Boundary (Admin)

This folder contains **payment/cart containment** for admin-facing compatibility lanes.
Default policy is **generated-client first**. Keep legacy wrappers only when compatibility is still required.

## Scope

- **Admin payments (browse/detail/cancel/refund/statistics)**: Use canonical generated admin endpoints directly (`@/api/generated/admin/admin`).
- **Legacy-compatible payment hooks**: `./payment.ts` also exposes compatibility hooks around `/api/pos/payment/*` where older admin flows still depend on that shape.
- **Cart**: Generated client at `@/api/generated/cart/cart` uses `/api/Cart/*`. No app code currently imports it. If you add Cart usage in admin, add a wrapper here (e.g. `./cart.ts`) and route all usage through it.
- **Product/Categories**: Admin uses **stable** `/api/admin/*` only; see `../admin/README.md`. Legacy Product/Categories paths are not used by admin.
- **Signature-debug**: Receipt diagnostics are consumed through `src/features/receipts/api/signature-debug.ts` (receipt forensics client).

## Rules

- New code must use `@/api/generated/*` directly unless a wrapper has a documented reason.
- For every remaining wrapper, include: **why manual**, **owner**, and **removal condition**.
- Do not auto-migrate fiscal or debug endpoints (e.g. receipt, TSE, signature-debug). Document them and require manual review.
- Query keys and hooks in this folder remain the containment point only for still-active legacy consumers.

## Deprecation timeline

- Legacy payment alias route family `/api/Payment/*` is deprecated at backend level.
- Sunset target: **2026-09-30 23:59:59 GMT**.
- Before sunset:
  - keep legacy consumers tracked in this folder only,
  - migrate active admin paths to `/api/admin/payments/*` (or `/api/pos/payment/*` when explicitly POS-bound),
  - avoid new dependencies on `/api/Payment/*`.
