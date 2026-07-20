# Online Orders — User & Operator Guide

> **Scope:** Customer orders from a tenant website, PWA, or native app.  
> **Not in scope:** POS carts as the Manager fulfillment path, TSE signing, RKSV receipts, `payment_details` fiscal chain.

**Last updated:** 2026-07-19

**Related:** [`docs/DIGITAL_SERVICES.md`](DIGITAL_SERVICES.md) · [`docs/WORKING_HOURS.md`](WORKING_HOURS.md) · [`docs/PERMISSIONS_MATRIX.md`](PERMISSIONS_MATRIX.md) · [`AGENTS.md`](../AGENTS.md) § Roles (Digital services & online orders) · § Working hours · [`REGKASSE_AI_ONBOARDING.md`](../REGKASSE_AI_ONBOARDING.md) § Online orders

---

## Overview

Online orders are placed by customers on the mandant’s **website or mobile app**. Operators handle them in **Frontend-Admin (FA)** — not on the POS terminal.

**Working hours:** Placement is rejected when the restaurant is closed or past the online-order cutoff (`ONLINE_ORDERS_CLOSED`). This gate applies **only** to customer website/app intake — POS and FA remain fully operational. See [`WORKING_HOURS.md`](WORKING_HOURS.md).

| | Online orders | POS / fiscal |
|--|---------------|--------------|
| Source | Website / PWA / Native | Cash register session |
| Storage | `online_orders` (+ items / status history) | Cart → payment → receipt |
| TSE / RKSV | **No** | **Yes** (when fiscal) |
| Manager job | Status lifecycle only | POS payment & receipts |

Do **not** treat an online order as a Beleg or Tagesabschluss input.

---

## Order status flow

```text
pending → accepted → preparing → ready → completed
   │          │          │         │
   └──────────┴──────────┴─────────┴──→ cancelled
```

Forward steps must be **sequential** (no skipping). From any non-terminal status you may go to `cancelled`. Terminal states (`completed`, `cancelled`) have no further transitions.

Invalid transitions return `ONLINE_ORDER_STATUS_TRANSITION_INVALID`.

### Status descriptions

| Status | Meaning |
|--------|---------|
| `pending` | New order, not yet reviewed |
| `accepted` | Order accepted, preparation not started |
| `preparing` | Order is being prepared |
| `ready` | Ready for pickup / delivery |
| `completed` | Picked up / delivered |
| `cancelled` | Cancelled (terminal) |

---

## For Mandanten-Admin (Manager)

### Viewing orders

1. Go to FA → **Online-Bestellungen** (`/orders/online`).  
   Deep link: `/tenant/{tenantId}/orders`.
2. Filter by status (badges / counts: pending, accepted, preparing, ready, completed).
3. Open an order for customer, items, notes, and status history.

**Permission:** `digital.orders.view`

### Updating status

1. Open the order (or use the list next-step action).
2. Advance with the next forward status button, or cancel from the detail view.
3. The API persists status history and may notify the customer (email / push when configured).

**Permission:** `digital.orders.manage`  
**API:** `PATCH /api/admin/online-orders/{id}/status`

Manager UI is **status-only**: no “Accept → POS cart”, no TSE, no fiscal receipt creation.

---

## For Super Admin

- Same inbox and status workflow as Manager.
- Optional legacy bridge: `POST /api/admin/online-orders/{id}/accept` creates a POS cart link — requires **`digital.orders.approve`**. Prefer status-only fulfillment unless a deliberate POS handoff is required.
- Cross-tenant list/access follows FA rules: wrong tenant → **HTTP 404**.

---

## Permissions

| Permission | Manager | Super Admin |
|------------|---------|-------------|
| `digital.orders.view` | Yes | Yes |
| `digital.orders.manage` | Yes | Yes |
| `digital.orders.approve` (POS bridge) | No | Yes |

`digital.manage` implies all `digital.orders.*`.  
FA routes also accept legacy `order.view` where still wired for list access.

---

## API cheat sheet

| Action | Method / path | Permission |
|--------|----------------|------------|
| List / filter | `GET /api/admin/online-orders` | `digital.orders.view` |
| Get one | `GET /api/admin/online-orders/{id}` | `digital.orders.view` |
| Update status | `PATCH /api/admin/online-orders/{id}/status` | `digital.orders.manage` |
| Accept → POS cart | `POST /api/admin/online-orders/{id}/accept` | `digital.orders.approve` |

Public customer placement / tracking uses `/api/public/*` online-order endpoints (separate from FA admin inbox).

| Public action | Method / path | Notes |
|---------------|---------------|-------|
| Website open / can-order status | `GET /api/sites/{tenantSlug}/status` | Display + CTA gate for sites/apps only |
| Place online order | `POST /api/public/online-orders` | 409 + `ONLINE_ORDERS_CLOSED` when hours deny intake |

---

## Important notes

1. **Not fiscal receipts** — no Beleg / DEP / RKSV machine code for these rows.
2. **No TSE** — status changes do not call the signature pipeline.
3. **Separate from POS** — Manager must not rely on POS to “complete” a website order for kitchen fulfillment.
4. **No skipped steps** — e.g. `pending` → `ready` is rejected.
5. **Tenant isolation** — EF tenant filters + ambient JWT; cross-tenant → 404.
6. **Working hours** — customer place-order only (`OnlineOrderIntakeService`); never blocks POS payments or FA management.

---

## Related code

| Area | Location |
|------|----------|
| Model | `backend/Models/OnlineOrder.cs`, status history |
| Status engine | `OnlineOrderStatusService` |
| Admin API | `AdminOnlineOrdersController` |
| Public intake + hours gate | `PublicOnlineOrdersController`, `OnlineOrderIntakeService` |
| Website status | `Sites/Controllers/WebsiteStatusController.cs` |
| Hours model | `Models/WorkingHours.cs` (`EvaluateWebsiteStatus`) |
| FA UI | `frontend-admin/.../features/orders/` (`OrderManagement`, `OrderDetail`) |
| Sites UI | `frontend-sites` (`OnlineOrderPanel`, `MenuDisplay`) |
| Routes | `/orders/online`, `/tenant/[id]/orders` |
| Hours scope doc | [`WORKING_HOURS.md`](WORKING_HOURS.md) |

---

## Operator checklist

- [ ] Train kitchen staff on sequential status buttons (not POS payment).
- [ ] Do not document Accept→POS as the Manager happy path.
- [ ] Keep online-order bugs off TSE / Tagesabschluss investigation paths unless the optional Super Admin bridge was used.
