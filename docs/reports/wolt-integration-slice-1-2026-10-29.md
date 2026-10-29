# Wolt marketplace orders — slice 1

**Date:** 2026-10-29
**Status:** Implemented. No pull request. No push.

Slice 1 is read-only intake: OAuth, webhook verification, order fetch, and the existing cart bridge. Order-status write-back to Wolt (accept / ready / delivered) is not in this slice.

## Files added

- `backend/Models/OnlineOrderIntegrationOAuthToken.cs`
- `backend/Services/Order/OnlineOrderIntegrationTokenProtector.cs`
- `backend/Services/Order/WoltOAuthClient.cs` (token HTTP client and access-token source)
- `backend/Services/Order/WoltOrderMapper.cs`
- `backend/Services/Order/WoltOrderNotificationService.cs`
- `backend/Services/Order/WoltIntegrationConnectService.cs`
- `backend/Controllers/AdminWoltIntegrationController.cs`
- `backend/Migrations/20261029102449_AddOnlineOrderIntegrationOAuthTokens.cs`
- `backend/Migrations/20261029102449_AddOnlineOrderIntegrationOAuthTokens.Designer.cs`
- `backend/KasseAPI_Final.Tests/WoltOrderMapperTests.cs`
- `frontend-admin/src/api/generated/model/getApiAdminOnlineOrderIntegrationsWoltCallbackParams.ts`

## Files modified

- `backend/Models/OnlineOrderIntegration.cs` — per-provider `IsConfirmed`. `WoltConfirmed` stays `false`.
- `backend/Services/Order/OnlineOrderIntegrationLocationService.cs`
- `backend/Services/Order/OnlineOrderIntegrationProcessor.cs`
- `backend/Services/Hosted/OnlineOrderIntegrationRetryHostedService.cs`
- `backend/Services/Order/OnlineOrderIntegrationWebhookService.cs` — Deliverect still enters through `AcceptAsync`.
- `backend/Controllers/OnlineOrderIntegrationWebhookController.cs`
- `backend/Configuration/OnlineOrderIntegrationOptions.cs`
- `backend/ApplicationHost.cs`
- `backend/Data/AppDbContext.cs`
- `backend/Data/OnlineOrderIntegrationEntityConfiguration.cs`
- `backend/Migrations/AppDbContextModelSnapshot.cs` — EF output only
- `backend/swagger.json`
- `frontend-admin/src/api/generated/admin/admin.ts`
- `frontend-admin/src/api/generated/model/index.ts`
- `backend/KasseAPI_Final.Tests/OnlineOrderIntegrationTests.cs`
- `frontend-admin/tests/e2e/online-order-product-mappings.spec.ts`

## Migration

`20261029102449_AddOnlineOrderIntegrationOAuthTokens` only creates `online_order_integration_oauth_tokens`.

| Column | Type |
|---|---|
| `id` | `uuid` PK |
| `tenant_id` | `uuid` NOT NULL, FK `tenants(id)` ON DELETE RESTRICT |
| `provider` | `varchar(32)` NOT NULL |
| `external_location_id` | `varchar(128)` NOT NULL |
| `refresh_token` | `text` NOT NULL (ciphertext) |
| `access_token` | `text` NOT NULL (ciphertext) |
| `access_token_expires_at` | `timestamptz` NOT NULL |
| `created_at` | `timestamptz` NOT NULL |
| `updated_at` | `timestamptz` NULL |
| `is_active` | `boolean` NOT NULL DEFAULT `true` |

Unique index `ux_online_order_integration_oauth_tokens_tenant_provider_location` on `(tenant_id, provider, external_location_id)`.

It is additive: `Up` is `CreateTable` plus that index. `Down` drops the new table. `20261028121002_InitialCreate`, `20261028151502_AddOnlineOrderIntegrations`, and `20261028230039_AddOnlineOrderProductMappings` were not edited. `online_order_integration_deliveries` was not added.

## Production lock

`OnlineOrderIntegrationSignatureStatus.IsConfirmed` is per provider.

| Provider | Production constant | Test hook |
|---|---|---|
| `deliverect` | `DeliverectConfirmed = false` | none |
| `wolt` | `WoltConfirmed = false` | `WoltConfirmedForTests`, reset in `finally` |
| any other | closed | none |

Config cannot flip either constant. Enabling a location in Production while that provider is unconfirmed returns HTTP 409 `INTEGRATION_SIGNATURE_UNCONFIRMED`. Setting the Wolt test hook does not confirm Deliverect.

The processor skips a row in Production only when that row's provider is unconfirmed. The retry worker still runs. `ListDueIdsAsync` drops unconfirmed providers. In Production with the Wolt hook off, no provider is due. With the hook on, only `provider = wolt` is due. The claim itself is still `ExecuteUpdate` (PostgreSQL). The InMemory test asserts the due-id list because the InMemory provider does not implement `ExecuteUpdate`.

## Token encryption

Purpose: `Regkasse.OnlineOrderIntegration.OAuthToken.v1`.

`OnlineOrderIntegrationTokenProtector` calls `IDataProtectionProvider.CreateProtector` from the existing `AddDataProtection()` registration. Ciphertext is base64 in `access_token` and `refresh_token`. No new key and no new package.

`Protect` runs when the callback stores a pair and when `WoltAccessTokenSource` stores a rotated pair. `Unprotect` runs only in `WoltAccessTokenSource.GetAccessTokenAsync`, immediately before a Wolt HTTP call. Token values are not written to logs.

Refresh is single-flight per tenant and venue. The new refresh token replaces the old one. A failed refresh does not delete the stored pair. The same refresh token is not sent twice.

## OAuth connect and callback

Both routes use the existing `digital.manage` permission. No new permission.

- `POST /api/admin/online-order-integrations/wolt/connect` stores a 10-minute `state` in `IMemoryCache`, bound to the signed-in user and the ambient tenant, and returns the configured `AuthorizationEndpoint` with `client_id`, `redirect_uri`, `state`, and `response_type=code`.
- `GET /api/admin/online-order-integrations/wolt/callback` requires the same user and tenant. A missing or mismatched `state` is HTTP 404 and writes nothing. The code is exchanged at `OnlineOrderIntegrations:Wolt:TokenUrl` (default `https://integrations-authentication-service.wolt.com/oauth2/token`) with HTTP Basic `client_id:client_secret`. Venue id is `integration.venue_id` on the access-token payload. A location owned by another tenant is HTTP 404 and no token row is stored. A new location is inserted with `enabled = false`. An existing location's `enabled` flag is left as it was.

Secrets (`ClientId`, `ClientSecret`, `RedirectUri`, `AuthorizationEndpoint`) come from configuration. They are not committed.

## Webhook mapping

`POST /api/webhooks/online-order-integrations/wolt` reads `WOLT-SIGNATURE` (header lookup is case-insensitive). Every other provider still reads `X-Regkasse-Integration-Signature`.

The Wolt branch checks HMAC-SHA256 hex of the raw body with `Wolt:ClientSecret` before JSON parsing and before any HTTP call. A missing secret or a bad signature is HTTP 400 and no row.

`type = order.notification` with `order.status` `CREATED`, or the first `PRODUCTION` when that order id is not stored yet, continues. Other notifications return HTTP 200 and write nothing. `order.venue_id` resolves `provider = wolt`. Unknown or disabled is HTTP 404 and no row.

The order document is `GET {OrderBaseUrl}/v2/orders/{order.id}` (default host `https://pos-integration-service.wolt.com`) with the stored bearer token. A fetch or token failure is HTTP 502 and no row.

The V2 document is mapped onto the existing `OnlineOrderIntegrationWebhookBody` and that JSON is `raw_payload`. The processor is unchanged.

- `external_order_id` = order `id`
- `external_location_id` = `venue.id`
- `external_product_id` = line `pos_id`
- Amounts are minor units divided by 100
- `delivery.fee` → `deliveryFee`
- `delivery.small_order_surcharge` → `serviceFee`
- `channelTotal` is `basket_price.total` (the declared total, not recomputed from the lines)
- Currency must be EUR. Otherwise the row is stored as `failed` and the processor is not called
- An empty or unknown `pos_id` uses the existing "not mapped" failure
- A masked or missing phone is not replaced. The existing phone check fails the row
- Line total plus fees must equal `channelTotal`, or the row is `failed` and no cart is opened

## Tests

| Step | Tests | Result |
|---|---|---|
| 0 | Production Wolt enable is 409 while `WoltConfirmed` is false. Deliverect enable stays 409. With `WoltConfirmedForTests` the Wolt enable returns 200 and Deliverect stays 409. Unconfirmed Wolt row stays `received` in Production. Retry due-list skips unconfirmed Wolt and, with the hook, returns only the Wolt id. | Pass |
| 1 | Authorization-code exchange and refresh against a stub handler. The second refresh sends the rotated token. Logs do not contain the refresh token or client secret. Stored columns are ciphertext. | Pass |
| 2 | `WOLT-SIGNATURE` hex is accepted. Wrong value, empty secret, and non-hex are rejected. A bad signature on a valid notification is HTTP 400, not 404, and writes no row. | Pass |
| 3 | Mapper: cents, `pos_id`, fee lines, declared total mismatch, non-EUR. Unknown or disabled venue is 404 with no row. Failed `GET /v2/orders` is 502 with no row. `CREATED` and a first `PRODUCTION` open a cart. A repeated `PRODUCTION` does not fetch again. Currency, total, unmapped `pos_id`, and missing phone fail with no cart. | Pass |
| 6 | Playwright `saves a wolt provider mapping` on `/admin/online-order-integrations`. No live OAuth. | Pass |

## Command results

`dotnet test backend/KasseAPI_Final.sln --filter "Category!=PostgreSql"`

```text
Passed!  - Failed:     0, Passed:  5888, Skipped:     5, Total:  5893, Duration: 59 s - KasseAPI_Final.Tests.dll (net10.0)
```

`dotnet test backend/KasseAPI_Final.sln --filter "Category=PostgreSql"`

```text
Passed!  - Failed:     0, Passed:    57, Skipped:     0, Total:    57, Duration: 17 s - KasseAPI_Final.Tests.dll (net10.0)
```

`cd frontend-admin && npm run test:e2e -- online-order-product-mappings`

```text
  ok 1 [chromium] › tests\e2e\online-order-product-mappings.spec.ts:90:7 › Marketplace product mapping › saves a mapping and shows it after reload (5.0s)
  ok 2 [chromium] › tests\e2e\online-order-product-mappings.spec.ts:121:7 › Marketplace product mapping › saves a wolt provider mapping (2.9s)

  2 passed (19.7s)
```

An earlier non-PostgreSQL run failed at `DemoProductImportServiceTests.cs:229` (`28P01` password authentication for user `regkasse`) because the EF design-time command had set `ConnectionStrings__DefaultConnection` in that shell. The variable was removed. The test file was not edited. The result above is the re-run.

## Deferred

Item 5 is not implemented. There is no `PUT /orders/{id}/accept`, `PUT /orders/{id}/ready`, or `PUT /orders/{id}/delivered`.

## Untouched

`PaymentService`, `TseService`, `RksvDepExportService`, `SignaturePipeline`, `RolePermissionMatrix`, and the three committed migrations listed above were not modified. No new NuGet or npm package was added.
