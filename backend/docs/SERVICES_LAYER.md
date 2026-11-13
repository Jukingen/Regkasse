# Services layer audit notes

**Last reviewed:** 2026-07-21  
**Scope:** `backend/Services/`, DI in `ApplicationHost.cs`

## Findings (high level)

| Topic | Result |
|-------|--------|
| `PosPaymentService` | **Does not exist.** Fiscal POS payments = `IPaymentService` / `PaymentService`. Card gateway = `ICardPaymentService`. Online orders = `IOnlineOrderPaymentService`. No merge needed. |
| Product overlap | Intentional: `IProductService` (read/cache) vs `IAdminProductListService` (admin list filters). |
| Cart | No `ICartService`; `CartController` + `IPosCartTableOpsService` + `CartLifecycleService`. |
| Singleton → `AppDbContext` | No direct ctor injection found; Hosted/singleton paths use `IServiceScopeFactory`. |

## Cleanup applied (2026-07-21)

1. **Merged invoice allocation:** `BillingService` now uses `IInvoiceNumberGenerator.AllocateAsync` (`InvoiceSequences` table) — removed duplicated private allocator.
2. **Removed unused DI registrations:** `ITagesabschlussReportService`, Null/Start/Schluss report services, `IBackupService`, `IIncrementalBackupService`, `TableOrderService`, `IScopeCheckService` (claim constants remain on `ScopeCheckService`).
3. **Deleted dead types:** `DemoResetService` / `IDemoResetService` (controller uses inline SQL), orphan `IPhysicalPostgreSqlBackupExecutionAdapter`.
4. **DI hygiene:** `FakeBackupExecutionAdapter` requires `IBackupEncryptionService`; retention cleaner no longer `new SmartRetentionService()` when smart mode is on.
5. Removed commented `IPrinterService` / `ITestService` stubs from `ApplicationHost`.

## Remaining intentional test seams

- `ReceiptService` may construct `RksvEnvironmentService` when optional ctor args are omitted in unit tests; production DI always supplies `IRksvEnvironmentService`.
- `MemoryCacheService` is not registered (prod uses `RedisCacheService`); kept for unit tests.
- `BackupService` / `IncrementalBackupService` types remain for `BackupServiceTests`; production trigger path is `IBackupManualTriggerService` + orchestrator.

## Contributor rules

1. New features → inject via interfaces registered in `ApplicationHost` (or feature registration extension).
2. Do not `new` application services inside other services; use DI (Stripe SDK types are an exception).
3. Singletons that need EF → `IServiceScopeFactory.CreateScope()` then resolve `AppDbContext` / factory.
4. Do not register parallel facades that nobody injects; prefer deleting or wiring them.
