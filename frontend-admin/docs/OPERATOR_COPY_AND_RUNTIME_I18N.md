# Operator canonical copy vs runtime i18n

> **Status:** NEEDS HUMAN REVIEW. Validate freshness before treating as current source of truth.

This note defines **who owns which strings** so German operator wording does not drift silently between TypeScript reference blocks and locale catalogs.

## Layers (do not merge)

| Layer | Location | Role |
|-------|----------|------|
| **Canonical operator (German)** | `src/shared/operatorTruthCopy.ts` (`OPERATOR_*` constants) | Long-form RKSV / investigation / badge / truth copy. Many `/rksv/*` pages render these literals **directly** — fixed German until a route is explicitly migrated to `t(...)`. |
| **Runtime UI (de / en / tr)** | `src/i18n/locales/{de,en,tr}/*.json` | Source for translatable admin UI (`useI18n` / `t('namespace.key')`). |
| **Raw backend** | `BackendRawTextBlock`, `extractRawApiErrorMessage`, etc. | Untranslated server text; keep separate from translated summaries. |
| **Technical logs** | `technicalConsole` | English only. |

## Shared “list / toolbar / incident empty” strings

`OPERATOR_SHARED_COPY` in `operatorTruthCopy.ts` is a **reference mirror** of German entries in `de/common.json` (same spelling, including ASCII transliterations like `verfuegbar`, `fuer`). It is **not** imported by feature code today; use **`t('common.*')`** in components.

| Canonical field (`OPERATOR_SHARED_COPY`) | Runtime key (`de` catalog) |
|------------------------------------------|----------------------------|
| `unknownErrorDetail` | `common.messages.noTechnicalDetail` |
| `loadFailedList` | `common.loadErrors.list` |
| `loadFailedBatch` | `common.loadErrors.batch` |
| `loadFailedIncident` | `common.loadErrors.incidentAggregate` |
| `notFoundIncidentTitle` | `common.incident.aggregateNotFoundTitle` |
| `notFoundIncidentDescription` | `common.incident.aggregateNotFoundDescription` |
| `loadingIncident` | `common.loading.incidentAggregate` |
| `loadingBatchDetail` | `common.loading.batchDetail` |
| `loadingInvoiceDetail` | `common.loading.invoiceDetail` |
| `emptyBatchForCorrelation` | `common.empty.batchDetailsForCorrelation` |
| `refetchHintToolbar` | `common.toolbar.refetchHint` |
| `investigateFurtherLabel` | `common.investigation.furtherLabel` |
| `retryLoadShort` | `common.buttons.reload` |
| `toolbarRefresh` | `common.buttons.refresh` |
| `retryAfterError` | `common.buttons.retry` |

When you change German wording for these concepts, update **both** the table row above (if you keep `OPERATOR_SHARED_COPY` in sync) and `de/common.json`.

## Invoices (`/invoices`)

- **Runtime:** `t('invoices.*')` → `invoices.json` per locale.
- **Canonical de-DE anchor:** `OPERATOR_INVOICE_COPY` in `operatorTruthCopy.ts`.
- **Rule:** For any key that exists in both places, **`de/invoices.json` must match** the German in `OPERATOR_INVOICE_COPY` (EN/TR are translations of that meaning).

**Explicit pairing (truth-critical):**

- `OPERATOR_INVOICE_COPY.detailProvenanceFooter` ↔ `invoices.detail.provenanceOperatorFooter`  
  Used from `invoiceProvenanceUiFacet` (`adminTruthFacets.ts`) via `t('invoices.detail.provenanceOperatorFooter')` when the OpenAPI client has no typed provenance field.

**Intentional duplication:** CSV header row (`OPERATOR_INVOICE_COPY.csvExportHeaderRow` vs `invoices.export.csvHeaderRow`) must stay identical in German — export column contract for operators.

## RKSV routes still on `OPERATOR_*` literals

Pages such as FinanzOnline queue, incident, replay batch, verifications, and FO operations import **`OPERATOR_FO_QUEUE_COPY`**, **`OPERATOR_VERIFICATIONS_COPY`**, etc. That is **intentional**: authoritative German operator prose for those flows. It is **not** accidental drift vs `rksvHub.*` keys where both exist — see per-page comments (e.g. verifications page: long procedural text vs short UI keys).

## QA

- Checklist item for invoice provenance footer: `docs/RKSV_TRUTH_SURFACES_QA_CHECKLIST.md` (align with `OPERATOR_INVOICE_COPY.detailProvenanceFooter`).
- After editing `de/*.json` for paired keys, run `npm run i18n:validate` in `frontend-admin`.

## Automated parity (CI-safe)

Vitest file `src/shared/__tests__/operatorCopyDeLocaleParity.test.ts` asserts **documented mirrors only**:

- every `OPERATOR_SHARED_COPY` field listed in the table above vs `de/common.json`;
- `OPERATOR_INVOICE_COPY.detailProvenanceFooter` vs `invoices.detail.provenanceOperatorFooter`;
- `OPERATOR_INVOICE_COPY.csvExportHeaderRow` vs `invoices.export.csvHeaderRow`.

Helper: `src/shared/__tests__/helpers/readDeLocaleJson.ts` (Node `fs` + path from test file — no `NEXT_PUBLIC_*`, no axios).

Run: `npm run test:operator-copy-locale-parity` in `frontend-admin`.

## Related docs

- `docs/CONTRACT_TRUTH_SURFACES.md` — contract-first truth surfaces.
- `docs/RKSV_TRUTH_SURFACES_QA_CHECKLIST.md` — manual QA for RKSV admin copy.
