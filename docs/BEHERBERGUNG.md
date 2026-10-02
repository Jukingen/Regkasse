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
2. POS check-in (`POST /api/pos/folios`) opens a folio for a customer and a free room.
3. **Auf Zimmer buchen** calls `POST /api/pos/folios/{id}/charge`. The line is stored on `guest_folio_items` and added to `guest_folios.balance`.
4. The rooms screen lists those lines and can check out (`PATCH /api/pos/folios/{id}` with status `Closed`).
5. Taking money is a separate, ordinary POS payment of that balance. This profile does not start that payment by itself.

## RKSV decision

**A folio charge is a non-fiscal deferred posting. It does not create a `payment_details` row, a receipt, or a TSE signature.**

Why:

- The guest has not paid. The line is an open account item.
- `PaymentService.CreatePaymentAsync` allocates a receipt number and enters the signature path. Calling it for a room charge would add a fiscal document before payment.
- A zero-amount receipt would still be a signature-chain entry. This change does not add one.
- `payment_detail_id` on `guest_folio_items` stays null so a later, explicit payment can be linked without inventing a receipt at charge time.
- Check-out only closes the folio and marks the room Cleaning. It does not sign anything.

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
- `GET /api/pos/folios/{id}/items`

Admin (`product.view`; writes need `product.manage`):

- `GET/POST /api/admin/rooms`
- `PATCH /api/admin/rooms/{id}`
- `GET /api/admin/folios`

Another tenant's room or folio returns HTTP 404.

## Screens

- POS: `frontend/app/(screens)/rooms.tsx` (tab when the profile is `beherbergung` or the layout is `rooms`). Default layout `rooms` opens this screen after login.
- POS cash register: room check-in and **Auf Zimmer buchen**.
- FA: `/admin/rooms` (room form, folio balances, occupancy, average stay of closed folios).
- Tenant detail: **Zimmer** tab when that tenant's profile has `roomTracking=true`.
