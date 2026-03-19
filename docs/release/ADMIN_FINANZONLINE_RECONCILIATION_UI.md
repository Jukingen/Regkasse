# Admin: FinanzOnline Reconciliation Console

## Overview

The admin RKSV "FinanzOnline Queue" screen was replaced by a **FinanzOnline Abgleich** (reconciliation) console that uses the existing backend reconciliation API. The UI supports filtering, metrics, row-level retry, and links to payment/receipt.

## Changed / New Files

| File | Change |
|------|--------|
| `frontend-admin/src/api/finanzonline-reconciliation.ts` | **New.** Types and API functions for list, metrics, retry. |
| `frontend-admin/src/app/(protected)/rksv/finanz-online-queue/page.tsx` | **Replaced.** Old queue/status UI → full reconciliation console. |
| `frontend-admin/src/app/(protected)/layout.tsx` | Nav label: "FinanzOnline Queue" → "FinanzOnline Abgleich". |
| `frontend-admin/src/api/replay-batch.ts` | Fixed: use `customInstance({ url, method })` instead of `customInstance.get()`. |
| `docs/release/ADMIN_FINANZONLINE_RECONCILIATION_UI.md` | This doc. |

## Component Structure

- **Page:** `frontend-admin/src/app/(protected)/rksv/finanz-online-queue/page.tsx`
  - Single page component (no extra subcomponents yet).
  - Uses `AdminPageHeader`, Ant Design `Card`, `Table`, `Select`, `DatePicker.RangePicker`, `Statistic`, `Button`, `Tag`, `Alert`, `Spin`.
- **API layer:** `frontend-admin/src/api/finanzonline-reconciliation.ts`
  - `getReconciliationList(params)`, `getReconciliationMetrics()`, `retryReconciliationSubmit(paymentId)`.
  - Types: `FinanzOnlineReconciliationItemDto`, `FinanzOnlineReconciliationListResponse`, `FinanzOnlineRetryResponse`, `FinanzOnlineMetricsResponse`, `GetReconciliationListParams`.

## Endpoints Used

| Method | Endpoint | Purpose |
|--------|----------|---------|
| GET | `/api/admin/finanzonline-reconciliation` | List items. Query: `status`, `cashRegisterId`, `fromUtc`, `toUtc`, `limit`. |
| GET | `/api/admin/finanzonline-reconciliation/metrics` | Submit totals and failure counts (transient/permanent/unknown). |
| POST | `/api/admin/finanzonline-reconciliation/retry/{paymentId}` | Manual retry for one payment. |
| GET | `/api/CashRegister` | Cash register list for filter dropdown (response: `{ registers: [...] }`). |

## Features

- **Status filters:** Pending, Failed, NeedsReconciliation, Submitted (multi-select).
- **Date range:** Optional from/to UTC for `createdAt`.
- **Cash register filter:** Optional; dropdown from GET `/api/CashRegister`.
- **Table columns:** Belegnummer (with link to receipt search), Zahlung (link to payments), Status (badge), Retries, Letzter Versuch (UTC), Fehlermeldung, Referenz (FO), Betrag, Aktionen (Erneut senden for Pending/Failed/NeedsReconciliation).
- **Row retry:** Safe, non-optimistic: button loading state, then success/warning/error message and list/metrics refetch.
- **Metrics cards:** Submit gesamt, Fehlgeschlagen gesamt, Transient, Permanent (from `/metrics`).
- **Loading / empty / error:** Spin when loading list without data; Alert when error; Alert when no items match filters.

## FailureKind and Status

- **Status** is shown as a badge per row (from `finanzOnlineStatus`).
- **FailureKind** is not returned per row by the list endpoint; it is only in the retry response. If needed in the table later, the backend could add `finanzOnlineFailureKind` to `FinanzOnlineReconciliationItemDto`.

## RKSV Navigation Suggestion

Current RKSV submenu: General Status, CMC/Certificate, Last 100 Verifications, **FinanzOnline Abgleich**, Fiscal-Export Diagnose, Replay-Batch. This keeps FO reconciliation under RKSV and is consistent. If the admin info architecture grows, consider a dedicated "Compliance / FO" group or moving "FinanzOnline Abgleich" next to "Fiscal-Export Diagnose" as the main FO ops entry.

## Test Plan

1. **Load**
   - Open `/rksv/finanz-online-queue` as user with `finanzonline.view`.
   - Expect metrics cards and filter bar; list loads (or empty state).
2. **Filters**
   - Change status (e.g. add Submitted); list updates.
   - Select a cash register if available; list filters.
   - Set date range; list filters by `createdAt`.
3. **Retry**
   - Pick a row with Status Pending or Failed; click "Erneut senden".
   - Expect button loading, then success/warning toast and list refresh.
   - If backend returns 403 (no `finanzonline.submit`), expect error toast.
4. **Links**
   - Click Zahlung link → `/payments?paymentId=...`.
   - Click "Belege suchen" → `/receipts?receiptNumber=...`.
5. **Permissions**
   - Without `finanzonline.view`: list/metrics should 403 (handled by backend + auth).
   - Retry requires `finanzonline.submit`; otherwise 403.
6. **Replay-batch**
   - Open `/rksv/replay-batch/[correlationId]` and confirm batch detail still loads (replay-batch API fix).

## Backend Compatibility

- No backend changes required for this UI.
- Optional future: add `finanzOnlineFailureKind` to list DTO and `receiptId` for direct receipt detail link.
