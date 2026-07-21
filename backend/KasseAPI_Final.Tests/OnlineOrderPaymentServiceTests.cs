using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Services.Order;
using KasseAPI_Final.Services.PaymentGateway;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class OnlineOrderPaymentServiceTests
{
    [Fact]
    public async Task CreatePaymentIntent_then_Confirm_marks_paid_and_notifies()
    {
        var (payments, _, db, activity, _) = CreateSut();
        var (tenantId, orderId) = await SeedOnlinePayableOrderAsync(db);

        var created = await payments.CreatePaymentIntentAsync(orderId, "pay-cafe");
        Assert.True(created.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(created.PaymentIntentId));
        Assert.False(string.IsNullOrWhiteSpace(created.ClientSecret));
        Assert.Equal("Mock", created.Provider);

        var confirmed = await payments.ConfirmPaymentAsync(created.PaymentIntentId!);
        Assert.True(confirmed.Succeeded);

        db.ChangeTracker.Clear();
        var order = await db.OnlineOrders.IgnoreQueryFilters().SingleAsync(o => o.Id == orderId);
        Assert.Equal(OnlineOrderPaymentStatuses.Paid, order.PaymentStatus);
        Assert.NotNull(order.PaidAt);
        Assert.Equal(created.PaymentIntentId, order.StripePaymentIntentId);

        activity.Verify(
            a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.OnlineOrderPaid,
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.Is<string?>(k => k != null && k.Contains(orderId.ToString("D"))),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CreatePaymentIntent_rejects_cash_orders()
    {
        var (payments, _, db, _, _) = CreateSut();
        var (_, orderId) = await SeedOnlinePayableOrderAsync(db, OnlineOrderPaymentMethods.Cash);

        var result = await payments.CreatePaymentIntentAsync(orderId, "pay-cafe");
        Assert.False(result.Succeeded);
        Assert.Equal(OnlineOrderPaymentService.InvalidPaymentMethodCode, result.Code);
    }

    [Fact]
    public async Task CreatePaymentIntent_requires_matching_tenant_slug()
    {
        var (payments, _, db, _, _) = CreateSut();
        var (_, orderId) = await SeedOnlinePayableOrderAsync(db);

        var result = await payments.CreatePaymentIntentAsync(orderId, "wrong-slug");
        Assert.False(result.Succeeded);
        Assert.Equal(OnlineOrderPaymentService.OrderNotFoundCode, result.Code);
    }

    [Fact]
    public async Task MarkPaidFromGateway_is_idempotent()
    {
        var (payments, _, db, activity, _) = CreateSut();
        var (_, orderId) = await SeedOnlinePayableOrderAsync(db);
        var created = await payments.CreatePaymentIntentAsync(orderId, "pay-cafe");
        Assert.True(created.Succeeded);

        var first = await payments.MarkPaidFromGatewayAsync(created.PaymentIntentId!);
        var second = await payments.MarkPaidFromGatewayAsync(created.PaymentIntentId!);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        activity.Verify(
            a => a.TryPublishAsync(
                It.IsAny<Guid>(),
                ActivityEventType.OnlineOrderPaid,
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UpdateStatus_notifies_activity()
    {
        var (_, status, db, activity, _) = CreateSut();
        var (tenantId, orderId) = await SeedOnlinePayableOrderAsync(db);

        var result = await status.UpdateStatusAsync(orderId, "accepted");
        Assert.True(result.Succeeded);
        Assert.Equal(OnlineOrderStatuses.Accepted, result.Order!.OrderStatus);

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

    private static async Task<(Guid TenantId, Guid OrderId)> SeedOnlinePayableOrderAsync(
        AppDbContext db,
        string paymentMethod = OnlineOrderPaymentMethods.Online)
    {
        var tenantId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Pay Cafe",
            Slug = "pay-cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.OnlineOrders.Add(new OnlineOrder
        {
            Id = orderId,
            TenantId = tenantId,
            OrderNumber = "ORD-PAY-1",
            CustomerName = "Max",
            CustomerPhone = "+43111",
            OrderType = OnlineOrderTypes.Takeaway,
            Subtotal = 10m,
            Tax = 2m,
            Total = 12m,
            PaymentMethod = paymentMethod,
            PaymentStatus = OnlineOrderPaymentStatuses.Pending,
            OrderStatus = OnlineOrderStatuses.Pending,
            Source = OnlineOrderSources.Web
        });
        await db.SaveChangesAsync();
        return (tenantId, orderId);
    }

    private static (
        OnlineOrderPaymentService Payments,
        OnlineOrderStatusService Status,
        AppDbContext Db,
        Mock<IActivityEventPublisher> Activity,
        MockCardGateway Gateway)
        CreateSut()
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
        var gateway = new MockCardGateway(
            NullLogger<MockCardGateway>.Instance,
            Options.Create(new PaymentGatewayOptions { SimulateDelayMs = 0 }));

        var payments = new OnlineOrderPaymentService(
            factory,
            NullCurrentTenantAccessor.Instance,
            gateway,
            notifications,
            TimeProvider.System,
            NullLogger<OnlineOrderPaymentService>.Instance);

        var status = new OnlineOrderStatusService(
            factory,
            NullCurrentTenantAccessor.Instance,
            notifications,
            Mock.Of<IOnlineOrderLoyaltyService>(),
            TimeProvider.System);

        return (payments, status, db, activity, gateway);
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
