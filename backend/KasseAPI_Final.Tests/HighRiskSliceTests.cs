using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class HighRiskSliceTests
{
    [Fact]
    public async Task Replay_PriceMismatch_MarksFailedWithoutRetry()
    {
        await AssertDeterministicPaymentFailureAsync(new PaymentResult
        {
            Success = false,
            Message = "Total amount mismatch between client and server calculation.",
            IsDeterministicFailure = false
        });
    }

    [Fact]
    public async Task Replay_InsufficientStock_MarksFailedWithoutRetry()
    {
        await AssertDeterministicPaymentFailureAsync(new PaymentResult
        {
            Success = false,
            Message = "Insufficient stock",
            IsDeterministicFailure = false
        });
    }

    [Fact]
    public async Task Replay_CashRegisterClosed_MarksFailedWithoutCallingPayment()
    {
        var harness = await SeedReplayAsync(registerStatus: RegisterStatus.Closed);
        var payment = new Mock<IPaymentService>();
        var service = CreateOfflineService(harness.Context, payment.Object);

        var response = await service.ReplayOfflineTransactionsAsync(harness.Request, "u1", "Cashier");

        Assert.Equal(OfflineTransactionStatus.Failed.ToString(), Assert.Single(response.Items).Status);
        var row = await harness.Context.OfflineTransactions.SingleAsync();
        Assert.Equal(OfflineTransactionStatus.Failed, row.Status);
        Assert.Equal("CASH_REGISTER_CLOSED", row.LastErrorCode);
        payment.Verify(
            p => p.CreatePaymentAsync(
                It.IsAny<CreatePaymentRequest>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()),
            Times.Never);
    }

    [Fact]
    public async Task Replay_InactiveProduct_MarksFailedWithoutCallingPayment()
    {
        var harness = await SeedReplayAsync(productActive: false);
        var payment = new Mock<IPaymentService>();
        var service = CreateOfflineService(harness.Context, payment.Object);

        var response = await service.ReplayOfflineTransactionsAsync(harness.Request, "u1", "Cashier");

        Assert.Equal(OfflineTransactionStatus.Failed.ToString(), Assert.Single(response.Items).Status);
        var row = await harness.Context.OfflineTransactions.SingleAsync();
        Assert.Equal(OfflineTransactionStatus.Failed, row.Status);
        Assert.Equal("PRODUCT_INACTIVE", row.LastErrorCode);
        payment.Verify(
            p => p.CreatePaymentAsync(
                It.IsAny<CreatePaymentRequest>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()),
            Times.Never);
    }

    [Fact]
    public async Task Replay_TransientFailure_StaysPending()
    {
        var harness = await SeedReplayAsync();
        var payment = new Mock<IPaymentService>();
        payment.Setup(p => p.CreatePaymentAsync(
                It.IsAny<CreatePaymentRequest>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync(new PaymentResult
            {
                Success = false,
                Message = "TSE temporarily unavailable",
                IsDeterministicFailure = false
            });
        var service = CreateOfflineService(harness.Context, payment.Object);

        await service.ReplayOfflineTransactionsAsync(harness.Request, "u1", "Cashier");

        var row = await harness.Context.OfflineTransactions.SingleAsync();
        Assert.Equal(OfflineTransactionStatus.NonFiscalPending, row.Status);
        Assert.Equal(1, row.RetryCount);
    }

    [Fact]
    public async Task OfflineReplayWorker_SetsTenantFromEachActiveTenant()
    {
        var dbName = "replay-tenant-" + Guid.NewGuid().ToString("N");
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using (var seed = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantA)))
        {
            seed.Tenants.AddRange(
                new Tenant { Id = tenantA, Name = "A", Slug = "ta" + tenantA.ToString("N")[..8], IsActive = true },
                new Tenant { Id = tenantB, Name = "B", Slug = "tb" + tenantB.ToString("N")[..8], IsActive = true });
            seed.OfflineTransactions.Add(PendingRow(tenantA));
            seed.OfflineTransactions.Add(PendingRow(tenantB));
            await seed.SaveChangesAsync();
        }

        var seen = new List<Guid?>();
        ICurrentTenantAccessor? latest = null;
        var offline = new Mock<IOfflineTransactionService>();
        offline.Setup(s => s.ReplayOfflineTransactionsAsync(
                It.IsAny<ReplayOfflineTransactionsRequest>(),
                It.IsAny<string>(),
                It.IsAny<string>()))
            .Callback<ReplayOfflineTransactionsRequest, string, string>((_, _, _) => seen.Add(latest!.TenantId))
            .ReturnsAsync(new ReplayOfflineTransactionsResponse());
        var notifier = new Mock<IOfflineReplayCompletionNotifier>();
        notifier.Setup(n => n.NotifyReplayBatchCompletedAsync(
                It.IsAny<Guid>(),
                It.IsAny<int>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var factory = new Mock<IServiceScopeFactory>();
        factory.Setup(f => f.CreateScope()).Returns(() =>
        {
            latest = TenantTestDoubles.TenantAccessorReturning(null);
            var db = new AppDbContext(options, latest);
            var services = new ServiceCollection();
            services.AddSingleton(latest);
            services.AddSingleton(db);
            services.AddSingleton(offline.Object);
            services.AddSingleton(notifier.Object);
            return new ServiceProviderScope(services.BuildServiceProvider());
        });

        var worker = new OfflineReplayHostedService(
            factory.Object,
            Mock.Of<global::KasseAPI_Final.Services.Tse.ITseHealthMonitor>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<OfflineReplayHostedService>>(),
            Options.Create(new TseOptions()).ToMonitor());

        await worker.ReplayPendingOnceAsync(CancellationToken.None);

        Assert.Equal(2, seen.Count);
        Assert.Contains(tenantA, seen);
        Assert.Contains(tenantB, seen);
    }

    [Fact]
    public void ZReport_ForcedOpenTables_StatesWarningAndKeepsPaidTotal()
    {
        var closing = new DailyClosing
        {
            Id = Guid.NewGuid(),
            CashRegisterId = Guid.NewGuid(),
            ClosingType = "Daily",
            ClosingDate = new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc),
            TotalAmount = 10m,
            TotalTaxAmount = 2m,
            TransactionCount = 1,
            TseSignature = "sig",
            OpenOrdersForced = true,
            OpenOrdersCount = 2,
            CreatedAt = DateTime.UtcNow
        };

        var report = DailyClosingReportComposer.Compose(closing, "K1", daySummary: null, cashCount: 0m, shiftDifference: 0m);

        Assert.Equal(10m, report.FiscalTotalAmount);
        Assert.Equal(10m, report.TotalSales);
        Assert.NotNull(report.OpenTablesForcedNotice);
        Assert.Contains("offene Tische", report.OpenTablesForcedNotice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("nicht in der Summe", report.OpenTablesForcedNotice, StringComparison.OrdinalIgnoreCase);

        var model = TagesabschlussReportModel.From(closing, new TagesabschlussCloudContext
        {
            CompanyName = "Cafe",
            CompanyAddress = "Wien",
            CompanyVatId = "ATU12345678"
        });
        var text = new RksvReportTextService(Mock.Of<ITagesabschlussReportEnricher>())
            .RenderTagesabschluss(model, "Production", "footer", "qr", "K1");
        Assert.Contains("ForceWithWarning", text, StringComparison.Ordinal);
        Assert.Contains("10", text, StringComparison.Ordinal);
    }

    private static async Task AssertDeterministicPaymentFailureAsync(PaymentResult paymentResult)
    {
        var harness = await SeedReplayAsync();
        var payment = new Mock<IPaymentService>();
        payment.Setup(p => p.CreatePaymentAsync(
                It.IsAny<CreatePaymentRequest>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync(paymentResult);
        var service = CreateOfflineService(harness.Context, payment.Object);

        var response = await service.ReplayOfflineTransactionsAsync(harness.Request, "u1", "Cashier");

        Assert.Equal(OfflineTransactionStatus.Failed.ToString(), Assert.Single(response.Items).Status);
        var row = await harness.Context.OfflineTransactions.SingleAsync();
        Assert.Equal(OfflineTransactionStatus.Failed, row.Status);
        Assert.Equal(1, row.RetryCount);
    }

    private static OfflineTransactionService CreateOfflineService(AppDbContext context, IPaymentService payment) =>
        new(
            context,
            payment,
            Mock.Of<IAuditLogService>(),
            Mock.Of<Microsoft.Extensions.Logging.ILogger<OfflineTransactionService>>(),
            new EphemeralDataProtectionProvider());

    private static async Task<ReplayHarness> SeedReplayAsync(
        RegisterStatus registerStatus = RegisterStatus.Open,
        bool? productActive = null)
    {
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("replay-det-" + Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options,
            TenantTestDoubles.TenantAccessorReturning(tenantId));

        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "T",
            Slug = "t" + tenantId.ToString("N")[..8],
            IsActive = true
        });
        context.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "L",
            Status = registerStatus,
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        if (productActive.HasValue)
        {
            context.Products.Add(new Product
            {
                Id = productId,
                TenantId = tenantId,
                Name = "Döner",
                Price = 6.90m,
                IsActive = productActive.Value,
                ArchivedAt = productActive.Value ? null : DateTime.UtcNow
            });
        }

        var requestDto = new CreatePaymentRequest
        {
            CustomerId = Guid.NewGuid(),
            CashRegisterId = registerId,
            TableNumber = 1,
            TotalAmount = 6.90m,
            Payment = new PaymentMethodRequest { Method = "cash" },
            Items = productActive.HasValue
                ? [new PaymentItemRequest { ProductId = productId, Quantity = 1 }]
                : []
        };
        var parsed = JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(requestDto));
        var prepared = OfflineVoucherPayloadProtector.PrepareForPersistence(
            parsed.GetRawText(),
            new EphemeralDataProtectionProvider().CreateProtector(OfflineVoucherPayloadProtector.Purpose));
        var offlineId = Guid.NewGuid();
        context.OfflineTransactions.Add(new OfflineTransaction
        {
            Id = offlineId,
            TenantId = tenantId,
            CashRegisterId = registerId,
            PayloadJson = prepared.StoredPayloadJson,
            PayloadHash = prepared.PayloadHashHex,
            Status = OfflineTransactionStatus.NonFiscalPending,
            ServerReceivedAtUtc = DateTime.UtcNow,
            OfflineCreatedAtUtc = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        });
        await context.SaveChangesAsync();

        return new ReplayHarness(
            context,
            new ReplayOfflineTransactionsRequest
            {
                Transactions =
                [
                    new ReplayOfflineTransactionItem
                    {
                        OfflineTransactionId = offlineId,
                        CashRegisterId = registerId,
                        CreatedAtUtc = DateTime.UtcNow,
                        Payload = parsed
                    }
                ]
            });
    }

    private static OfflineTransaction PendingRow(Guid tenantId) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = tenantId,
        CashRegisterId = Guid.NewGuid(),
        PayloadJson = "{}",
        Status = OfflineTransactionStatus.NonFiscalPending,
        ServerReceivedAtUtc = DateTime.UtcNow,
        OfflineCreatedAtUtc = DateTime.UtcNow,
        CreatedAt = DateTime.UtcNow,
        CreatedBy = "cashier",
        IsActive = true
    };

    private sealed record ReplayHarness(AppDbContext Context, ReplayOfflineTransactionsRequest Request);

    private sealed class ServiceProviderScope(ServiceProvider provider) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = provider;
        public void Dispose() => provider.Dispose();
    }
}
