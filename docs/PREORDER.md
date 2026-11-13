# POS Vorbestellung (pre-order / Besorgerzettel)

Paid POS sales can be marked **Vorbestellung**. The customer pays now and picks up later.

## Fiscal rules

- The RKSV receipt and TSE signature are created **at payment time** (existing `POST /api/pos/payment`).
- The fiscal total is always the catalog/cart total of **that** sale. TSE totals are not rewritten.
- Pickup (**Abholung**) only updates operational status. **No second fiscal receipt.**
- An operational **Offener Betrag** is paid later as a **new** fiscal sale (`PreorderBalanceOrderId`). That later sale has its own Beleg + TSE.
- Cancel uses the existing fiscal **storno** (`IPaymentService.CancelPaymentAsync`). Do not invent a second reversal path.

## Receipt (Besorgerzettel)

Printed on the fiscal sale receipt when `isPreorder` is true:

- Header: `VORBESTELLUNG / BESORGERZETTEL`
- Number: `BS{yyMMdd}{seq}` (Vienna date, daily sequence). This is **not** the fiscal Belegnummer.
- `Anzahlungsbetrag` = amount already paid (fiscal payments on this pre-order)
- `Offener Betrag` = remaining operational amount
- Footer: `Besorger innerhalb von {weeks} Wochen abholen!` and the configured return-policy line

Pickup confirmation is on-screen only (`ABHOLUNG - Vorbestellung abgeholt am: …`).

## Status

`pending` → `ready` → `collected`. `cancelled` only after fiscal storno.

Pickup is rejected while `Offener Betrag` > 0 (`PREORDER_BALANCE_OPEN`).

## Permissions

| Action | Permission |
|--------|------------|
| Create (checkbox at payment) | `payment.take` / `sale.create` (existing payment) |
| List / search | `order.view` |
| Mark ready / collected | `order.update` |
| Cancel (storno) | `order.cancel` + `payment.cancel` (Manager) |
| FA pickup-deadline / policy text | `settings.view` |

## Surfaces

| Client | Path |
|--------|------|
| POS payment | Vorbestellung checkbox, optional Offener Betrag, optional Restzahlung by BS number |
| POS receipt | Besorgerzettel header + Anzahlung / Offen + pickup note |
| POS orders | Search by BS or Beleg + Abholung |
| FA list | `/orders/preorders` |
| FA settings | Same page (`settings.view`) |
| FA widget | Dashboard `preorder-status` |
| POS API | `GET /api/pos/orders/preorders`, `PUT /api/pos/orders/{id}/preorder-status` |
| Admin API | `GET /api/admin/orders/preorder-stats`, `GET /api/admin/orders/preorders`, `GET/PUT /api/admin/orders/preorder-settings` |

Website **online orders** (`online_orders`) stay separate.
