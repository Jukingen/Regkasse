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
| `payment.before-sign` | Immediately before Austrian TSE signing on sale, storno, refund, and `GenerateTseSignatureAsync`. On a sale the payload includes cart lines (`lines`: product id, quantity, tax type, gross total). A returned `ModifiedPayload` replaces those line gross totals before the existing sign call. Other payment hooks do not include lines. |
| `payment.after-persist` | After the sale payment row is written in the open transaction and before commit. A failure rolls the transaction back. |
| `receipt.before-print` | Before plain-text sale receipt rendering in `ReceiptService`. |
| `customer.before-save` | Before POS and Admin customer insert or update. |
| `folio.before-close` | Before an open guest folio is saved as closed or cancelled. |

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

GET returns each registered extension with `name`, `enabled`, and `hooks`. An unknown name returns HTTP 400. A successful change writes `tenant_settings` key `Extensions.Enabled` and audit event `TenantExtensionToggled` (137). The audit row stores the extension name and the enabled flag. It does not store payment or signature material.
