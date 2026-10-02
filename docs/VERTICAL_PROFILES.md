# Vertical Profiles

Vertical profiles describe POS capabilities, field requirements, and the preferred POS layout for a tenant. They are product configuration, not fiscal regimes or feature flags. No fiscal or compliance behavior is inferred from a vertical profile.

## Data model

`vertical_profiles` is a global catalog:

- `id`: stable slug, up to 64 characters.
- `name`: localization key; the database does not store a rendered label.
- `pos_features`: JSON object whose values are booleans. Initial keys include `tables`, `kitchenDisplay`, `patientRecord`, `serviceDuration`, `appointment`, `imeiTracking`, `routeTracking`, `roomTracking`, and `ticketScan`. Additional boolean keys may be introduced.
- `required_fields`: JSON object with `customer` and `product` string arrays.
- `optional_fields`: JSON object with `customer` and `product` string arrays.
- `pos_layout`: `standard`, `tables`, `appointment`, `queue`, `taxi`, or `ticket`.
- `is_active`, `created_at_utc`, `updated_at_utc`.

`company_settings.vertical_profile_id` is a nullable foreign key to the catalog. The migration assigns `gastronomy` to existing Austrian (`country = 'AT'`) settings rows. A null assignment resolves to `gastronomy` so older or non-classified tenants remain readable.

`tenant_vertical_overrides` stores one tenant-scoped JSON overlay per tenant:

- `id`: UUID.
- `tenant_id`: required tenant foreign key and unique.
- `overrides_json`: JSON object.
- `created_at_utc`, `updated_at_utc`.

The override root accepts `posFeatures`, `requiredFields`, `optionalFields`, and `posLayout`. Objects merge recursively. Arrays and scalar values replace the corresponding base value. For example, overriding only `requiredFields.customer` preserves the profile's `requiredFields.product` list.

## Seed profiles

The initial catalog contains:

- `gastronomy` — standard layout with kitchen display support.
- `gastronomy-tables` — tables layout with table and kitchen display support.
- `vet` — patient-record layout capabilities and pet customer fields.
- `hair-salon` — appointment layout with appointment and service-duration fields.
- `mobile-services` — appointment layout with service duration and route tracking.
- `handy-shop` — standard layout with IMEI tracking and optional serial number, brand, and model fields.
- `taxi` — taxi layout with route tracking and an optional per-kilometre tariff.
- `ticket-sales` — ticket layout with `ticketScan`, optional room and seat fields, and no kitchen display.
- `beherbergung` — `rooms` layout with lodging `roomTracking` and `kitchenDisplay` (hotel rooms and guest folios, not ticket seats). See [`docs/BEHERBERGUNG.md`](BEHERBERGUNG.md).

Profile names use localization keys such as `verticalProfiles.gastronomy.name`.

## API

System-critical Admin endpoints:

- `GET /api/admin/vertical-profiles` — active profiles.
- `GET /api/admin/vertical-profiles/{id}` — one active profile.
- `GET /api/admin/tenants/{id}/vertical-profile` — effective merged profile for a tenant.
- `PUT /api/admin/tenants/{id}/vertical-profile` — assign a profile and replace its tenant override.

PUT request:

```json
{
  "profileId": "gastronomy-tables",
  "overrides": {
    "posFeatures": {
      "kitchenDisplay": false
    },
    "requiredFields": {
      "customer": ["name"]
    }
  }
}
```

Authenticated POS endpoints:

- `GET /api/pos/vertical-profile` — effective merged profile for the ambient JWT tenant.
- `GET /api/pos/staff` — active Cashier, Waiter, and Manager users for the ambient tenant (`cart.view`). Used by the POS staff picker; not an Admin boundary.

Catalog-editor Admin endpoints (`product.view`):

- `GET /api/admin/vertical-profile` — effective merged profile for the ambient JWT tenant.
- `GET /api/admin/staff` — active Cashier, Waiter, and Manager users for the ambient tenant.

The assignment endpoint validates the active profile, tenant, company-settings row, override shape, boolean feature values, field-list arrays, and layout value. Changes emit `AuditEventType.TenantVerticalProfileChanged`.

## Appointment server API

Appointments are tenant-scoped POS bookings. They are not fiscal receipts and do not touch RKSV/TSE.

`appointments` columns: `id`, `tenant_id`, `customer_id`, `service_product_id`, `staff_id`, `start_utc`, `end_utc`, `status` (`Booked`, `Confirmed`, `Completed`, `Cancelled`, `NoShow`), `notes`, `version` (concurrency token), `created_at_utc`, `updated_at_utc`, `created_by_user_id`.

A unique index `ux_appointments_tenant_staff_start` on `(tenant_id, staff_id, start_utc)` applies to occupying rows (`Booked`, `Confirmed`) with a non-null staff id. Soft-cancel sets `status=Cancelled` so the slot can be reused. Cross-tenant reads and writes return HTTP **404**.

Authenticated POS endpoints (`cart.view`):

- `POST /api/pos/appointments` — create. Body includes `startUtc`, optional `endUtc`, `staffId`, `serviceProductId`, `customerId` / `customerName`, `notes`, and `expectedVersion` (`0` means the slot is empty). Occupied slot or version mismatch → HTTP **409** `{ code: "APPOINTMENT_CONFLICT", appointment }`.
- `GET /api/pos/appointments?from=&to=&staffId=` — list for the ambient tenant.
- `PATCH /api/pos/appointments/{id}` — update; `expectedVersion` must match the current row.
- `DELETE /api/pos/appointments/{id}` — soft-cancel.

Audit events: `AppointmentCreated` (120), `AppointmentUpdated` (121), `AppointmentCancelled` (122).

The hair-salon screen `frontend/app/(screens)/appointment.tsx` posts to this API. A 409 response shows `appointments.conflict.title` with a reload action. Device `secureStorage` is no longer the booking source of truth.

## POS frontend consumption

`frontend/contexts/VerticalProfileContext.tsx` loads the effective endpoint only after
authentication. It exposes:

- `useVerticalFeatures()` for `posFeatures`, `requiredFields`, `optionalFields`, and
  `posLayout`.
- `IfVerticalFeature` for declarative capability gates.
- `DynamicField` for profile-declared entity fields.

The cache key includes the authenticated tenant id (or tenant slug fallback).
`secureStorage` uses Expo SecureStore on iOS/Android and localStorage on web. A cached
profile is rendered while the authenticated GET refreshes it; missing cache/network
falls back to the standard POS surface with vertical-only features disabled.

`tables` enables table selection/order navigation, `kitchenDisplay` exposes the kitchen
queue, `patientRecord` exposes the patient screen, and `appointment` exposes the
calendar screen. `tables` and `appointment` layouts select their corresponding default
view after login.

### Kitchen queue

- Catalog ids `gastronomy` and `gastronomy-tables` seed `kitchenDisplay=true`.
- `POST /api/pos/kitchen-orders` (`cart.view`) creates a tenant-scoped kitchen order from the current cart items. Payment is independent and is not blocked.
- `GET /api/pos/kitchen-orders` (`kitchen.view`) lists orders. Status patches use `kitchen.update`. Soft cancel (`DELETE`) is allowed until `Served`.
- SignalR hub `/hubs/kitchen` broadcasts `KitchenOrderCreated`, `KitchenOrderStatusChanged`, and `KitchenOrderItemStatusChanged` to the tenant group only.
- Audit: `KitchenOrderCreated` (126), `KitchenOrderStatusChanged` (127), `KitchenOrderCancelled` (128).
- POS cash-register shows **An die Küche senden** when `kitchenDisplay` is on. After a successful create, the local cart is cleared. The kitchen tab reads the server queue.

### Kitchen Display Screen (KDS)

- Route: `frontend/app/(screens)/kitchen-display.tsx`. The dedicated tab
  `frontend/app/(tabs)/kitchen-display.tsx` re-exports that screen when
  `posFeatures.kitchenDisplay === true`. User menu also has a **Küche** entry.
- Three columns: Pending | In Preparation | Ready. Cards show table number or
  take-away, age as `mm:ss` (red after 15 minutes), per-item status toggles, and
  highlighted notes. Tapping a card opens a detail modal. Each open column has
  **Mark all ready**.
- POS SignalR client `frontend/services/kitchenHubClient.ts` connects to
  `/hubs/kitchen` on mount, reconnects on network failure, and applies
  `KitchenOrderCreated` / status events to the list without a full refresh.
  A green / yellow / red badge shows connection state.
- A new Pending order plays a short sound (when the device allows it) and
  increments the KDS tab badge. Kitchen status is not a fiscal or payment gate.

### Kitchen FA settings

- `company_settings.kitchen_order_auto_clear_minutes` (int, default 30) and
  `company_settings.kitchen_order_sound` (bool, default true) are tenant
  configuration only. POS does not read them yet.
- Mandanten-Admin page: `/admin/kitchen` (`settings.view` read,
  `settings.manage` write). Super Admin tenant detail shows a **Küche** tab
  when the effective profile has `kitchenDisplay=true`
  (`/admin/tenants/{id}?tab=kitchen`).
- The page has a settings editor, a read-only live order list (5s polling), and
  audit-derived analytics (average prep time, orders per hour). APIs:
  `GET/PATCH /api/admin/kitchen/*` (ambient tenant) and
  `GET/PATCH /api/admin/tenants/{id}/kitchen/*` (Super Admin).

## Implemented vertical workflows

### Vet

- Route: `frontend/app/(screens)/patient-record.tsx`.
- `customers.pet_data` stores optional `petName`, `petSpecies`, `petBreed`, and
  `petBirthDate` as JSONB.
- POS customer creation uses canonical `POST /api/pos/customers`.
- Payment UI shows an optional `Rezept` switch only while `patientRecord` is enabled.
  When the switch is on, POS sends `prescriptionReference` on `POST /api/pos/payment`.
  The server stores it on `payment_details.prescription_reference` (varchar 255, nullable)
  only when the tenant's effective profile has `patientRecord=true`; otherwise the request
  is rejected with HTTP **400** `PRESCRIPTION_NOT_ALLOWED`. The value is not part of the
  RKSV/TSE payload or DEP export. Successful storage emits `AuditEventType.PaymentWithPrescription` (123)
  without logging the reference text.

### Hair salon

- Route: `frontend/app/(screens)/appointment.tsx`.
- Products may define `durationMinutes` and a default `staffId`
  (`products.duration_minutes`, `products.staff_id`).
- Admin POST/PUT `/api/admin/products` accept those fields; GET `/api/admin/products/{id}`
  returns them. The FA product form shows Duration (minutes) and Staff when the ambient
  profile has `serviceDuration=true` or `appointment=true`. Staff options come from
  `GET /api/admin/staff` (active Cashier, Waiter, and Manager memberships for the tenant).
  Ambient profile for that form: `GET /api/admin/vertical-profile` (`product.view`).
- POS staff picker (`frontend/components/StaffPicker.tsx`) reads `GET /api/pos/staff`
  (`cart.view`; same Cashier / Waiter / Manager set). The client caches the list for
  5 minutes. Each row shows initials and a localized role; there is no photo upload.
- The default POS surface is a daily calendar (`frontend/components/AppointmentDayCalendar.tsx`)
  with one column per staff member and 30-minute slots from 08:00–18:00 local time.
  Occupying statuses are `Booked` and `Confirmed`. Tapping an empty slot opens the
  form with that staff member and time prefilled. Drag-and-drop is out of scope.
- The appointment form captures customer, date, time, service, and staff.
- Bookings persist through `POST /api/pos/appointments`. A 409 `APPOINTMENT_CONFLICT`
  shows a reload banner (`appointments.conflict.*`). A successful create shows an
  in-app toast on the POS device. Native iOS/Android also schedules a **device-local**
  reminder via `expo-notifications` 15 minutes before `startUtc` (no server push;
  web skips scheduling). The reminder is cancelled only by replacing the same local
  identifier on a later schedule, not by a backend job.

### Mobile services

- Catalog id `mobile-services` (already seeded). Layout is `appointment` with
  `serviceDuration` and `routeTracking`.
- `customers.address_data` stores optional `street`, `postalCode`, `city`, and
  `notes` as JSONB. The existing flat `customers.address` column stays and is
  filled from those fields when empty.
- `orders.location_data` stores the same shape for the job location of that
  order. It is not part of RKSV/TSE content.
- POS create: `POST /api/pos/customers` accepts `addressData`. Existing
  customers can update it with `PATCH /api/pos/customers/{id}`.
- Order create (`POST /api/orders`) accepts `locationData`.
- POS shows `MobileServiceRoutePanel` on cash-register, orders, and the
  appointment form only when `profileId === "mobile-services"`. The panel
  edits customer address and the current job location.
- FA customer detail / list shows the structured address (`addressData`) next
  to the flat address field.

### Taxi

- Catalog id `taxi` (already seeded). Layout is `taxi` with `routeTracking`.
- `company_settings.taxi_tariff_per_km` (decimal 8,2, nullable) is the per-kilometre
  fare suggestion. Super Admin edits it on the tenant vertical-profile override panel.
  POS reads it from `GET /api/pos/vertical-profile` (`taxiTariffPerKm`).
- POS cash-register (`posLayout === "taxi"`) replaces the product-grid cart with
  `TaxiSalePanel`: **Fahrt starten** starts a local timer, **Fahrt beenden** opens
  route/km/manual-fare fields, then a compact service picker. Drag-and-drop is not used.
- Suggested fare is `km × tariff` or a manual amount. The **fiscal** sale amount stays
  the selected cart/service line; TSE signing is unchanged.
- `POST /api/pos/payment` accepts `routeFrom`, `routeTo`, `routeKm`, and
  `tripStartedAtUtc` only when the effective profile id is `taxi`. Other tenants
  receive HTTP **400** `TAXI_FIELDS_NOT_ALLOWED`. Values persist on
  `payment_details.route_from`, `route_to`, `route_km`, and `taxi_trip_started_at_utc`
  and are not part of the RKSV/TSE payload.

### Handy shop

- Catalog id `handy-shop` (already seeded). Layout is `standard` with `imeiTracking`.
- `products.imei_tracked` (boolean, default false) marks catalog rows that sell
  unique devices. The FA product form shows the toggle when `imeiTracking` is on.
- `product_imeis` stores per-device stock: `imei` (varchar 20), `status`
  (`InStock` / `Sold` / `Returned`), optional `sold_payment_id`, `warranty_months`,
  `created_at_utc`, `sold_at_utc`. Unique index `(tenant_id, imei)`.
- Authenticated POS endpoints (`product.view`):
  - `POST /api/pos/products/{id}/imeis` — add an IMEI to stock.
  - `GET /api/pos/products/{id}/imeis?status=InStock` — list.
- Duplicate IMEI for the same tenant returns HTTP **409** `IMEI_DUPLICATE`.
- FA product detail lists IMEIs via `GET /api/admin/products/{id}/imeis`.
- POS cash-register: adding an IMEI-tracked product opens `ImeiPickerModal`
  (list of `InStock` IMEIs or scan via `expo-camera`). The selected IMEI is sent
  on `POST /api/pos/payment` as `items[].imei`. Payment marks the row `Sold` and
  sets `sold_payment_id`. Missing IMEI for a tracked product returns HTTP **400**
  `IMEI_REQUIRED`. IMEI values are not written into RKSV receipt content.

### Ticket sales

- Catalog id `ticket-sales` (already seeded). Layout is `ticket` with `ticketScan` and `kitchenDisplay=false`.
- `products.is_ticket` (boolean, default false) marks catalog rows that issue a redeemable ticket on sale.
- The FA product form shows the Ticket toggle only when the ambient profile id is `ticket-sales`.
- `ticket_redemptions` stores issued tickets: `ticket_code` is a non-secret hash prefix for display, `ticket_code_hash` is SHA-256 of the normalized plaintext. The plaintext code is returned once on the payment response for print and is not persisted.
- Unique index `ux_ticket_redemptions_tenant_code_hash` on `(tenant_id, ticket_code_hash)`.
- On a ticket-sales sale, each `is_ticket` line issues one code per quantity. `ValidUntilUtc` comes from tenant setting `ticketDefaultValidityDays` (default 365).
- Authenticated POS endpoints (`cart.view`):
  - `POST /api/pos/tickets/{code}/validate` — validity only, does not redeem.
  - `POST /api/pos/tickets/{code}/redeem` — marks `Redeemed`. Already redeemed → HTTP **409** `TICKET_ALREADY_REDEEMED`. Expired → HTTP **400** `TICKET_EXPIRED`. Missing/cross-tenant → HTTP **404**.
- Admin list: `GET /api/admin/tickets/redemptions` (`product.view`). Super Admin may pass `tenantId`; others use the ambient tenant. FA page: `/admin/tickets/redemptions`.
- POS cash-register (`posLayout === "ticket"`) keeps the sale flow. Ticket products show a **Ticket erzeugen** toggle. After payment, POS prints a ticket HTML/PDF whose QR payload is the ticket code, not the RKSV machine code.
- POS screen `frontend/app/(tabs)/ticket-validate.tsx` (user menu when `ticketScan` is on): scan via `expo-camera` or enter the code, then Redeem.
- Ticket codes are not written into RKSV receipt content, TSE payloads, or DEP export.

### Beherbergung

- Catalog id `beherbergung` (seeded). Layout is `rooms`. Features: `roomTracking=true`, `kitchenDisplay=true`, `tables=false`, `appointment=false`, `patientRecord=false`.
- The code seed in `VerticalProfileSeedData` is the catalog default. Migration `AddRoomsAndGuestFolios` updates the existing `vertical_profiles` row. Later edits still go through the config hub override table; they do not rewrite the original seed migration.
- UI gates on `profileId === "beherbergung"` (and POS layout `rooms`). Ticket-sales also uses `roomTracking` for seat/room labels; that is a different product field and does not use these tables.
- `rooms` stores tenant rooms: `number` (varchar 32), `type` (varchar 64), `capacity` (1–20), `status` (`Available`, `Occupied`, `Cleaning`, `Maintenance`), `is_active`. Unique index `(tenant_id, number)`.
- `guest_folios` stores stays: `customer_id`, `room_id`, `check_in`, optional `check_out`, `status` (`Open`, `Closed`, `Cancelled`), `balance` (`decimal(18,2)`), optional `notes`. One open folio per room.
- `guest_folio_items` stores deferred charges: `folio_id`, optional `payment_detail_id`, `description` (varchar 255), `amount` (`decimal(10,2)`). A charge leaves `payment_detail_id` null.
- Authenticated POS endpoints (`cart.view`):
  - `GET /api/pos/rooms` — rooms with status.
  - `POST /api/pos/rooms` — create a room. Manager or Super Admin only; other roles → HTTP **403**.
  - `PATCH /api/pos/rooms/{id}` — set status. Missing or cross-tenant → HTTP **404**.
  - `GET /api/pos/folios?roomId=&openOnly=true` — guest folios.
  - `POST /api/pos/folios` — open a folio. Duplicate occupancy → HTTP **409** `ROOM_OCCUPIED`. Check-out ≤ check-in → HTTP **400** `INVALID_STAY`. Missing/cross-tenant room or customer → HTTP **404**.
  - `PATCH /api/pos/folios/{id}` — note, check-out, or status.
  - `POST /api/pos/folios/{id}/charge` — add a non-fiscal charge. Closed folio → HTTP **409** `FOLIO_CLOSED`.
  - `GET /api/pos/folios/{id}/items` — charge lines.
- Admin endpoints (`product.view`; create and status change need `product.manage`):
  - `GET/POST /api/admin/rooms`, `PATCH /api/admin/rooms/{id}`.
  - `GET /api/admin/folios` — folio view with balance.
- POS default layout `rooms` opens `frontend/app/(screens)/rooms.tsx`. Cash register shows `RoomPicker` and **Auf Zimmer buchen** only when `profileId === "beherbergung"`.
- FA page: `/admin/rooms`. Tenant detail shows a **Zimmer** tab when the tenant profile has `roomTracking=true`.
- Folio charges are not RKSV receipts. Decision record: [`docs/BEHERBERGUNG.md`](BEHERBERGUNG.md).

## Super Admin config hub

Super Admin manages the catalog at `/admin/vertical-profiles` (sidebar **POS-Profile**, `system.critical`). The page does not change POS screen code. POS and FA keep reading the effective profile; that read now applies database overrides.

Code seeds in `VerticalProfileSeedData` stay the default. Edits and custom profiles are stored in `vertical_profile_overrides`:

| Column | Role |
|--------|------|
| `profile_id` | Primary key. Same slug as the profile id. Not editable after create. |
| `name` | Display name or localization key. |
| `pos_features_json` | Full boolean feature map. Replaces the seed map. |
| `required_fields_json` | Field lists (`customer`, `product`, `order`). |
| `optional_fields_json` | Field lists (`customer`, `product`, `order`). |
| `pos_layout` | `standard`, `tables`, `appointment`, `queue`, `taxi`, `ticket`, or `rooms`. |
| `is_deleted` | Soft delete. |
| `created_at_utc`, `updated_at_utc` | Timestamps. |

The registry merges **code seed → database override**. When an override row exists and is not deleted, its columns win. A slug that is not a seed is `custom`. A seed without an override row is `source=seed`. A seed with an override row is `source=override`. Custom profiles also insert an active `vertical_profiles` row so `company_settings.vertical_profile_id` can reference them. Seed rows in `vertical_profiles` are left unchanged.

`posLayout=rooms` is a catalog value and a POS layout. For `beherbergung`, the client opens the rooms screen instead of treating the layout as `standard`.

Hub APIs (`system.critical`):

- `POST /api/admin/vertical-profiles` — create blank or from `cloneFrom`. Audit `VerticalProfileCreated` (131).
- `PATCH /api/admin/vertical-profiles/{id}` — edit name, features, fields, and layout. Does not change `id`. Audit `VerticalProfileUpdated` (132).
- `PUT /api/admin/vertical-profiles/{id}/features` — replace `posFeatures`.
- `POST /api/admin/vertical-profiles/{id}/clone` — new slug copied from the source profile.
- `DELETE /api/admin/vertical-profiles/{id}` — soft delete. Seed profiles return `PROFILE_IS_SEED`. A profile assigned to any tenant returns **409** `PROFILE_IN_USE` with the tenant list. Audit `VerticalProfileDeleted` (133).
- `GET /api/admin/vertical-profiles/{id}/tenants` — tenants on the profile and their override-key counts. A null `vertical_profile_id` counts as `gastronomy`.
- `GET /api/admin/tenants/by-profile` — tenants grouped by profile.

Feature edits remove `vertical_profile_catalog` and `vertical_profile_effective_{tenantId}` for every tenant on that profile. If a save turns a feature off and tenants are assigned, the response lists those tenants in `affectedTenants`. The hub shows that list before and after save. The save itself is not blocked.

The editor preview is generated from the profile JSON (enabled capabilities, POS surfaces, FA areas). It does not embed the POS app.

## Current scope and follow-ups

The POS integration is web-first and does not require a native binary.
Cross-device kitchen status still needs dedicated lifecycle APIs.
Vertical profiles remain separate from `FeatureFlagNames`.
