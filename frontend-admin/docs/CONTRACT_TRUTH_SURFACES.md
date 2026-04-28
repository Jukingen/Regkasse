# Contract discipline — truth-critical admin surfaces

> **Status:** Umbrella contract/truth-surface map. This is not the canonical source for every domain-specific truth.
> - Canonical FinanzOnline admin source: `frontend-admin/docs/FINANZONLINE_ADMIN_SOURCE_OF_TRUTH.md`
> - Canonical RKSV truth matrix: `frontend-admin/docs/rksv-truth-matrix.md`

These UI areas must stay aligned with **Orval-generated** types under `src/api/generated/`. Silent drift (casts to `any`, duplicate DTOs, swallowing parse errors) is treated as a release risk.

**QA (manual + automation plan):** `docs/RKSV_TRUTH_SURFACES_QA_CHECKLIST.md` · **Automated truth tests:** `docs/TRUTH_SURFACE_AUTOMATED_TESTS.md` · **Reusable QA template:** `docs/QA_TRUTH_CRITICAL_TEMPLATE.md` (pointer) · **Operator copy vs i18n catalogs:** `docs/OPERATOR_COPY_AND_RUNTIME_I18N.md`

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

## Register identity field map (semantic)

| Field / param | Source (DTO or UI) | Meaning | Class |
|---------------|-------------------|---------|--------|
| `cashRegisterId` | `Invoice`, `InvoiceListItemDto`, `ReceiptDTO` / list item, `FinanzOnlineReconciliationItemDto`, FO join on incident | Backend register FK / `cash_registers` row id as string; **may be non-UUID** | Authoritative raw FK (show always when present) |
| `kassenId` | `Invoice`, `InvoiceListItemDto` | Display / RegisterNumber-style text for operators | Display-only (never sole input to FO URL filter) |
| `kassenID` | `ReceiptDTO` (detail) | Same role as kassen display; mapped to `registerDisplayNumber` in forensics | Display-only |
| `registerDisplayNumber` | `ReceiptDetailDto`, list row (optional) | View-model display slot; list often empty until OpenAPI adds list field | Display-only / unknown |
| `registerRowId` (helper arg) | Passed into `buildFinanzOnlineQueuePath` / `buildFinanzOnlineQueueInvestigationHref` | Must be API `cashRegisterId` string; only **link-safe** subset is serialized as query `cashRegisterId` | Use `toLinkSafeRegisterRowId(apiCashRegisterId)` at call sites |
| Query `cashRegisterId` | FO queue URL → `finanz-online-queue` page | Machine filter: only set from `parseAuthoritativeRegisterGuid` | Authoritative when present |
| `focusPaymentId` (query) | Investigation hrefs | Payment UUID for row highlight; same UUID gate as register | Authoritative when accepted |
| `investigationBatchCorrelationId` | Investigation hrefs | Opaque operator context; truncated; **not** a register id | Context-only (not FK) |
| Replay batch register column | `FinanzOnlineReconciliationItemDto` via `foByPayment` | FO row’s `cashRegisterId`; badge `derived_from_foreign_row` | Authoritative raw from joined row; link policy same as above |

Policy module: `src/shared/utils/registerIdentity.ts`. View helpers: `viewInvoiceListRegister`, `viewFinanzReconciliationRegister` in `rksvAdminTruth.ts`.

## Admin truth model matrix (screens × concepts)

Rows are truth-critical surfaces; columns are **independent** facets (not a single rolled-up status). Compose UI from multiple facets where needed.

| Concept | Invoice list/detail | FO queue | Incident | Replay batch detail | Verifications |
|--------|---------------------|----------|----------|----------------------|---------------|
| **Record origin** | Invoice DTO / list item (Orval) | FO reconciliation row | Incident aggregate (`getApiAdminIncidents…`) | Replay batch detail DTO | Audit log list / correlation filter |
| **Provenance (invoice row)** | `invoiceProvenanceUiFacet` → explicit JSON `invoiceDataProvenance` **or** documented OpenAPI gap | N/A (FO status, not invoice provenance) | N/A | N/A | N/A |
| **Authoritative id availability** | `cashRegisterId` raw always shown when present; UUID subset for links | Same on row DTO | FO join supplies `cashRegisterId` on payment row | Batch `correlationId` / `auditCorrelationId` for trace links | `correlationId` query drives filtered list |
| **Display-only identifier** | `kassenId` + badge `display_only_label` | Register column is FK text (truncated when UUID); no separate display field in DTO | N/A in FO column | N/A | N/A |
| **Deep-link safe (register)** | `registerDeepLinkEligibleBadgeKind` + `toLinkSafeRegisterRowId` / `viewInvoiceListRegister` | Same badge helper on reconciliation column | FO-derived cell: `derived_from_foreign_row` + same UUID rules for links | FO context link often **without** register param | FO link usually context-only |
| **Joined / aggregate / diagnostic** | List row is direct DTO | Direct DTO | Replay payments **joined** to FO → derived badge | Aggregate batch + observability counts | Diagnostic: `diagnostic_support` + client keyword filter (non-contract) |
| **Incomplete contract / fallback** | `invoiceItems` unknown; provenance footer; register link omitted if FK not UUID | Empty FK → `—` + `link_incomplete` | Missing FO → short path | Verifications link fallback copy when audit id missing | Filter semantics = best-effort |

Shared facet module: `src/shared/adminTruthFacets.ts` (register deep-link badge + invoice provenance facet). Register analysis remains in `registerIdentity.ts` / `rksvAdminTruth.ts`.

## Response-only / weakly typed fields (today)

| Area | Field | Notes |
|------|--------|--------|
| Invoice detail | `invoiceItems` | OpenAPI: `unknown \| null`. Display via `shared/contract/invoiceInvoiceItemsDisplay.ts`. |
| Invoice list | `sortBy` / `sortDir` | Generated list params use `string`; frontend narrows with `INVOICE_LIST_SORT_FIELDS` until enum is in spec. |
| Receipt signature debug | whole response | OpenAPI types GET `/api/Receipts/{id}/signature-debug` as `unknown`; forensics normalizes with runtime checks only. |
| Invoice detail | `invoiceDataProvenance` | Backend may serialize `Persisted` / `DerivedFromPayment` before the OpenAPI `Invoice` schema lists it; UI reads via `shared/contract/invoiceDetailResponseExtensions.ts` until Orval includes the field. |

## Release gate — truth-critical admin (practical rules)

1. **Block merge (or fail CI)** if `npm run generate:api` produces a diff in `src/api/generated/**` that was not committed after a backend OpenAPI change touching invoice list/detail, FO reconciliation, incident aggregate, replay batch, receipts, or audit-log correlation endpoints. Treat uncommitted generated output as contract drift.
2. **Fail loudly in development:** use React Query devtools during QA on these routes; keep load-failure `Alert` patterns instead of silent empty tables.
3. **Safe UI degradation:** placeholders (`—`), omitting deep links when UUID policy fails, `normalizeInvoiceItemsForDisplay` parse-error branch, and “OpenAPI: unknown” copy for line items — honest degradation, not invented DTO fields.
4. **Do not merge** new `as any` / unchecked widening casts on truth surfaces without a tracked OpenAPI follow-up in `RKSv_ADMIN_CONTRACT_GAPS` or removal after regeneration.
5. **Before release:** run `npm run test:truth-surfaces` and `npm run test:contract`; spot-check `docs/RKSV_TRUTH_SURFACES_QA_CHECKLIST.md` for cross-links.

## Contract-sensitive components (quick checklist)

- [ ] `InvoiceList.tsx` — `viewInvoiceListRegister`, `normalizeInvoiceItemsForDisplay`, `buildFinanzOnlineQueueInvestigationHref`; sort via `coerceInvoiceListSortField`.
- [ ] `finanz-online-queue/page.tsx` — `viewFinanzReconciliationRegister` + investigation href builders.
- [ ] `incident/page.tsx` — typed Orval aggregate; `parseReplayMeta` only for unstructured audit JSON (documented).
- [ ] `replay-batch/[correlationId]/page.tsx` — `viewReplayBatchTraceIds` + shared link builders.
- [ ] `verifications/page.tsx` — correlation filter + shared investigation links.
- [ ] `forensics-client.ts` — `ReceiptDTO` mapping; signature-debug response until OpenAPI is typed.
- [ ] `adminTruthFacets.ts` — invoice provenance facet + register deep-link badge helper; keep incident `derived_from_foreign_row` composition separate.
