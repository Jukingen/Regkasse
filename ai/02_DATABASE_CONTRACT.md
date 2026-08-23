# Database contract (PostgreSQL + EF Core)

## Database schema

### Multi-tenant columns

On every tenant-scoped table that implements `ITenantEntity`:

- `tenant_id uuid NOT NULL` — FK `tenants.id`
- Index for performance (`AppDbContext` `HasIndex(e => e.TenantId)`)
- Value comes from the application per request: **Production shared hosts** → JWT `tenant_id` after auth; **Dev** → `X-Tenant-Id` / `?tenant=` slug; **Host** (legacy slug / `TenantDomain` sites) → `CurrentTenantService` → `ICurrentTenantAccessor.TenantId` (Guid)

Root / global examples: `tenants`, Identity user tables. External string key: `tenants.slug` (Host / Dev resolution; not the POS production entry).

### Global query filters

EF Core adds a filter to every `ITenantEntity` query:

```text
WHERE tenant_id = @currentTenantId
```

Source: `AppDbContext.CreateTenantQueryFilter` → `_tenantAccessor.TenantId == null || e.TenantId == _tenantAccessor.TenantId`.

### AppDbContext constructors and DI

| Constructor | Purpose |
|-------------|---------|
| `AppDbContext(DbContextOptions<AppDbContext> options)` | Design-time / `dotnet ef` — `NullCurrentTenantAccessor`, filters off |
| `AppDbContext(options, ICurrentTenantAccessor)` | Runtime — `[ActivatorUtilitiesConstructor]` |

Use `IDbContextFactory<AppDbContext>` in singleton services **only from inside an `IServiceScopeFactory` scope** (`LicenseService`).

**Not tenant-scoped:** `activated_licenses` (deployment-local license activation).

## Multi-tenant architecture

- Tenant root table: `tenants` (`Tenant` entity — global, not `ITenantEntity`).
- Tenant-scoped tables: `tenant_id` (UUID, non-null) + `ITenantEntity` / `BaseTenantEntity`.
- User–tenant: `user_tenant_memberships` (login expects one active membership; multiple memberships are logged).
- Cross-tenant read/write: **404** at the application layer; `IgnoreQueryFilters()` only for Super Admin / seed / deliberate bypass.
- Adding `tenant_id` in migrations: follow fiscal/audit waves (`AddTenantIdToFiscalAndAuditTables`, and similar).

## Migrating existing databases

Existing single-tenant PostgreSQL installs use a **wave-by-wave** migration chain (there is no single `AddTenantIdToAllTables`).

### Pattern (EF Core)

1. `tenants` table + default tenant seed (`20260403190133_AddTenantsAndSettingsTenantId`).
2. Add `tenant_id uuid NOT NULL` on related tables — temporary/default: `SystemTenantIds.Platform` (fixed Guid; not the string `'legacy'`).
3. Data backfill migrations (for example `BackfillUserTenantMembershipsData`).
4. Wave migrations: payment methods / cash registers (Wave2), categories / products (Wave3A), modifiers (Wave3B), fiscal / audit / offline (`20260516101549_AddTenantIdToFiscalAndAuditTables`).
5. `HasIndex(e => e.TenantId)` — inside `AppDbContext`.

### Commands

```bash
dotnet ef migrations list --project backend/KasseAPI_Final.csproj --startup-project backend/KasseAPI_Final.csproj
dotnet ef database update --project backend/KasseAPI_Final.csproj --startup-project backend/KasseAPI_Final.csproj
```

When adding `tenant_id` to a new table:

```bash
dotnet ef migrations add <DescriptiveName> --project backend/KasseAPI_Final.csproj --startup-project backend/KasseAPI_Final.csproj
```

### Cautions

- Do not treat destructive migrations as acceptable on fiscal / receipt / TSE tables; use additive changes + a default Guid.
- `IgnoreQueryFilters()` only on Super Admin services, seed, or deliberate maintenance paths.
- Detail: `docs/MULTI_TENANT.md`, `REGKASSE_AI_ONBOARDING.md` (Database Schema).
- **Production safety:** expand → backfill → contract (minimum two releases); [`docs/DATABASE_MIGRATION_STRATEGY.md`](../docs/DATABASE_MIGRATION_STRATEGY.md). Status: `GET /health/migrations`, FA `/admin/database/migrations`.

## Source

- Real model source: `backend/Data/AppDbContext.cs` and `backend/Migrations/*`.
- EF Core owns migration management; migration history is authoritative.

## Data model principles

- Financial fields commonly use `decimal(18,2)`; tax/rate fields may use tighter precision (for example `decimal(5,2)`).
- Identity and application tables live in the same context.
- Auth session tables are critical: `auth_sessions`, `refresh_tokens`.
- JSON / flexible payload columns exist; do not add arbitrary new JSON columns.

## Sensitive domain areas

- **PaymentDetails** and related payment lines: normal sales plus RKSV special-receipt fields (`RksvSpecialReceiptKind`, year/month metadata, `RksvNullbelegActsAsJahresbeleg`, storno/refund and offline replay metadata — real column list: `AppDbContext` + migrations).
- **Receipt** / **ReceiptSequence** / **`signature_chain_state`**: receipt-number sequence and signature-chain consistency; do not split these across unrelated tables.
- **Voucher:** `vouchers`, `voucher_ledger_entries` (balance and audit trail; plaintext voucher codes are not stored — hash/masked display model).
- TSE device/signature tables (`tse_devices`, `tse_signatures`, and similar)
- `offline_transactions` — legacy payment-intent replay, payload hash, device/sequence coverage
- **`offline_orders`** — full POS order snapshots (`order_data` JSONB), 72 h expiry, sync to `payment_details` via replay (`20260627002059_AddOfflineOrdersTable`)
- `DailyClosing` and tables related to report close
- FinanzOnline outbox/submission tables
- Backup/restore verification tables (operational assurance)

## Schema change rules

1. Inspect existing entity mapping and migration patterns first.
2. Evaluate public-contract impact (DTO/OpenAPI) separately.
3. Do not make destructive changes without backward compatibility.
4. **Fiscal/RKSV fields:** prefer nullable/additive migrations and small, tested rollback steps (aligned with the `REGKASSE_AI_ONBOARDING.md` migration note).
5. Do not change indexes/constraints on sensitive fields without tests.

## Minimum checks

- `dotnet ef migrations list --project backend/KasseAPI_Final.csproj --startup-project backend/KasseAPI_Final.csproj`
- `dotnet ef database update --project backend/KasseAPI_Final.csproj --startup-project backend/KasseAPI_Final.csproj`
