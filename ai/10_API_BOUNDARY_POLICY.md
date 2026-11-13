# API boundary policy

## Canonical boundary

- Admin client (`frontend-admin`) → `/api/admin/*`
- POS client (`frontend`) → `/api/pos/*`
- Customer sites (`frontend-sites`) → `/api/public/*`, `/api/sites/*` (storefront / online-order intake; **not** POS or admin)

## API headers

### Tenant identification

- Production (target): JWT `tenant_id` on `api.regkasse.at` / `pos.regkasse.at` (`docs/POS_PRODUCTION_ARCHITECTURE.md`); Development: `X-Tenant-Id: {slug}` or `?tenant={slug}`.
- Custom website Host → slug: verified `TenantDomain` (`website.manage`).

### Super Admin endpoints

- `/api/admin/tenants/*` — `SuperAdmin`; global `tenants` CRUD; impersonation for business data.

## Multi-tenant architecture

- POS: single UI (`pos.regkasse.at`); FA: `admin.regkasse.at`; API: `api.regkasse.at`.
- Before adding new “global” admin endpoints, evaluate the tenant-filter requirement.
- Backend singletons use `IServiceScopeFactory` for EF (`LicenseService`); see `REGKASSE_AI_ONBOARDING.md`.
- **Working hours:** only `/api/public/*` / sites online-order intake; authenticated `/api/pos/*` and `/api/admin/*` are never closed by hours (`docs/WORKING_HOURS.md`).

## Hard rules

1. New endpoints open under the canonical boundary (POS / Admin / Sites as separate families).
2. Legacy aliases (`/api/Payment`, `/api/Cart`, `/api/Product`) were **removed** (2026-08-13). Do not reintroduce them.
3. New POS/admin features go under `/api/pos/*` or `/api/admin/*` only.
4. Contract changes are reviewed with an OpenAPI diff.
5. `offline_transactions` and `offline_orders` are not merged / mixed into one UI.

**Removal note:** [`docs/API_LEGACY_DEPRECATION.md`](../docs/API_LEGACY_DEPRECATION.md).

## Explicit exceptions (shared surfaces)

These surfaces sit outside the strict boundary on purpose or as migration debt:

- `/api/Auth/*`
- `/api/user/*` (prefer settings/profile; for new features prefer `/api/admin` or `/api/pos`)
- `/api/Receipts/*` (POS-allowed migration debt — prefer `/api/pos` for new features)
- `/api/Invoice/*`, `/api/Orders/*` (migration debt; prefer canonical prefix for new features)
- `/api/rksv/*` (RKSV special receipts; according to admin/POS permission; high risk)
- `/api/offline-transactions` (legacy payment-intent replay)
- `/api/pos/offline-orders` (POS full order snapshots — save/replay)
- `/api/admin/offline-orders` (admin list/manual replay)
- Historical PascalCase families (`/api/Tse/*`, `/api/UserManagement/*`, `/api/Tagesabschluss/*`, and similar)

## Admin guidance

- `src/api/generated/**` is the primary client surface.
- Do not hand-edit `src/api/generated/**`.
- Do not blindly add transformer strips to hide leftover legacy use; move the client to canonical endpoints.

## POS guidance

- Keep API access inside `frontend/services/api/*`.
- New code must not use `/api/Payment`, `/api/Cart`, `/api/Product` (aliases removed).
