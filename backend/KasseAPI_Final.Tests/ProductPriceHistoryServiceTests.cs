using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ProductPriceHistoryServiceTests
{
    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"price_history_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    [Fact]
    public async Task EnsureInitialVersionAsync_SeedsHistoryAndCurrentVersion()
    {
        var tenantId = SystemTenantIds.Platform;
        await using var db = CreateDb(tenantId);
        var (productId, taxGroupId) = await SeedProductAsync(db, tenantId, price: 3.5m, taxRate: 20m);

        var sut = new ProductPriceHistoryService(db, NullLogger<ProductPriceHistoryService>.Instance);
        await sut.EnsureInitialVersionAsync(tenantId, productId, 3.5m, taxGroupId, 20m, Guid.NewGuid());

        var history = await sut.GetHistoryAsync(tenantId, productId);
        Assert.Single(history);
        Assert.True(history[0].IsActive);
        Assert.Equal(3.5m, history[0].NewPrice);
        Assert.True(history[0].IsRksvCompliant);

        var current = await sut.GetCurrentVersionAsync(tenantId, productId);
        Assert.NotNull(current);
        Assert.Equal("1.0", current!.Version);
        Assert.True(current.IsCurrent);
        Assert.Equal(3.5m, current.Price);
    }

    [Fact]
    public async Task RecordChangeAsync_ClosesPreviousAndBumpsVersion()
    {
        var tenantId = SystemTenantIds.Platform;
        await using var db = CreateDb(tenantId);
        var (productId, taxGroupId) = await SeedProductAsync(db, tenantId, price: 3.5m, taxRate: 20m);

        var sut = new ProductPriceHistoryService(db, NullLogger<ProductPriceHistoryService>.Instance);
        var actor = Guid.NewGuid();
        await sut.EnsureInitialVersionAsync(tenantId, productId, 3.5m, taxGroupId, 20m, actor);

        await sut.RecordChangeAsync(
            tenantId,
            productId,
            oldPrice: 3.5m,
            newPrice: 4.0m,
            oldTaxGroupId: taxGroupId,
            newTaxGroupId: taxGroupId,
            oldTaxRate: 20m,
            newTaxRate: 20m,
            changedBy: actor,
            reason: "Price increase");

        var history = await sut.GetHistoryAsync(tenantId, productId);
        Assert.Equal(2, history.Count);
        Assert.Single(history.Where(h => h.IsActive));
        Assert.Equal(4.0m, history.First(h => h.IsActive).NewPrice);
        Assert.Equal("Price increase", history.First(h => h.IsActive).Reason);

        var versions = await sut.GetVersionsAsync(tenantId, productId);
        Assert.Equal(2, versions.Count);
        Assert.Single(versions.Where(v => v.IsCurrent));
        Assert.Equal("2.0", versions.First(v => v.IsCurrent).Version);
        Assert.Equal(4.0m, versions.First(v => v.IsCurrent).Price);
    }

    [Fact]
    public async Task RecordChangeAsync_NoOpWhenUnchanged()
    {
        var tenantId = SystemTenantIds.Platform;
        await using var db = CreateDb(tenantId);
        var (productId, taxGroupId) = await SeedProductAsync(db, tenantId, price: 3.5m, taxRate: 20m);

        var sut = new ProductPriceHistoryService(db, NullLogger<ProductPriceHistoryService>.Instance);
        await sut.EnsureInitialVersionAsync(tenantId, productId, 3.5m, taxGroupId, 20m, Guid.NewGuid());

        var result = await sut.RecordChangeAsync(
            tenantId,
            productId,
            3.5m,
            3.5m,
            taxGroupId,
            taxGroupId,
            20m,
            20m,
            Guid.NewGuid());

        Assert.Null(result);
        Assert.Single(await sut.GetHistoryAsync(tenantId, productId));
    }

    private static async Task<(Guid ProductId, Guid TaxGroupId)> SeedProductAsync(
        AppDbContext db,
        Guid tenantId,
        decimal price,
        decimal taxRate)
    {
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Legacy", Slug = "legacy", IsActive = true });

        var taxGroupId = Guid.NewGuid();
        db.TaxGroups.Add(new TaxGroup
        {
            Id = taxGroupId,
            TenantId = tenantId,
            Name = "Normalsatz",
            Rate = taxRate,
            IsActive = true,
            IsSystem = true,
            CreatedAt = DateTime.UtcNow,
        });

        var categoryId = Guid.NewGuid();
        db.Categories.Add(new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Key = "test",
            Name = "Test",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        var productId = Guid.NewGuid();
        db.Products.Add(new Product
        {
            Id = productId,
            TenantId = tenantId,
            CategoryId = categoryId,
            TaxGroupId = taxGroupId,
            Name = "Espresso",
            Category = "Test",
            Price = price,
            TaxRate = taxRate,
            TaxType = TaxTypes.FromRate(taxRate),
            Unit = "pcs",
            Barcode = "ESP-1",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return (productId, taxGroupId);
    }
}
