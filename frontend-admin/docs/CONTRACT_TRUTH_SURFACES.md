# Contract discipline — truth-critical admin surfaces

These UI areas must stay aligned with **Orval-generated** types under `src/api/generated/`. Silent drift (casts to `any`, duplicate DTOs, swallowing parse errors) is treated as a release risk.

**QA (manual + automation plan):** `docs/RKSV_TRUTH_SURFACES_QA_CHECKLIST.md` · **Automated truth tests:** `docs/TRUTH_SURFACE_AUTOMATED_TESTS.md` · **Reusable QA template:** `docs/QA_TRUTH_CRITICAL_TEMPLATE.md`

## In-scope paths

- `src/app/(protected)/rksv/**` — incident, replay batch, FinanzOnline reconciliation, verifications, integrity, etc.
- `src/features/invoices/**` — invoice list/detail, reconciliation handoff.
- `src/shared/rksvAdminTruth.ts`, `src/shared/contract/**` — contract helpers and gap documentation.

## Release checklist (manual + CI)

1. Run `npm run generate:api` (orval) after backend OpenAPI changes; commit regenerated `src/api/generated/**`.
2. Run `npm run build` (includes `tsc` via Next) and `npm test`.
3. Run `npm run lint` — contract surfaces use stricter `@typescript-eslint/no-explicit-any` (warn); prefer fixing new warnings before release.
4. For invoice detail: confirm `Invoice.invoiceItems` is still typed `unknown` in OpenAPI until a real schema exists; UI must use `normalizeInvoiceItemsForDisplay`, not ad-hoc casts.
5. Cross-check `RKSv_ADMIN_CONTRACT_GAPS` in `src/shared/rksvAdminTruth.ts` for documented OpenAPI follow-ups.

## OpenAPI / backend sources of truth

- Single spec consumed by Orval (see `orval.config.*` in repo).
- Do not add parallel “admin DTO” interfaces for the same endpoints; extend the spec and regenerate.

## Response-only / weakly typed fields (today)

| Area | Field | Notes |
|------|--------|--------|
| Invoice detail | `invoiceItems` | OpenAPI: `unknown \| null`. Display via `shared/contract/invoiceInvoiceItemsDisplay.ts`. |
| Invoice list | `sortBy` / `sortDir` | Generated list params use `string`; frontend narrows with `INVOICE_LIST_SORT_FIELDS` until enum is in spec. |
