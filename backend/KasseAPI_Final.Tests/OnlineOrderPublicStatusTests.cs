using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Order;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class OnlineOrderPublicStatusTests
{
    [Fact]
    public async Task GetPublicStatusAsync_returns_masked_status_for_valid_lookup()
    {
        var (sut, db) = CreateSut();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Tracker Cafe",
            Slug = "tracker-cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.OnlineOrders.Add(new OnlineOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNumber = "ORD-77",
            CustomerName = "Max Mustermann",
            CustomerPhone = "+43123456789",
            OrderType = OnlineOrderTypes.Takeaway,
            Subtotal = 10m,
            Tax = 2m,
            Total = 12m,
            PaymentMethod = OnlineOrderPaymentMethods.Cash,
            PaymentStatus = OnlineOrderPaymentStatuses.Pending,
            OrderStatus = OnlineOrderStatuses.Preparing,
            Source = OnlineOrderSources.Web
        });
        await db.SaveChangesAsync();

        var status = await sut.GetPublicStatusAsync("tracker-cafe", "ord-77", "6789");

        Assert.NotNull(status);
        Assert.Equal("ORD-77", status!.OrderNumber);
        Assert.Equal(OnlineOrderStatuses.Preparing, status.OrderStatus);
        Assert.Equal(12m, status.Total);
        Assert.Equal("Max M.", status.CustomerDisplayName);
    }

    [Fact]
    public async Task GetPublicStatusAsync_returns_null_on_phone_mismatch()
    {
        var (sut, db) = CreateSut();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Phone Cafe",
            Slug = "phone-cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.OnlineOrders.Add(new OnlineOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNumber = "ORD-1",
            CustomerName = "Anna",
            CustomerPhone = "111",
            OrderType = OnlineOrderTypes.Delivery,
            Total = 5m,
            OrderStatus = OnlineOrderStatuses.Pending,
            PaymentMethod = OnlineOrderPaymentMethods.Online,
            PaymentStatus = OnlineOrderPaymentStatuses.Pending,
            Source = OnlineOrderSources.Pwa
        });
        await db.SaveChangesAsync();

        var status = await sut.GetPublicStatusAsync("phone-cafe", "ORD-1", "999");
        Assert.Null(status);
    }

    private static (OnlineOrderQueryService Sut, AppDbContext Db) CreateSut()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options);
        var factory = new Factory(options);
        return (new OnlineOrderQueryService(factory), db);
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
