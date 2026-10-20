# Tenant extensions

Tenant extensions are additive hooks. A mandant enables them by name. Core payment, receipt, customer, and folio code does not branch on a tenant id. An extension observes a hook and may reject the operation or, on `payment.before-sign`, return an adjusted cart. It does not call the TSE signer itself.

Vertical profiles remain a separate product configuration. See [`VERTICAL_PROFILES.md`](VERTICAL_PROFILES.md).

## Architecture

```text
tenant_settings key Extensions.Enabled
        JSON array of names, e.g. ["hair_salon_campaign"]
                    |
                    v
        ITenantExtensionRegistry.GetForHook(tenantId, hookName)
                    |
                    +-- name is in that tenant's enabled list
                    +-- [TenantExtensionHook] matches the hook
                    +-- extension.AppliesTo(tenantId)
                    |
                    v
        extension.ExecuteAsync(context)
                    |
                    +-- Success == false -> ExtensionFailedException
                        HTTP 400, code EXTENSION_FAILED
```

`ITenantFeatureExtension` exposes `FeatureName`, `AppliesTo`, and `ExecuteAsync`. Hook membership is declared with `[TenantExtensionHook("payment.before-sign")]` on the class so the registry can filter without a tenant check in core services.

On a sale, `payment.before-sign` may return `ExtensionResult.ModifiedPayload` with adjusted line gross totals. The sale path re-taxes those totals with the existing country tax strategy and writes them onto the payment. The unchanged `SignAsync` call then signs that amount, so the signature covers the adjusted gross. Storno, refund, and `GenerateTseSignatureAsync` pass no cart lines. An extension that only understands lines leaves those amounts unchanged. When the tenant has not enabled the extension, the hook is not invoked and the catalog total is signed.

Enabled names are cached under `CacheKeys.TenantSettings` (`tenant_settings_{tenantId}`) for `CacheSettings:TenantSettingsCacheMinutes` (default 60). A toggle removes that key.

## Available hook points

| Hook | When it runs |
|------|----------------|
| `payment.before-sign` | Immediately before Austrian TSE signing on sale, storno, refund, and `GenerateTseSignatureAsync`. On a sale the payload includes cart lines (`lines`: product id, quantity, tax type, gross total, `requiresPrescription`), plus `hasPrescription` and `prescriptionReference`. A returned `ModifiedPayload` replaces those line gross totals before the existing sign call. Other payment hooks do not include lines. |
| `payment.after-persist` | After the sale payment row is written in the open transaction and before commit. A failure rolls the transaction back. |
| `receipt.before-print` | Before plain-text sale receipt rendering in `ReceiptService`. |
| `customer.before-save` | Before POS and Admin customer insert or update. |
| `folio.before-close` | Before an open guest folio is saved as closed or cancelled. `Success: false` blocks the close (HTTP 400 `EXTENSION_FAILED`). `Success: true` may add non-fiscal `guest_folio_items` before that save. |
| `folio.opened` | After a new guest folio row is saved inside the open transaction and before that transaction commits. `Success: false` rolls the folio back (HTTP 400 `EXTENSION_FAILED`). `Success: true` may add non-fiscal `guest_folio_items`. The extension updates `guest_folios.balance`; `LodgingService` saves that change before commit. |

When no extension is enabled for the tenant, each hook returns immediately and the existing flow continues.

## How to write an extension

1. Implement `ITenantFeatureExtension`.
2. Set `FeatureName` to a stable lowercase name (`^[a-z0-9_]{1,64}$`), for example `hair_salon_campaign`.
3. Mark the class with one `[TenantExtensionHook]` per hook it handles.
4. Return `AppliesTo` from tenant-scoped rules inside the extension. Do not add `if (tenantId == ...)` in payment, TSE, or receipt services.
5. Return `new ExtensionResult(true)` when the hook should continue. Return `Success: false` to stop the operation with HTTP 400 `EXTENSION_FAILED`.
6. Register it in `ApplicationHost.cs`:

```csharp
builder.Services.AddScoped<ITenantFeatureExtension, HairSalonCampaignExtension>();
```

`ITenantExtensionRegistry` resolves every `ITenantFeatureExtension` from DI and keeps only the ones enabled for that tenant and hook.

## Example: hair salon campaigns

`HairSalonCampaignExtension` (`hair_salon_campaign`) handles `payment.before-sign` only. It runs only when `Extensions.Enabled` contains `hair_salon_campaign` for that tenant. It loads rows from `hair_salon_campaigns` where `is_active` is true and `valid_from_utc <= now < valid_until_utc`. An empty `applies_to_product_ids` list matches every product. Otherwise only listed product ids match. When several campaigns match one line, the highest `discount_percent` is used. The discount is applied to that line's gross total. Lines that do not change produce no `ModifiedPayload`.

A sale that is discounted this way stores and signs the discounted gross. The Austrian sign function is not modified. The extension writes audit event `HairSalonCampaignApplied` (138) with the campaign ids when at least one line gross changes. The audit row does not store signature material.

| Method | Path | Permission |
|--------|------|------------|
| `GET` | `/api/admin/extensions/hair-salon-campaigns` | `settings.view` |
| `POST` | `/api/admin/extensions/hair-salon-campaigns` | `settings.manage` |
| `PUT` | `/api/admin/extensions/hair-salon-campaigns/{id}` | `settings.manage` |
| `DELETE` | `/api/admin/extensions/hair-salon-campaigns/{id}` | `settings.manage` |

These routes require an ambient tenant and the extension to be enabled for that tenant. A missing ambient tenant returns HTTP 404. A disabled extension returns HTTP 403 `EXTENSION_NOT_ENABLED`. A campaign id that belongs to another tenant returns HTTP 404. `DELETE` sets `is_active` to false.

POST body:

```json
{
  "name": "Weekday color",
  "discountPercent": 10,
  "appliesToProductIds": [],
  "validFromUtc": "2026-10-01T00:00:00Z",
  "validUntilUtc": "2026-10-31T00:00:00Z"
}
```

`discountPercent` is 0–100. `validFromUtc` must be earlier than `validUntilUtc`. Voucher settlement is checked against the catalog total before this hook, so a campaign sale should be paid in cash or card rather than a voucher sized to the pre-discount total.

### Vet prescription

`VetPrescriptionExtension` (`vet_prescription`) handles the existing `payment.before-sign` hook. It is not enabled for any tenant by default. Opt in with `PUT /api/admin/tenant/extensions` and `{ "name": "vet_prescription", "enabled": true }`. `GET /api/admin/tenant/extensions` lists it with that hook once the type is registered.

`AppliesTo` is true only when the tenant's effective vertical profile has both `patientRecord` and `prescription`. The extension does not read `company_settings` itself. On a sale it reads each line's `requiresPrescription` and the payload `prescriptionReference`. When a line requires a prescription and the reference is missing, it returns `Success: false` with `VET_PRESCRIPTION_REQUIRED`. The runner maps that to HTTP 400 `EXTENSION_FAILED`. It does not change the cart and does not call the TSE signer. An empty cart (storno or refund) and lines that do not require a prescription continue with `Success: true`. A passing prescription sale is already audited as `PaymentWithPrescription` (123); this extension does not write a second audit row.

### Example: minibar auto-charge

`MinibarAutoChargeExtension` (`minibar_auto_charge`) handles `folio.before-close` only. It is not enabled for any tenant by default. Opt in with `PUT /api/admin/tenant/extensions` and `{ "name": "minibar_auto_charge", "enabled": true }`. `AppliesTo` is true only when the effective vertical profile id is `beherbergung`.

Pending rows live in `room_minibar_items`. POS adds them with `POST /api/pos/rooms/{roomId}/minibar` (`cart.view`). A missing or foreign room returns HTTP 404. On close, if `tenant_settings` key `Extensions.MinibarAutoCharge` is absent or `{ "requireSettlementBeforeClose": true }`, pending rows reject the close with `MINIBAR_SETTLEMENT_REQUIRED` (HTTP 400 `EXTENSION_FAILED`) and nothing is posted. When the flag is false, each pending row becomes a `guest_folio_items` line (`payment_detail_id` null), the folio balance increases, the minibar row becomes `Charged`, and `FolioCharged` (136) is written once per line. A second close does not charge those rows again. The charges are not fiscal and do not call TSE. `GET /api/admin/tenant/extensions` includes this extension's `options.requireSettlementBeforeClose` (`true` when the key is missing). The same PUT accepts an optional `options.requireSettlementBeforeClose` boolean for this extension only. Sending `options` for an extension that does not declare a schema, including an unknown name, returns HTTP 400 `EXTENSION_OPTIONS_UNSUPPORTED`. A changed flag is stored on the existing `TenantExtensionToggled` (137) row's `newValues`; it does not add an audit type.

### Example: breakfast included

`BreakfastIncludedExtension` (`breakfast_included`) handles `folio.opened` only. It is not enabled for any tenant by default. Opt in with `PUT /api/admin/tenant/extensions` and `{ "name": "breakfast_included", "enabled": true }`. `AppliesTo` is true only when the effective vertical profile id is `beherbergung`.

The price lives in `tenant_settings` key `Extensions.BreakfastIncluded`: `{ "pricePerPersonPerNight": 12.50 }`. When that key is absent, or the price is not greater than zero, the extension returns `Success: true` and writes no rows. When the new folio's `notes` contain `breakfast` (any case) and the price is greater than zero, it inserts one `guest_folio_items` row. Nights are `(check_out - check_in).Days`, at least 1. There is no guest-count column, so persons is 1. The amount is nights × persons × price. The description is `Frühstück (N Nächte)`. `payment_detail_id` stays null. The extension adds that amount to `guest_folios.balance`. The charge is not fiscal and does not call TSE. The extension writes no audit row. `GET /api/admin/tenant/extensions` includes `options.pricePerPersonPerNight` (`0` when the key is missing). The same PUT accepts an optional `options.pricePerPersonPerNight` number for this extension only.

### Example: late checkout fee

`LateCheckoutFeeExtension` (`late_checkout_fee`) handles `folio.before-close` only. It is not enabled for any tenant by default. Opt in with `PUT /api/admin/tenant/extensions` and `{ "name": "late_checkout_fee", "enabled": true }`. `AppliesTo` is true only when the effective vertical profile id is `beherbergung`. It does not reject the close.

The rule lives in `tenant_settings` key `Extensions.LateCheckoutFee`: `{ "freeUntilLocalTime": "11:00", "fee": 30.00, "timeZone": "Europe/Vienna" }`. When that key is absent, or `fee` is not greater than zero, the extension returns `Success: true` and writes no rows. On close it reads the current time in `timeZone`. When that local time is later than `freeUntilLocalTime`, it inserts one `guest_folio_items` row with description `Später Check-out`, amount `fee`, and `payment_detail_id` null, and adds the fee to `guest_folios.balance`. A folio that already has that description is not charged again. The charge is not fiscal and does not call TSE. The extension writes no audit row. `GET /api/admin/tenant/extensions` includes the three options (`11:00`, `0`, `Europe/Vienna` when the key is missing). The same PUT accepts those fields for this extension only. `timeZone` must be one of the common IANA zones the admin page lists.

## How FA manages them

Ambient tenant comes from the JWT. A missing ambient tenant returns HTTP 404.

| Method | Path | Permission |
|--------|------|------------|
| `GET` | `/api/admin/tenant/extensions` | `settings.view` |
| `PUT` | `/api/admin/tenant/extensions` | `settings.manage` |

PUT body:

```json
{ "name": "hair_salon_campaign", "enabled": true }
```

`minibar_auto_charge` may also send `options`:

```json
{ "name": "minibar_auto_charge", "enabled": true, "options": { "requireSettlementBeforeClose": false } }
```

`breakfast_included` may send:

```json
{ "name": "breakfast_included", "enabled": true, "options": { "pricePerPersonPerNight": 12.50 } }
```

`late_checkout_fee` may send:

```json
{ "name": "late_checkout_fee", "enabled": true, "options": { "freeUntilLocalTime": "11:00", "fee": 30.00, "timeZone": "Europe/Vienna" } }
```

Omitting `options` leaves that extension's stored options unchanged. GET returns each registered extension with `name`, `enabled`, and `hooks`. `minibar_auto_charge`, `breakfast_included`, and `late_checkout_fee` also return `options`. An unknown name returns HTTP 400. A successful change writes `tenant_settings` key `Extensions.Enabled` and audit event `TenantExtensionToggled` (137). The audit row stores the extension name and the enabled flag. When minibar options change in the same call, those values are on that same row's `newValues`. It does not store payment or signature material.
