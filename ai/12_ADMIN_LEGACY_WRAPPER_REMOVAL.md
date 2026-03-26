# Admin legacy API wrapper removal

**Status:** Completed (frontend-admin).  
**Related:** `ai/10_API_BOUNDARY_POLICY.md`, `ai/11_OPENAPI_CONTRACT_GOVERNANCE.md`

## What was removed

- **`frontend-admin/src/api/legacy/`** (entire folder): `payment.ts`, `index.ts`, `README.md`.
- **Rationale:** No application code imported these modules; payments already used `useGetApiAdminPayments`, `postApiAdminPaymentsIdCancel`, etc. from `@/api/generated/admin/admin`. The wrappers only re-exported POS `/api/pos/payment/*` hooks and manual cancel/refund — redundant with admin endpoints and unused.

## What remains (not legacy wrappers)

- **`src/lib/axios.ts`**: `normalized` error attachment for all API calls — intentional cross-cutting behavior, not route-specific compatibility.
- **`features/receipts/api/forensics-client.ts`**: Maps generated `ReceiptDTO` to admin view models; tightens loose OpenAPI gaps (documented in `rksvAdminTruth.ts` contract gaps).
- **Page-local helpers** (e.g. `getPaymentsListErrorMessage` on payments page): read `error.normalized` from axios; keep until a shared `extractApiErrorMessage` is used app-wide.

## Acceptance criteria (issue)

| Criterion | Result |
|-----------|--------|
| Obsolete compatibility code removed for migrated routes | `src/api/legacy/*` deleted |
| Admin uses generated client directly | **Payments** already use `@/api/generated/admin/admin` |
| No critical regression | Run `npm run build` / `npm run test:contract` in `frontend-admin` after changes |

## Docs updated

- `frontend-admin/docs/rksv-admin-api-conventions.md`
- `frontend-admin/docs/payment-boundary-hardening.md`
- `frontend-admin/docs/CLEANUP_AND_CONSISTENCY_REPORT.md`
- `frontend-admin/docs/LEGACY_API_BOUNDARY_DELIVERABLE.md` (superseded banner)
