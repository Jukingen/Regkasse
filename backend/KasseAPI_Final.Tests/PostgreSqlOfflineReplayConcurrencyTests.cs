using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Http;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// PostgreSQL-backed integration tests for offline replay locking, idempotency, receipt sequence UPSERT,
/// signature chain FOR UPDATE serialization, and fiscal transaction rollback. Requires Docker or REGKASSE_TEST_POSTGRES.
/// These tests provide the proof for: (1) advisory lock serialization per register (AdvisoryLock_SecondAcquireWaitsUntilFirstScopeDisposed),
/// (2) concurrent replay + unique index (CashRegisterId, payload_hash) → single offline row and single payment (ConcurrentReplay_*),
/// (3) payment idempotency key unique index under concurrency (ConcurrentCreatePayment_SameIdempotencyKey_SingleRow_PostgresUniqueIndex),
/// (4) idempotent second replay after sync (SecondReplayAfterSync_ReturnsSyncedWithoutNewReceipt). Schema presence of the unique index
/// is validated by scripts/sql/fiscal_go_live_validation.sql (idx_offline_transactions_cash_register_payload_hash_unique).
/// </summary>
[Collection("PostgreSqlReplay")]
[Trait("Category", "PostgreSql")]
public sealed class PostgreSqlOfflineReplayConcurrencyTests
{
    private readonly PostgreSqlReplayFixture _fixture;

    public PostgreSqlOfflineReplayConcurrencyTests(PostgreSqlReplayFixture fixture)
    {
        _fixture = fixture;
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_fixture.ConnectionString)
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<(Guid categoryId, Guid productId, Guid customerId, Guid cashRegisterId)> SeedMinimalDomainAsync(AppDbContext ctx)
    {
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var cashRegisterId = Guid.NewGuid();

        ctx.Categories.Add(new Category
        {
            Id = categoryId,
            Name = "Speisen",
            VatRate = 10m
        });
        ctx.Products.Add(new Product
        {
            Id = productId,
            Name = "Döner",
            Price = 6.90m,
            CategoryId = categoryId,
            Category = "Speisen",
            StockQuantity = 1000,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 2,
            IsActive = true
        });
        ctx.Customers.Add(new Customer { Id = customerId, Name = "PGTest", Email = "pg@test.com", Phone = "1", IsActive = true });
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = cashRegisterId,
            RegisterNumber = "PG-K01",
            Location = "T",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await ctx.SaveChangesAsync();
        return (categoryId, productId, customerId, cashRegisterId);
    }

    private static CompanyProfileOptions TestCompany => new()
    {
        CompanyName = "PG Test",
        TaxNumber = "ATU12345678",
        Street = "S1",
        ZipCode = "1010",
        City = "Wien",
        FooterText = ""
    };

    private static (PaymentService payment, OfflineTransactionService offline) CreateServices(
        AppDbContext ctx,
        Mock<IAuditLogService> audit,
        ITseService tse,
        IReceiptSequenceService receiptSeq)
    {
        var paymentRepo = new GenericRepository<PaymentDetails>(ctx, Mock.Of<ILogger<GenericRepository<PaymentDetails>>>());
        var productRepo = new GenericRepository<Product>(ctx, Mock.Of<ILogger<GenericRepository<Product>>>());
        var customerRepo = new GenericRepository<Customer>(ctx, Mock.Of<ILogger<GenericRepository<Customer>>>());

        var finanzMock = new Mock<IFinanzOnlineService>();
        finanzMock.Setup(x => x.SubmitInvoiceAsync(It.IsAny<Invoice>()))
            .ReturnsAsync(new FinanzOnlineSubmitResponse { Success = true });

        var userMock = new Mock<IUserService>();
        userMock.Setup(x => x.GetUserByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(new ApplicationUser { Id = "u1", UserName = "cashier", Role = "Cashier", IsDemo = false });

        var receiptService = new ReceiptService(
            ctx,
            Mock.Of<ILogger<ReceiptService>>(),
            tse,
            Options.Create(TestCompany),
            userMock.Object);

        var cashRegResolver = new CashRegisterResolutionService(ctx, Mock.Of<ILogger<CashRegisterResolutionService>>());
        var httpAccessor = Mock.Of<IHttpContextAccessor>();
        var paymentService = new PaymentService(
            ctx,
            paymentRepo,
            productRepo,
            customerRepo,
            tse,
            finanzMock.Object,
            userMock.Object,
            new NoOpProductModifierValidationService(),
            receiptSeq,
            receiptService,
            audit.Object,
            Options.Create(TestCompany),
            Options.Create(new TseOptions { TseMode = "Demo" }),
            Mock.Of<ILogger<PaymentService>>(),
            cashRegResolver,
            httpAccessor);

        var offlineService = new OfflineTransactionService(
            ctx,
            paymentService,
            audit.Object,
            Mock.Of<ILogger<OfflineTransactionService>>());

        return (paymentService, offlineService);
    }

    private static void SetupAuditMocks(Mock<IAuditLogService> audit)
    {
        audit.Setup(x => x.LogPaymentOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<decimal?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<object?>(), It.IsAny<object?>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<double?>()))
            .ReturnsAsync(new AuditLog());
        audit.Setup(x => x.LogSystemOperationAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<AuditLogStatus>(), It.IsAny<string?>(),
                It.IsAny<object?>(), It.IsAny<object?>(), It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());
    }

    private static CreatePaymentRequest BuildPaymentRequest(Guid customerId, Guid cashRegisterId, Guid productId, string idempotencyKey)
    {
        return new CreatePaymentRequest
        {
            CustomerId = customerId,
            TableNumber = 1,
            TotalAmount = 6.90m,
            Steuernummer = "ATU12345678",
            CashRegisterId = cashRegisterId,
            Payment = new PaymentMethodRequest { Method = "cash", TseRequired = true },
            Items = new List<PaymentItemRequest>
            {
                new() { ProductId = productId, Quantity = 1, TaxType = TaxType.Reduced }
            },
            IdempotencyKey = idempotencyKey
        };
    }

    [SkippableFact]
    public async Task AdvisoryLock_SecondAcquireWaitsUntilFirstScopeDisposed()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        var cs = _fixture.ConnectionString;
        var reg = Guid.NewGuid();
        const int holdMs = 350;

        var t2AcquiredAt = DateTime.UtcNow;
        var t2Start = DateTime.UtcNow;

        var t1 = Task.Run(async () =>
        {
            await using var _ = await OfflineReplayRegisterLock.AcquireAsync(new[] { reg }, cs);
            await Task.Delay(holdMs);
        });

        await Task.Delay(80);
        t2Start = DateTime.UtcNow;
        var t2 = Task.Run(async () =>
        {
            await using var _ = await OfflineReplayRegisterLock.AcquireAsync(new[] { reg }, cs);
            t2AcquiredAt = DateTime.UtcNow;
        });

        await Task.WhenAll(t1, t2);
        var waitMs = (t2AcquiredAt - t2Start).TotalMilliseconds;
        Assert.True(waitMs >= holdMs - 120,
            $"Second acquire should block until first releases; waited ~{waitMs}ms, expected at least ~{holdMs}ms.");
    }

    [SkippableFact]
    public async Task ConcurrentReplay_SameRegisterSameOfflineId_ProducesSinglePayment()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        await using var seedCtx = CreateContext();
        var (_, productId, customerId, cashRegisterId) = await SeedMinimalDomainAsync(seedCtx);

        var audit1 = new Mock<IAuditLogService>();
        var audit2 = new Mock<IAuditLogService>();
        SetupAuditMocks(audit1);
        SetupAuditMocks(audit2);

        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, Mock.Of<ILogger<SignaturePipeline>>());

        await using var ctx1 = CreateContext();
        var tseA = new TseService(ctx1, pipeline, keyProvider, Mock.Of<ILogger<TseService>>());
        var seqA = new ReceiptSequenceService(ctx1, Mock.Of<ILogger<ReceiptSequenceService>>());
        var (_, offline1) = CreateServices(ctx1, audit1, tseA, seqA);

        await using var ctx2 = CreateContext();
        var tseB = new TseService(ctx2, pipeline, keyProvider, Mock.Of<ILogger<TseService>>());
        var seqB = new ReceiptSequenceService(ctx2, Mock.Of<ILogger<ReceiptSequenceService>>());
        var (_, offline2) = CreateServices(ctx2, audit2, tseB, seqB);

        var offlineId = Guid.NewGuid();
        var idem = Guid.NewGuid().ToString("N");
        var req = BuildPaymentRequest(customerId, cashRegisterId, productId, idem);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(req));

        var replayReq = new ReplayOfflineTransactionsRequest
        {
            Transactions = new List<ReplayOfflineTransactionItem>
            {
                new()
                {
                    OfflineTransactionId = offlineId,
                    CreatedAtUtc = new DateTime(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc),
                    CashRegisterId = cashRegisterId,
                    Payload = doc.RootElement.Clone()
                }
            }
        };

        var taskA = offline1.ReplayOfflineTransactionsAsync(replayReq, "u1", "Cashier");
        var taskB = offline2.ReplayOfflineTransactionsAsync(replayReq, "u1", "Cashier");
        await Task.WhenAll(taskA, taskB);

        await using var verify = CreateContext();
        var payments = await verify.PaymentDetails.AsNoTracking()
            .Where(p => p.CashRegisterId == cashRegisterId && p.IdempotencyKey == idem)
            .ToListAsync();
        Assert.Single(payments);

        var receipts = await verify.Receipts.AsNoTracking()
            .Where(r => r.PaymentId == payments[0].Id)
            .ToListAsync();
        Assert.Single(receipts);

        var offlineRows = await verify.OfflineTransactions.AsNoTracking()
            .Where(o => o.CashRegisterId == cashRegisterId && o.PayloadHash != null)
            .ToListAsync();
        Assert.Single(offlineRows);
        Assert.Equal(OfflineTransactionStatus.Synced, offlineRows[0].Status);
    }

    [SkippableFact]
    public async Task ConcurrentReplay_DifferentOfflineIdsSamePayload_OneCanonicalPayment()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        await using var seedCtx = CreateContext();
        var (_, productId, customerId, cashRegisterId) = await SeedMinimalDomainAsync(seedCtx);

        var audit1 = new Mock<IAuditLogService>();
        var audit2 = new Mock<IAuditLogService>();
        SetupAuditMocks(audit1);
        SetupAuditMocks(audit2);

        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, Mock.Of<ILogger<SignaturePipeline>>());

        var offlineIdA = Guid.NewGuid();
        var offlineIdB = Guid.NewGuid();
        var idem = Guid.NewGuid().ToString("N");
        var req = BuildPaymentRequest(customerId, cashRegisterId, productId, idem);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(req));
        var payload = doc.RootElement.Clone();

        await using var ctx1 = CreateContext();
        var (_, offline1) = CreateServices(ctx1, audit1,
            new TseService(ctx1, pipeline, keyProvider, Mock.Of<ILogger<TseService>>()),
            new ReceiptSequenceService(ctx1, Mock.Of<ILogger<ReceiptSequenceService>>()));

        await using var ctx2 = CreateContext();
        var (_, offline2) = CreateServices(ctx2, audit2,
            new TseService(ctx2, pipeline, keyProvider, Mock.Of<ILogger<TseService>>()),
            new ReceiptSequenceService(ctx2, Mock.Of<ILogger<ReceiptSequenceService>>()));

        var reqA = new ReplayOfflineTransactionsRequest
        {
            Transactions = new List<ReplayOfflineTransactionItem>
            {
                new()
                {
                    OfflineTransactionId = offlineIdA,
                    CreatedAtUtc = new DateTime(2026, 3, 18, 10, 0, 0, DateTimeKind.Utc),
                    CashRegisterId = cashRegisterId,
                    Payload = payload.Clone()
                }
            }
        };
        var reqB = new ReplayOfflineTransactionsRequest
        {
            Transactions = new List<ReplayOfflineTransactionItem>
            {
                new()
                {
                    OfflineTransactionId = offlineIdB,
                    CreatedAtUtc = new DateTime(2026, 3, 18, 10, 0, 1, DateTimeKind.Utc),
                    CashRegisterId = cashRegisterId,
                    Payload = payload.Clone()
                }
            }
        };

        await Task.WhenAll(
            offline1.ReplayOfflineTransactionsAsync(reqA, "u1", "Cashier"),
            offline2.ReplayOfflineTransactionsAsync(reqB, "u1", "Cashier"));

        await using var verify = CreateContext();
        var paymentCount = await verify.PaymentDetails.CountAsync(p =>
            p.CashRegisterId == cashRegisterId && p.IdempotencyKey == idem);
        Assert.Equal(1, paymentCount);

        var synced = await verify.OfflineTransactions.CountAsync(o =>
            o.CashRegisterId == cashRegisterId && o.Status == OfflineTransactionStatus.Synced);
        Assert.Equal(1, synced);
    }

    [SkippableFact]
    public async Task ConcurrentPayments_SameRegister_RealTse_UniqueBelegNrAndMonotonicChain()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        await using var ctx = CreateContext();
        var (_, productId, customerId, cashRegisterId) = await SeedMinimalDomainAsync(ctx);

        var audit = new Mock<IAuditLogService>();
        SetupAuditMocks(audit);

        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, Mock.Of<ILogger<SignaturePipeline>>());
        var tse = new TseService(ctx, pipeline, keyProvider, Mock.Of<ILogger<TseService>>());
        var receiptSeq = new ReceiptSequenceService(ctx, Mock.Of<ILogger<ReceiptSequenceService>>());
        var (paymentService, _) = CreateServices(ctx, audit, tse, receiptSeq);

        const int n = 12;
        var tasks = new List<Task<PaymentResult>>();
        for (var i = 0; i < n; i++)
        {
            var local = i;
            tasks.Add(Task.Run(async () =>
            {
                await using var c = CreateContext();
                var tseI = new TseService(c, pipeline, keyProvider, Mock.Of<ILogger<TseService>>());
                var seqI = new ReceiptSequenceService(c, Mock.Of<ILogger<ReceiptSequenceService>>());
                var (pay, _) = CreateServices(c, audit, tseI, seqI);
                var req = BuildPaymentRequest(customerId, cashRegisterId, productId, Guid.NewGuid().ToString("N"));
                return await pay.CreatePaymentAsync(req, "u1");
            }));
        }

        var results = await Task.WhenAll(tasks);
        Assert.All(results, r => Assert.True(r.Success));

        await using var verify = CreateContext();
        var payments = await verify.PaymentDetails.AsNoTracking()
            .Where(p => p.CashRegisterId == cashRegisterId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync();
        Assert.Equal(n, payments.Count);
        var numbers = payments.Select(p => p.ReceiptNumber).Distinct().ToList();
        Assert.Equal(n, numbers.Count);

        var chain = await verify.SignatureChainState.AsNoTracking()
            .FirstOrDefaultAsync(s => s.CashRegisterId == cashRegisterId);
        Assert.NotNull(chain);
        Assert.Equal(n, chain!.LastCounter);
    }

    [SkippableFact]
    public async Task ConcurrentCreatePayment_SameIdempotencyKey_SingleRow_PostgresUniqueIndex()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        await using var seedCtx = CreateContext();
        var (_, productId, customerId, cashRegisterId) = await SeedMinimalDomainAsync(seedCtx);

        var audit = new Mock<IAuditLogService>();
        SetupAuditMocks(audit);

        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, Mock.Of<ILogger<SignaturePipeline>>());

        var sharedKey = Guid.NewGuid().ToString("N");
        const int parallel = 20;
        var tasks = Enumerable.Range(0, parallel).Select(_ => Task.Run(async () =>
        {
            await using var c = CreateContext();
            var tse = new TseService(c, pipeline, keyProvider, Mock.Of<ILogger<TseService>>());
            var seq = new ReceiptSequenceService(c, Mock.Of<ILogger<ReceiptSequenceService>>());
            var (pay, _) = CreateServices(c, audit, tse, seq);
            var req = BuildPaymentRequest(customerId, cashRegisterId, productId, sharedKey);
            return await pay.CreatePaymentAsync(req, "u1");
        })).ToArray();

        var results = await Task.WhenAll(tasks);
        Assert.All(results, r => Assert.True(r.Success));

        await using var verify = CreateContext();
        var count = await verify.PaymentDetails.CountAsync(p => p.IdempotencyKey == sharedKey);
        Assert.Equal(1, count);

        var firstId = results[0].PaymentId;
        Assert.All(results, r => Assert.Equal(firstId, r.PaymentId));
    }

    [SkippableFact]
    public async Task CreatePayment_TseSignatureThrows_RollsBack_NoPaymentInvoiceReceipt()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        await using var ctx = CreateContext();
        var (_, productId, customerId, cashRegisterId) = await SeedMinimalDomainAsync(ctx);

        var audit = new Mock<IAuditLogService>();
        SetupAuditMocks(audit);

        var tseMock = new Mock<ITseService>();
        tseMock.Setup(x => x.CreateInvoiceSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<string?>(),
                It.IsAny<IDbContextTransaction?>()))
            .ThrowsAsync(new InvalidOperationException("Simulated TSE failure"));

        var receiptSeq = new ReceiptSequenceService(ctx, Mock.Of<ILogger<ReceiptSequenceService>>());
        var (paymentService, _) = CreateServices(ctx, audit, tseMock.Object, receiptSeq);

        var req = BuildPaymentRequest(customerId, cashRegisterId, productId, Guid.NewGuid().ToString("N"));
        var result = await paymentService.CreatePaymentAsync(req, "u1");
        Assert.False(result.Success);

        var payCount = await ctx.PaymentDetails.CountAsync(p => p.CashRegisterId == cashRegisterId);
        Assert.Equal(0, payCount);
        var recCount = await ctx.Receipts.CountAsync(r => r.CashRegisterId == cashRegisterId);
        Assert.Equal(0, recCount);
        var paymentIds = await ctx.PaymentDetails.Where(p => p.CashRegisterId == cashRegisterId)
            .Select(p => p.Id).ToListAsync();
        var invForRegister = paymentIds.Count == 0
            ? 0
            : await ctx.Invoices.CountAsync(i =>
                i.SourcePaymentId != null && paymentIds.Contains(i.SourcePaymentId.Value));
        Assert.Equal(0, invForRegister);

        var seqRows = await ctx.ReceiptSequences.CountAsync(r => r.CashRegisterId == cashRegisterId);
        Assert.Equal(0, seqRows);
    }

    [SkippableFact]
    public async Task SecondReplayAfterSync_ReturnsSyncedWithoutNewReceipt()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        await using var ctx = CreateContext();
        var (_, productId, customerId, cashRegisterId) = await SeedMinimalDomainAsync(ctx);

        var audit = new Mock<IAuditLogService>();
        SetupAuditMocks(audit);

        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, Mock.Of<ILogger<SignaturePipeline>>());
        var tse = new TseService(ctx, pipeline, keyProvider, Mock.Of<ILogger<TseService>>());
        var seq = new ReceiptSequenceService(ctx, Mock.Of<ILogger<ReceiptSequenceService>>());
        var (_, offline) = CreateServices(ctx, audit, tse, seq);

        var offlineId = Guid.NewGuid();
        var idem = Guid.NewGuid().ToString("N");
        var req = BuildPaymentRequest(customerId, cashRegisterId, productId, idem);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(req));
        var replayReq = new ReplayOfflineTransactionsRequest
        {
            Transactions = new List<ReplayOfflineTransactionItem>
            {
                new()
                {
                    OfflineTransactionId = offlineId,
                    CreatedAtUtc = new DateTime(2026, 3, 19, 8, 0, 0, DateTimeKind.Utc),
                    CashRegisterId = cashRegisterId,
                    Payload = doc.RootElement.Clone()
                }
            }
        };

        var r1 = await offline.ReplayOfflineTransactionsAsync(replayReq, "u1", "Cashier");
        var r2 = await offline.ReplayOfflineTransactionsAsync(replayReq, "u1", "Cashier");

        Assert.Equal("Synced", r1.Items[0].Status);
        Assert.Equal("Synced", r2.Items[0].Status);
        Assert.Equal(r1.Items[0].SyncedPaymentId, r2.Items[0].SyncedPaymentId);

        var receiptCount = await ctx.Receipts.CountAsync(r => r.CashRegisterId == cashRegisterId);
        Assert.Equal(1, receiptCount);
    }

    /// <summary>
    /// Lock starvation: when one holder keeps the lock longer than max wait, the waiter gets OfflineReplayLockTimeoutException.
    /// </summary>
    [SkippableFact]
    public async Task AdvisoryLock_Timeout_WhenHolderKeepsLockLongerThanMaxWait()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        var cs = _fixture.ConnectionString;
        var reg = Guid.NewGuid();
        const int holdMs = 5000;
        const int maxWaitMs = 2200;
        const int retryIntervalMs = 100;

        var t1 = Task.Run(async () =>
        {
            await using var scope = await OfflineReplayRegisterLock.AcquireAsync(new[] { reg }, cs, default, 60_000, retryIntervalMs);
            await Task.Delay(holdMs);
        });

        await Task.Delay(100);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        OfflineReplayLockTimeoutException? ex = null;
        try
        {
            await using var _ = await OfflineReplayRegisterLock.AcquireAsync(new[] { reg }, cs, default, maxWaitMs, retryIntervalMs);
        }
        catch (OfflineReplayLockTimeoutException e)
        {
            ex = e;
        }
        sw.Stop();

        Assert.NotNull(ex);
        Assert.True(ex.WaitDurationMs >= maxWaitMs - 500, $"Expected wait >= {maxWaitMs}ms, got {ex.WaitDurationMs}ms");
        Assert.Single(ex.CashRegisterIds);
        Assert.Equal(reg, ex.CashRegisterIds[0]);
        Assert.True(sw.ElapsedMilliseconds >= maxWaitMs - 300, $"Expected elapsed >= {maxWaitMs}ms, got {sw.ElapsedMilliseconds}ms");
        await t1;
    }

    /// <summary>
    /// When no one holds the lock, acquire succeeds immediately; WaitDurationMs is 0 or very small.
    /// </summary>
    [SkippableFact]
    public async Task AdvisoryLock_AcquireSucceedsWhenLockFree_WaitDurationZeroOrSmall()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        var cs = _fixture.ConnectionString;
        var reg = Guid.NewGuid();

        await using var scope = await OfflineReplayRegisterLock.AcquireAsync(new[] { reg }, cs, default, 10_000, 100);
        Assert.True(scope.WaitDurationMs >= 0 && scope.WaitDurationMs < 200,
            $"Expected immediate or fast acquire, got WaitDurationMs={scope.WaitDurationMs}");
    }
}
