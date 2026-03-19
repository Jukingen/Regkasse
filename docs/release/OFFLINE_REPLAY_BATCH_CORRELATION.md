# Offline replay batch correlation

## Lifecycle

1. **Start:** `POST .../offline/replay` with a non-empty `transactions` array → server generates `Guid` **once** per HTTP request.
2. **Scope:** Same id for every item in that request: response root, each result row, structured logs (`ReplayBatchCorrelationId`), offline-related **system** audit rows (`AuditLog.CorrelationId` = 32-char hex without dashes).
3. **Payment path:** When replay calls `CreatePaymentAsync` with `offlineReplayBatchCorrelationId`, `payment_details.offline_replay_batch_correlation_id` is set (nullable; only offline replay).
4. **Payment/receipt audits:** `PaymentCreated` / `ReceiptPersisted` use the same `CorrelationId` when the payment was created in that replay batch.

## Support use case (incident investigation)

1. User reports failed sync; app shows or logs `replayBatchCorrelationId` from last replay API response.
2. In DB: `SELECT * FROM audit_logs WHERE correlation_id = '<32hex>' ORDER BY timestamp` → all offline system events, payment/receipt, and FinanzOnline attempts for that batch.
3. Join payments: `SELECT * FROM payment_details WHERE offline_replay_batch_correlation_id = '<uuid>'`.
4. Fiscal export JSON: `receipts[].offlineReplayBatchCorrelationId` (schema 1.3); CSV column `offline_replay_batch_correlation_id`.

Audit entries now include **replay path** (`requested_id` / `hash_match` / `recompute` / `structural`), **payload repair flag**, and **FinanzOnline submit/retry history** so support can find root cause with one query. See **AUDIT_INCIDENT_INVESTIGATION.md**.

## API fields (additive)

| Location | Field |
|----------|--------|
| `ReplayOfflineTransactionsResponse` | `replayBatchCorrelationId` (null if empty request) |
| Each item | `replayBatchCorrelationId` |

## Backward compatibility

- New nullable DB column; existing payments unchanged.
- Clients ignoring new JSON fields continue to work.
- Fiscal export schema **1.3** adds optional receipt field and CSV column.

## Migration

`20260319002427_OfflineReplayBatchCorrelation` — adds `payment_details.offline_replay_batch_correlation_id`.
