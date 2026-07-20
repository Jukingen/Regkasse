using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Order;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PublicCustomerDashboardServiceTests
{
    [Fact]
    public async Task GetDashboard_returns_loyalty_and_orders_for_matching_phone()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options);
        var factory = new Factory(options);
        var tenantId = Guid.NewGuid();

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "cafe-demo",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        db.Customers.Add(new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Max Mustermann",
            Phone = "+43 664 1234567",
            Email = "max@test.at",
            IsActive = true,
            LoyaltyPoints = 250,
            TotalSpent = 80.5m
        });
        db.OnlineOrders.Add(new OnlineOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            OrderNumber = "ORD-1",
            CustomerName = "Max Mustermann",
            CustomerPhone = "06641234567",
            OrderType = OnlineOrderTypes.Takeaway,
            Total = 15m,
            OrderStatus = OnlineOrderStatuses.Completed,
            PaymentStatus = OnlineOrderPaymentStatuses.Paid,
            PaymentMethod = OnlineOrderPaymentMethods.Online,
            Source = OnlineOrderSources.Web,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        await db.SaveChangesAsync();

        var sut = new PublicCustomerDashboardService(factory);
        var dto = await sut.GetDashboardAsync("cafe-demo", "6641234567");

        Assert.NotNull(dto);
        Assert.Equal(250, dto!.LoyaltyPoints);
        Assert.Equal(2m, dto.RedeemableEuro);
        Assert.Equal(1, dto.TotalOrders);
        Assert.Single(dto.Orders);
        Assert.Equal("ORD-1", dto.Orders[0].OrderNumber);
        Assert.Contains('*', dto.CustomerDisplayName);
    }

    [Fact]
    public async Task GetDashboard_returns_null_when_unknown()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new AppDbContext(options);
        var factory = new Factory(options);
        db.Tenants.Add(new Tenant
        {
            Id = Guid.NewGuid(),
            Name = "Cafe",
            Slug = "cafe-demo",
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var sut = new PublicCustomerDashboardService(factory);
        Assert.Null(await sut.GetDashboardAsync("cafe-demo", "9999999999"));
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
