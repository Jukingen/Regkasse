# Tagesabschluss schema quality sprint

This note summarizes the contract hardening for Tagesabschluss in the `backend → swagger → Orval → frontend-admin` flow.

## Previous issues

- Core fields on `TagesabschlussResult` (`success`, `closingDate`, `totalAmount`, `totalTaxAmount`, `transactionCount`, `paymentsWithoutInvoiceCount`) were not marked required in OpenAPI.
- `canClose` and `paymentsWithoutInvoiceCount` were not required on `TagesabschlussCanCloseResponse`.
- Numeric summary fields on `TagesabschlussStatisticsResponse` were not required.
- Orval therefore generated those fields as optional (`?`), which pushed extra null/void defenses into the page.

## Fixes

- Added `TagesabschlussSchemaRequiredFilter` to backend Swagger generation.
- The filter writes required fields explicitly on these endpoint response schemas:
  - `POST /api/Tagesabschluss/daily`
  - `POST /api/Tagesabschluss/monthly`
  - `POST /api/Tagesabschluss/yearly`
  - `GET /api/Tagesabschluss/history`
  - `GET /api/Tagesabschluss/can-close/{cashRegisterId}`
  - `GET /api/Tagesabschluss/statistics`
- Anonymous error bodies on the controller were typed as `TagesabschlussErrorResponse`.
- Swagger and Orval were regenerated.

## Result

- Critical Tagesabschluss fields are required in Orval types.
- Some `?? 0` / `number | undefined` defensive code on the frontend-admin Tagesabschluss page was removed.
- Runtime behavior is unchanged; contract reliability improved.
