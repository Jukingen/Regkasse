# POS API Guardrail

**Purpose:** Frontend POS integrations should use the canonical POS routes. Do not revert to deprecated Cart/Payment routes. This document is the single reference for POS API path expectations.

**Scope:** Frontend experience and integration only. Endpoint refactors are out of scope unless explicitly requested.

---

## Cart flow – POS routes (preferred)

Use when backend exposes them; keep existing POS-oriented usage.

- `GET  /api/pos/cart/current` (e.g. `?tableNumber=X`)
- `GET  /api/pos/cart` (as needed)
- `POST /api/pos/cart/add-item`
- `GET  /api/pos/cart/{cartId}/items`
- `POST /api/pos/cart/items/{itemId}/increment`
- `POST /api/pos/cart/items/{itemId}/decrement`
- `POST /api/pos/cart/{cartId}/complete`

---

## Payment flow – POS routes (preferred)

- `GET  /api/pos/payment/methods`
- `POST /api/pos/payment`
- `GET  /api/pos/payment/{id}`
- `POST /api/pos/payment/{id}/cancel`
- `POST /api/pos/payment/{id}/refund`

---

## Rules

1. **Prefer `/api/pos/...`** for new or updated POS integrations where the backend supports these routes.
2. **Do not revert** to deprecated Cart/Payment routes for the POS flow.
3. **Preserve** existing POS service usage (e.g. `cartService`, `paymentService`) and contracts; changes are presentation/UX-focused unless an endpoint migration is explicitly requested.
4. **Backend contract:** Any path change (e.g. from `/cart/...` to `/api/pos/cart/...`) is a backend/frontend alignment task and must be done with explicit scope and rollout.

---

## Current frontend usage (reference)

- Cart: `cartService` and `CartContext` call paths such as `/cart/current`, `/cart/add-item`, `/cart/{cartId}/complete` (base URL from config).
- Payment: `paymentService` uses a base path (e.g. `/Payment`) for methods and process.
- Table recovery / other: may use distinct paths (e.g. table-orders-recovery).

When aligning with the guardrail, prefer migrating these to the `/api/pos/...` paths above if and when the backend provides them, without breaking existing behaviour.
