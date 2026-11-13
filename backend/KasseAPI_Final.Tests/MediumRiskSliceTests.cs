using System.Text.Json;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Offline;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class MediumRiskSliceTests
{
    [Fact]
    public async Task Replay_InactiveOrArchivedProduct_DeterministicFailed_NoReceipt()
    {
        await AssertCatalogRejectionAsync(active: false, archived: true);
        await AssertCatalogRejectionAsync(active: true, archived: true);
    }

    [Fact]
    public async Task Replay_PriceMismatchAndClosedRegister_DeterministicFailed()
    {
        var mismatch = await SeedReplayAsync();
        var payment = new Mock<IPaymentService>();
        payment.Setup(p => p.CreatePaymentAsync(
                It.IsAny<CreatePaymentRequest>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync(new PaymentResult
            {
                Success = false,
                Message = "Total amount mismatch between client and server calculation.",
                IsDeterministicFailure = false
            });
        var mismatchService = CreateOfflineService(mismatch.Context, payment.Object);
        await mismatchService.ReplayOfflineTransactionsAsync(mismatch.Request, "u1", "Cashier");
        var mismatchRow = await mismatch.Context.OfflineTransactions.SingleAsync();
        Assert.Equal(OfflineTransactionStatus.Failed, mismatchRow.Status);
        Assert.Equal(1, mismatchRow.RetryCount);
        Assert.Null(mismatchRow.SyncedPaymentId);

        var closed = await SeedReplayAsync(registerStatus: RegisterStatus.Closed);
        var closedPayment = new Mock<IPaymentService>();
        var closedService = CreateOfflineService(closed.Context, closedPayment.Object);
        await closedService.ReplayOfflineTransactionsAsync(closed.Request, "u1", "Cashier");
        var closedRow = await closed.Context.OfflineTransactions.SingleAsync();
        Assert.Equal(OfflineTransactionStatus.Failed, closedRow.Status);
        Assert.Equal("CASH_REGISTER_CLOSED", closedRow.LastErrorCode);
        Assert.Null(closedRow.SyncedPaymentId);
        closedPayment.Verify(
            p => p.CreatePaymentAsync(
                It.IsAny<CreatePaymentRequest>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()),
            Times.Never);
    }

    [Fact]
    public async Task OfflineReplayHostedService_SetsTenantFromRegister_ProcessesNonFiscalPending()
    {
        var dbName = "replay-register-tenant-" + Guid.NewGuid().ToString("N");
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var offlineId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        await using (var seed = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId)))
        {
            seed.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = "Cafe",
                Slug = "rg" + tenantId.ToString("N")[..8],
                IsActive = true
            });
            seed.CashRegisters.Add(new CashRegister
            {
                Id = registerId,
                TenantId = tenantId,
                RegisterNumber = "K1",
                Location = "Bar",
                Status = RegisterStatus.Open,
                StartingBalance = 0,
                CurrentBalance = 0,
                LastBalanceUpdate = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            });
            seed.OfflineTransactions.Add(new OfflineTransaction
            {
                Id = offlineId,
                TenantId = tenantId,
                CashRegisterId = registerId,
                PayloadJson = "{}",
                Status = OfflineTransactionStatus.NonFiscalPending,
                ServerReceivedAtUtc = DateTime.UtcNow,
                OfflineCreatedAtUtc = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "cashier",
                IsActive = true
            });
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

        Assert.Equal(new Guid?[] { tenantId }, seen);
        offline.Verify(
            s => s.ReplayOfflineTransactionsAsync(
                It.Is<ReplayOfflineTransactionsRequest>(r =>
                    r.Transactions.Count == 1
                    && r.Transactions[0].OfflineTransactionId == offlineId
                    && r.Transactions[0].CashRegisterId == registerId),
                "cashier",
                "Cashier"),
            Times.Once);
    }

    [Fact]
    public async Task CreatePayment_RegisterClosed_CartStillActive_ReturnsClosed_NoPaymentRow()
    {
        await using var ctx = PaymentServiceCoverageHarness.CreateContext();
        var (customerId, productId, registerId, _) = await PaymentServiceCoverageHarness.SeedCatalogAsync(ctx);
        var register = await ctx.CashRegisters.SingleAsync(r => r.Id == registerId);
        register.Status = RegisterStatus.Closed;
        ctx.Carts.Add(new Cart
        {
            CartId = "cart-closed",
            UserId = PaymentServiceCoverageHarness.CashierId,
            TableNumber = 1,
            Status = CartStatus.Active,
            IsActive = true,
            ExpiresAt = DateTime.UtcNow.AddHours(2),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await ctx.SaveChangesAsync();

        var sut = PaymentServiceCoverageHarness.CreatePaymentService(ctx);
        var result = await sut.CreatePaymentAsync(
            PaymentServiceCoverageHarness.SaleRequest(customerId, productId, registerId),
            PaymentServiceCoverageHarness.CashierId);

        Assert.False(result.Success);
        Assert.Equal("CASH_REGISTER_CLOSED", result.DiagnosticCode);
        Assert.Equal(0, await ctx.PaymentDetails.CountAsync());
        Assert.Equal(CartStatus.Active, (await ctx.Carts.SingleAsync()).Status);
    }

    [Fact]
    public async Task CleanupExpiredOrders_SetsExpired_AndPendingFetchExcludesIt()
    {
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("offline-expire-" + Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var accessor = TenantTestDoubles.TenantAccessorReturning(tenantId);
        await using var db = new AppDbContext(options, accessor);
        var now = DateTime.UtcNow;
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "T",
            Slug = "ex" + tenantId.ToString("N")[..8],
            IsActive = true
        });
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "L",
            Status = RegisterStatus.Open,
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = now,
            CreatedAt = now,
            IsActive = true
        });
        db.OfflineOrders.Add(new OfflineOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = registerId,
            OfflineOrderId = "OFFLINE-EXPIRED",
            OrderData = "{}",
            OrderTotal = 12m,
            PaymentMethod = "cash",
            Status = OfflineOrderStatuses.Pending,
            CreatedAtUtc = now.AddHours(-80),
            ExpiresAtUtc = now.AddHours(-1)
        });
        db.OfflineOrders.Add(new OfflineOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = registerId,
            OfflineOrderId = "OFFLINE-FRESH",
            OrderData = "{}",
            OrderTotal = 8m,
            PaymentMethod = "cash",
            Status = OfflineOrderStatuses.Pending,
            CreatedAtUtc = now,
            ExpiresAtUtc = now.AddHours(70)
        });
        await db.SaveChangesAsync();

        var audit = new Mock<IAuditLogService>();
        audit.Setup(x => x.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        var sut = new OfflineOrderService(
            db,
            Mock.Of<IPaymentService>(),
            audit.Object,
            accessor,
            TenantTestDoubles.SuperAdminHttpAccessor("cashier-1"),
            NullLogger<OfflineOrderService>.Instance);

        var marked = await sut.CleanupExpiredOrdersAsync();
        var pending = await sut.GetPendingOrdersAsync(registerId);

        Assert.Equal(1, marked);
        Assert.Equal(2, await db.OfflineOrders.CountAsync());
        Assert.Equal(OfflineOrderStatuses.Expired, (await db.OfflineOrders.SingleAsync(o => o.OfflineOrderId == "OFFLINE-EXPIRED")).Status);
        var fresh = Assert.Single(pending);
        Assert.Equal("OFFLINE-FRESH", fresh.OfflineOrderId);
    }

    private static async Task AssertCatalogRejectionAsync(bool active, bool archived)
    {
        var harness = await SeedReplayAsync(productActive: active, productArchived: archived);
        var payment = new Mock<IPaymentService>();
        var service = CreateOfflineService(harness.Context, payment.Object);

        await service.ReplayOfflineTransactionsAsync(harness.Request, "u1", "Cashier");

        var row = await harness.Context.OfflineTransactions.SingleAsync();
        Assert.Equal(OfflineTransactionStatus.Failed, row.Status);
        Assert.Equal("PRODUCT_INACTIVE", row.LastErrorCode);
        Assert.Null(row.SyncedPaymentId);
        Assert.Equal(0, await harness.Context.PaymentDetails.CountAsync());
        payment.Verify(
            p => p.CreatePaymentAsync(
                It.IsAny<CreatePaymentRequest>(),
                It.IsAny<string>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()),
            Times.Never);
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
        bool? productActive = true,
        bool productArchived = false)
    {
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase("medium-replay-" + Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options,
            TenantTestDoubles.TenantAccessorReturning(tenantId));

        context.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "T",
            Slug = "m" + tenantId.ToString("N")[..8],
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
        context.Products.Add(new Product
        {
            Id = productId,
            TenantId = tenantId,
            Name = "Döner",
            Price = 6.90m,
            IsActive = productActive ?? true,
            ArchivedAt = productArchived ? DateTime.UtcNow : null
        });

        var requestDto = new CreatePaymentRequest
        {
            CustomerId = Guid.NewGuid(),
            CashRegisterId = registerId,
            TableNumber = 1,
            TotalAmount = 6.90m,
            Payment = new PaymentMethodRequest { Method = "cash" },
            Items = [new PaymentItemRequest { ProductId = productId, Quantity = 1 }]
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

    private sealed record ReplayHarness(AppDbContext Context, ReplayOfflineTransactionsRequest Request);

    private sealed class ServiceProviderScope(ServiceProvider provider) : IServiceScope
    {
        public IServiceProvider ServiceProvider { get; } = provider;
        public void Dispose() => provider.Dispose();
    }
}
