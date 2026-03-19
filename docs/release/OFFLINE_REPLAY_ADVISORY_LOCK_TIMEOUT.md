# Offline replay advisory lock — timeout and safety

## Amaç

Uzun süreli (deadlock benzeri) beklemeleri engellemek: `pg_advisory_lock` yerine **try-lock + retry** ile en fazla **max wait** süresi kadar beklenir; aşılırsa audit + log + client’a LOCK_TIMEOUT ile fail dönülür.

## Davranış

1. **Try-lock + retry:** `pg_try_advisory_lock` ile non-blocking deneme; başarısızsa `LockRetryIntervalMs` (varsayılan 100 ms) bekleyip tekrar denenir.
2. **Max wait:** Toplam bekleme süresi `MaxLockWaitMs` (varsayılan 10 s) aşılınca lock alınmaz.
3. **Timeout sonrası:**
   - `OfflineReplayLockTimeoutException` fırlatılır.
   - `OfflineTransactionService`: log (Warning, wait duration + register ids), audit (`LogSystemOperationAsync`: action `OfflineReplayLockTimeout`, status Failed, requestData: WaitDurationMs, RegisterIds), tüm batch item’ları `ErrorCode: LOCK_TIMEOUT`, mesaj "Advisory lock timeout; try again later." ile fail döner.
4. **Log:**
   - Lock alındığında bekleme varsa: `"Offline replay advisory lock acquired after {WaitDurationMs}ms. ReplayBatchCorrelationId=..."`.
   - Timeout: `"Offline replay advisory lock timeout after {WaitDurationMs}ms for register(s) {RegisterIds}. ReplayBatchCorrelationId=..."`.

## Config

- **OfflineReplay:MaxLockWaitMs** — Max bekleme (ms). Varsayılan 10000 (10 s).
- **OfflineReplay:LockRetryIntervalMs** — Denemeler arası bekleme (ms). Varsayılan 100.

## Testler

- **AdvisoryLock_SecondAcquireWaitsUntilFirstScopeDisposed** — İkinci acquire, ilki bırakana kadar bekler (try+retry ile, varsayılan 10 s timeout).
- **AdvisoryLock_Timeout_WhenHolderKeepsLockLongerThanMaxWait** — Bir instance lock’u max wait’ten uzun tutar; ikinci timeout alır, `OfflineReplayLockTimeoutException` ve WaitDurationMs/RegisterIds doğrulanır.
- **AdvisoryLock_AcquireSucceedsWhenLockFree_WaitDurationZeroOrSmall** — Kimse lock almamışken acquire hemen başarılır, `WaitDurationMs` 0 veya çok küçük.

PostgreSQL (Docker veya `REGKASSE_TEST_POSTGRES`) yoksa testler Skip edilir.

## Değişen dosyalar

| Dosya | Değişiklik |
|-------|------------|
| `backend/Options/OfflineReplayOptions.cs` | MaxLockWaitMs, LockRetryIntervalMs. |
| `backend/Services/OfflineReplayLockTimeoutException.cs` | Yeni: WaitDurationMs, CashRegisterIds. |
| `backend/Services/OfflineReplayRegisterLock.cs` | pg_try_advisory_lock + retry döngüsü, timeout’ta exception; scope’a WaitDurationMs. |
| `backend/Services/OfflineTransactionService.cs` | Acquire’da timeout options; catch’te audit + log + tüm item’lar LOCK_TIMEOUT. |
| `backend/appsettings.json` | OfflineReplay: MaxLockWaitMs, LockRetryIntervalMs. |
| `backend/KasseAPI_Final.Tests/PostgreSqlOfflineReplayConcurrencyTests.cs` | AdvisoryLock_Timeout_..., AdvisoryLock_AcquireSucceedsWhenLockFree_.... |
