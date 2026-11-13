# QA template — truth-critical admin surfaces

Derived from the discipline used in **`ROLE_MANAGEMENT_QA_CHECKLIST.md`** (users/roles) and extended for **fiscal/truth** risks: provenance, deep-links, and contract alignment.

## How to use

1. Copy this structure per feature area (one doc or one major section per route family).
2. At the top, list **automated** coverage (Vitest file paths + what they assert).
3. Split **Backend (API)** from **Frontend** so failures are triaged quickly.
4. For each screen, always cover the **state matrix**: loading → error → empty → success → stale/refetch.
5. Add **negative** cases (4xx/5xx, invalid query params, missing permissions) and **truth** cases (badges, “kein API-Filter”, disabled links).

## Section order (mirror roles style)

| Section                         | Purpose                                                                                         |
| ------------------------------- | ----------------------------------------------------------------------------------------------- |
| **Automated coverage**          | Pointers to tests; avoid duplicating generic “unit tests exist”.                                |
| **Backend (API)**               | Endpoint, expected status, role/capability; matrix of success + failure codes.                  |
| **Frontend — &lt;Route&gt;**    | One subsection per page or drawer.                                                              |
| **Initial render & states**     | Loading, error alert, empty table/message, success with data.                                   |
| **Mutations / actions**         | Save, delete, retry — success toast + `invalidateQueries` expectations where relevant.          |
| **Permissions / visibility**    | What non-admin users must not see (if applicable).                                              |
| **Deep links & URL discipline** | Query/path params, rejected invalid UUIDs, context params that do **not** filter API.           |
| **Provenance & lineage**        | `AdminTruthBadge` (API / Anzeige / Verknüpft / Diagnose / Ohne Link); no single merged “truth”. |
| **Row-level failure**           | Expandable row, FO error text, retry button gating — no invented retryability.                  |
| **Stale / refetch**             | Toolbar “Aktualisieren”, mutation success → list/detail refresh.                                |
| **Quick smoke**                 | 3–6 numbered steps for release train.                                                           |

## Truth-specific additions (not in generic QA)

- **Authoritative vs derived**: checklist steps must name the badge or label (e.g. “Register (Maschinenbezug)” + API badge).
- **Contract gaps**: reference `RKSv_ADMIN_CONTRACT_GAPS` / `docs/CONTRACT_TRUTH_SURFACES.md` when UI shows “unknown” or parse warnings.
- **Correlation / replay**: same ID across Incident, Replay-Batch URL, Verifications query — explicit “open each and compare header ID”.
- **FinanzOnline queue**: `investigationBatchCorrelationId` and `focusPaymentId` — confirm banner text states **no server filter**.

## Automated test plan (per feature)

When adding Vitest/RTL tests for truth surfaces, prefer:

- **Query keys**: assert `invalidateQueries` is called with the same namespace as `rksvAdminQueryKeys` / invoice keys after mutations.
- **Pure helpers**: `normalizeInvoiceItemsForDisplay`, `buildFinanzOnlineQueueInvestigationHref`, `getFinanzOnlineRetryUiState`, `viewReplayBatchTraceIds` — table-driven cases.
- **Guards**: invalid `cashRegisterId` in URL → info alert text (mock `useSearchParams`).
- **Permissions**: mock policy hook if introduced; otherwise manual-only until centralized RKSV policy exists.

Avoid relying on full fiscal E2E in unit tests; mark those **manual** or **Playwright (future)**.
