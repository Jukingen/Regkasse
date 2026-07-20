using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Cache;
using KasseAPI_Final.Services.Metrics;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ProductServiceTests
{
    [Fact]
    public async Task GetProductsAsync_FiltersInactiveAndProjectsCategoryName()
    {
        await using var db = CreateContext();
        var tenantId = LegacyDefaultTenantIds.Primary;
        var catId = Guid.NewGuid();
        db.Categories.Add(new Category
        {
            Id = catId,
            TenantId = tenantId,
            Name = "Getränke",
            VatRate = 20m,
            Key = "drinks",
        });
        db.Products.AddRange(
            NewProduct(Guid.NewGuid(), tenantId, catId, "Cola", 3.5m, isActive: true),
            NewProduct(Guid.NewGuid(), tenantId, catId, "Hidden", 1m, isActive: false));
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        var items = await sut.GetProductsAsync(tenantId);

        Assert.Single(items);
        Assert.Equal("Cola", items[0].Name);
        Assert.Equal("Getränke", items[0].CategoryName);
        Assert.Equal(catId, items[0].CategoryId);
    }

    [Fact]
    public async Task GetProductsAsync_FiltersByCategoryId()
    {
        await using var db = CreateContext();
        var tenantId = LegacyDefaultTenantIds.Primary;
        var catA = Guid.NewGuid();
        var catB = Guid.NewGuid();
        db.Categories.AddRange(
            new Category { Id = catA, TenantId = tenantId, Name = "A", VatRate = 10m, Key = "a" },
            new Category { Id = catB, TenantId = tenantId, Name = "B", VatRate = 10m, Key = "b" });
        db.Products.AddRange(
            NewProduct(Guid.NewGuid(), tenantId, catA, "InA", 2m),
            NewProduct(Guid.NewGuid(), tenantId, catB, "InB", 2m));
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        var items = await sut.GetProductsAsync(tenantId, catA);

        Assert.Single(items);
        Assert.Equal("InA", items[0].Name);
    }

    [Fact]
    public async Task GetProductsPagedAsync_ReturnsTotalAndPageSlice()
    {
        await using var db = CreateContext();
        var tenantId = LegacyDefaultTenantIds.Primary;
        var catId = Guid.NewGuid();
        db.Categories.Add(new Category
        {
            Id = catId,
            TenantId = tenantId,
            Name = "Food",
            VatRate = 10m,
            Key = "food",
        });
        for (var i = 0; i < 5; i++)
            db.Products.Add(NewProduct(Guid.NewGuid(), tenantId, catId, $"P{i}", 1m + i));
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        var page = await sut.GetProductsPagedAsync(tenantId, page: 2, pageSize: 2);

        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(2, page.Items.Count());
    }

    [Fact]
    public async Task GetProductsAsync_UsesCacheUntilInvalidated()
    {
        await using var db = CreateContext();
        var tenantId = LegacyDefaultTenantIds.Primary;
        var catId = Guid.NewGuid();
        db.Categories.Add(new Category
        {
            Id = catId,
            TenantId = tenantId,
            Name = "Cat",
            VatRate = 10m,
            Key = "cat",
        });
        var productId = Guid.NewGuid();
        db.Products.Add(NewProduct(productId, tenantId, catId, "Cached", 2m));
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        Assert.Single(await sut.GetProductsAsync(tenantId));

        db.Products.Remove(await db.Products.SingleAsync(p => p.Id == productId));
        await db.SaveChangesAsync();

        // Still served from cache
        Assert.Single(await sut.GetProductsAsync(tenantId));

        await sut.InvalidateProductsCacheAsync(tenantId, productId);
        Assert.Empty(await sut.GetProductsAsync(tenantId));
    }

    private static ProductService CreateSut(AppDbContext db) =>
        new(db, new MemoryCacheService(
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<MemoryCacheService>.Instance,
            new CacheMetricsService()));

    private static Product NewProduct(
        Guid id,
        Guid tenantId,
        Guid categoryId,
        string name,
        decimal price,
        bool isActive = true) => new()
    {
        Id = id,
        TenantId = tenantId,
        Name = name,
        Price = price,
        CategoryId = categoryId,
        Category = "legacy",
        StockQuantity = 5,
        MinStockLevel = 0,
        Unit = "Stk",
        TaxType = TaxTypes.Standard,
        TaxRate = TaxTypes.GetTaxRate(TaxTypes.Standard),
        Barcode = $"bc-{id:N}",
        IsFiscalCompliant = true,
        IsTaxable = true,
        RksvProductType = RksvProductTypes.Standard,
        IsActive = isActive,
    };

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var ctx = new AppDbContext(
            options,
            TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        return ctx;
    }
}
