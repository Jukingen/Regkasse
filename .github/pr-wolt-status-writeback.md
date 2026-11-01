## Summary

- Adds nullable `wolt_self_delivery` on `online_order_integrations` (additive migration `20261030125427_AddWoltSelfDeliveryToOnlineOrderIntegrations` only).
- Maps local kitchen transitions to Wolt: `pending → accepted` = PUT accept, `accepted → preparing` = no call, `preparing → ready` = PUT ready, `ready → completed` = PUT delivered only when `self_delivery` is true. Courier deliveries (`false` or missing) complete locally.
- A Wolt 409 whose follow-up order GET is already in the target state advances the local order once. A conflicting 409 or 422 does not advance the order, sets `last_error`, and is not picked up by the intake retry worker.
- Non-gastronomy Wolt calls return 403 `PROFILE_ENDPOINT_DISABLED`. Website order status transitions are unchanged.
- The status endpoint still requires only `digital.orders.manage`. Status buttons stay hidden when the Wolt provider is disabled or the venue has no OAuth token.

## Not changed

PaymentService, TseService, RksvDepExportService, SignaturePipeline, RolePermissionMatrix, existing migrations, and the online-order webhook controller.

## Tests

- Unit/integration (`Category!=PostgreSql`): Failed 0, Passed 5899, Skipped 5, Total 5904.
- PostgreSQL: Failed 0, Passed 57, Skipped 0, Total 57.
- Playwright: `online-order-product-mappings` 4 passed; `online-order-wolt-status` 2 passed.
- `npm run i18n:ci` exited 0.

## Report

`docs/reports/wolt-slice-2-2026-10-30.md`

## Migration

`20261030125427_AddWoltSelfDeliveryToOnlineOrderIntegrations` adds nullable `wolt_self_delivery` (`boolean`, no default) after `20261030112213_AddOnlineOrderIntegrationProviders`. Do not merge until `dotnet ef migrations list` confirms that order on the target database.
