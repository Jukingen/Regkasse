# Truth-surface automated tests

**Run:** `npm run test:contract` (includes `src/shared/contract`, all `src/shared/__tests__`, invoice list sort coercion).

## Intent

Catch regressions where admin UI would: hide backend FK text, treat display labels as machine IDs, mis-build investigation URLs, infer retry when the contract status does not allow it, or blur audit vs batch correlation for Verifications.

## File → behavior map

| File | Semantics under test |
|------|----------------------|
| `truthSurfaceBehavior.test.ts` | `kassenId` never drives `finanzQueueRegisterRowId`; FO row without `cashRegisterId` has no invented link id; cross-link URL encoding; `viewReplayBatchTraceIds` audit-only vs fallback policy |
| `adminTruthBadge.semantic.test.tsx` | `aria-label` exposes German provenance (API vs Anzeige vs Verknüpft vs Diagnose vs Ohne Link) including fiscal-negation copy |
| `investigationNavigation.test.ts` | FO investigation href drops invalid payment UUID; Verifications href shape |
| `foReconciliationRowTriage.test.ts` | FO status → retry UI state mirrors button contract; `Success` ≠ retry |
| `rksvAdminTruth.test.ts` | Register view + replay trace link building |
| `adminTruthBadges.test.ts` | Every kind has non-empty copy; no `success` color for lineage |
| `contract/*` | `invoiceItems` unknown shaping; Axios error narrowing |
| `invoiceListSort.test.ts` | Unknown table sort field does not pass through to API params |
| `adminTruthFacets.test.ts` | `registerDeepLinkEligibleBadgeKind`; `invoiceProvenanceUiFacet` explicit vs contract-gap |

## Not covered here (integration / E2E)

See `docs/RKSV_TRUTH_SURFACES_QA_CHECKLIST.md` — full page RTL, React Query invalidation spies, real 403 bodies, FO retry round-trip to backend.
