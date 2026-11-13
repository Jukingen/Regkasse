# Database overview

This file summarizes the main data regions. It is not the schema authority.

## Database schema

### Multi-tenant columns

- Tenant-scoped tables: `tenant_id uuid NOT NULL` (+ index), FK `tenants.id`
- Request context: JWT `tenant_id` (shared POS/API hosts) / Dev `X-Tenant-Id` slug / Host (`TenantDomain` or legacy slug) → Guid accessor; rows are written/read with that Guid

### Global query filters

- EF: `WHERE tenant_id = @currentTenantId` (`ITenantEntity` types)
- Detail: `ai/02_DATABASE_CONTRACT.md`, `REGKASSE_AI_ONBOARDING.md`
- `AppDbContext`: design-time ctor (migrations) + runtime ctor (`ICurrentTenantAccessor`, `[ActivatorUtilitiesConstructor]`).
- **Deployment-local (not `ITenantEntity`):** `activated_licenses` — machine fingerprint; read via `IServiceScopeFactory` in `LicenseService`.

## Multi-tenant architecture

- **Tenant:** `tenants` (slug, status, license fields); all operational data is partitioned by `tenant_id`.
- **Isolation:** EF global filter — tenants cannot see each other’s sales/receipt/TSE/voucher rows.
- **Super Admin:** tenant list/CRUD via `tenants`; business data via impersonation JWT in the target tenant context.

## Main data regions

- Sales core: `Product`, `Category`, `Cart`, `CartItem`, `Order`, `OrderItem`, `PaymentDetails` (normal payments and **RKSV special receipts** share the same payment model; special-receipt kind and year/month fields live on `PaymentDetails`; **tenant-scoped**).
- Receipt/fiscal layer: `Receipt*`, **`ReceiptSequence` / receipt number allocation**, **`SignatureChainState`** (per-register signature chain), `TseDevice`, `TseSignature`, `DailyClosing`.
- **Voucher / Gutschein:** `Voucher`, `VoucherLedgerEntry` (balance movements; code hash + masked display; plaintext code is not stored in the DB).
- Identity and session: `ApplicationUser` + `auth_sessions` + `refresh_tokens`.
- Finance integration: `FinanzOnlineError`, `FinanzOnlineSubmission`, `FinanzOnlineOutboxMessage`, RKSV special-receipt submission/outbox tables (for example `rksv_special_receipt_finanz_online_submissions` — exact name in migrations).
- Operational assurance: backup/restore verification tables.

## Operational flow (summary)

1. POS creates/updates a cart.
2. Payment completes; receipt/fiscal rows are created.
3. End-of-day and report tables (`Tagesbericht` / `Monatsbericht` / `Jahresbericht`) are fed.
4. FinanzOnline/outbox processes run asynchronously.

## Agent notes

- For schema decisions, use `AppDbContext` and the related migration files, not this overview.
- Avoid “refactor” changes on fiscal and audit tables; do not touch them without a clear need.
- For project context read `REGKASSE_AI_ONBOARDING.md` first, then `ai/02_DATABASE_CONTRACT.md`.
