# RKSV / Invoice / Reconciliation — Manual QA Checklist

> **Status:** NEEDS HUMAN REVIEW. QA checklist may still be operational. Canonical RKSV truth matrix: `frontend-admin/docs/rksv-truth-matrix.md`.

**Scope:** `/invoices`, `/rksv/finanz-online-queue`, `/rksv/incident`, `/rksv/replay-batch/[correlationId]` (plus cross-links to Verifications, Payments, Receipts).

**Canonical operator-facing German copy:** `src/shared/operatorTruthCopy.ts` (and badge tooltips via `adminTruthBadges` → `OPERATOR_TRUTH_BADGE`).

**Template:** `docs/QA_TRUTH_CRITICAL_TEMPLATE.md` · **Contract gate:** `docs/CONTRACT_TRUTH_SURFACES.md` · **Canonical vs runtime i18n:** `docs/OPERATOR_COPY_AND_RUNTIME_I18N.md` · **Roles reference:** `ROLE_MANAGEMENT_QA_CHECKLIST.md`

---

## Automated test coverage (today)

Run: `npm run test:contract` (plus full `npm test` before release).

| Area                                                          | Files (indicative)                                                                                                                                                                                 |
| ------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Contract / navigation helpers                                 | `src/shared/__tests__/rksvAdminTruth.test.ts`, `src/shared/__tests__/investigationNavigation.test.ts`, `src/shared/__tests__/foReconciliationRowTriage.test.ts`, `src/shared/contract/__tests__/*` |
| Truth semantics (register, links, replay policy, badges a11y) | `src/shared/__tests__/truthSurfaceBehavior.test.ts`, `src/shared/__tests__/adminTruthBadge.semantic.test.tsx`                                                                                      |
| Invoice sort coercion                                         | `src/features/invoices/utils/__tests__/invoiceListSort.test.ts`                                                                                                                                    |
| Truth badges copy                                             | `src/shared/__tests__/adminTruthBadges.test.ts`                                                                                                                                                    |

**Gap:** No RTL tests yet for `InvoiceList`, `finanz-online-queue`, `incident`, or `replay-batch` pages — see **Proposed automated test areas** at the end.

---

## Backend (API) — spot-check (staging)

Use admin-capable user. Adjust IDs to your environment.

- [ ] **GET `/api/Invoice/list`** — 200; pagination; optional `cashRegisterId`, `status`, date range.
- [ ] **GET `/api/Invoice/{id}`** — 200 for valid id; 404 for missing id (invoice detail modal should surface error path).
- [ ] **POST `/api/admin/finanzonline-reconciliation/retry/{paymentId}`** — success + failure shapes match UI (toast/modal).
- [ ] **GET `/api/admin/finanzonline-reconciliation`** — 200; filters `status`, `cashRegisterId`, `fromUtc`/`toUtc` respected.
- [ ] **GET `/api/admin/finanzonline-reconciliation/metrics`** — 200; metrics cards populate.
- [ ] **GET `/api/admin/incidents/{correlationId}`** — 200 when batch exists; empty/404 behavior matches Incident page (info alert, not fake data).
- [ ] **GET `/api/admin/replay-batch/{correlationId}`** — 200; payments array shape matches table.
- [ ] **403 / 401** — With user lacking admin rights, endpoints fail; UI shows error or redirect (document actual product behavior).

---

## Frontend — `/invoices` (Invoice list + detail modal)

### Initial render & states

- [ ] **Loading:** Table shows loading; no false “empty” flash that implies no data in DB.
- [ ] **Error:** Break API (or invalid base URL) → red Alert **„Liste konnte nicht geladen werden“**; **description shows `Error.message`** when thrown; action button **„Erneut laden“** refetches.
- [ ] **Empty:** Filters that match no rows → Ant Design empty state (date range message).
- [ ] **Invalid date range:** Validation message; query not sent (`dateRangeError`).

### List — truth & register

- [ ] **Register FK:** Rows with invalid `cashRegisterId` show **Ohne Link** badge when applicable; valid UUID shows **API** badge and safe FO handoff links.
- [ ] **„Nur ohne gültige Register-UUID (kein Link-FK)“** filter narrows to non-link-safe rows only.
- [ ] **Register filter footnote** matches **„Freitext wird unverändert an die Listen-API übergeben; Deep-Links zur Abgleichsseite nur mit gültiger UUID.“**
- [ ] **TSE (Präfix)** column: tooltip shows full signature; no truncation claimed as full value.
- [ ] **Storno-Ref:** `originalInvoiceId` shown when present.

### Detail modal

- [ ] **Loading:** **„Rechnungsdetails werden geladen…“** while `useGetApiInvoiceId` pending.
- [ ] **Provenance strip:** Register line shows **API** or **Ohne Link**; **Kassen-ID** line shows **Anzeige** badge; labels use **„Register (Maschinenbezug)“** where applicable.
- [ ] **Herkunft** paragraph text matches **`OPERATOR_INVOICE_COPY.detailProvenanceFooter`** (Maschinenbezug `cashRegisterId` vs `kassenId`, API liefert Persistenz vs. Ableitung nicht als Feld; Verweis auf technische Doku — kein `RKSv_ADMIN_CONTRACT_GAPS`-Code im UI-String).
- [ ] **Correlation:** When `correlationId` present — links **„Incident (Aggregat)“** + **„Replay-Batch-Detail“** open correct routes with same id.
- [ ] **„FinanzOnline-Abgleich (mit URL-Kontext)“:** URL includes safe `cashRegisterId` when UUID valid; includes `focusPaymentId` / `investigationBatchCorrelationId` when UUID rules allow (see FO queue checklist).
- [ ] **Positionen / Artikel:** Panel title says **OpenAPI: unknown** (dynamic); Alert title **„Vertragslage (Positionen)“** with contract-gap description from `RKSv_ADMIN_CONTRACT_GAPS`; invalid JSON → **warning** Alert with parse message; valid array → JSON preview only (no fake line-item types).
- [ ] **Unsafe register UUID in detail:** Warning title **„Register-Feld vom Server ohne link-sichere UUID“** (`OPERATOR_REGISTER_LINK_COPY`).

### Mutations & invalidation

- [ ] **Reconciliation Retry** (footer): triggers POST retry; success → handoff modal opens correct FO URL; **invalidates** FinanzOnline queries + invoice detail query on success path.
- [ ] **Retry failure:** Modal/error path still offers FO link with same investigation params.
- [ ] **Batch reconciliation** (multi-select): completion handoff includes first successful payment id + correlation when present.
- [ ] **Credit note:** 409 → warning toast; 400 → message from API when possible; form validation does not show generic failure.

### Stale / refetch

- [ ] List **reload** control shows tooltip **„Daten manuell aktualisieren (Cache kann veraltet sein).“**; after external FO status change, refetch updates data (or document cache `staleTime` if observation differs).

### Negative

- [ ] Open detail for deleted id → empty/error handled without silent blank fiscal fields.
- [ ] Export/print errors: user sees message (401/404 paths).

---

## Frontend — `/rksv/finanz-online-queue`

### Initial render & states

- [ ] **Loading:** Table and/or metrics show spinners; no crash.
- [ ] **Error:** List or metrics failure → red Alert with message.
- [ ] **Empty:** No rows for filters → info Alert **„Keine Abgleichszeilen“** / description **„Keine Zahlungen für die gewählten Filter. Status oder Zeitraum anpassen.“**
- [ ] **Metrics:** Cards show numbers or loading state consistently.

### Filters & URL

- [ ] **cashRegisterId** in URL: valid register UUID pre-fills Kasse; **invalid** UUID → Alert **„Register-Parameter ungültig“** (`OPERATOR_FO_QUEUE_COPY`); API not sent misleading filter.
- [ ] **focusPaymentId** invalid → Alert **„Zahlungs-Fokus verworfen“**.
- [ ] **investigationBatchCorrelationId** present → banner **„Untersuchungskontext (nur Anzeige, kein API-Filter)“**; body matches **`OPERATOR_INVESTIGATION_CONTEXT_COPY.bannerBody`**; links **„Incident (Aggregat)“** + **„Replay-Batch-Detail“**; **„URL mit aktuellen Filtern übernehmen“** present.
- [ ] **focusPaymentId** valid + row in result set → row **highlighted** (light blue); if payment outside limit/window → highlight absent; copy under **„Fokus-Zahlung (nur Hervorhebung)“** explains no “missing from system” claim.

### Row-level & retry

- [ ] **Erneut senden** only for statuses aligned with `getFinanzOnlineRetryUiState` (Pending, Failed, NeedsReconciliation); **not** for Submitted.
- [ ] **FO-Aktion (UI)** tag/tooltip matches same rule (no extra client-side “terminal” inference).
- [ ] **Expand row:** Full FO error, timestamps, ref; DTO gap note if correlation/actor absent.
- [ ] Retry success/failure → toast; **invalidateQueries** on `rksvAdminQueryKeys.finanzOnline` base + metrics.

### Provenance

- [ ] Register column: raw FK visible; link to FO queue only if UUID policy satisfied (or honest disabled state).

### Stale / refetch

- [ ] **Aktualisieren** (header) shows tooltip **„Daten manuell aktualisieren (Cache kann veraltet sein).“**; invalidates list + metrics.

### Negative

- [ ] Network drop during retry → error toast; table can refetch without stale button stuck.

---

## Frontend — `/rksv/incident`

### Initial render & states

- [ ] **Search:** Paste correlation (with/without dashes, optional `RKSV_HANDOFF_V1:` payload) → normalized search.
- [ ] **Loading:** Spin tip **„Lade Incident-Aggregat…“**.
- [ ] **Error:** API failure → error Alert **„Incident-Aggregat konnte nicht geladen werden“** with message.
- [ ] **Not found:** Info Alert **„Kein Incident-Aggregat“** / description **„Für diese Correlation-ID liefert die API kein zusammengefasstes Ergebnis — nicht als „keine Daten in der Kasse“ interpretieren.“**

### With batch data

- [ ] **OperatorSummaryStrip:** Batch vs Audit correlation labeled; FO line uses aggregate wording **„FinanzOnline (Aggregat über den Incident-Endpunkt): … keine Zeilen-genaue FO-Wahrheit.“** (`OPERATOR_INCIDENT_COPY.foAggregateLine`).
- [ ] **Weiter untersuchen:** **„Replay-Batch-Detail“**, **„Verifications (Audit)“**, **„FinanzOnline-Abgleich (Kontext)“**; helper matches **„Abgleichszeilen enthalten keine Batch-Correlation — die URL übernimmt sie nur zur Orientierung zwischen Incident, Replay und dieser Ansicht.“**
- [ ] **Payments table:** FO column tooltips from `OPERATOR_INCIDENT_COPY` (Status/Join, FO-Aktion, Zeiten, FO-Ref, Kasse FK); expandable row DTO note from **`OPERATOR_INCIDENT_COPY.expandDtoNote`**.
- [ ] **Timeline:** Audit entries; replay meta parsed without assuming non-object JSON (dev console warning only if shape wrong).

### Deep links

- [ ] Open `/rksv/incident?correlationId=<valid>` directly → same as in-app search.

### Negative

- [ ] Garbage correlation → not-found or validation behavior; no infinite loading.

---

## Frontend — `/rksv/replay-batch/[correlationId]`

### Initial render & states

- [ ] **Loading:** **„Lade Batch-Details…“** when applicable.
- [ ] **Error:** Alert **„Batch konnte nicht geladen werden“** with message.
- [ ] **Empty:** **„Keine Batch-Details für diese Correlation-ID.“** when no data.
- [ ] **Empty payments:** Table handles empty array without crash.

### Investigation path card

- [ ] Card title **„Untersuchungspfad (getrennte Datenquellen)“**; intro matches **`OPERATOR_REPLAY_COPY.investigationPathIntro`** (separate APIs, keine eine Wahrheit).
- [ ] **API** badge note: **„Batch- und Audit-Correlation auf dieser Seite (API)“**.
- [ ] **Verifications:** If `auditCorrelationId` missing → inline copy **„Verifications: `auditCorrelationId` fehlt —** link **„Audit-Filter mit Batch-Correlation“** **„(kann weniger oder andere Treffer liefern).“**
- [ ] **„FinanzOnline-Abgleich (Kontext)“** opens with batch correlation in URL only (display context).

### Row expand

- [ ] Payments card Alert **„Zeilen ohne Erfolgs-/Fehler-Felder“**; body after `ReplayBatchPaymentItemDto` matches **`OPERATOR_REPLAY_COPY.paymentsDtoGapBody`**; expand shows correlation ids + payment/offline ids for copy.

### Observability

- [ ] Footnotes: Coverage / OFFLINE_SYNCED / FINAL_FAILURE use **`OPERATOR_REPLAY_COPY.observability*`** (FINAL_FAILURE: Audit-Label, keine Backend-Endgültigkeits-Garantie).

### Links

- [ ] Payment → `/payments?paymentId=…`; Receipt → `/receipts/{id}` open correctly.

---

## Cross-screen — deep-link & provenance (smoke)

1. From **invoice detail** with correlation → **Incident (Aggregat)** → **Replay-Batch-Detail** → confirm same **Batch-Correlation** text in headers.
2. From **Replay-Batch** detail → **FO queue (Kontext)** → banner shows correlation; adjust Kasse filter → **„URL mit aktuellen Filtern übernehmen“** preserves context + valid params.
3. From **FO queue** banner → **Incident** → back to FO via browser history; URL params still honest about API filter scope.
4. **Verifications** `?correlationId=` → banner **„Audit-Logs (gefiltert, Correlation-Parameter)“** + **Diagnose** badge; intro **`OPERATOR_VERIFICATIONS_COPY.diagnosticLine`**; **„Weiter untersuchen:“** links **„Incident (Aggregat)“**, **„Replay-Batch-Detail“**, **„FinanzOnline-Abgleich (Kontext)“**.

---

## Proposed automated test areas (RTL / Vitest)

| Priority | Target                         | What to assert                                                                                                                                                                                                    |
| -------- | ------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| P1       | `InvoiceList`                  | Error alert **„Liste konnte nicht geladen werden“**; description `Error.message`; **„Erneut laden“** refetch; mock `useQuery` error state.                                                                        |
| P1       | `finanz-online-queue`          | `useSearchParams` mock: rejected `cashRegisterId` → **„Register-Parameter ungültig“**; `investigationBatchCorrelationId` → banner **„Untersuchungskontext (nur Anzeige, kein API-Filter)“** + Incident link href. |
| P2       | `incident` page                | Loading → notFound → success with minimal `IncidentInvestigationResponse` mock; **„Weiter untersuchen:“** link hrefs.                                                                                             |
| P2       | `replay-batch/[correlationId]` | `auditCorrelationId` null → **„Audit-Filter mit Batch-Correlation“** link text + **„kann weniger oder andere Treffer liefern)“** fragment present.                                                                |
| P3       | Mutations                      | Spy `queryClient.invalidateQueries` after FO retry success (mock mutation), matching `rksvAdminQueryKeys.finanzOnline.base`.                                                                                      |

Use **`@testing-library/react`** + **`QueryClientProvider`**; mock `next/navigation` `useSearchParams` / `useParams` as in `users/page.test.tsx`.

---

## Gaps needing backend / product support

- [ ] **Central RKSV admin policy hook** (like `useUsersPolicy`) — until then, permission rows stay partially manual per environment.
- [ ] **Stable 403 body** for admin routes — UI can then show consistent German operator message + English log reference.
- [ ] **Invoice list error payload** — if API returns structured problem details, surface them in Alert description (today: `Error.message` + fallback **„Keine technische Detailmeldung verfügbar.“**).
- [ ] **FO list** — no `correlationId` filter on API; deep-link context remains UI-only until OpenAPI adds optional filter (documented in UI).
- [ ] **E2E** — Playwright/Cypress against real staging for TSE/FO signing not covered by unit tests; keep manual smoke for fiscal end-to-end.
