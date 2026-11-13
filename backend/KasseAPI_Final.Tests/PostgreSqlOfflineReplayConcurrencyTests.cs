using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Pricing;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Tse;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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

    // xUnit builds one instance per test, and the fixture database is shared across the whole collection.
    // A per-test tenant keeps the per-tenant unique indexes (register number, category key, …) from colliding.
    private readonly Guid _tenantId = Guid.NewGuid();

    public PostgreSqlOfflineReplayConcurrencyTests(PostgreSqlReplayFixture fixture)
    {
        _fixture = fixture;
    }

    private AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseAppNpgsql(_fixture.ConnectionString)
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(_tenantId));
    }

    private static TseService CreateTseService(AppDbContext ctx, SignaturePipeline pipeline, ITseKeyProvider keyProvider)
    {
        var closingProvider = new RealTseProvider(pipeline, keyProvider, ctx, NullLogger<RealTseProvider>.Instance);
        return new TseService(ctx, pipeline, keyProvider, closingProvider, Mock.Of<ILogger<TseService>>());
    }

    private async Task<(Guid categoryId, Guid productId, Guid customerId, Guid cashRegisterId)> SeedMinimalDomainAsync(AppDbContext ctx)
    {
        var categoryId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var cashRegisterId = Guid.NewGuid();

        TenantTestDoubles.EnsureTenant(ctx, _tenantId);
        var taxGroupId = TenantTestDoubles.EnsureTaxGroup(ctx, _tenantId);
        ctx.Categories.Add(new Category
        {
            TenantId = _tenantId,
            Id = categoryId,
            Name = "Speisen",
            VatRate = 10m
        });
        ctx.Products.Add(new Product
        {
            Id = productId,
            TenantId = _tenantId,
            Name = "Döner",
            Description = "-",
            Price = 6.90m,
            CategoryId = categoryId,
            TaxGroupId = taxGroupId,
            Category = "Speisen",
            StockQuantity = 1000,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = 2,
            TaxRate = TaxTypes.GetTaxRate(2),
            Barcode = $"t-{productId:N}",
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true
        });
        ctx.Customers.Add(new Customer { Id = customerId, TenantId = _tenantId, Name = "PGTest", Email = "pg@test.com", Phone = "1", IsActive = true });
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = _tenantId,
            Id = cashRegisterId,
            RegisterNumber = $"PG-{cashRegisterId:N}"[..20],
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

    private (PaymentService payment, OfflineTransactionService offline) CreateServices(
        AppDbContext ctx,
        Mock<IAuditLogService> audit,
        ITseService tse,
        IReceiptSequenceService receiptSeq)
    {
        var tenantResolver = TenantTestDoubles.SettingsResolverReturning(_tenantId);
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
            TenantTestDoubles.CompanyProfileProviderReturning(TestCompany),
            userMock.Object,
            tenantResolver, TenantTestDoubles.ProductionHostEnvironment);

        var cashRegResolver = new CashRegisterResolutionService(ctx, Mock.Of<ILogger<CashRegisterResolutionService>>(), tenantResolver, RksvStartbelegTestDoubles.GateOff(), RksvMonatsbelegTestDoubles.GateOff());
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
            TenantTestDoubles.CompanyProfileProviderReturning(TestCompany),
            Options.Create(new TseOptions { TseMode = "Demo" }),
            Options.Create(new InventoryOptions()),
            Mock.Of<ILogger<PaymentService>>(),
            cashRegResolver,
            httpAccessor,
            new PaymentMethodCatalogService(ctx, tenantResolver),
            new PricingRuleResolver(ctx, tenantResolver),
            tenantResolver);

        var dataProtection = new EphemeralDataProtectionProvider();
        var offlineService = new OfflineTransactionService(
            ctx,
            paymentService,
            audit.Object,
            Mock.Of<ILogger<OfflineTransactionService>>(),
            dataProtection);

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
        var tseA = CreateTseService(ctx1, pipeline, keyProvider);
        var seqA = new ReceiptSequenceService(ctx1, Mock.Of<ILogger<ReceiptSequenceService>>());
        var (_, offline1) = CreateServices(ctx1, audit1, tseA, seqA);

        await using var ctx2 = CreateContext();
        var tseB = CreateTseService(ctx2, pipeline, keyProvider);
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
            CreateTseService(ctx1, pipeline, keyProvider),
            new ReceiptSequenceService(ctx1, Mock.Of<ILogger<ReceiptSequenceService>>()));

        await using var ctx2 = CreateContext();
        var (_, offline2) = CreateServices(ctx2, audit2,
            CreateTseService(ctx2, pipeline, keyProvider),
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
        var tse = CreateTseService(ctx, pipeline, keyProvider);
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
                var tseI = CreateTseService(c, pipeline, keyProvider);
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

        var chain = await verify.SignatureChainState.IgnoreQueryFilters().AsNoTracking()
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
            var tse = CreateTseService(c, pipeline, keyProvider);
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
        var tse = CreateTseService(ctx, pipeline, keyProvider);
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

    [SkippableFact]
    public async Task ConcurrentStorno_TwoKeys_SingleReversal()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        await using var seed = CreateContext();
        var (_, productId, customerId, cashRegisterId) = await SeedMinimalDomainAsync(seed);

        var audit = new Mock<IAuditLogService>();
        SetupAuditMocks(audit);
        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, Mock.Of<ILogger<SignaturePipeline>>());
        var (payment, _) = CreateServices(
            seed,
            audit,
            CreateTseService(seed, pipeline, keyProvider),
            new ReceiptSequenceService(seed, Mock.Of<ILogger<ReceiptSequenceService>>()));

        var sale = await payment.CreatePaymentAsync(
            BuildPaymentRequest(customerId, cashRegisterId, productId, Guid.NewGuid().ToString("N")),
            "u1");
        Assert.True(sale.Success, sale.Message);
        var saleId = sale.PaymentId!.Value;

        var results = await Task.WhenAll(
            Task.Run(() => CancelOnFreshContextAsync(audit, pipeline, keyProvider, saleId, "storno-key-a")),
            Task.Run(() => CancelOnFreshContextAsync(audit, pipeline, keyProvider, saleId, "storno-key-b")));

        Assert.Single(results, r => r.Success);
        var failure = Assert.Single(results, r => !r.Success);
        Assert.Equal("STORNO_ALREADY_EXISTS", failure.DiagnosticCode);
        Assert.Equal(StatusCodes.Status409Conflict, PaymentApiContractMapper.MapToStatusCode(failure));

        await using var verify = CreateContext();
        var stornoCount = await verify.PaymentDetails.CountAsync(p => p.OriginalPaymentId == saleId && p.IsStorno);
        Assert.Equal(1, stornoCount);
    }

    [SkippableFact]
    public async Task ConcurrentPayment_SameCart_DifferentIdempotencyKeys_SingleReceipt()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        await using var seed = CreateContext();
        var (_, productId, customerId, cashRegisterId) = await SeedMinimalDomainAsync(seed);
        var userId = "cart-" + Guid.NewGuid().ToString("N");
        seed.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = userId,
            NormalizedUserName = userId.ToUpperInvariant(),
            Email = userId + "@t.test",
            NormalizedEmail = (userId + "@t.test").ToUpperInvariant(),
            FirstName = "Ada",
            LastName = "Lovelace",
            SecurityStamp = Guid.NewGuid().ToString("N"),
            IsActive = true
        });
        seed.UserTenantMemberships.Add(new UserTenantMembership
        {
            UserId = userId,
            TenantId = _tenantId,
            IsActive = true
        });
        var cartId = "cart-" + Guid.NewGuid().ToString("N");
        seed.Carts.Add(new Cart
        {
            CartId = cartId,
            UserId = userId,
            TableNumber = 1,
            Status = CartStatus.Active,
            ExpiresAt = DateTime.UtcNow.AddHours(2),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsActive = true,
            Items =
            {
                new CartItem
                {
                    ProductId = productId,
                    Quantity = 1,
                    UnitPrice = 6.90m
                }
            }
        });
        await seed.SaveChangesAsync();

        var audit = new Mock<IAuditLogService>();
        SetupAuditMocks(audit);
        var keyProvider = new SoftwareTseKeyProvider();
        var pipeline = new SignaturePipeline(keyProvider, Mock.Of<ILogger<SignaturePipeline>>());

        var results = await Task.WhenAll(
            Task.Run(() => PayOnFreshContextAsync(audit, pipeline, keyProvider, customerId, cashRegisterId, productId, userId, Guid.NewGuid().ToString("N"))),
            Task.Run(() => PayOnFreshContextAsync(audit, pipeline, keyProvider, customerId, cashRegisterId, productId, userId, Guid.NewGuid().ToString("N"))));

        Assert.Single(results, r => r.Success);
        var failure = Assert.Single(results, r => !r.Success);
        Assert.Equal("CART_ALREADY_PAID", failure.DiagnosticCode);
        Assert.Equal(StatusCodes.Status409Conflict, PaymentApiContractMapper.MapToStatusCode(failure));

        await using var verify = CreateContext();
        var saleCount = await verify.PaymentDetails.CountAsync(p => p.CashRegisterId == cashRegisterId && !p.IsStorno && !p.IsRefund);
        Assert.Equal(1, saleCount);
        var cart = await verify.Carts.AsNoTracking().SingleAsync(c => c.CartId == cartId);
        Assert.Equal(CartStatus.Completed, cart.Status);
    }

    [SkippableFact]
    public async Task CartLifecycle_FortyFiveMinutes_DoesNotDeleteOtherTenantsOrRecentlyUpdatedCarts()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);
        var tenantB = Guid.NewGuid();
        var userA = "life-a-" + Guid.NewGuid().ToString("N");
        var userB = "life-b-" + Guid.NewGuid().ToString("N");
        var staleId = "stale-" + Guid.NewGuid().ToString("N");
        var freshId = "fresh-" + Guid.NewGuid().ToString("N");
        var otherId = "other-" + Guid.NewGuid().ToString("N");
        var now = DateTime.UtcNow;
        var old = now.AddHours(-2);

        await using var seed = CreateContext();
        TenantTestDoubles.EnsureTenant(seed, _tenantId, "ta" + _tenantId.ToString("N")[..8]);
        TenantTestDoubles.EnsureTenant(seed, tenantB, "tb" + tenantB.ToString("N")[..8]);
        await seed.SaveChangesAsync();

        var entityA = TestUser(userA);
        var entityB = TestUser(userB);
        entityA.EmployeeNumber = Guid.NewGuid().ToString("N")[..8];
        entityB.EmployeeNumber = Guid.NewGuid().ToString("N")[..8];
        seed.Users.Add(entityA);
        seed.Users.Add(entityB);
        seed.UserTenantMemberships.Add(new UserTenantMembership { UserId = userA, TenantId = _tenantId, IsActive = true });
        seed.UserTenantMemberships.Add(new UserTenantMembership { UserId = userB, TenantId = tenantB, IsActive = true });
        seed.Carts.Add(TestCart(staleId, userA, old, old));
        seed.Carts.Add(TestCart(freshId, userA, old, now));
        seed.Carts.Add(TestCart(otherId, userB, old, old));
        await seed.SaveChangesAsync();

        var deleted = await CartLifecycleService.CleanupFortyFiveMinuteCartsAsync(seed, now);
        Assert.Equal(1, deleted);

        await using var verify = CreateContext();
        Assert.False(await verify.Carts.AnyAsync(c => c.CartId == staleId));
        Assert.True(await verify.Carts.AnyAsync(c => c.CartId == freshId));
        Assert.True(await verify.Carts.AnyAsync(c => c.CartId == otherId));
        Assert.Equal(userA, (await verify.Carts.SingleAsync(c => c.CartId == freshId)).UserId);
        Assert.Equal(userB, (await verify.Carts.SingleAsync(c => c.CartId == otherId)).UserId);
    }

    private async Task<PaymentResult> CancelOnFreshContextAsync(
        Mock<IAuditLogService> audit,
        SignaturePipeline pipeline,
        SoftwareTseKeyProvider keyProvider,
        Guid paymentId,
        string idempotencyKey)
    {
        await using var ctx = CreateContext();
        var (pay, _) = CreateServices(
            ctx,
            audit,
            CreateTseService(ctx, pipeline, keyProvider),
            new ReceiptSequenceService(ctx, Mock.Of<ILogger<ReceiptSequenceService>>()));
        return await pay.CancelPaymentAsync(
            paymentId,
            "Customer changed mind",
            "u1",
            idempotencyKey,
            CancellationReasonCode.CustomerRequest);
    }

    private async Task<PaymentResult> PayOnFreshContextAsync(
        Mock<IAuditLogService> audit,
        SignaturePipeline pipeline,
        SoftwareTseKeyProvider keyProvider,
        Guid customerId,
        Guid cashRegisterId,
        Guid productId,
        string userId,
        string idempotencyKey)
    {
        await using var ctx = CreateContext();
        var (pay, _) = CreateServices(
            ctx,
            audit,
            CreateTseService(ctx, pipeline, keyProvider),
            new ReceiptSequenceService(ctx, Mock.Of<ILogger<ReceiptSequenceService>>()));
        var req = BuildPaymentRequest(customerId, cashRegisterId, productId, idempotencyKey);
        return await pay.CreatePaymentAsync(req, userId);
    }

    private static ApplicationUser TestUser(string userId) => new()
    {
        Id = userId,
        UserName = userId,
        NormalizedUserName = userId.ToUpperInvariant(),
        Email = userId + "@t.test",
        NormalizedEmail = (userId + "@t.test").ToUpperInvariant(),
        FirstName = "Test",
        LastName = "User",
        SecurityStamp = Guid.NewGuid().ToString("N"),
        IsActive = true
    };

    private static Cart TestCart(string cartId, string userId, DateTime createdAt, DateTime updatedAt) => new()
    {
        CartId = cartId,
        UserId = userId,
        TableNumber = 4,
        Status = CartStatus.Active,
        ExpiresAt = DateTime.UtcNow.AddHours(12),
        CreatedAt = createdAt,
        UpdatedAt = updatedAt,
        IsActive = true
    };
}
