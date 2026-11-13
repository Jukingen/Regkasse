using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminProducts;
using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Services.Metrics;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ProductService"/> read paths, barcode normalization, and barcode lookup.
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
                new CacheMetricsService()),
            Microsoft.Extensions.Options.Options.Create(new KasseAPI_Final.Configuration.CacheSettings()));
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

    /// <summary>
    /// Product updates invalidate via <see cref="IProductService.InvalidateProductsCacheAsync"/> —
    /// Moq verifies product-list prefix + optional single-product key removal.
    /// </summary>
    [Fact]
    public async Task UpdateProduct_ThenProductListCacheIsInvalidated()
    {
        var category = NewCategory("Invalidate");
        _db.Categories.Add(category);
        var product = NewProduct(category.Id, "Before", 2m);
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var cache = new Mock<ICacheService>();
        cache.Setup(c => c.RemoveByPrefixAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        cache.Setup(c => c.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new ProductService(
            _db,
            cache.Object,
            Microsoft.Extensions.Options.Options.Create(new KasseAPI_Final.Configuration.CacheSettings()));

        // Simulate post-update invalidation (admin controllers call this after product writes).
        product.Name = "After";
        await _db.SaveChangesAsync();
        await sut.InvalidateProductsCacheAsync(_tenantId, product.Id);

        cache.Verify(
            c => c.RemoveByPrefixAsync(CacheKeys.Format(CacheKeys.ProductList, _tenantId), It.IsAny<CancellationToken>()),
            Times.Once);
        cache.Verify(
            c => c.RemoveAsync(CacheKeys.Format(CacheKeys.ProductDetail, product.Id), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task PrepareBarcodeAsync_CreateWithBarcode_PersistsNormalizedValue()
    {
        var category = NewCategory("Retail");
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        var prepared = await _service.PrepareBarcodeAsync(_tenantId, "  ab-12  ");
        Assert.Equal(ProductBarcodeWriteStatus.Ok, prepared.Status);
        Assert.Equal("ab-12", prepared.Barcode);

        var product = NewProduct(category.Id, "Scanned", 3m);
        product.Barcode = prepared.Barcode;
        _db.Products.Add(product);
        await _db.SaveChangesAsync();

        var found = await _service.GetByBarcodeAsync(_tenantId, "ab-12");
        Assert.NotNull(found);
        Assert.Equal(product.Id, found.Id);
        Assert.False(found.HasVariants);
    }

    [Fact]
    public async Task PrepareBarcodeAsync_DuplicateBarcode_ReturnsDuplicateCode()
    {
        var category = NewCategory("Retail");
        _db.Categories.Add(category);
        var existing = NewProduct(category.Id, "First", 1m);
        existing.Barcode = "EAN-1";
        _db.Products.Add(existing);
        await _db.SaveChangesAsync();

        var duplicate = await _service.PrepareBarcodeAsync(_tenantId, "EAN-1");
        Assert.Equal(ProductBarcodeWriteStatus.Duplicate, duplicate.Status);

        var controller = CreateAdminController(_db, _service, _tenantId);
        var payload = NewProduct(category.Id, "Second", 2m);
        payload.Barcode = "EAN-1";
        payload.TaxGroupId = await SeedTaxGroupAsync();
        var result = await controller.Create(payload);
        var conflict = Assert.IsType<ConflictObjectResult>(result);
        Assert.Equal(409, conflict.StatusCode);
        var json = JsonSerializer.Serialize(conflict.Value);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("BARCODE_DUPLICATE", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task GetByBarcodeAsync_OtherTenant_ReturnsNull()
    {
        var otherTenantId = Guid.NewGuid();
        _db.Tenants.Add(new Tenant
        {
            Id = otherTenantId,
            Name = "Other",
            Slug = $"o-{otherTenantId:N}"[..12],
        });
        var category = NewCategory("Foreign");
        category.TenantId = otherTenantId;
        _db.Categories.Add(category);
        var foreign = NewProduct(category.Id, "Foreign", 4m);
        foreign.TenantId = otherTenantId;
        foreign.Barcode = "FOREIGN-1";
        _db.Products.Add(foreign);
        await _db.SaveChangesAsync();

        var found = await _service.GetByBarcodeAsync(_tenantId, "FOREIGN-1");
        Assert.Null(found);
    }

    private async Task<Guid> SeedTaxGroupAsync()
    {
        var taxGroupId = Guid.NewGuid();
        _db.TaxGroups.Add(new TaxGroup
        {
            Id = taxGroupId,
            TenantId = _tenantId,
            Name = "Normalsatz",
            Rate = 20m,
            IsActive = true,
            IsSystem = true,
            IsDefault = true,
            GroupType = TaxGroupType.Standard,
            AustrianCode = "A",
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
        return taxGroupId;
    }

    private static AdminProductsController CreateAdminController(
        AppDbContext ctx,
        IProductService products,
        Guid tenantId)
    {
        var priceHistory = new ProductPriceHistoryService(ctx, NullLogger<ProductPriceHistoryService>.Instance);
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid() });

        var priceChange = new PriceChangeService(
            ctx,
            priceHistory,
            new RksvPriceChangeComplianceChecker(
                ctx,
                new TaxRegulationService(ctx, NullLogger<TaxRegulationService>.Instance),
                NullLogger<RksvPriceChangeComplianceChecker>.Instance),
            audit.Object,
            NullLogger<PriceChangeService>.Instance);

        var controller = new AdminProductsController(
            ctx,
            new GenericRepository<Product>(ctx, NullLogger<GenericRepository<Product>>.Instance),
            NullLogger<AdminProductsController>.Instance,
            TenantTestDoubles.SettingsResolverReturning(tenantId),
            Mock.Of<IWebHostEnvironment>(),
            Options.Create(new ProductMediaOptions()),
            new ProductImageThumbnailService(
                Options.Create(new ProductMediaOptions()),
                NullLogger<ProductImageThumbnailService>.Instance),
            Mock.Of<IDemoProductImportService>(),
            TenantTestDoubles.TenantAccessorReturning(tenantId),
            new AdminProductListService(ctx, TenantTestDoubles.SettingsResolverReturning(tenantId)),
            products,
            Mock.Of<IProductExportService>(),
            Mock.Of<KasseAPI_Final.Services.Operations.IOperationLogService>(),
            priceHistory,
            priceChange);

        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, Roles.Manager), new Claim(ClaimTypes.Name, "manager1")],
            "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
        return controller;
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
