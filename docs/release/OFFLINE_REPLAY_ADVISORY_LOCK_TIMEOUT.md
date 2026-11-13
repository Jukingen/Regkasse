# Offline replay advisory lock — timeout and safety

## Purpose

Avoid deadlock-like long waits: replace `pg_advisory_lock` with **try-lock + retry**, wait at most **max wait**, then fail the client with LOCK_TIMEOUT plus audit + log.

## Behavior

1. **Try-lock + retry:** Non-blocking `pg_try_advisory_lock`; on failure wait `LockRetryIntervalMs` (default 100 ms) and retry.
2. **Max wait:** If total wait exceeds `MaxLockWaitMs` (default 10 s), the lock is not taken.
3. **After timeout:**
   - Throw `OfflineReplayLockTimeoutException`.
   - `OfflineTransactionService`: log (Warning, wait duration + register ids), audit (`LogSystemOperationAsync`: action `OfflineReplayLockTimeout`, status Failed, requestData: WaitDurationMs, RegisterIds), fail every batch item with `ErrorCode: LOCK_TIMEOUT` and message "Advisory lock timeout; try again later."
4. **Log:**
   - When a lock is acquired after waiting: `"Offline replay advisory lock acquired after {WaitDurationMs}ms. ReplayBatchCorrelationId=..."`.
   - Timeout: `"Offline replay advisory lock timeout after {WaitDurationMs}ms for register(s) {RegisterIds}. ReplayBatchCorrelationId=..."`.

## Config

- **OfflineReplay:MaxLockWaitMs** — Max wait (ms). Default 10000 (10 s).
- **OfflineReplay:LockRetryIntervalMs** — Wait between attempts (ms). Default 100.

## Tests

- **AdvisoryLock_SecondAcquireWaitsUntilFirstScopeDisposed** — Second acquire waits until the first is released (try+retry, default 10 s timeout).
- **AdvisoryLock_Timeout_WhenHolderKeepsLockLongerThanMaxWait** — One instance holds the lock longer than max wait; the second times out; `OfflineReplayLockTimeoutException` and WaitDurationMs/RegisterIds are asserted.
- **AdvisoryLock_AcquireSucceedsWhenLockFree_WaitDurationZeroOrSmall** — Acquire succeeds immediately when nobody holds the lock; `WaitDurationMs` is 0 or very small.

Tests Skip when PostgreSQL (Docker or `REGKASSE_TEST_POSTGRES`) is missing.

## Changed files

| File | Change |
|------|--------|
| `backend/Options/OfflineReplayOptions.cs` | MaxLockWaitMs, LockRetryIntervalMs. |
| `backend/Services/OfflineReplayLockTimeoutException.cs` | New: WaitDurationMs, CashRegisterIds. |
| `backend/Services/OfflineReplayRegisterLock.cs` | pg_try_advisory_lock + retry loop, exception on timeout; WaitDurationMs on the scope. |
| `backend/Services/OfflineTransactionService.cs` | Timeout options on acquire; catch: audit + log + all items LOCK_TIMEOUT. |
| `backend/appsettings.json` | OfflineReplay: MaxLockWaitMs, LockRetryIntervalMs. |
| `backend/KasseAPI_Final.Tests/PostgreSqlOfflineReplayConcurrencyTests.cs` | AdvisoryLock_Timeout_..., AdvisoryLock_AcquireSucceedsWhenLockFree_.... |
