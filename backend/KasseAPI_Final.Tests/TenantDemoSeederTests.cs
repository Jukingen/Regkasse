using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Seed;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TenantDemoSeederTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TenantDemoSeeder_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    [Fact]
    public async Task SeedAsync_AddsFourProductsAndTwoCustomersPerProfile_AndIsIdempotent()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Demo",
            Slug = "demo-test",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var seeder = new TenantDemoSeeder(db);
        var first = await seeder.SeedAsync(tenantId);

        Assert.Equal(DevTenantSeeder.Catalog.Count * 4 + 2, first.ProductsAdded);
        Assert.Equal(DevTenantSeeder.Catalog.Count * 2, first.CustomersAdded);
        Assert.NotNull(first.CashRegisterId);

        foreach (var profile in DevTenantSeeder.Catalog)
        {
            var categoryId = await db.Categories.IgnoreQueryFilters()
                .Where(row => row.TenantId == tenantId && row.Key == profile.ProfileId)
                .Select(row => row.Id)
                .SingleAsync();
            var productCount = await db.Products.IgnoreQueryFilters()
                .CountAsync(row => row.TenantId == tenantId && row.CategoryId == categoryId && row.IsActive);
            var customerCount = await db.Customers.IgnoreQueryFilters()
                .CountAsync(row =>
                    row.TenantId == tenantId
                    && row.CustomerNumber.StartsWith("D-")
                    && row.IsActive
                    && profile.CustomerNames.Contains(row.Name));

            var expectedProducts = profile.ProductNames.Count
                + (profile.ProfileId is VerticalProfileIds.HandyShop or VerticalProfileIds.HairSalon ? 1 : 0);
            Assert.Equal(expectedProducts, productCount);
            Assert.Equal(2, customerCount);
        }

        Assert.Null(first.FailedProfile);
        await DemoVerticalSeedAssertions.AssertAsync(db, tenantId);

        var register = await db.CashRegisters.IgnoreQueryFilters().SingleAsync(row => row.TenantId == tenantId);
        Assert.Equal(first.CashRegisterId, register.Id);
        Assert.Equal(DevTenantSeeder.RegisterNumber, register.RegisterNumber);
        Assert.Equal(RegisterStatus.Open, register.Status);
        Assert.Equal(
            1,
            await db.CashierShifts.IgnoreQueryFilters().CountAsync(row =>
                row.CashRegisterId == register.Id && row.Status == CashierShiftStatuses.Active && row.EndedAt == null));

        var second = await seeder.SeedAsync(tenantId);
        Assert.Equal(0, second.ProductsAdded);
        Assert.Equal(0, second.CustomersAdded);
        Assert.Equal(register.Id, second.CashRegisterId);
        Assert.Equal(first.ProductsAdded, await db.Products.IgnoreQueryFilters().CountAsync(row => row.TenantId == tenantId));
        Assert.Equal(first.CustomersAdded, await db.Customers.IgnoreQueryFilters().CountAsync(row => row.TenantId == tenantId));
        Assert.Equal(1, await db.CashRegisters.IgnoreQueryFilters().CountAsync(row => row.TenantId == tenantId));
        Assert.Null(second.FailedProfile);
        await DemoVerticalSeedAssertions.AssertAsync(db, tenantId);
    }

    [Fact]
    public async Task SeedAsync_MatchesExistingBarcodesAndCustomerNumbers()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Demo",
            Slug = "demo-existing",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var seeder = new TenantDemoSeeder(db);
        await seeder.SeedAsync(tenantId);
        var productsBefore = await db.Products.IgnoreQueryFilters().CountAsync(row => row.TenantId == tenantId);
        var customersBefore = await db.Customers.IgnoreQueryFilters().CountAsync(row => row.TenantId == tenantId);

        var again = await TenantDemoSeeder.SeedTenantAsync(db, tenantId);

        Assert.Equal(0, again.ProductsAdded);
        Assert.Equal(0, again.CustomersAdded);
        Assert.Equal(productsBefore, await db.Products.IgnoreQueryFilters().CountAsync(row => row.TenantId == tenantId));
        Assert.Equal(customersBefore, await db.Customers.IgnoreQueryFilters().CountAsync(row => row.TenantId == tenantId));
        Assert.Null(again.FailedProfile);
        await DemoVerticalSeedAssertions.AssertAsync(db, tenantId);
    }
}
