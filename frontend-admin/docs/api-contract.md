# API Contract & Integration

This document maps the Admin Panel pages to the Backend API endpoints used.

## API Headers

### Tenant Identification

- **Production:** Tenant from request host subdomain (automatic).
- **Development:** `X-Tenant-Id: {slug}` header (tenant slug, not UUID).
- **Development:** `?tenant={slug}` query parameter.

Axios mutator / dev tenant selector should set the header on loopback; see `src/features/auth/` and `REGKASSE_AI_ONBOARDING.md`.

### Super Admin Endpoints

- `/api/admin/tenants/*` — requires `SuperAdmin` role.
- Manages global tenant registry (not filtered like `ITenantEntity` business rows).
- **`POST /api/admin/tenants/{tenantId}/impersonate`** — short-lived JWT for support in a target tenant context.

### Tenant admin API reference

Base path: `/api/admin/tenants`. Auth: `Bearer` JWT with `SuperAdmin` role.

| Method | Path | Response | Notes |
|--------|------|----------|--------|
| `GET` | `/api/admin/tenants` | `AdminTenantListItemDto[]` | Query: `includeDeleted` (bool, default false) |
| `GET` | `/api/admin/tenants/{tenantId}` | `AdminTenantDetailDto` | 404 if missing |
| `POST` | `/api/admin/tenants` | `201` + `AdminTenantDetailDto` | Body: `CreateAdminTenantRequest` (`name`, `slug` required) |
| `PUT` | `/api/admin/tenants/{tenantId}` | `AdminTenantDetailDto` | Body: `UpdateAdminTenantRequest` (`status`: `active` \| `suspended` \| `deleted`) |
| `DELETE` | `/api/admin/tenants/{tenantId}` | `204` | Soft-delete |
| `POST` | `/api/admin/tenants/{tenantId}/impersonate` | `TenantImpersonationResponseDto` | 400 if suspended/inactive; 404 if tenant missing |

**Impersonation response fields:** `token`, `expiresIn`, `refreshToken`, `refreshTokenExpiresAtUtc`, `tenantId`, `tenantSlug`, `tenantDisplayName`, `impersonation` (bool).

**FA client:** `src/features/super-admin/api/adminTenants.ts` — `impersonateAdminTenant`, `applyTenantImpersonationSession` (production: fragment redirect to `https://{tenantSlug}.regkasse.at/impersonate-callback`). See `docs/IMPERSONATION_FLOW.md`.

**OpenAPI:** `backend/swagger.json` (tags: admin tenants). Full architecture: `docs/MULTI_TENANT.md`.

## Auth
- **Login**: `POST /api/Auth/login`
- **Logout**: `POST /api/Auth/logout`

## Modules

### Dashboard (`/dashboard`)
- **Live reports integration**:
  - `GET /api/Reports/sales`
  - `GET /api/Reports/products`
  - `GET /api/Reports/payments`
  - `GET /api/Reports/customers`
- **Date range filter**: dashboard queries all report endpoints with `startDate` and `endDate`.
- **Current source of truth**: `frontend-admin/src/app/(protected)/dashboard/page.tsx`.

### Invoices (`/invoices`)
- **List**: `GET /api/Invoice/pos-list` (POS-authoritative admin list)
- **Details**: `GET /api/Invoice/{id}`
- **CSV export (implemented)**:
  - Filtered export (all matching rows): `GET /api/Invoice/export`
  - Batch export (selected rows): client-side CSV build from `GET /api/Invoice/{id}` per selected invoice
- **FinanzOnline action (admin UI)**: payment-based reconciliation retry via `POST /api/admin/finanzonline-reconciliation/retry/{paymentId}`.
- **Not used by admin UI**: legacy `POST /api/FinanzOnline/submit-invoice`.
- **Current source of truth**: `frontend-admin/src/features/invoices/components/InvoiceList.tsx`.

### Audit Logs (`/audit-logs`)
- **List**: `GET /api/AuditLog`
  - Params: `page`, `pageSize`, `startDate`, `endDate`, `userId`, `action`
- **Details**: `GET /api/AuditLog/{id}`

## API Client Generation
The client is generated using Orval from `swagger.json`.
Configuration: `orval.config.ts`
Output: `src/api/generated`

## RKSV/Admin Canonical Flows
- **Payments (Admin UI)** use canonical admin payment routes: `/api/admin/payments/*` (list/detail/statistics/cancel/refund).
- **FinanzOnline reconciliation**: `/api/admin/finanzonline-reconciliation`, `/metrics`, `/retry/{paymentId}` via generated `admin` client.
- **Incident investigation**: `/api/admin/incidents/{correlationId}` (aggregate source for replay + audit + FO state).
  - Single request / single payload model; no client-side composition across multiple incident APIs.
- **Replay batch detail**: `/api/admin/replay-batch/{correlationId}`.
- **Integrity checks**: `/api/admin/integrity`.
- **Offline coverage**: `/api/admin/offline-intent-coverage` and `/top-risk`.
- **Payload-hash maintenance**: `/api/admin/offline-payload-hash/{risk|analyze|export|repair}`.
- **Operations summary bridge**: `/api/admin/operations/summary` for replay backlog/incident density/export-risk first-glance cards on `/rksv`.
- **Operational UI flow**:
  - Start from `RKSV Operations` dashboard (`/rksv`) for status cards and drill-down links.
  - Investigate queue/state in `/rksv/finanz-online-queue`.
  - Use `/rksv/incident` for correlation-centric aggregate analysis.
  - Deep-dive with `/rksv/replay-batch`, `/rksv/integrity`, `/rksv/offline-intent-coverage`, `/rksv/payload-hash-conflicts`, and `/rksv/fiscal-export-diagnostics`.
