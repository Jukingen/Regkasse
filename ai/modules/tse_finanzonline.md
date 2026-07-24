# Module: TSE & FinanzOnline

## Scope
- TSE imza/cihaz/state akışları (**fiscal core**)
- FinanzOnline submission/outbox/reconciliation akışları

> **Super Admin TSE ops** (health, failover UI, auto-healing, scaling, knowledge, …) is a **separate** surface: mostly diagnostic.  
> Do **not** treat `/api/admin/tse/*` ops tools as a license to rewrite signature chains / DEP / Startbeleg.  
> Agent contract: [`tse_admin_ops.md`](tse_admin_ops.md) · Inventory: [`docs/PROJECT_COMPREHENSIVE_DOCUMENTATION.md`](../../docs/PROJECT_COMPREHENSIVE_DOCUMENTATION.md)

## Multi-Tenant Architecture

- TSE cihazları, imza zinciri ve FinanzOnline outbox/submission kayıtları `tenant_id` ile ayrılır; kiracılar arası paylaşım yok.

## Rules
- Davranış değişikliği ancak açık görev kapsamıyla yapılır.
- Alan adları/payload eşleşmeleri keyfi değiştirilmez.
- Hata durumları sessizce yutulmaz; izlenebilirlik korunur.
- Bu alanlarda route/contract değişikliği yaparken migration ve rollback etkisi ayrıca değerlendirilir.
- Ops dalgası (auto-healing / failover) cihaz seçimini değiştirebilir; fiş baytlarını / DEP içeriğini yeniden yazmak için kullanılmaz.
