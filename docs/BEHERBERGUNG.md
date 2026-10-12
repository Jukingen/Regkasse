# Beherbergung (lodging)

Product notes for the `beherbergung` POS profile. This page describes how rooms and guest folios behave in the app. It is not a statement about tax, RKSV, or any other legal regime.

## Profile

Seed id `beherbergung`:

| Setting | Value |
|---------|--------|
| `posLayout` | `rooms` |
| `roomTracking` | true |
| `kitchenDisplay` | true |
| `tables`, `appointment`, `patientRecord` | false |

The code seed is the default. Migration `20261001063000_AddRoomsAndGuestFolios` updates the stored `vertical_profiles` row to the same values. Super Admin can change the live profile later in the Vertical Config Hub (`vertical_profile_overrides`). That override wins over the seed at read time.

## Tables

`rooms` and `guest_folios` already existed. This migration adds room and folio status, folio notes, and `guest_folio_items`.

| Table | Role |
|-------|------|
| `rooms` | Tenant room. Unique `(tenant_id, number)`. Status: Available, Occupied, Cleaning, Maintenance. |
| `guest_folios` | One stay. Status: Open, Closed, Cancelled. Balance is the sum of charges. |
| `guest_folio_items` | One deferred charge. `payment_detail_id` is reserved for a later settlement row and stays null on charge. |

Check-in sets the room to Occupied. Check-out sets the folio to Closed and the room to Cleaning. Cancelling a folio sets the room back to Available.

## Folio flow

1. Front desk creates rooms in FA (`/admin/rooms`) or, as Manager, with `POST /api/pos/rooms`.
2. POS check-in on the room grid (`POST /api/pos/folios`) opens a folio for a customer and a free room. The cash register can also check in the sale customer via `RoomPicker`.
3. **Auf Zimmer buchen** on the cash register, and **Buchung speichern** on the open folio, both call `POST /api/pos/folios/{id}/charge`. The line is stored on `guest_folio_items` and added to `guest_folios.balance`. The cash-register action then clears the cart.
4. **Konto anzeigen** lists those charges with amounts and timestamps. Check-out confirms the balance.
5. **Check-out bestätigen** closes the folio (`PATCH /api/pos/folios/{id}` with status `Closed`) and sets the room to Cleaning. That path does not sign a receipt. **Zur Kasse** calls `POST /api/pos/folios/{id}/stage-checkout`, which replaces the cashier's cart with one line `Zimmer {room} — Aufenthalt` priced at the folio balance. The existing POS payment creates the RKSV receipt. After that payment succeeds, the folio is closed and the room goes to Cleaning.

## RKSV decision

**A folio charge is a non-fiscal deferred posting. It does not create a `payment_details` row, a receipt, or a TSE signature.**

Why:

- The guest has not paid. The line is an open account item.
- `PaymentService.CreatePaymentAsync` allocates a receipt number and enters the signature path. Calling it for a room charge would add a fiscal document before payment.
- A zero-amount receipt would still be a signature-chain entry. This change does not add one.
- `payment_detail_id` on `guest_folio_items` stays null so a later, explicit payment can be linked without inventing a receipt at charge time.
- **Check-out bestätigen** only closes the folio and marks the room Cleaning. It does not sign anything.
- **Zur Kasse** does not accept a client price. The server writes `CartItem.UnitPrice` from the open folio balance and a stay label in `Notes`. The existing cart-snapshot pricing uses that gross on the RKSV receipt and the stay label as the line name. The folio closes only after that payment succeeds.

The signature chain, receipt sequence, and DEP export are unchanged. Audit events for this flow are operational, not fiscal: `RoomCreated` (129), `GuestFolioOpened` (130), `RoomStatusChanged` (134), `FolioClosed` (135), `FolioCharged` (136).

## API

POS (`cart.view`; create room is Manager or Super Admin):

- `GET /api/pos/rooms`
- `POST /api/pos/rooms`
- `PATCH /api/pos/rooms/{id}`
- `GET /api/pos/folios`
- `POST /api/pos/folios`
- `PATCH /api/pos/folios/{id}`
- `POST /api/pos/folios/{id}/charge`
- `POST /api/pos/folios/{id}/stage-checkout` — one cart line at the folio balance; no payment and no TSE receipt
- `GET /api/pos/folios/{id}/items`

Admin (`product.view`; writes need `product.manage`):

- `GET/POST /api/admin/rooms`
- `GET /api/admin/rooms/{id}`
- `PATCH /api/admin/rooms/{id}` — status only
- `PATCH /api/admin/rooms/{id}/details` — number, type, capacity
- `DELETE /api/admin/rooms/{id}` — only when the room has never had a folio (`409` `ROOM_IN_USE` otherwise)
- `GET /api/admin/folios` — optional `openOnly`, `fromUtc`, `toUtc` (closed folios by check-out)
- `GET /api/admin/folios/{id}`
- `GET /api/admin/folios/{id}/items`
- `POST /api/admin/folios/{id}/charge` — same non-fiscal charge as POS
- `PATCH /api/admin/folios/{id}` — check-out (`status=Closed` sets the room to Cleaning). No invoice and no TSE receipt.

Another tenant's room or folio returns HTTP 404.

## Screens

- POS: `frontend/app/(screens)/rooms.tsx` loads `GET /api/pos/rooms`. The room grid is color-coded: green Available (Frei), yellow Occupied (Belegt), blue Cleaning (Reinigung), gray Maintenance (Wartung). Tapping a room opens a detail modal (guest, folio balance, check-in, status change via `PATCH /api/pos/rooms/{id}`). **Konto anzeigen** (`frontend/components/lodging/FolioViewModal.tsx`) lists charges with amounts and timestamps. **Check-in** (`frontend/components/lodging/CheckInModal.tsx`) finds or creates a guest, sets the check-in date, optional nights, assigns a free room, and opens the folio with `POST /api/pos/folios` (the room becomes Occupied). **Check-out bestätigen** closes the folio and sets the room to Cleaning without a receipt. **Zur Kasse** stages the stay line and opens the normal payment modal; the folio closes after that RKSV payment.
- POS cash register: **Auf Zimmer buchen** (`frontend/components/FolioChargeBar.tsx`) posts the open sale to the current customer's open folio and clears the cart. The charge is not a payment.
- FA: `/admin/rooms` (create, edit, status, delete unused) and `/admin/rooms/{id}`.
- FA: `/admin/folios` and `/admin/folios/{id}` (charges, manual charge, check-out). Check-out does not generate an invoice.
- Tenant detail: **Zimmer** tab when that tenant's profile has `roomTracking=true`.
