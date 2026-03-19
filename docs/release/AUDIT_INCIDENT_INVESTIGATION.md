# Audit log for incident investigation

Support can find root cause with **one query** by correlation ID. This document describes the audit fields and how to use them.

## Correlation ID everywhere

All replay- and FinanzOnline-related audit entries set `CorrelationId` so that:

- **Replay batch:** `CorrelationId` = `ReplayBatchCorrelationId.ToString("N")` (32-char hex, no dashes).
- **Lock timeout:** Same batch correlation is used; the timeout audit row is included when querying by that id.
- **Payment/Receipt:** When payment is created from replay, `LogPaymentOperationAsync` receives the same correlation; when from retry job, `CorrelationId` = `OfflineReplayBatchCorrelationId` (if present) or `PaymentId.ToString("N")`.
- **FinanzOnline submit/retry:** Each attempt is logged with the same correlation (batch id or payment id), so all FO attempts for a payment or batch appear in one query.

### Single query (support)

```sql
SELECT id, action, entity_type, entity_id, status, description, request_data, response_data, timestamp
FROM audit_logs
WHERE correlation_id = '<32-char-hex-or-payment-id>'
ORDER BY timestamp;
```

Admin UI: **RKSV → Verifications** with `?correlationId=<value>` shows the same set.

## Replay path marker

Each replayed item is resolved by one of four paths. The path is stored in audit so support can see whether hash match, recompute, or structural fallback was used.

| Value | Meaning |
|-------|--------|
| `requested_id` | OfflineTransaction was found by the client-provided OfflineTransactionId. |
| `hash_match` | Resolved by (CashRegisterId, PayloadHash); dedup audit `PAYLOAD_HASH_DEDUPLICATED` includes `replayPath`. |
| `recompute` | Resolved by recomputing runtime hash from stored PayloadJson. |
| `structural` | Last-resort structural payload match (when `AllowStructuralFallback` is true). |

**Where it appears**

- **OFFLINE_SYNCED:** `responseData.replayPath`, `responseData.payloadRepaired`.
- **PAYLOAD_HASH_DEDUPLICATED:** `requestData.replayPath`, `responseData.replayPath`.
- **OFFLINE replay failed audits:** `requestData.replayPath`, `responseData.replayPath` (e.g. `PAYLOAD_IMMUTABLE_MISMATCH`, `MAX_RETRY_LIMIT_EXCEEDED`, `OFFLINE_REPLAY_FAILED_FINAL`, `OFFLINE_REPLAY_EXCEPTION_FINAL`).

## Payload repair flag

When the stored `payload_hash` was legacy and we aligned it to the runtime canonical hash during replay, the audit records `payloadRepaired: true`.

- **OFFLINE_SYNCED:** `responseData.payloadRepaired` (boolean).
- **OFFLINE failed:** `responseData.payloadRepaired` so support can see if repair was applied before the failure.

This helps distinguish “replay used clean hash” vs “replay repaired hash then succeeded/failed”.

## FinanzOnline retry history

Every FinanzOnline submit (initial and background retry) is written to the audit log:

- **Action:** `FinanzOnlineSubmit` (first attempt) or `FinanzOnlineRetry` (background job).
- **EntityType:** `Payment`, **EntityId:** payment id.
- **responseData:** `Attempt`, `Success`, `ReferenceId`, `FailureKind`, `ErrorMessage`, `CorrelationId`.
- **CorrelationId:** Replay batch id (if payment came from replay) or payment id, so one query returns all FO attempts for that payment/batch.

Support can see the full retry history (attempt 1, 2, …) and final success/failure without scanning application logs.

## Summary: one-query root cause

1. Get the correlation id from the user (replay response `replayBatchCorrelationId` or payment id).
2. Query `audit_logs WHERE correlation_id = '<id>' ORDER BY timestamp`.
3. From the result set:
   - **Replay path:** Check `replayPath` in OFFLINE_SYNCED / PAYLOAD_HASH_DEDUPLICATED / failed action response data.
   - **Payload repair:** Check `payloadRepaired` in OFFLINE_SYNCED or failed replay audits.
   - **FO history:** Filter `action IN ('FinanzOnlineSubmit','FinanzOnlineRetry')` and read `Attempt`, `Success`, `FailureKind`, `ErrorMessage`.

No need to join multiple systems; the same correlation id ties replay, payment, receipt, and FinanzOnline attempts together.
