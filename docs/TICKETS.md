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
