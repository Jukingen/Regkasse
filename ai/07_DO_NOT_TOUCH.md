# Critical: high-risk areas (change only with explicit scope)

## 1) Cart → Payment → Receipt → DailyClosing

- Behavior changes on this chain create financial and legal risk.
- Do not refactor or rewrite beyond the requested change.

## 2) TSE and signature chain

- Keep TSE signature production, **receipt numbering / sequence**, and **`signature_chain_state`** validation intact.
- Do not change signature payload fields or flow order without cause; no client or flag may skip the chain.
- **Super Admin TSE ops** (`/api/admin/tse/*`, FA `/admin/tse/*`) are mostly diagnostic; they are not for DEP / cert / Startbeleg rewrite.
- **Failover / auto-healing** can change which device signs (fiscal-adjacent) — default healing is off, `AllowAutoFailover` defaults to false; do not loosen without an explicit task.
- Ops inventory: `ai/modules/tse_admin_ops.md`; fiscal core: `ai/modules/tse_finanzonline.md`.

## 3) RKSV special-receipt lifecycle

- Nullbeleg, Startbeleg, Monatsbeleg, Jahresbeleg, Schlussbeleg: uniqueness rules, register state, and TSE availability are strict in code.
- Do not confuse Schlussbeleg with daily closing; it belongs to decommissioned-register transition.

## 4) Decommissioned register guardrails

- A **decommissioned** cash register must not accept a new session or payment. Do not loosen or bypass these guards.

## 5) Voucher ledger and balance

- `Voucher` / `VoucherLedgerEntry` consistency and audit trail; no plaintext code storage.
- Handle ledger movement types and rules such as going below zero with care.

## 6) FinanzOnline / outbox / reconciliation

- Mapping fields, retry taxonomy, and reconciliation semantics are sensitive.
- Even if the RKSV submission skeleton is not production-complete, outbox rows and status fields matter for audit.
- Do not swallow errors or reduce the audit trail.

## 7) Authorization / RBAC

- Permission names, the role-permission matrix, and guard flows are sensitive.
- Do not loosen endpoint authorization; if it must change, write an explicit migration plan.

## 8) Money precision / rounding

- Keep existing precision and rounding behavior on money calculations.

## 9) Tenant isolation / query filters

- Do not loosen or remove `AppDbContext` global query filters or the `ICurrentTenantAccessor` flow.
- Do not go back to root `IDbContextFactory` / `AppDbContext` in singleton services (do not break the `IServiceScopeFactory` pattern — `LicenseService`).
- Do not leave multiple runtime constructors on `AppDbContext` that DI could resolve (keep `[ActivatorUtilitiesConstructor]` + design-time ctor only).
- Leave `IgnoreQueryFilters()` only on deliberate Super Admin / migration paths.
- Do not replace cross-tenant 404 semantics with 403 or an empty 200.
- Do not change middleware order (`TenantResolutionMiddleware` → auth → `TenantContextMiddleware`) without cause.
- Do not make Production POS entry `{slug}.regkasse.at` again, and do not invent a Host==JWT requirement on reserved `pos`/`api`/`admin` hosts — `docs/POS_PRODUCTION_ARCHITECTURE.md`.

## 10) Offline systems (do not merge)

- Legacy TSE intents (`offline_transactions`) and full order snapshots (`offline_orders`) are **separate systems**.
- Do not merge them into one table/UI/API family. FA routes: `/admin/tse/offline-transactions` vs `/rksv/offline-orders`.
- Modules: `ai/modules/offline_transactions_legacy.md`, `ai/modules/offline_orders.md`; hub: `docs/OFFLINE_SYSTEM_INDEX.md`.

## 11) Backup / restore

- Do not mix Tenant vs System strategy; Mandanten-Admin must not access System dumps.
- No production DB restore via API (isolated validation / drill only).
- Detail: `docs/BACKUP_SYSTEM.md`, `docs/restore-boundary-notes.md`, `ai/modules/backup_permissions.md`.

## 12) Working hours / digital storefront

- Working hours **never** close POS or FA / authenticated `/api/pos/*` / `/api/admin/*`; they only gate website/app online-order intake (`docs/WORKING_HOURS.md`).
- The storefront (`frontend-sites`) is not fiscal POS; do not wire it into the payment/RKSV chain.

> If you are unsure, do not assume: narrow the scope and write risks and unknowns explicitly. Summary context: `REGKASSE_AI_ONBOARDING.md`.
