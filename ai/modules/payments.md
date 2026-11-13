# Module: Payments

## Risk surface

- Amount, tax, rounding, idempotency, receipt link, cancellation/refund.
- Payment outputs are tied to the receipt / daily-closing / fiscal chain.

## Multi-tenant architecture

- `PaymentDetails`, receipt sequence, and the signature chain are tenant-scoped; payment/receipt IDs return **404** across tenants.

## Rules

- Keep money precision and the current rounding behavior.
- Do not break the Payment → receipt → fiscal record link.
- Do not weaken audit or authorization checks on cancel/refund flows.
- If the contract changes, update OpenAPI and consumers together.
