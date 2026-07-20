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

namespace KasseAPI_Final.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ProductService"/> read paths (projection + paging).
/// Does not cover create/update — those live on admin/product controllers, not <see cref="IProductService"/>.
/// </summary>
public sealed class ProductServiceTests : IAsyncDisposable
{
    private readonly AppDbContext _db;
    private readonly ProductService _service;
    private readonly Guid _tenantId;

    public ProductServiceTests()
    {
        _tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(_tenantId));
        _db.Tenants.Add(new Tenant
        {
            Id = _tenantId,
            Name = "Test Tenant",
            Slug = $"t-{_tenantId:N}"[..20],
        });
        _db.SaveChanges();

        _service = new ProductService(
            _db,
            new MemoryCacheService(
                new MemoryCache(new MemoryCacheOptions()),
                NullLogger<MemoryCacheService>.Instance,
                new CacheMetricsService()));
    }

    public async ValueTask DisposeAsync() => await _db.DisposeAsync();

    [Fact]
    public async Task GetProductsAsync_WhenTenantHasProducts_ReturnsProducts()
    {
        var category = NewCategory("Test Category");
        _db.Categories.Add(category);
        _db.Products.AddRange(
            NewProduct(category.Id, "Product 1", 10.99m),
            NewProduct(category.Id, "Product 2", 15.99m));
        await _db.SaveChangesAsync();

        var result = await _service.GetProductsAsync(_tenantId);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.Name == "Product 1");
        Assert.Contains(result, p => p.Name == "Product 2");
        Assert.All(result, p => Assert.True(p.IsActive));
        Assert.Contains(result, p => p.Price == 10.99m);
        Assert.Equal("Test Category", result.First(p => p.Name == "Product 1").CategoryName);
    }

    [Fact]
    public async Task GetProductsAsync_WhenNoProducts_ReturnsEmptyList()
    {
        var result = await _service.GetProductsAsync(_tenantId);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetProductsAsync_ExcludesInactiveProducts()
    {
        var category = NewCategory("Cat");
        _db.Categories.Add(category);
        _db.Products.AddRange(
            NewProduct(category.Id, "Active", 5m, isActive: true),
            NewProduct(category.Id, "Inactive", 5m, isActive: false));
        await _db.SaveChangesAsync();

        var result = await _service.GetProductsAsync(_tenantId);

        Assert.Single(result);
        Assert.Equal("Active", result[0].Name);
    }

    [Fact]
    public async Task GetProductsAsync_WhenCategoryFilter_ReturnsOnlyMatchingCategory()
    {
        var catA = NewCategory("A", key: "a");
        var catB = NewCategory("B", key: "b");
        _db.Categories.AddRange(catA, catB);
        _db.Products.AddRange(
            NewProduct(catA.Id, "InA", 1m),
            NewProduct(catB.Id, "InB", 2m));
        await _db.SaveChangesAsync();

        var result = await _service.GetProductsAsync(_tenantId, catA.Id);

        Assert.Single(result);
        Assert.Equal("InA", result[0].Name);
        Assert.Equal(catA.Id, result[0].CategoryId);
    }

    [Fact]
    public async Task GetProductsAsync_DoesNotReturnOtherTenantProducts()
    {
        var otherTenantId = Guid.NewGuid();
        _db.Tenants.Add(new Tenant
        {
            Id = otherTenantId,
            Name = "Other",
            Slug = $"o-{otherTenantId:N}"[..20],
        });
        var ownCat = NewCategory("Own");
        var otherCat = new Category
        {
            Id = Guid.NewGuid(),
            TenantId = otherTenantId,
            Name = "Other Cat",
            Key = "other",
            VatRate = 20m,
        };
        _db.Categories.AddRange(ownCat, otherCat);
        _db.Products.AddRange(
            NewProduct(ownCat.Id, "Mine", 1m),
            new Product
            {
                Id = Guid.NewGuid(),
                TenantId = otherTenantId,
                CategoryId = otherCat.Id,
                Name = "Theirs",
                Price = 9m,
                Category = "Other Cat",
                StockQuantity = 1,
                MinStockLevel = 0,
                Unit = "Stk",
                TaxType = TaxTypes.Standard,
                TaxRate = TaxTypes.GetTaxRate(TaxTypes.Standard),
                Barcode = $"bc-{Guid.NewGuid():N}",
                IsFiscalCompliant = true,
                IsTaxable = true,
                RksvProductType = RksvProductTypes.Standard,
                IsActive = true,
            });
        await _db.SaveChangesAsync();

        var result = await _service.GetProductsAsync(_tenantId);

        Assert.Single(result);
        Assert.Equal("Mine", result[0].Name);
    }

    [Fact]
    public async Task GetProductsPagedAsync_WhenProductsExist_ReturnsPagedEnvelope()
    {
        var category = NewCategory("Food");
        _db.Categories.Add(category);
        for (var i = 0; i < 5; i++)
            _db.Products.Add(NewProduct(category.Id, $"P{i}", 1m + i));
        await _db.SaveChangesAsync();

        var page = await _service.GetProductsPagedAsync(_tenantId, page: 2, pageSize: 2);

        Assert.Equal(5, page.Total);
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(2, page.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(2, page.Items.Count);
    }

    [Fact]
    public async Task GetProductsPagedAsync_WhenNoProducts_ReturnsEmptyPage()
    {
        var page = await _service.GetProductsPagedAsync(_tenantId, page: 1, pageSize: 20);

        Assert.Empty(page.Items);
        Assert.Equal(0, page.Total);
        Assert.Equal(0, page.TotalPages);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
    }

    [Fact]
    public async Task GetProductsPagedAsync_ClampsInvalidPageAndPageSize()
    {
        var category = NewCategory("Clamp");
        _db.Categories.Add(category);
        _db.Products.Add(NewProduct(category.Id, "Only", 1m));
        await _db.SaveChangesAsync();

        var page = await _service.GetProductsPagedAsync(_tenantId, page: 0, pageSize: 0);

        Assert.Equal(1, page.Page);
        Assert.Equal(1, page.PageSize);
        Assert.Single(page.Items);
    }

    private Category NewCategory(string name, string? key = null) => new()
    {
        Id = Guid.NewGuid(),
        TenantId = _tenantId,
        Name = name,
        Key = key ?? name.ToLowerInvariant().Replace(' ', '-'),
        VatRate = 20m,
    };

    private Product NewProduct(Guid categoryId, string name, decimal price, bool isActive = true)
    {
        var id = Guid.NewGuid();
        return new Product
        {
            Id = id,
            TenantId = _tenantId,
            CategoryId = categoryId,
            Name = name,
            Price = price,
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
    }
}
