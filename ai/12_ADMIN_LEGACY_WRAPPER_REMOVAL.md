# Admin legacy wrapper status

## Current status (2026-05-04)

- The `frontend-admin/src/api/legacy/` folder is not in the repo (already removed).
- Admin consumption is the generated client (`src/api/generated/**`) plus the `src/api/admin/**` helper layer.

## Multi-tenant architecture

- Super Admin tenant UI: `src/features/super-admin/`, route `/admin/tenants` — keep generated/manual clients aligned with `/api/admin/tenants`.

## What still needs attention

- Generated surfaces that still emit legacy paths (especially `generated/cart`) must still be watched and migrated.
- Do not rely only on the transformer strip list to erase legacy paths; migrate real consumers.

## Validation

- `node scripts/verify-api-client.mjs`
- `cd frontend-admin && npm run test:contract`
- `cd frontend-admin && npm run build`
