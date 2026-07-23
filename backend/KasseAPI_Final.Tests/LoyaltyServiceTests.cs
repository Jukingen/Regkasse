using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Loyalty;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LoyaltyServiceTests
{
    [Fact]
    public async Task AddPoints_awards_one_point_per_whole_euro()
    {
        var (sut, db, customerId) = await CreateSutAsync(startingPoints: 0);

        var result = await sut.AddPointsAsync(customerId, 12.50m);
        Assert.True(result.Succeeded);
        Assert.Equal(12, result.Value);
        Assert.Equal(12, result.Balance);

        db.ChangeTracker.Clear();
        var customer = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == customerId);
        Assert.Equal(12, customer.LoyaltyPoints);
        Assert.Equal(12.50m, customer.TotalSpent);
    }

    [Fact]
    public async Task RedeemPoints_converts_100_points_to_1_euro()
    {
        var (sut, db, customerId) = await CreateSutAsync(startingPoints: 250);

        var result = await sut.RedeemPointsAsync(customerId, 200);
        Assert.True(result.Succeeded);
        Assert.Equal(2m, result.Value);
        Assert.Equal(50, result.Balance);

        db.ChangeTracker.Clear();
        var customer = await db.Customers.IgnoreQueryFilters().SingleAsync(c => c.Id == customerId);
        Assert.Equal(50, customer.LoyaltyPoints);
    }

    [Fact]
    public async Task RedeemPoints_fails_when_insufficient()
    {
        var (sut, _, customerId) = await CreateSutAsync(startingPoints: 10);

        var result = await sut.RedeemPointsAsync(customerId, 100);
        Assert.False(result.Succeeded);
        Assert.Equal(LoyaltyService.InsufficientCode, result.Code);
    }

    [Fact]
    public async Task AddPoints_rejects_system_customer()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var tenant = new StubTenantAccessor(tenantId);
        var db = new AppDbContext(options, tenant);
        var factory = new TenantAwareFactory(options, tenant);
        var customerId = Guid.NewGuid();

        db.Customers.Add(new Customer
        {
            Id = customerId,
            TenantId = tenantId,
            Name = "Guest",
            IsSystem = true,
            IsActive = true,
            LoyaltyPoints = 0
        });
        await db.SaveChangesAsync();

        var sut = new LoyaltyService(factory, TimeProvider.System, NullLogger<LoyaltyService>.Instance);
        var result = await sut.AddPointsAsync(customerId, 20m);
        Assert.False(result.Succeeded);
        Assert.Equal(LoyaltyService.SystemCustomerCode, result.Code);
    }

    private static async Task<(LoyaltyService Sut, AppDbContext Db, Guid CustomerId)> CreateSutAsync(int startingPoints)
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var tenant = new StubTenantAccessor(tenantId);
        var db = new AppDbContext(options, tenant);
        var factory = new TenantAwareFactory(options, tenant);
        var customerId = Guid.NewGuid();

        db.Customers.Add(new Customer
        {
            Id = customerId,
            TenantId = tenantId,
            Name = "Max",
            Phone = "+431111",
            Email = "max@test.at",
            IsActive = true,
            LoyaltyPoints = startingPoints
        });
        await db.SaveChangesAsync();

        var sut = new LoyaltyService(factory, TimeProvider.System, NullLogger<LoyaltyService>.Instance);
        return (sut, db, customerId);
    }

    private sealed class StubTenantAccessor : ICurrentTenantAccessor
    {
        public StubTenantAccessor(Guid tenantId) => TenantId = tenantId;
        public Guid? TenantId { get; set; }
    public string? TenantSlug { get; set; }
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
}
