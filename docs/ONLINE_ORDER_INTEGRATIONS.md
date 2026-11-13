# Online order integrations

Marketplace orders land in `online_orders`. They are not receipts. A Beleg exists only after a cashier pays the linked cart through `POST /api/pos/payment`.

**Last updated:** 2026-11-12

**Related:** [`ONLINE_ORDERS.md`](ONLINE_ORDERS.md) · [`MARKETPLACE_INTEGRATIONS_OPERATOR_GUIDE.md`](MARKETPLACE_INTEGRATIONS_OPERATOR_GUIDE.md) · [`WORKING_HOURS.md`](WORKING_HOURS.md)

Webhook, mapper, and cart bridge do not call `PaymentService`. Website closed hours do not drop a marketplace order (`OnlineOrderIntakeService` is for Sites / PWA / native only).

## Providers

| Provider | Status | Auth | Where the secret lives |
|----------|--------|------|------------------------|
| `wolt` | Implemented | OAuth authorization code, then the stored access token | Deployment OAuth client on `online_order_integration_providers`. Venue token on `online_order_integration_oauth_tokens`. |
| `foodora` | Implemented | `client_credentials` | Tenant row on `online_order_integration_tenant_credentials` |
| `lieferando` | Implemented (JET Connect) | Authorization key + HMAC (`X-JET-Connect-Hash`) | Same tenant credential table |

FA screen for all three: `/admin/online-order-integrations` (`ProductMappingScreen`). Marketplace admin routes run only for gastronomy profiles (`MarketplaceProfileGate`).

## BYOK

`online_order_integration_providers` is deployment-wide (not tenant-scoped). One row per `provider`. `credential_owner` is `deployment` or `tenant`.

| Owner | Who configures | What they store |
|-------|----------------|-----------------|
| `deployment` (Wolt) | `digital.manage` | Enable flag plus partner OAuth client id and secret (ciphertext). |
| `tenant` (Foodora, Lieferando) | `digital.manage` toggles `enabled` only. Mandanten-Admin with `digital.orders.manage` (the form hides for Super Admin) stores client id, client secret, chain/brand id, and webhook token. | Ciphertext on `online_order_integration_tenant_credentials`. Unique per tenant and provider. |

Plaintext secrets are not returned by the status APIs.

## Tables

| Table | Role |
|-------|------|
| `online_order_integration_providers` | Provider switch and, for Wolt, deployment client credentials. |
| `online_order_integration_tenant_credentials` | Foodora and Lieferando secrets for one tenant. |
| `online_order_integration_locations` | Maps `(provider, external_location_id)` to a tenant. Unknown or disabled location: HTTP 404, nothing stored. |
| `online_order_integration_oauth_tokens` | Wolt access token after connect. |
| `online_order_integrations` | One inbox row per external order. Identity is `(tenant_id, provider, external_order_id)`. |
| `online_order_product_mappings` | External product id → catalog `ProductId` for that tenant. |

## HTTP

| Call | Permission | Purpose |
|------|------------|---------|
| `POST /api/webhooks/online-order-integrations/{provider}` | Anonymous | Intake. `wolt`, `foodora`, and `lieferando` have their own verifiers. Any other provider uses the generic signature header. |
| `GET` / `PUT /api/admin/online-order-integrations/providers` | `digital.manage` | Enable a provider. Wolt also accepts client id and secret. |
| `GET /api/admin/online-order-integrations/providers/status` | tenant session | Wolt enabled / connected for the ambient tenant. |
| `POST /api/admin/online-order-integrations/wolt/connect` | `digital.orders.manage` | Starts the Wolt OAuth redirect. |
| `GET` / `PUT …/foodora/credentials` | `digital.orders.manage` | Tenant Foodora client id, secret, chain id, webhook token. |
| `GET` / `PUT …/lieferando/credentials` | `digital.orders.manage` | Tenant Lieferando authorization key, HMAC secret, Flyt API key, brand id. |
| `PUT …/locations` | `digital.manage` | Location → tenant. |
| `GET` / `PUT …/product-mappings` | `digital.manage` | PLU / external id → product. |

Invalid signature: HTTP 400, nothing stored. A replay of the same external order id does not insert a second `online_orders` row.

## After the webhook

1. Verify the provider, then resolve the tenant from the location row. Ignore a tenant id in the JSON body.
2. Insert `online_order_integrations`, then map lines. An unmapped line leaves the integration `failed` and does not auto-reject the channel.
3. Insert `online_orders` and items. `tax` stays `0`; `total` is the gross snapshot from the channel.
4. A cart is created only when the location has a claiming user and `OrderIntegrationService.PushOrderToPosAsync` runs. That call does not take payment.
5. A cashier opens the cart on POS and pays. That is the TSE / receipt path.

Inbox for operators: FA `/orders/online` (`digital.orders.view`, legacy `order.view`). Status changes use `digital.orders.manage`.

Do not write marketplace payloads to `offline_orders` or `offline_transactions`.
