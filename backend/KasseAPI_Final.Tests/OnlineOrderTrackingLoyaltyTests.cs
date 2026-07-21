using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Services.Loyalty;
using KasseAPI_Final.Services.Order;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class OnlineOrderTrackingLoyaltyTests
{
    [Fact]
    public async Task UpdateStatus_records_history_and_notifies()
    {
        var (status, db, activity) = CreateStatusSut();
        var (tenantId, orderId) = await SeedOrderAsync(db);

        var result = await status.UpdateStatusAsync(orderId, "accepted", "actor-1");
        Assert.True(result.Succeeded);

        var changes = await db.OnlineOrderStatusChanges.IgnoreQueryFilters()
            .Where(c => c.OnlineOrderId == orderId)
            .ToListAsync();
        Assert.Single(changes);
        Assert.Equal(OnlineOrderStatuses.Pending, changes[0].FromStatus);
        Assert.Equal(OnlineOrderStatuses.Accepted, changes[0].ToStatus);
        Assert.Equal("actor-1", changes[0].ActorUserId);

        activity.Verify(
            a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.OnlineOrderStatusChanged,
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateStatus_rejects_skipped_lifecycle_step()
    {
        var (status, db, _) = CreateStatusSut();
        var (_, orderId) = await SeedOrderAsync(db);

        var result = await status.UpdateStatusAsync(orderId, "preparing", "actor-1");
        Assert.False(result.Succeeded);
        Assert.Equal(OnlineOrderStatusService.InvalidTransitionCode, result.Code);
    }

    [Fact]
    public async Task Loyalty_earns_points_on_completed_paid_order()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
        var factory = new Factory(options);
        var tenantId = Guid.NewGuid();
        var customerId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Loy Cafe",
            Slug = "loy-cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.Customers.Add(new Customer
        {
            Id = customerId,
            TenantId = tenantId,
            Name = "Max",
            Phone = "+43123456789",
            Email = "max@test.at",
            IsActive = true
        });
        db.OnlineOrders.Add(new OnlineOrder
        {
            Id = orderId,
            TenantId = tenantId,
            OrderNumber = "ORD-L1",
            CustomerName = "Max",
            CustomerPhone = "56789",
            OrderType = OnlineOrderTypes.Takeaway,
            Total = 12.50m,
            PaymentMethod = OnlineOrderPaymentMethods.Online,
            PaymentStatus = OnlineOrderPaymentStatuses.Paid,
            OrderStatus = OnlineOrderStatuses.Completed,
            Source = OnlineOrderSources.Web
        });
        await db.SaveChangesAsync();

        var loyalty = new OnlineOrderLoyaltyService(
            factory,
            new LoyaltyService(factory, TimeProvider.System, NullLogger<LoyaltyService>.Instance),
            TimeProvider.System,
            NullLogger<OnlineOrderLoyaltyService>.Instance);

        var order = await db.OnlineOrders.IgnoreQueryFilters().SingleAsync(o => o.Id == orderId);
        var earn = await loyalty.TryEarnOnCompletedAsync(order);
        Assert.True(earn.Succeeded);
        Assert.Equal(12, earn.PointsAwarded);

        db.ChangeTracker.Clear();
        var customer = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == customerId);
        Assert.Equal(12, customer.LoyaltyPoints);
        Assert.Equal(1, customer.VisitCount);

        var linked = await db.OnlineOrders.IgnoreQueryFilters().SingleAsync(o => o.Id == orderId);
        Assert.Equal(customerId, linked.CustomerId);
    }

    [Fact]
    public async Task Analytics_aggregates_orders()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var tenantAccessor = new StubTenantAccessor(tenantId);
        var db = new AppDbContext(options, tenantAccessor);
        var factory = new TenantAwareFactory(options, tenantAccessor);

        db.OnlineOrders.AddRange(
            new OnlineOrder
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrderNumber = "A1",
                CustomerName = "A",
                CustomerPhone = "1",
                OrderType = OnlineOrderTypes.Takeaway,
                Total = 10m,
                OrderStatus = OnlineOrderStatuses.Pending,
                PaymentStatus = OnlineOrderPaymentStatuses.Pending,
                PaymentMethod = OnlineOrderPaymentMethods.Cash,
                Source = OnlineOrderSources.Web,
                CreatedAt = DateTime.UtcNow
            },
            new OnlineOrder
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                OrderNumber = "A2",
                CustomerName = "B",
                CustomerPhone = "2",
                OrderType = OnlineOrderTypes.Delivery,
                Total = 20m,
                OrderStatus = OnlineOrderStatuses.Completed,
                PaymentStatus = OnlineOrderPaymentStatuses.Paid,
                PaymentMethod = OnlineOrderPaymentMethods.Online,
                Source = OnlineOrderSources.Pwa,
                CreatedAt = DateTime.UtcNow,
                AcceptedAt = DateTime.UtcNow.AddMinutes(-30),
                ReadyAt = DateTime.UtcNow.AddMinutes(-10),
                CompletedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var sut = new OnlineOrderQueryService(factory);
        var analytics = await sut.GetAnalyticsAsync();
        Assert.Equal(2, analytics.TotalOrders);
        Assert.Equal(1, analytics.Pending);
        Assert.Equal(1, analytics.Completed);
        Assert.True(analytics.AvgAcceptToReadyMinutes is >= 19 and <= 21);
    }

    private static async Task<(Guid TenantId, Guid OrderId)> SeedOrderAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        db.OnlineOrders.Add(new OnlineOrder
        {
            Id = orderId,
            TenantId = tenantId,
            OrderNumber = "ORD-T1",
            CustomerName = "Anna",
            CustomerPhone = "111",
            OrderType = OnlineOrderTypes.Takeaway,
            Total = 8m,
            OrderStatus = OnlineOrderStatuses.Pending,
            PaymentMethod = OnlineOrderPaymentMethods.Cash,
            PaymentStatus = OnlineOrderPaymentStatuses.Pending,
            Source = OnlineOrderSources.Web
        });
        await db.SaveChangesAsync();
        return (tenantId, orderId);
    }

    private static (OnlineOrderStatusService Status, AppDbContext Db, Mock<IActivityEventPublisher> Activity)
        CreateStatusSut()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
        var factory = new Factory(options);
        var activity = new Mock<IActivityEventPublisher>();
        activity.Setup(a => a.TryPublishAsync(
                It.IsAny<Guid>(),
                It.IsAny<ActivityEventType>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var notifications = new OnlineOrderNotificationService(
            activity.Object,
            Mock.Of<IOnlineOrderCustomerEmailService>(),
            Mock.Of<IOnlineOrderPushSender>(),
            NullLogger<OnlineOrderNotificationService>.Instance);

        var status = new OnlineOrderStatusService(
            factory,
            NullCurrentTenantAccessor.Instance,
            notifications,
            Mock.Of<IOnlineOrderLoyaltyService>(),
            TimeProvider.System);

        return (status, db, activity);
    }

    private sealed class StubTenantAccessor : ICurrentTenantAccessor
    {
        public StubTenantAccessor(Guid tenantId) => TenantId = tenantId;
        public Guid? TenantId { get; set; }
    }

    private sealed class TenantAwareFactory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        private readonly ICurrentTenantAccessor _tenant;
        public TenantAwareFactory(DbContextOptions<AppDbContext> options, ICurrentTenantAccessor tenant)
        {
            _options = options;
            _tenant = tenant;
        }
        public AppDbContext CreateDbContext() => new(_options, _tenant);
        public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            new(new AppDbContext(_options, _tenant));
    }

    private sealed class Factory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public Factory(DbContextOptions<AppDbContext> options) => _options = options;
        public AppDbContext CreateDbContext() => new(_options);
        public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            new(new AppDbContext(_options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary)));
    }
}
