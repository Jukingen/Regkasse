# Offline Systems — Separation Guide

> **Purpose:** Prevent mixing two independent offline pipelines in Regkasse.  
> **Rule for agents:** Never extend `offline_transactions` APIs for order-snapshot work, and never route TSE backlog diagnostics through `offline_orders`.

---

## At a glance

| | **Legacy — TSE payment intents** | **New — Full order snapshots** |
|---|----------------------------------|--------------------------------|
| **Problem solved** | Non-fiscal payments queued while TSE unavailable; replay to obtain fiscal signature | Complete POS checkout saved when transport fails; replay as normal payment |
| **Database table** | `offline_transactions` | `offline_orders` |
| **Backend service** | `IOfflineTransactionService` / `OfflineTransactionService` | `IOfflineOrderService` / `OfflineOrderService` |
| **POS sync API** | `POST /api/offline-transactions/replay` | `/api/pos/offline-orders/*` |
| **Admin API** | `/api/admin/offline-transactions/*` | `/api/admin/offline-orders/*` |
| **Admin UI (FA)** | `/admin/tse/offline-transactions` (Fiskal / TSE section) | `/rksv/offline-orders` (RKSV → Diagnose) |
| **POS local queue** | `pendingPaymentQueue.ts` (`@regkasse/offline_transactions_v1`) | `offlineOrderManager.ts` / `offlineStorage.ts` |
| **Primary operator context** | TSE health, NonFiscalPending backlog, manual TSE replay | Order sync backlog, 72 h expiry, batch BelegNr reserve |
| **Related RKSV tooling** | `/rksv/offline-intent-coverage`, replay-batch, payload-hash | *(none of the intent-coverage tools)* |

**Both systems may write to `payment_details` on successful replay — that is the only shared outcome, not shared storage or APIs.**

---

## Legacy: `offline_transactions`

### When to use

- TSE offline / degraded mode: payment accepted **without** immediate fiscal signature
- Server-side **NonFiscalPending** queue and TSE replay chain
- Admin investigates “payments not yet fiscally signed”
- Dashboard **Offline queue** card, TSE health metrics

### Key backend files

| File | Role |
|------|------|
| `Models/OfflineTransaction.cs` | Entity |
| `Services/OfflineTransactionService.cs` | Replay, dedup, payload hash, advisory lock |
| `Controllers/OfflineTransactionsController.cs` | `POST /api/offline-transactions/replay` |
| `Controllers/AdminOfflineTransactionsController.cs` | Admin list, retry, export-failed |
| `Services/OfflineReplayHostedService.cs` | Background replay (if enabled) |

### Key frontend-admin files

| File | Role |
|------|------|
| `app/(protected)/admin/tse/offline-transactions/page.tsx` | Operator UI (de-DE copy) |
| Orval: `getApiAdminOfflineTransactions*`, `postApiAdminOfflineTransactionsIdRetry` | Generated client |

### Key POS files

| File | Role |
|------|------|
| `services/payment/pendingPaymentQueue.ts` | Local AsyncStorage queue |
| `app/(screens)/offline-queue.tsx` | Settings → offline queue screen |
| `docs/release/POS_OFFLINE_QUEUE_UX.md` | UX spec |

### Do **not**

- Store full order snapshots in `offline_transactions`
- Point `/rksv/offline-orders` at `/api/admin/offline-transactions`
- Use `OfflineOrderService` for TSE NonFiscalPending replay

---

## New: `offline_orders`

### When to use

- POS saves **full `CreatePaymentRequest` snapshot** when checkout cannot reach API
- Server persists row in `offline_orders`; replay creates payment via `PaymentService`
- Admin manages pending order sync under RKSV diagnostics

### Key backend files

| File | Role |
|------|------|
| `Models/OfflineOrder.cs` | Entity |
| `Services/Offline/OfflineOrderService.cs` | Save, replay, admin list |
| `Services/Offline/SequenceReservationService.cs` | Batch BelegNr reservation on replay |
| `Controllers/PosOfflineOrdersController.cs` | POS API |
| `Controllers/AdminOfflineOrdersController.cs` | Admin API |
| `Services/Hosted/OfflineOrderCleanupHostedService.cs` | Expired pending cleanup (72 h) |

### Key frontend-admin files

| File | Role |
|------|------|
| `app/(protected)/rksv/offline-orders/page.tsx` | RKSV panel (i18n de/en/tr) |
| Orval: `useGetApiAdminOfflineOrders`, `usePostApiAdminOfflineOrdersIdReplay`, `usePostApiAdminOfflineOrdersReplayAll` | Generated hooks |

### Key POS files

| File | Role |
|------|------|
| `services/offline/offlineOrderManager.ts` | Upload + replay orchestration |
| `services/offline/offlineStorage.ts` | Local persistence |
| `components/OfflineBanner.tsx` | German banner + manual sync |

### Do **not**

- Reuse `OfflineTransactionService` or `/api/offline-transactions/replay` for order snapshots
- Move this UI under `/admin/tse/*` (different operator task)
- Queue voucher payments offline (rejected in both systems)

---

## Verification checklist (code review)

Use this when reviewing PRs that touch “offline”:

- [ ] Table touched is **either** `offline_transactions` **or** `offline_orders`, not both in one feature
- [ ] Controller route prefix matches model (`/api/admin/offline-transactions` vs `/api/admin/offline-orders`)
- [ ] FA route stays in correct nav tree (`/admin/tse/*` vs `/rksv/*`)
- [ ] POS client calls correct API boundary (`/api/offline-transactions/replay` vs `/api/pos/offline-orders/*`)
- [ ] No shared service interface merging the two domains
- [ ] OpenAPI / Orval types use distinct DTO names (`AdminOfflineTransaction*` vs `AdminOfflineOrder*`)
- [ ] Tests name the system explicitly (`OfflineTransaction*` vs `OfflineOrder*`)

---

## Related documentation

| Doc | Topic |
|-----|--------|
| [`OFFLINE_ORDERS_FULL_SNAPSHOT.md`](OFFLINE_ORDERS_FULL_SNAPSHOT.md) | New system implementation |
| [`POS_OFFLINE_QUEUE_UX.md`](POS_OFFLINE_QUEUE_UX.md) | Legacy POS queue UX |
| [`OFFLINE_REPLAY_BATCH_CORRELATION.md`](OFFLINE_REPLAY_BATCH_CORRELATION.md) | Legacy replay correlation |
| [`DEVICE_SEQUENCE_COVERAGE.md`](DEVICE_SEQUENCE_COVERAGE.md) | Legacy intent coverage metrics |
| [`../../ai/modules/offline_orders.md`](../../ai/modules/offline_orders.md) | AI guardrails (new) |
| [`../../ai/modules/offline_transactions_legacy.md`](../../ai/modules/offline_transactions_legacy.md) | AI guardrails (legacy) |

---

**Last updated:** 2026-06-27
