# Tickets

Ticket sales is a vertical-profile flow, not a fiscal regime. Ticket QR codes are separate from RKSV receipt QR codes.

## Issue

When the tenant's effective profile is `ticket-sales` and a sold product has `is_ticket=true`, payment create issues one ticket per quantity. The plaintext code is returned once on the payment response (`issuedTickets`) for the printed ticket QR. The database stores only `ticket_code_hash` (SHA-256 of the normalized code) plus a short hash prefix in `ticket_code` for operator display.

Validity defaults to 365 days (`tenant_settings.ticketDefaultValidityDays`).

## Validate and redeem

POS (`cart.view`):

- `POST /api/pos/tickets/{code}/validate` — returns status and whether the ticket can be redeemed. Does not change state.
- `POST /api/pos/tickets/{code}/redeem` — sets `Redeemed`, `RedeemedAtUtc`, and `RedeemedByUserId`.

Errors: HTTP **409** `TICKET_ALREADY_REDEEMED`, HTTP **400** `TICKET_EXPIRED`, HTTP **404** when missing or in another tenant.

Admin (`product.view`): `GET /api/admin/tickets/redemptions` lists hashed display codes, status, validity, and redemption metadata. FA: `/admin/tickets/redemptions`.

## Print

The POS prints a ticket HTML/PDF after a successful sale when `issuedTickets` is present. That QR payload is the ticket code only. The RKSV receipt QR is unchanged and must not include the ticket code.

## v2 (seat map + categories)

v2 adds a price catalog and seat-grid metadata. It does not change receipt content, TSE payloads, or DEP export. `POST /api/pos/ticket-events/{id}/seats/sell` still does not call `PaymentService` or `TseService`. The category price on that response is a label. `ticket_code` and `ticket_code_hash` are unchanged.

Tables:

- `ticket_price_categories` (`ITenantEntity`): `code`, `name`, `sort_order`, `is_active`. Unique `(tenant_id, code)`.
- `ticket_event_category_prices` (not `ITenantEntity`; the parent event carries the tenant filter, same as `ticket_seats`): `price numeric(18,2)`, `capacity`. Unique `(event_id, category_id)`.

Columns:

- `ticket_events.stage_edge` (`top` or `bottom`, default `top`).
- `ticket_seats.category_id` (nullable), `grid_column` (nullable), `seat_role` (`0` standard, `1` accessible, `2` companion), `reserved_by_user_id`, `updated_at_utc`.

Hold ownership: a reservation still lasts 15 minutes (`TicketEventService.ReservationHold`). Reserve stores `reserved_by_user_id`. Sell returns **409** `SEAT_TAKEN` when the hold is still active and the caller is not that user. An expired hold can be sold by any cashier and is cleared. `Sold` is still **409** `SEAT_SOLD`.

Admin (`product.view` read, `product.manage` write; missing ambient tenant → **404**):

- `GET/POST /api/admin/ticket-price-categories`, `GET/PUT/DELETE /api/admin/ticket-price-categories/{id}` (delete sets `is_active=false`). Duplicate code → **409** `CATEGORY_CODE_DUPLICATE`.
- `GET/PUT /api/admin/ticket-events/{id}/category-prices`. Unknown category → **400** `CATEGORY_INVALID`. Another tenant's event → **404**.
- `PUT /api/admin/ticket-events/{id}` accepts `stageEdge`.
- `POST /api/admin/ticket-events/{id}/seats` accepts `categoryId`, `gridColumn`, `seatRole`. A category with no price row for the event → **400** `CATEGORY_PRICE_MISSING`. Category cap → **409** `CATEGORY_CAPACITY_REACHED`.

POS seat list and sell include `categoryId`, category code and name, `gridColumn`, `seatRole`, and `price` (label only). Sell writes `AuditEventType.TicketSeatSold` (147). Category and price changes write `AuditEventType.TicketPriceCategoryChanged` (146). Neither audit stores the ticket plaintext.

FA Platzplan now surfaces price categories, per-event prices, seat roles, and stage edge; redemption rows include the category name when a seat is set.

POS renders a stage-relative seat grid with category colors and role labels, and surfaces `SEAT_TAKEN` on the cashier screen.

## v2.1 (companion pairing)

`ticket_seats.companion_of_seat_id` is a nullable self-FK to `ticket_seats(id)` (`ON DELETE RESTRICT`), indexed. The pairing is one-way: a companion seat may point at the accessible seat it accompanies. There is no reverse `companion_seat_id`.

Create (`POST /api/admin/ticket-events/{id}/seats`) accepts optional `companionOfSeatId`. A companion seat with that id must reference a seat in the same event whose `seat_role` is accessible. Otherwise HTTP **400** `COMPANION_TARGET_INVALID`. Any other role must not set the column. Seat lists (admin and POS) return `companionOfSeatId`.

Sell still checks hold ownership first (`SEAT_TAKEN`). After that, a companion seat whose `companion_of_seat_id` is set can be sold only when that target is already `Sold`. Otherwise HTTP **409** `COMPANION_TARGET_NOT_SOLD`. A companion seat with no pairing sells as before. Reserve is unchanged. Every successful seat sell still writes `AuditEventType.TicketSeatSold` (147). Pairing does not add an audit event.

The fiscal boundary is unchanged. This path does not call `PaymentService` or `TseService`. The seat id and the companion target id are not written into `payment_details`, RKSV receipts, TSE payloads, or DEP export. `ticket_code` and `ticket_code_hash` are unchanged.
