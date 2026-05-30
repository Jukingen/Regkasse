using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class CategoryDemoResetServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"category_reset_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void ResolveDemoDisplayName_UsesOriginalDemoNameFirst()
    {
        var category = new Category
        {
            Key = "pizza-mittel",
            Name = "Custom Pizza",
            OriginalDemoName = "Pizza, mittel",
        };

        Assert.Equal("Pizza, mittel", CategoryDemoResetService.ResolveDemoDisplayName(category));
    }

    [Fact]
    public void ResolveDemoDisplayName_FallsBackToSeedByKey()
    {
        var category = new Category
        {
            Key = "kebap",
            Name = "Custom Kebap",
        };

        Assert.Equal("Kebap", CategoryDemoResetService.ResolveDemoDisplayName(category));
    }

    [Fact]
    public async Task ResetDemoDisplayNamesAsync_RestoresNamesAndSyncsProducts()
    {
        await using var db = CreateDb();
        var tenantId = Guid.NewGuid();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Test", Slug = "test-reset", IsActive = true });

        var category = new Category
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = "salate",
            Name = "Meine Salate",
            OriginalDemoName = "Salate",
            IsSystemCategory = true,
            IsActive = true,
            VatRate = 10m,
            FiscalCategory = RksvProductCategory.Food,
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync();

        var service = new CategoryDemoResetService(db);
        var result = await service.ResetDemoDisplayNamesAsync(tenantId);

        Assert.Equal(1, result.ResetCount);
        Assert.Equal("Salate", category.Name);
    }
}
