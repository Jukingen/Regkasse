using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Activity;
using KasseAPI_Final.Services.Email;
using KasseAPI_Final.Services.Order;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class OrderIntegrationServiceTests
{
    [Fact]
    public async Task PushOrderToPosAsync_creates_cart_links_order_and_notifies()
    {
        var (sut, db, activity) = CreateSut(nameof(PushOrderToPosAsync_creates_cart_links_order_and_notifies));
        var (tenantId, userId, orderId) = await SeedOrderAsync(db);

        var result = await sut.PushOrderToPosAsync(orderId, userId);

        Assert.True(result.Succeeded);
        Assert.False(result.AlreadyPushed);
        Assert.False(string.IsNullOrWhiteSpace(result.PosCartId));

        db.ChangeTracker.Clear();
        var order = await db.OnlineOrders.IgnoreQueryFilters().SingleAsync(o => o.Id == orderId);
        Assert.Equal(result.PosCartId, order.PosCartId);
        Assert.Equal(OnlineOrderStatuses.Accepted, order.OrderStatus);
        Assert.NotNull(order.AcceptedAt);
        Assert.NotNull(order.PushedToPosAt);

        var cart = await db.Carts.Include(c => c.Items).SingleAsync(c => c.CartId == result.PosCartId);
        Assert.Equal(userId, cart.UserId);
        Assert.Equal(CartStatus.Active, cart.Status);
        Assert.Single(cart.Items);
        Assert.Equal(2, cart.Items.First().Quantity);

        activity.Verify(
            a => a.TryPublishAsync(
                tenantId,
                ActivityEventType.OnlineOrderPushedToPos,
                It.IsAny<object?>(),
                userId,
                It.Is<string?>(k => k != null && k.Contains(orderId.ToString("D"))),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PushOrderToPosAsync_is_idempotent_when_already_pushed()
    {
        var (sut, db, activity) = CreateSut(nameof(PushOrderToPosAsync_is_idempotent_when_already_pushed));
        var (_, userId, orderId) = await SeedOrderAsync(db);

        var first = await sut.PushOrderToPosAsync(orderId, userId);
        var second = await sut.PushOrderToPosAsync(orderId, userId);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.True(second.AlreadyPushed);
        Assert.Equal(first.PosCartId, second.PosCartId);
        Assert.Equal(1, await db.Carts.CountAsync());

        activity.Verify(
            a => a.TryPublishAsync(
                It.IsAny<Guid>(),
                ActivityEventType.OnlineOrderPushedToPos,
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PushOrderToPosAsync_rejects_cancelled_orders()
    {
        var (sut, db, _) = CreateSut(nameof(PushOrderToPosAsync_rejects_cancelled_orders));
        var (_, userId, orderId) = await SeedOrderAsync(db);
        var order = await db.OnlineOrders.IgnoreQueryFilters().SingleAsync(o => o.Id == orderId);
        order.OrderStatus = OnlineOrderStatuses.Cancelled;
        await db.SaveChangesAsync();

        var result = await sut.PushOrderToPosAsync(orderId, userId);

        Assert.False(result.Succeeded);
        Assert.Equal(OrderIntegrationService.OrderCancelledCode, result.Code);
    }

    [Fact]
    public async Task PushOrderToPosAsync_requires_claiming_user()
    {
        var (sut, db, _) = CreateSut(nameof(PushOrderToPosAsync_requires_claiming_user));
        var (_, _, orderId) = await SeedOrderAsync(db);

        var result = await sut.PushOrderToPosAsync(orderId, " ");

        Assert.False(result.Succeeded);
        Assert.Equal(OrderIntegrationService.ClaimingUserRequiredCode, result.Code);
    }

    private static async Task<(Guid TenantId, string UserId, Guid OrderId)> SeedOrderAsync(AppDbContext db)
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid().ToString("D");
        var orderId = Guid.NewGuid();
        var itemId = Guid.NewGuid();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Push Cafe",
            Slug = "push-cafe-" + tenantId.ToString("N")[..8],
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = "cashier@" + tenantId.ToString("N")[..6],
            Email = "cashier@" + tenantId.ToString("N")[..6] + ".test",
            FirstName = "Cash",
            LastName = "Ier",
            Role = "Cashier"
        });
        db.OnlineOrders.Add(new OnlineOrder
        {
            Id = orderId,
            TenantId = tenantId,
            OrderNumber = "ORD-100",
            CustomerName = "Anna",
            CustomerPhone = "+43111",
            OrderType = OnlineOrderTypes.Takeaway,
            Subtotal = 10m,
            Tax = 2m,
            Total = 12m,
            PaymentMethod = OnlineOrderPaymentMethods.Cash,
            PaymentStatus = OnlineOrderPaymentStatuses.Pending,
            OrderStatus = OnlineOrderStatuses.Pending,
            Source = OnlineOrderSources.Web,
            Items =
            [
                new OnlineOrderItem
                {
                    Id = itemId,
                    OnlineOrderId = orderId,
                    ProductId = Guid.NewGuid(),
                    ProductName = "Latte",
                    Quantity = 2,
                    Price = 5m,
                    Total = 10m,
                    Modifiers =
                    [
                        new OnlineOrderItemModifier
                        {
                            Id = Guid.NewGuid(),
                            OnlineOrderItemId = itemId,
                            Name = "Oat milk",
                            Price = 0.5m,
                            Quantity = 1
                        }
                    ]
                }
            ]
        });
        await db.SaveChangesAsync();
        return (tenantId, userId, orderId);
    }

    private static (OrderIntegrationService Sut, AppDbContext Db, Mock<IActivityEventPublisher> Activity)
        CreateSut(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name + Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options);
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

        var sut = new OrderIntegrationService(
            factory,
            NullCurrentTenantAccessor.Instance,
            notifications,
            TimeProvider.System,
            NullLogger<OrderIntegrationService>.Instance);

        return (sut, db, activity);
    }

    private sealed class Factory : IDbContextFactory<AppDbContext>
    {
        private readonly DbContextOptions<AppDbContext> _options;
        public Factory(DbContextOptions<AppDbContext> options) => _options = options;
        public AppDbContext CreateDbContext() => new(_options);
        public ValueTask<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            new(new AppDbContext(_options));
    }
}
