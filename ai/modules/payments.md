# Module: Payments

## Risk Notes
- Payment amount, rounding, currency format (EUR) kritik
- Split payment (cash+card) varsa açıkça belirtilmeli
- Payment logs/metrics tabloları etkilenebilir

## Rules
- DB decimal(18,2) uyumunu bozma
- Payment ile receipt/closing bağlantıları varsa koparma
- İade/iptal senaryolarında audit/log beklentisini koru
