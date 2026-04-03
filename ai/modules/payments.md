# Module: Payments

## Risk surface
- Amount, tax, rounding, idempotency, receipt link, cancellation/refund.
- Payment çıktıları receipt/daily closing/fiscal zinciriyle bağlıdır.

## Rules
- Para hassasiyetini ve mevcut rounding davranışını koru.
- Payment → receipt → fiscal kayıt bağını koparma.
- İptal/iade akışında audit ve authorization kontrollerini zayıflatma.
- Contract değişikliği varsa OpenAPI + consumer güncellemesini birlikte yap.
