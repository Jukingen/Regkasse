using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.DataRetention;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace KasseAPI_Final.Tests;

public sealed class RksvDataRetentionServiceTests
{
    [Fact]
    public async Task GetRetentionStatusAsync_MissingTenant_Throws()
    {
        var (_, factory) = CreateDb();
        var sut = new RksvDataRetentionService(factory);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GetRetentionStatusAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task GetRetentionStatusAsync_ReportsNonRksvCountsAndRetentionWindow()
    {
        var (db, factory) = CreateDb();
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var oldest = DateTime.UtcNow.AddYears(-3);

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "cafe",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "Main",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Espresso",
            Price = 2.5m,
            TaxType = 1,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.Customers.Add(new Customer
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = "Guest",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Walk-in",
            TableNumber = 1,
            CashierId = "c1",
            TotalAmount = 10m,
            TaxAmount = 1m,
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            CreatedAt = oldest,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var sut = new RksvDataRetentionService(factory);
        var report = await sut.GetRetentionStatusAsync(tenantId);

        Assert.Equal(tenantId, report.TenantId);
        Assert.Equal(7, report.RetentionYears);
        Assert.Equal(1, report.RksvData.PaymentDetailsCount);
        Assert.Equal(0, report.RksvData.PaymentDetailsPastRetentionCount);
        Assert.Equal(oldest, report.RksvData.OldestPaymentDate);
        Assert.Equal(oldest.AddYears(7), report.RksvData.RetentionUntil);
        Assert.Equal(oldest.AddYears(7).AddDays(1), report.RksvData.WillBeDeletedOn);
        Assert.True(report.RksvData.IsUnderRetentionObligation);
        Assert.Equal(1, report.NonRksvData.ProductsCount);
        Assert.Equal(1, report.NonRksvData.CustomersCount);
        Assert.True(report.NonRksvData.CanBeDeleted);
    }

    [Fact]
    public async Task GetRetentionStatusAsync_PastRetentionPayments_AreCountedSeparately()
    {
        var (db, factory) = CreateDb();
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        var ancient = DateTime.UtcNow.AddYears(-8);

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Old",
            Slug = "old",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "Main",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        db.PaymentDetails.Add(new PaymentDetails
        {
            Id = Guid.NewGuid(),
            CustomerId = Guid.NewGuid(),
            CustomerName = "Walk-in",
            TableNumber = 1,
            CashierId = "c1",
            TotalAmount = 5m,
            TaxAmount = 0.5m,
            Steuernummer = "ATU12345678",
            CashRegisterId = registerId,
            CreatedAt = ancient,
            IsActive = true,
        });
        await db.SaveChangesAsync();

        var report = await new RksvDataRetentionService(factory).GetRetentionStatusAsync(tenantId);

        Assert.Equal(1, report.RksvData.PaymentDetailsPastRetentionCount);
        Assert.False(report.RksvData.IsUnderRetentionObligation);
    }

    private static (AppDbContext Db, IDbContextFactory<AppDbContext> Factory) CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return (new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary)), new Factory(options));
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
