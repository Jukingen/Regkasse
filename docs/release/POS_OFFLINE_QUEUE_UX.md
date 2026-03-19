# POS: Offline Queue Management Screen

## Overview

Operator-facing offline payment queue visibility and recovery UX on the mobile/POS frontend. The queue model and backend replay are unchanged; only UI and recovery flows were added.

## Changed / New Files

| File | Change |
|------|--------|
| `frontend/services/payment/pendingPaymentQueue.ts` | Added `getAllQueueEntries()`, `retrySinglePending(queueId)`. |
| `frontend/services/payment/offlineQueueSyncNotifier.ts` | **New.** Subscribe/notify for background sync complete (reconnect). |
| `frontend/hooks/useApiManager.ts` | After sync, calls `notifyOfflineSyncComplete(processed, failed)`. |
| `frontend/app/(tabs)/_layout.tsx` | Subscribes to sync complete; shows Alert with summary when `processed > 0`. |
| `frontend/app/(screens)/offline-queue.tsx` | **New.** Full queue screen: list, filters, retry single, sync all, copy queue ID. |
| `frontend/app/(tabs)/settings.tsx` | Section "Offline-Warteschlange" + link to queue screen. |
| `docs/release/POS_OFFLINE_QUEUE_UX.md` | This doc. |

## Queue State Model (unchanged)

- **Storage:** `@regkasse/offline_transactions_v1` (AsyncStorage).
- **Entry:** `PendingPaymentEntry`: `queueId`, `createdAt`, `cashRegisterId`, `paymentRequest`, `status`, `syncedPaymentId`, `lastAttemptAt`, `lastError`, `deviceId`, `clientSequenceNumber`.
- **Status:** `Pending` | `Synced` | `Failed`.
- **Flow:** Payment fails (e.g. network) → response `fiscalStatus: NON_FISCAL_PENDING` → `enqueuePendingPayment()` → entry stays `Pending`. Background (useApiManager health check) or manual "Sync all" calls `syncPendingPaymentQueue()` → POST `/api/offline-transactions/replay` → entries updated to Synced/Failed/Pending with `lastError` and optional `syncedPaymentId`.
- **Invariant:** Offline entries never contain receipt number/signature; fiscal artifact is only created on server after successful replay.

## Recovery UX

1. **Entry point:** Settings → "Offline-Warteschlange" → "Warteschlange öffnen" → navigates to `/(screens)/offline-queue`.
2. **List:** All entries (Pending, Synced, Failed), newest first. Per row: date, amount, status badge, last attempt time, error summary, synced payment ID (if any).
3. **Filters:** All | Ausstehend (Pending) | Fehlgeschlagen (Failed). Counts in chip labels.
4. **Actions:**
   - **Alle synchronisieren:** Calls `syncPendingPaymentQueue()`, then Alert with processed/failed count.
   - **Erneut senden** (per row): Calls `retrySinglePending(queueId)` for Pending/Failed only; no duplicate payment (backend idempotent).
   - **Queue-ID kopieren:** Share sheet with `queueId` (for support/debug).
5. **User-friendly text:**
   - Status: "In Warteschlange" | "Synchronisiert" | "Fehlgeschlagen".
   - Error summary: e.g. "Verbindung fehlgeschlagen", "Bereits übertragen / Duplikat", "Noch nicht an Server gesendet" instead of raw codes/NON_FISCAL_PENDING.
6. **Reconnect auto-sync:** When `checkOnlineStatus()` succeeds, `syncPendingPaymentQueue()` runs in background. If `processed > 0`, `notifyOfflineSyncComplete(processed, failed)` runs; tab layout subscriber shows Alert: "X Zahlung(en) erfolgreich synchronisiert" (and failed count if any). No toast dependency; Alert is sufficient.

## Duplicate Payment Safety

- Queue screen does not create or modify payment intent; it only triggers replay (single or batch). Backend replay is idempotent (e.g. duplicate offline id → same payment).
- "Erneut senden" is only shown for Pending/Failed; Synced rows have no retry button. Copy ID does not trigger any payment.

## Test Scenarios

1. **Open queue:** Settings → Warteschlange öffnen → list loads (or empty state).
2. **Filter:** Switch All / Ausstehend / Fehlgeschlagen; list updates, counts correct.
3. **Sync all:** With Pending entries, tap "Alle synchronisieren" → loading → Alert with result; list refreshes.
4. **Retry single:** Tap "Erneut senden" on one Pending/Failed row → loading on button → Alert; row updates to Synced or keeps Failed with new lastError.
5. **Copy ID:** Tap "Queue-ID kopieren" → Share sheet or Alert with queueId.
6. **Reconnect alert:** With app on tabs and Pending entries, disconnect network then reconnect; after health check, background sync runs; Alert "X Zahlung(en) erfolgreich synchronisiert" appears.
7. **Empty state:** No entries → "Keine Einträge in der Offline-Warteschlange."
8. **User-friendly error:** Create a Pending entry, force a known error (e.g. transport); open queue → error summary shows short German text, not raw code.

## Backend / Domain

- No backend changes. Replay API and DTOs unchanged.
- Fiscal artifact and offline intent model unchanged; only operator visibility and recovery actions added.
