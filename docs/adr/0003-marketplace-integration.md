# ADR 0003: Marketplace integration

- **Status:** Accepted
- **Date:** 2026-11-14

## Context

Mandants already take orders on Wolt, Foodora, and Lieferando. Staff were retyping those orders into the cash register. A marketplace order is not an Austrian receipt: tax signing happens only when a cashier pays the cart on POS.

The channels do not share one credential model. Wolt uses partner OAuth. Foodora and Lieferando use tenant client credentials and a webhook token. Putting one platform secret on the host would mix mandants.

## Decision

Intake lands in `online_orders` (website, app, and these channels). Fulfillment is status-only until POS payment. The fiscal receipt is still `POST` payment on the cash register.

Credentials are bring-your-own (BYOK). The mandant stores its own tokens. Provider rows can be enabled per deployment; secrets for Foodora stay on the tenant credential row. Wolt connect stores the location token after the venue approves access.

Webhooks are unauthenticated to the user but checked before insert:

| Channel | Endpoint | Check |
|---------|----------|--------|
| Wolt | `POST /api/webhooks/online-order-integrations/wolt` | Provider signature header |
| Foodora | `POST /api/webhooks/online-order-integrations/foodora` | Tenant webhook token on `Authorization` |
| Lieferando | Same integration family as Foodora | Tenant webhook token |

Unknown locations are rejected. Product lines need a mapping from the channel product id to a catalog product. A failed check stores nothing.

## Consequences

- Operators enable providers and paste secrets in FA (`/admin/online-order-integrations`). See [`../MARKETPLACE_INTEGRATIONS_OPERATOR_GUIDE.md`](../MARKETPLACE_INTEGRATIONS_OPERATOR_GUIDE.md).
- The online-order inbox (`/orders/online`) is not a Beleg list. Export type `orders` follows that rule.
- Working hours may close website intake. They do not close POS or these already-accepted channel orders' cashier payment.
- A new channel should reuse this webhook-and-mapping path instead of a second order table.
