# RKSV/Admin API Layer Conventions

This note defines the frontend-admin API boundary for RKSV/support/admin tooling.

## Scope

Covered domains:

- FinanzOnline reconciliation
- Incident investigation
- Replay batch detail
- Fiscal export diagnostics
- Integrity report
- Offline intent coverage
- Offline payload-hash analyze/repair/export

## Boundary

- Use `src/api/admin-rksv/client.ts` as the single adapter entrypoint for RKSV/admin features.
- Keep API contracts aligned with generated types from `src/api/generated/model`.
- Use `src/api/admin-rksv/query-keys.ts` for query key naming and invalidation.
- Keep page components focused on UI state and presentation only.

## Rules

- Prefer generated admin endpoints (`src/api/generated/admin/admin.ts`) under the adapter.
- Avoid page-local `customInstance` calls for covered RKSV/admin routes.
- Keep error text extraction consistent via `extractApiErrorMessage`.
- Keep file download behavior in adapter helpers (`downloadFiscalExportJson`, `downloadOfflinePayloadHashExportCsv`).
- Use canonical admin payment endpoints (`/api/admin/payments/*`) for admin payment UI via `@/api/generated/admin/admin`. (The former `src/api/legacy/` admin shim was removed; POS compatibility stays in the mobile app under `frontend/services/api/`.)

## Query Key Conventions

- Root namespace: `['admin', 'rksv', ...]`
- Domain slices:
  - `finanzonline-reconciliation`
  - `incident`
  - `replay-batch`
  - `integrity`
  - `offline-intent-coverage`
  - `offline-payload-hash`
- Dashboard quick cards continue to use `['rksv-operations', ...]` but are built from the same adapter.

## Migration Pattern

When moving a page:

1. Replace direct page-level HTTP calls with adapter function imports.
2. Replace ad hoc query keys with `rksvAdminQueryKeys`.
3. Replace local request/response type aliases with generated model types where available.
4. Remove obsolete wrappers once no imports remain.
