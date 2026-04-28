# FinanzOnline Operations Console (Admin)

> **Status:** Secondary operations-console note. Canonical FinanzOnline admin source of truth: `frontend-admin/docs/FINANZONLINE_ADMIN_SOURCE_OF_TRUTH.md`.

This note summarizes the operations-console scope only. For source-of-truth semantics, use the canonical document above.

## Current console scope (diagnostic/support)

- `GET /api/FinanzOnline/status`
- `GET /api/FinanzOnline/config`
- `POST /api/FinanzOnline/test-connection`
- `GET /api/FinanzOnline/errors`
- `GET /api/FinanzOnline/history/{invoiceId}`

## Historical notes (legacy/deprecated context)

- `POST /api/FinanzOnline/submit-invoice` is intentionally excluded from admin console actions.
- References to this endpoint are historical/deprecated and are not the preferred operational path.
- Payment-level operational action remains reconciliation/retry flows.
