# Marketplace integrations — operator guide

Wolt and Foodora orders arrive on the online-order inbox. They are not fiscal receipts until a cashier pays the cart on POS.

**Last updated:** 2026-11-12

Screen: FA `/admin/online-order-integrations`. The tenant must use a gastronomy profile. Lieferando uses the same screen and is already wired (see [`ONLINE_ORDER_INTEGRATIONS.md`](ONLINE_ORDER_INTEGRATIONS.md)).

## 1. Enable the provider

Someone with `digital.manage` (typically Super Admin):

1. Open `/admin/online-order-integrations`.
2. Wolt: turn the provider on and save the partner OAuth client id and client secret. The form clears the secret fields after a successful save. Status should read configured.
3. Foodora: turn the provider on. Do not put the restaurant's client secret in this form. Foodora secrets belong to the tenant (next section). The provider row only stores `enabled` and `credential_owner = tenant`.

If the page returns 403, the tenant profile is not gastronomy.

## 2. Enter credentials

### Wolt (OAuth)

The Mandanten-Admin needs `digital.orders.manage` and an **enabled** Wolt provider.

1. Stay on `/admin/online-order-integrations`.
2. Use **Connect**. The browser goes to Wolt (`POST /api/admin/online-order-integrations/wolt/connect` returns `authorizationUrl`).
3. Approve the venue. The callback stores the access token for that location. The section should show connected.

Connect stays disabled until the provider is enabled. A missing OAuth client id/secret on the provider row fails the start with a validation error.

### Foodora (client credentials)

The Mandanten-Admin needs `digital.orders.manage`. Super Admin does not see this form; switch into the tenant (or use a Mandanten-Admin session).

1. Provider must already be enabled.
2. Fill client id, client secret, chain id, and webhook token.
3. Save. Secrets are write-only ciphertext on `online_order_integration_tenant_credentials`.

The API then requests Foodora tokens with `grant_type=client_credentials`. The webhook token is what the intake check expects on `POST /api/webhooks/online-order-integrations/foodora`.

## 3. Map products

Still on `/admin/online-order-integrations`, with `digital.manage`:

1. Set provider to `wolt` or `foodora` (the field is free text, max 32).
2. Enter the channel's external product id.
3. Pick an active catalog product (first 100 active products are listed).
4. Save. The row is `online_order_product_mappings` for the ambient tenant.

Repeat per product. An order line with no mapping does not create a receipt and does not cancel the channel order by itself. Fix the mapping and allow the provider to retry, or correct the inbox row.

Locations are not on this form. A Super Admin maps `(provider, external location id)` to the tenant with `PUT /api/admin/online-order-integrations/locations` (`digital.manage`). An unknown location is rejected and nothing is stored.

## 4. Watch incoming orders

1. Open `/orders/online` (`digital.orders.view`).
2. A accepted webhook creates an online order. Payment status on that row is not a Beleg.
3. If the location has a claiming user, the order may already have a POS cart. Otherwise it stays pending until staff handle it.
4. The cashier pays on POS. That payment is the RKSV receipt.

Webhook URL to give the channel:

```text
POST https://api.regkasse.at/api/webhooks/online-order-integrations/wolt
POST https://api.regkasse.at/api/webhooks/online-order-integrations/foodora
```

Wolt signs with its signature header. Foodora sends the tenant webhook token as `Authorization`.

## 5. Troubleshooting

| What you see | What it usually means |
|--------------|------------------------|
| HTTP 400 `SIGNATURE_INVALID` | Wolt signature or Foodora `Authorization` does not match the stored secret. Nothing was saved. Re-save the webhook token or reconnect Wolt. |
| HTTP 404 `LOCATION_NOT_FOUND` | Provider is off, the location row is missing, or the id does not belong to this tenant. |
| Connect button disabled | Wolt provider is not enabled. |
| Connect fails immediately | Deployment OAuth client id/secret missing, or the OAuth state expired (start Connect again). |
| Foodora form missing | You are Super Admin, or you lack `digital.orders.manage`. Use the mandant session. |
| Foodora fields disabled | Provider is not enabled. |
| Order in the inbox, no cart | Location has no claiming user, or the cart bridge did not run. The order is still not a receipt. |
| Order failed, line unmapped | Add the product mapping, then let the channel retry the same external order id. |
| HTTP 403 on the admin page | Tenant profile is not gastronomy. |
| Paid on the channel, no RKSV receipt | Expected until a cashier completes `POST /api/pos/payment`. |
