# Module: Offline transactions (legacy — TSE payment intents)

## Scope

- **`offline_transactions`** table — server-side non-fiscal payment intent queue (TSE replay backlog)
- POS replay: `POST /api/offline-transactions/replay`
- Admin: `/api/admin/offline-transactions/*`
- FA page: **`/admin/tse/offline-transactions`** (Fiskal / TSE area — **not** RKSV offline orders)

**Do not conflate** with `offline_orders` / `/rksv/offline-orders`. See [`docs/release/OFFLINE_SYSTEMS_SEPARATION.md`](../../docs/release/OFFLINE_SYSTEMS_SEPARATION.md).

## Multi-tenant

- `offline_transactions.tenant_id` NOT NULL; stamped from cash register / ambient tenant on insert.
- Cross-tenant admin access → HTTP **404** (tenant-scoped joins via cash register).

## API boundaries

| Client | Prefix | Notes |
|--------|--------|-------|
| POS | `/api/offline-transactions/replay` | Batch replay of stored intents |
| Admin | `/api/admin/offline-transactions/*` | List, summary, retry, export-failed |

**Forbidden for this module:** `/api/pos/offline-orders/*`, `/api/admin/offline-orders/*`, `OfflineOrderService`.

### Admin (`AdminOfflineTransactionsController`)

| Method | Path | Permission |
|--------|------|------------|
| `GET` | `/api/admin/offline-transactions/summary` | `payment.view` |
| `GET` | `/api/admin/offline-transactions` | `payment.view` |
| `GET` | `/api/admin/offline-transactions/export-failed` | `payment.view` |
| `POST` | `/api/admin/offline-transactions/{id}/retry` | `payment.view` |
| `POST` | `/api/admin/offline-transactions/retry-all` | `payment.view` |

## Business rules (high risk)

1. **Voucher** — must not enter NonFiscalPending offline queue (`PaymentService.TseOffline`).
2. **TSE offline limit** — max 50 transactions per register (`TseOptions.MaxOfflineTransactionsPerCashRegister`).
3. **Replay** — advisory lock per register; payload hash dedup; audit + correlation IDs.
4. **Do not** migrate order-snapshot logic here; use `offline_orders` instead.

## Key files

| Area | Path |
|------|------|
| Model | `backend/Models/OfflineTransaction.cs` |
| Service | `backend/Services/OfflineTransactionService.cs` |
| POS replay | `backend/Controllers/OfflineTransactionsController.cs` |
| Admin | `backend/Controllers/AdminOfflineTransactionsController.cs` |
| POS queue | `frontend/services/payment/pendingPaymentQueue.ts` |
| FA page | `frontend-admin/src/app/(protected)/admin/tse/offline-transactions/page.tsx` |

## Related RKSV / diagnostics (legacy only)

- `/rksv/offline-intent-coverage` — device/sequence coverage **samples from intent replay**
- `/rksv/replay-batch` — batch correlation for **intent** replay audits

These tools do **not** apply to `offline_orders`.

## Human documentation

- [`docs/release/OFFLINE_SYSTEMS_SEPARATION.md`](../../docs/release/OFFLINE_SYSTEMS_SEPARATION.md)
- [`docs/release/POS_OFFLINE_QUEUE_UX.md`](../../docs/release/POS_OFFLINE_QUEUE_UX.md)
