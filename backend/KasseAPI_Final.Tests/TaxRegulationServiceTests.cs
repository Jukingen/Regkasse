using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TaxRegulationServiceTests
{
    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tax_regulation_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static TaxRegulationService CreateService(AppDbContext db) =>
        new(db, NullLogger<TaxRegulationService>.Instance);

    [Fact]
    public async Task GetCurrentRegulationAsync_ReturnsActiveAustrianBands()
    {
        await using var db = CreateDb(SystemTenantIds.Platform);
        var sut = CreateService(db);

        var current = await sut.GetCurrentRegulationAsync();

        Assert.True(current.IsActive);
        Assert.Equal(20m, current.StandardRate);
        Assert.Equal(10m, current.ReducedRate);
        Assert.Equal(4.9m, current.ReducedNewRate);
        Assert.Equal(13m, current.MiddleRate);
        Assert.Equal(0m, current.ZeroRate);
        Assert.Contains(4.9m, current.AllowedRates);
    }

    [Fact]
    public async Task GetRegulationHistoryAsync_IncludesCurrentAndPrior()
    {
        await using var db = CreateDb(SystemTenantIds.Platform);
        var sut = CreateService(db);

        var history = (await sut.GetRegulationHistoryAsync()).ToList();

        Assert.True(history.Count >= 2);
        Assert.Contains(history, r => r.IsActive && r.ReducedNewRate == 4.9m);
        Assert.Contains(history, r => !r.IsActive && !r.AllowedRates.Contains(4.9m));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(4.9, true)]
    [InlineData(10, true)]
    [InlineData(13, true)]
    [InlineData(20, true)]
    [InlineData(19, false)]
    [InlineData(5, false)]
    [InlineData(7.5, false)]
    public async Task IsTaxRateValidAsync_MatchesCurrentCatalog(decimal rate, bool expected)
    {
        await using var db = CreateDb(SystemTenantIds.Platform);
        var sut = CreateService(db);

        var actual = await sut.IsTaxRateValidAsync(rate);

        Assert.Equal(expected, actual);
    }

    [Fact]
    public async Task GetTaxChangeImpactAsync_CountsProductsAtOldRate()
    {
        var tenantId = SystemTenantIds.Platform;
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Legacy", Slug = "legacy", IsActive = true });

        var taxGroupId = Guid.NewGuid();
        db.TaxGroups.Add(new TaxGroup
        {
            Id = taxGroupId,
            TenantId = tenantId,
            Name = "Ermäßigt",
            Rate = 10m,
            IsActive = true,
            IsSystem = true,
            AustrianCode = "B",
            GroupType = TaxGroupType.Reduced,
            CreatedAt = DateTime.UtcNow,
        });

        var categoryId = Guid.NewGuid();
        db.Categories.Add(new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Name = "Test",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        db.Products.AddRange(
            CreateProduct(tenantId, categoryId, taxGroupId, "A", 100m, 10m),
            CreateProduct(tenantId, categoryId, taxGroupId, "B", 50m, 10m),
            CreateProduct(tenantId, categoryId, taxGroupId, "C", 200m, 20m));
        await db.SaveChangesAsync();

        var sut = CreateService(db);
        var impact = await sut.GetTaxChangeImpactAsync(tenantId, 10m, 13m);

        Assert.Equal(2, impact.AffectedProductCount);
        Assert.Equal(150m, impact.AffectedCatalogValue);
        Assert.Equal(4.5m, impact.EstimatedVatDelta);
    }

    private static Product CreateProduct(
        Guid tenantId,
        Guid categoryId,
        Guid taxGroupId,
        string name,
        decimal price,
        decimal taxRate) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CategoryId = categoryId,
            TaxGroupId = taxGroupId,
            Name = name,
            Category = "Test",
            Price = price,
            TaxRate = taxRate,
            TaxType = TaxTypes.FromRate(taxRate),
            Unit = "pcs",
            Barcode = $"BC-{name}",
            StockQuantity = 0,
            MinStockLevel = 0,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
}
