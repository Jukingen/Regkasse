# Project Summary

## Product identity
Regkasse is an internal POS operations/compliance platform, not a public dealership/brochure/marketplace site.

It consists of:
- mobile POS client
- web admin console
- backend API and domain services

## Applications

### backend
- **Purpose:** central API and domain logic.
- **Main stack:** ASP.NET Core/.NET, EF Core, PostgreSQL, JWT/Identity, permission-based authorization.
- **Main responsibility:** auth/session, business/domain rules, persistence, fiscal/RKSV/TSE/FinanzOnline behavior, reporting, API contract surface.

### frontend (POS)
- **Purpose:** operator-facing mobile POS app.
- **Main stack:** Expo + React Native.
- **Main responsibility:** table/cart/payment execution, receipt-facing POS flows, stable consumption of POS API boundaries.

### frontend-admin
- **Purpose:** web backoffice/admin console.
- **Main stack:** Next.js App Router, Ant Design, TanStack Query, Orval-generated client.
- **Main responsibility:** catalog/inventory/users/roles/reporting operations, RKSV/FinanzOnline operational support surfaces.

## Core workflows
- login/session lifecycle
- table/cart/modifier flow
- payment execution
- receipt creation and fiscal signing
- invoice/receipt read-only and investigation surfaces
- daily/period closing
- admin catalog/inventory/users/reporting operations
- RKSV/TSE/FinanzOnline operational monitoring and reconciliation flows
- backup/restore/DR surfaces (where documented)

## Technical stack
- **Backend:** ASP.NET Core/.NET, EF Core, PostgreSQL, JWT/Identity, permission-based authorization
- **POS:** Expo/React Native
- **Admin:** Next.js App Router, Ant Design, TanStack Query, Orval

## API boundary rules
- POS client should prefer `/api/pos/*`.
- Admin client should prefer `/api/admin/*`.
- Shared auth endpoints may exist where already established.
- Legacy aliases must not be expanded.

## High-risk areas
- payment
- receipt
- fiscal/RKSV/TSE/FinanzOnline
- daily closing
- auth/RBAC
- offline replay
- OpenAPI/Orval contract
- backup/restore

## Canonical documentation map
- `docs/architecture/FINAL_AUTHORIZATION_MODEL.md`
- `frontend-admin/docs/FINANZONLINE_ADMIN_SOURCE_OF_TRUTH.md`
- `frontend-admin/docs/rksv-truth-matrix.md`
- `frontend-admin/docs/CONTRACT_TRUTH_SURFACES.md`
- `docs/architecture/archive/`
- `frontend-admin/docs/archive/`
- `frontend/archive/`

## Development guardrails
- Make small, reversible changes.
- Preserve API contracts and boundary intent.
- Do not casually change fiscal/payment/auth behavior.
- Use separate context/chat and explicit risk notes for high-risk work.
- Preserve generated client workflow (OpenAPI → Orval).
- Prefer canonical API boundaries over legacy aliases.

## Current documentation status
- Historical planning/deliverable docs were archived under architecture/admin/frontend archive folders.
- Pointer stubs were created for selected historical entry points.
- Stale admin stack claim in `gemini/GEMINI.md` was corrected to current admin stack.
- Remaining human-review docs:
  - `docs/architecture/POS_AUTHORIZATION_DESIGN_PHASE1.md`
  - `frontend-admin/docs/OPERATOR_COPY_AND_RUNTIME_I18N.md`
  - `frontend-admin/docs/RKSV_TRUTH_SURFACES_QA_CHECKLIST.md`
  - `frontend-admin/src/features/users/README_TESTS.md`
