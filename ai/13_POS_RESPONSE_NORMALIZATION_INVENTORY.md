# POS response normalization inventory

**Last reviewed:** 2026-05-04

## Multi-tenant architecture

- POS API calls use `tenantStorage` for the Development header and production host alignment; normalizers must not bypass tenant boundaries.

## Why this exists

Some POS adapter/normalizer layers tolerate backend response drift. Goal: reduce that safely.

## Active normalization hotspots

- `frontend/services/api/paymentService.ts` (`normalizePaymentResponse`): payment legacy/v2 response split.
- `frontend/services/api/normalizePosPaymentMethods.ts`: method-list casing/envelope alignment.
- `frontend/services/api/normalizeUserSettingsResponse.ts`: envelope/flat payload unwrap.
- Receipt mapping helpers (`PaymentModal`, `receiptPrinter`): casing/shape normalize.

## Keep vs reduce

- **Keep (for now):** offline queue migration normalizations.
- **Reduce (measured):** payment response branches, settings unwrap layers, duplicate receipt normalizers.

## Safe reduction strategy

1. First lock the canonical response shape in the contract (`swagger.json` + backend).
2. Reduce branches while keeping POS tests green.
3. Remove legacy parse paths step by step, not in a single PR.

## Contract-related POS tests

- `npm run test:contract` (frontend)
