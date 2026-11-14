# ADR 0001: Multi-tenant architecture

- **Status:** Accepted
- **Date:** 2026-11-14

## Context

Regkasse is one product used by many mandants (restaurants and other businesses). Each mandant has its own catalog, users, cash registers, and fiscal history. Running a separate database per mandant would multiply backups, migrations, and Fiskaly cutovers. A shared process still has to make cross-mandant reads impossible.

Production hosts are shared: POS `pos.regkasse.at`, Admin `admin.regkasse.at`, API `api.regkasse.at`. The host name is not the mandant.

## Decision

Use one database and one API. Every tenant-scoped row implements `ITenantEntity` and stores a non-null `tenant_id`. `AppDbContext` applies a global query filter from `ICurrentTenantAccessor.TenantId`.

After login, the JWT `tenant_id` claim is the tenant. In Production, shared hosts ignore `X-Tenant-Id` and `?tenant=`. Development may send `X-Tenant-Id` (prefer the seeded slug `dev`).

A request that names another mandant's id returns HTTP 404, not 403. `IgnoreQueryFilters()` is limited to Super Admin operations that must read every mandant on purpose. Background services have no HTTP tenant; they open a scope with `IServiceScopeFactory` before creating a `DbContext`.

There is no business tenant slug `default`. The platform row (`platform`) is a sentinel, not a mandant.

## Consequences

- New tenant-scoped tables need `tenant_id` and the entity interface. A missing filter is a data leak.
- Super Admin impersonation issues a JWT for the target tenant (`tenant_impersonation=true`). Later queries follow that tenant.
- Hosted jobs must not resolve `DbContext` from the root provider.
- Cross-tenant SaaS routes (tenant CRUD, billing, backup) are an explicit allow-list. Mandant data APIs still require an ambient tenant, including for Super Admin.

See [`../MULTI_TENANT.md`](../MULTI_TENANT.md) and [`../POS_PRODUCTION_ARCHITECTURE.md`](../POS_PRODUCTION_ARCHITECTURE.md).
