# Provider management slice

**Date:** 2026-10-30
**Status:** Implemented. No pull request. No push. No merge.

Deployment-wide marketplace partner credentials and the two admin surfaces on `/admin/online-order-integrations`. Wolt order-status write-back is not in this change.

## Files added

- `backend/Models/OnlineOrderIntegrationProvider.cs`
- `backend/Services/Order/OnlineOrderIntegrationProviderService.cs`
- `backend/Controllers/AdminOnlineOrderIntegrationProvidersController.cs`
- `backend/Controllers/TenantOnlineOrderIntegrationProvidersController.cs`
- `backend/Migrations/20261030112213_AddOnlineOrderIntegrationProviders.cs`
- `backend/Migrations/20261030112213_AddOnlineOrderIntegrationProviders.Designer.cs`
- `frontend-admin/src/features/online-orders/ProviderSettingsSection.tsx`
- `frontend-admin/src/features/online-orders/ProviderConnectSection.tsx`
- `frontend-admin/src/api/generated/model/getApiAdminOnlineOrderIntegrationsProvidersParams.ts`
- `frontend-admin/src/api/generated/model/getApiAdminOnlineOrderIntegrationsProvidersStatusParams.ts`
- `frontend-admin/src/api/generated/model/onlineOrderIntegrationProviderAdminDto.ts`
- `frontend-admin/src/api/generated/model/onlineOrderIntegrationProviderTenantDto.ts`
- `frontend-admin/src/api/generated/model/upsertOnlineOrderIntegrationProviderRequest.ts`

## Files modified

- `backend/Data/AppDbContext.cs`
- `backend/Data/OnlineOrderIntegrationEntityConfiguration.cs`
- `backend/DTOs/OnlineOrderIntegrationDtos.cs`
- `backend/ApplicationHost.cs`
- `backend/Services/Order/WoltOAuthClient.cs`
- `backend/Services/Order/WoltIntegrationConnectService.cs`
- `backend/Services/Order/WoltOrderNotificationService.cs`
- `backend/Controllers/AdminWoltIntegrationController.cs`
- `backend/KasseAPI_Final.Tests/OnlineOrderIntegrationTests.cs`
- `backend/Migrations/AppDbContextModelSnapshot.cs` (EF-generated with the new migration)
- `backend/swagger.json`
- `frontend-admin/src/api/generated/admin/admin.ts`
- `frontend-admin/src/api/generated/model/index.ts`
- `frontend-admin/src/features/online-orders/ProductMappingScreen.tsx`
- `frontend-admin/src/shared/adminSidebarRegistry.ts`
- `frontend-admin/src/shared/auth/routePermissions.ts`
- `frontend-admin/src/constants/permissions.ts`
- `frontend-admin/src/shared/__tests__/fixtures/adminAppPermissionFixtures.ts`
- `frontend-admin/src/i18n/locales/de/onlineOrders.json`
- `frontend-admin/src/i18n/locales/en/onlineOrders.json`
- `frontend-admin/src/i18n/locales/tr/onlineOrders.json`
- `frontend-admin/src/i18n/generated/translationKeys.ts`
- `frontend-admin/tests/e2e/online-order-product-mappings.spec.ts`

`/admin/online-order-integrations` was removed from `MANAGER_FORBIDDEN_MENU_KEYS` because the approved screen is opened by `digital.orders.manage`. It was not added to the Manager required-menu list, because the gastronomy profile gate can still hide it.

## Migration and encryption

One new table: `online_order_integration_providers`. It has no `tenant_id` and is not `ITenantEntity`. Columns: `id` (uuid PK), `provider` varchar(32), `enabled` boolean default false, `client_id` text, `client_secret` text, `created_at`, `updated_at` nullable, `is_active` boolean default true. Unique index `ux_online_order_integration_providers_provider` on `provider`.

`client_id` and `client_secret` are ciphertext from the existing `OnlineOrderIntegrationTokenProtector`, purpose `Regkasse.OnlineOrderIntegration.OAuthToken.v1`. GET responses return `configured` or `missing`. They never return plaintext. `online_order_integration_oauth_tokens` was not changed.

`OnlineOrderIntegrations:Wolt:ClientId` and `ClientSecret` do not open a closed provider. Redirect URI and authorization endpoint stay in config. Token exchange and refresh still have the options overloads so a missing provider row keeps the previous client behavior. When a row exists and is disabled, refresh returns null and does not fall back to config. When the row is enabled, refresh uses the decrypted table credentials.

## Super Admin and Tenant Admin

Super Admin (`digital.manage`; `system.critical` satisfies it):

- `GET /api/admin/online-order-integrations/providers`
- `PUT /api/admin/online-order-integrations/providers`

The screen shows the enable switch and Client-ID / Client-Secret. Client id and client secret are written together.

Tenant Admin (`digital.orders.manage`):

- `GET /api/admin/online-order-integrations/providers/status` (enabled, credential status, connected)
- `POST /api/admin/online-order-integrations/wolt/connect`

The screen shows **Eigenes Konto verbinden** and no secret fields. `connected` is true when the tenant has any active OAuth token for that provider.

`POST /api/admin/online-order-integrations/wolt/connect` and `GET /api/admin/online-order-integrations/wolt/callback` both require `digital.orders.manage`. Super Admin still calls them because `system.critical` satisfies every permission. The callback writes the OAuth token for the ambient tenant. It does not write a deployment-wide token.

## 409 PROVIDER_DISABLED

Connect returns HTTP 409 `PROVIDER_DISABLED` when the provider row is missing, disabled, or the stored secrets cannot be decrypted. No location row is created. The authorize URL, when the row is enabled, uses the table client id.

The Wolt webhook HMAC uses the enabled row's decrypted client secret. If the row is missing, disabled, or has no usable secret, the existing path stays HTTP 400. It does not write a row and it does not return 403. The signature is still checked before JSON parse and before the HTTP call to Wolt. A bad signature is still 400.

## Vertical gating

The new admin endpoints call `MarketplaceProfileGate.RejectAsync`, which uses `EnforceEndpointAsync("marketplace-orders")`. A profile other than `gastronomy` or `gastronomy-tables` gets HTTP 403 `PROFILE_ENDPOINT_DISABLED`. A missing company-settings row stays allowed. The menu item keeps `onlyForProfiles: ['gastronomy', 'gastronomy-tables']` and `enforceProfileInAllView: true`.

## Tests

`dotnet test backend/KasseAPI_Final.sln --filter "Category!=PostgreSql"`

```text
Passed!  - Failed:     0, Passed:  5892, Skipped:     5, Total:  5897, Duration: 25 s - KasseAPI_Final.Tests.dll (net10.0)
```

`dotnet test backend/KasseAPI_Final.sln --filter "Category=PostgreSql"`

Docker was running (`Server Version: 29.9.2`). The earlier skip is recorded at the end of this report. The rerun:

```text
Passed!  - Failed:     0, Passed:    57, Skipped:     0, Total:    57, Duration: 10 s - KasseAPI_Final.Tests.dll (net10.0)
```

`cd frontend-admin && npm run test:e2e -- online-order-product-mappings`

Port 3000 was free, so Playwright started `npm start` from the fresh production build:

```text
  ok 1 [chromium] › tests\e2e\online-order-product-mappings.spec.ts:115:7 › Marketplace product mapping › saves a mapping and shows it after reload (2.3s)
  ok 2 [chromium] › tests\e2e\online-order-product-mappings.spec.ts:147:7 › Marketplace product mapping › saves a wolt provider mapping (1.7s)
  ok 3 [chromium] › tests\e2e\online-order-product-mappings.spec.ts:172:7 › Marketplace product mapping › hides marketplace settings for a non-gastronomy tenant (1.8s)
  ok 4 [chromium] › tests\e2e\online-order-product-mappings.spec.ts:193:7 › Marketplace product mapping › gastronomy super admin sees partner credentials and tenant admin does not (1.9s)

  4 passed (12.3s)
```

Unit coverage: Super Admin PUT then GET returns `configured` and the response and stored columns do not contain the plaintext secret. An enabled row builds an authorize URL with the table client id. A disabled row returns 409 `PROVIDER_DISABLED`. Hair-salon calls to the new admin endpoints return 403 `PROFILE_ENDPOINT_DISABLED`. Existing Wolt webhook tests still sign with the harness secret because the harness seeds an enabled provider row encrypted with that secret.

## Not in this change

Wolt status write-back (accept / ready / delivered) was not implemented. No money path was added.

`PaymentService`, `TseService`, `RksvDepExportService`, `SignaturePipeline`, `RolePermissionMatrix`, existing migration files, and `online_order_integration_oauth_tokens` were not modified.

PostgreSQL integration: 1 passed, 56 skipped (2026-10-30, Docker unavailable).
PostgreSQL integration: 57 passed, 0 skipped (2026-10-30, Docker available).
