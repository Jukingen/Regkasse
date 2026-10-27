using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Seed;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DevTenantSeederTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DevTenantSeeder_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, NullCurrentTenantAccessor.Instance);
    }

    [Fact]
    public async Task SeedAsync_AddsActiveProductsPerProfile_AndOpensTheCashRegister()
    {
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant
        {
            Id = DemoTenantIds.Dev,
            Name = "Development",
            Slug = "dev",
            Status = TenantStatuses.Active,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        await DevTenantSeeder.SeedAsync(db);

        foreach (var profile in DevTenantSeeder.Catalog)
        {
            var categoryId = await db.Categories.IgnoreQueryFilters()
                .Where(row => row.TenantId == DemoTenantIds.Dev && row.Key == profile.ProfileId)
                .Select(row => row.Id)
                .SingleAsync();
            var products = await db.Products.IgnoreQueryFilters()
                .Where(row => row.TenantId == DemoTenantIds.Dev && row.CategoryId == categoryId)
                .ToListAsync();

            var expectedProducts = profile.ProductNames.Count
                + (profile.ProfileId is VerticalProfileIds.HandyShop or VerticalProfileIds.HairSalon ? 1 : 0);
            Assert.Equal(expectedProducts, products.Count);
            Assert.All(products, product => Assert.True(product.IsActive));
            Assert.InRange(products.Count, 3, 5);
        }

        await DemoVerticalSeedAssertions.AssertAsync(db, DemoTenantIds.Dev);

        var register = await db.CashRegisters.IgnoreQueryFilters()
            .SingleAsync(row => row.TenantId == DemoTenantIds.Dev);
        Assert.Equal(RegisterStatus.Open, register.Status);
        Assert.True(register.IsActive);
        Assert.Equal(
            1,
            await db.CashierShifts.IgnoreQueryFilters().CountAsync(row =>
                row.CashRegisterId == register.Id && row.Status == CashierShiftStatuses.Active));

        var before = await db.Products.IgnoreQueryFilters().CountAsync(row => row.TenantId == DemoTenantIds.Dev);
        await DevTenantSeeder.SeedAsync(db);
        Assert.Equal(before, await db.Products.IgnoreQueryFilters().CountAsync(row => row.TenantId == DemoTenantIds.Dev));
        Assert.Equal(RegisterStatus.Open, (await db.CashRegisters.IgnoreQueryFilters().SingleAsync(row => row.TenantId == DemoTenantIds.Dev)).Status);
        await DemoVerticalSeedAssertions.AssertAsync(db, DemoTenantIds.Dev);
    }
}
