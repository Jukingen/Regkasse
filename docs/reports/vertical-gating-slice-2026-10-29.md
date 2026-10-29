# Marketplace vertical gating

**Date:** 2026-10-29
**Status:** Implemented. No pull request. No push.

This slice only hides marketplace intake from non-gastronomy tenants. The provider table and the two admin screens (Super Admin credentials, Tenant Admin connect) are not in this change.

## Files added

- `backend/Controllers/MarketplaceProfileGate.cs`

## Files modified

- `backend/Services/VerticalProfiles/VerticalProfileGuard.cs`
- `backend/Controllers/AdminOnlineOrderIntegrationLocationsController.cs`
- `backend/Controllers/AdminOnlineOrderIntegrationProductMappingsController.cs`
- `backend/Controllers/AdminWoltIntegrationController.cs`
- `backend/Services/Order/OnlineOrderIntegrationWebhookService.cs`
- `backend/Services/Order/WoltOrderNotificationService.cs`
- `backend/KasseAPI_Final.Tests/OnlineOrderIntegrationTests.cs`
- `frontend-admin/src/shared/adminSidebarRegistry.ts`
- `frontend-admin/src/shared/sidebarVerticalProfile.ts`
- `frontend-admin/src/shared/__tests__/sidebarVerticalProfile.test.ts`
- `frontend-admin/tests/e2e/online-order-product-mappings.spec.ts`

The webhook controller was not changed. `OnlineOrderIntegrationWebhookController` still only reads the body and forwards it.

## 403 vs 404

Authenticated admin routes call `IVerticalProfileGuard.EnforceEndpointAsync("marketplace-orders")`. That feature is not a `posFeatures` flag. Lodging also has `kitchenDisplay`, so the guard allows only effective profile ids `gastronomy` and `gastronomy-tables`. Anything else throws `ProfileEndpointDisabledException`. The HTTP result is **403** `PROFILE_ENDPOINT_DISABLED`.

Covered routes:

- `PUT /api/admin/online-order-integrations/locations`
- `GET` and `PUT /api/admin/online-order-integrations/product-mappings`
- `POST /api/admin/online-order-integrations/wolt/connect`
- `GET /api/admin/online-order-integrations/wolt/callback`

A missing company-settings row stays allowed. The effective-profile service already treats an unclassified tenant as gastronomy.

The anonymous webhook does not use 403. After the existing signature check, a known enabled location whose tenant is not gastronomy returns **404** `LOCATION_NOT_FOUND` and writes no row. A bad signature is still 400 and is still checked first. This applies to the Deliverect accept path and the Wolt notification path.

## Menu and route guard

`onlineOrderProductMappings` in `adminSidebarRegistry.ts` has `onlyForProfiles: ['gastronomy', 'gastronomy-tables']`.

`PermissionRouteGuard` already calls `resolveVerticalProfileRouteAccess` with that registry, so a direct URL for a Mandanten-Admin on another vertical is rejected in the client. The server 403 is the control.

Super Admin's default "all profiles" simulation sets `viewAllProfiles` and skips every profile gate. This leaf sets `enforceProfileInAllView`, so the marketplace item still follows `onlyForProfiles` for the ambient tenant. Other vertical menus keep the existing all-profiles bypass.

## Tests

`dotnet test backend/KasseAPI_Final.sln --filter "Category!=PostgreSql"`

```text
Passed!  - Failed:     0, Passed:  5890, Skipped:     5, Total:  5895, Duration: 30 s - KasseAPI_Final.Tests.dll (net10.0)
```

`dotnet test backend/KasseAPI_Final.sln --filter "Category=PostgreSql"`

Docker was not running (`dockerDesktopLinuxEngine` pipe missing). The fixture skipped instead of failing:

```text
Passed!  - Failed:     0, Passed:     1, Skipped:    56, Total:    57, Duration: 199 ms - KasseAPI_Final.Tests.dll (net10.0)
```

`cd frontend-admin && npm run test:e2e -- online-order-product-mappings`

The first local run reused an older `npm start` on port 3000 and the new hair-salon case failed. After `npm run build`, the same spec on a fresh server:

```text
  ok 1 [chromium] › tests\e2e\online-order-product-mappings.spec.ts:114:7 › Marketplace product mapping › saves a mapping and shows it after reload (3.3s)
  ok 2 [chromium] › tests\e2e\online-order-product-mappings.spec.ts:146:7 › Marketplace product mapping › saves a wolt provider mapping (2.2s)
  ok 3 [chromium] › tests\e2e\online-order-product-mappings.spec.ts:171:7 › Marketplace product mapping › hides marketplace settings for a non-gastronomy tenant (2.4s)

  3 passed (19.3s)
```

Unit coverage in this slice: a gastronomy tenant gets 200 on location upsert, product mapping, and Wolt connect. A hair-salon tenant gets 403 `PROFILE_ENDPOINT_DISABLED` on location, mapping, connect, and callback. A valid Wolt signature for a known salon location returns 404, does not call Wolt, and writes no row. A bad Deliverect signature on that salon is still 400.

## Not in this change

No `online_order_integration_providers` table. No Super Admin credential screen. No Tenant Admin read-only connect screen. No order-status write-back to Wolt.

`PaymentService`, `TseService`, `RksvDepExportService`, `SignaturePipeline`, `RolePermissionMatrix`, existing migrations, and `online_order_integration_oauth_tokens` were not modified.

PostgreSQL integration: 1 passed, 56 skipped (2026-10-29, Docker unavailable).
