using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class CategoryDemoResetServiceTests
{
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
}

[Collection("PostgreSqlReplay")]
[Trait("Category", "PostgreSql")]
public sealed class CategoryDemoResetServicePostgreSqlTests
{
    private readonly PostgreSqlReplayFixture _fixture;

    public CategoryDemoResetServicePostgreSqlTests(PostgreSqlReplayFixture fixture) => _fixture = fixture;

    [SkippableFact]
    public async Task ResetDemoDisplayNamesAsync_RestoresNamesAndSyncsProducts()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        var tenantId = Guid.NewGuid();
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseAppNpgsql(_fixture.ConnectionString).Options,
            TenantTestDoubles.TenantAccessorReturning(tenantId));

        TenantTestDoubles.EnsureTenant(db, tenantId);
        var category = new Category
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Key = $"salate-{tenantId:N}"[..20],
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
