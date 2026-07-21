using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class CategorySeedDataTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"category_seed_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    [Fact]
    public async Task SeedLegacyDevCategoriesAsync_CreatesFiveCategoriesOnEmptyDatabase()
    {
        await using var db = CreateDb();
        var tenantId = LegacyDefaultTenantIds.Primary;
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Legacy", Slug = "legacy", IsActive = true });
        await db.SaveChangesAsync();

        var created = await CategorySeedData.SeedLegacyDevCategoriesAsync(db, tenantId);

        Assert.Equal(5, created);
        Assert.Equal(5, await db.Categories.IgnoreQueryFilters().CountAsync(c => c.TenantId == tenantId));
        Assert.Contains(await db.Categories.IgnoreQueryFilters().ToListAsync(), c => c.Name == "Desserts" && c.Key == "desserts");
    }

    [Fact]
    public async Task SeedLegacyDevCategoriesAsync_IsIdempotentWhenCategoriesAlreadyExist()
    {
        await using var db = CreateDb();
        var tenantId = LegacyDefaultTenantIds.Primary;
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Legacy", Slug = "legacy", IsActive = true });
        db.Categories.Add(new Category
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "desserts-2",
            Name = "Desserts",
            VatRate = 10m,
            FiscalCategory = RksvProductCategory.Food,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var created = await CategorySeedData.SeedLegacyDevCategoriesAsync(db, tenantId);

        Assert.Equal(4, created);
        Assert.Equal(5, await db.Categories.IgnoreQueryFilters().CountAsync(c => c.TenantId == tenantId));
        Assert.Equal(1, await db.Categories.IgnoreQueryFilters().CountAsync(c => c.TenantId == tenantId && c.Name == "Desserts"));

        var secondPass = await CategorySeedData.SeedLegacyDevCategoriesAsync(db, tenantId);
        Assert.Equal(0, secondPass);
        Assert.Equal(5, await db.Categories.IgnoreQueryFilters().CountAsync(c => c.TenantId == tenantId));
    }
}
