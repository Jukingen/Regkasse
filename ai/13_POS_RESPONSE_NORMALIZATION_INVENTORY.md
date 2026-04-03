# POS response normalization inventory

**Last reviewed:** 2026-04-03

## Why this exists
POS servislerinde bazı adapter/normalizer katmanları backend response drift’ini tolere ediyor. Amaç: güvenli şekilde azaltmak.

## Active normalization hotspots
- `frontend/services/api/paymentService.ts` (`normalizePaymentResponse`): payment legacy/v2 response ayrımı.
- `frontend/services/api/normalizePosPaymentMethods.ts`: method listesi casing/envelope uyumu.
- `frontend/services/api/normalizeUserSettingsResponse.ts`: envelope/flat payload unwrap.
- Receipt mapping helpers (`PaymentModal`, `receiptPrinter`) : casing/shape normalize.

## Keep vs reduce
- **Keep (şimdilik):** offline queue migration normalizasyonları.
- **Reduce (ölçümlü):** payment response branch’leri, settings unwrap katmanları, duplicate receipt normalizer’lar.

## Safe reduction strategy
1. Önce canonical response shape’i contract ile sabitle (`swagger.json` + backend).
2. POS testlerini yeşil tutarak branch azalt.
3. Legacy parse yollarını tek PR’de değil adım adım kaldır.

## Contract-related POS tests
- `npm run test:contract` (frontend)
