# Module: TSE & FinanzOnline

## Scope
- TSE imza/cihaz/state akışları
- FinanzOnline submission/outbox/reconciliation akışları

## Rules
- Davranış değişikliği ancak açık görev kapsamıyla yapılır.
- Alan adları/payload eşleşmeleri keyfi değiştirilmez.
- Hata durumları sessizce yutulmaz; izlenebilirlik korunur.
- Bu alanlarda route/contract değişikliği yaparken migration ve rollback etkisi ayrıca değerlendirilir.
