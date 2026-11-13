# Module: TSE and FinanzOnline

## Scope

- TSE signature / device / state flows (**fiscal core**)
- FinanzOnline submission / outbox / reconciliation flows

> **Super Admin TSE ops** (health, failover UI, auto-healing, scaling, knowledge, and similar) is a **separate** surface: mostly diagnostic.  
> Do **not** treat `/api/admin/tse/*` ops tools as a license to rewrite signature chains / DEP / Startbeleg.  
> Agent contract: [`tse_admin_ops.md`](tse_admin_ops.md) · Inventory: [`docs/PROJECT_COMPREHENSIVE_DOCUMENTATION.md`](../../docs/PROJECT_COMPREHENSIVE_DOCUMENTATION.md)

## Multi-tenant architecture

- TSE devices, the signature chain, and FinanzOnline outbox/submission rows are split by `tenant_id`; there is no sharing across tenants.

## Rules

- Change behavior only with explicit task scope.
- Do not arbitrarily change field names or payload mappings.
- Do not swallow error cases; keep them observable.
- When changing routes/contracts in this area, also evaluate migration and rollback impact.
- An ops wave (auto-healing / failover) may change device selection; it must not rewrite receipt bytes / DEP content.
