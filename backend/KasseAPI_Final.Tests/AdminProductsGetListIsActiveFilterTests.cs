using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminProducts;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// In-memory <see cref="AdminProductsController.GetProducts"/> coverage for isActive filter, search, and pagination.
/// </summary>
public sealed class AdminProductsGetListIsActiveFilterTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminProductsList_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }

    private static async Task<AppDbContext> SeedThreeProductsAsync()
    {
        var ctx = CreateContext();
        TenantTestDoubles.EnsurePlatformTenant(ctx);
        var taxGroupId = TenantTestDoubles.EnsureTaxGroup(ctx, SystemTenantIds.Platform);
        var catId = Guid.NewGuid();
        ctx.Categories.Add(new Category
        {
            TenantId = SystemTenantIds.Platform,
            Id = catId,
            Name = "C",
            VatRate = 10m
        });

        ctx.Products.AddRange(
            NewProduct(Guid.NewGuid(), "Alpha", isActive: true, catId, taxGroupId),
            NewProduct(Guid.NewGuid(), "Beta", isActive: true, catId, taxGroupId),
            NewProduct(Guid.NewGuid(), "Gamma", isActive: false, catId, taxGroupId));

        await ctx.SaveChangesAsync();
        return ctx;
    }

    private static Product NewProduct(Guid id, string name, bool isActive, Guid categoryId, Guid taxGroupId) => new()
    {
        Id = id,
        TenantId = SystemTenantIds.Platform,
        Name = name,
        Price = 1m,
        CategoryId = categoryId,
        TaxGroupId = taxGroupId,
        Category = "C",
        StockQuantity = 1,
        MinStockLevel = 0,
        Unit = "Stk",
        TaxType = TaxTypes.Reduced,
        TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
        Barcode = $"bc-{id:N}",
        IsFiscalCompliant = true,
        IsTaxable = true,
        RksvProductType = RksvProductTypes.Standard,
        IsActive = isActive
    };

    private static AdminProductsController CreateController(AppDbContext ctx) =>
        new(
            ctx,
            Mock.Of<IGenericRepository<Product>>(),
            NullLogger<AdminProductsController>.Instance,
            TenantTestDoubles.SettingsResolverReturning(SystemTenantIds.Platform),
            Mock.Of<IWebHostEnvironment>(),
            Options.Create(new ProductMediaOptions()),
            new ProductImageThumbnailService(
                Options.Create(new ProductMediaOptions()),
                NullLogger<ProductImageThumbnailService>.Instance),
            Mock.Of<IDemoProductImportService>(),
            NullCurrentTenantAccessor.Instance,
            new AdminProductListService(ctx, TenantTestDoubles.SettingsResolverReturning(SystemTenantIds.Platform)),
            Mock.Of<IProductService>(),
            Mock.Of<IProductExportService>(),
            Mock.Of<KasseAPI_Final.Services.Operations.IOperationLogService>(),
            Mock.Of<IProductPriceHistoryService>(),
            Mock.Of<IPriceChangeService>());

    private static int ReadTotalCount(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.GetProperty("data").GetProperty("pagination").GetProperty("totalCount").GetInt32();
    }

    private static string ReadFirstItemName(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.GetProperty("data").GetProperty("items");
        return items[0].GetProperty("name").GetString()!;
    }

    [Fact]
    public async Task GetList_OmittedIsActive_ReturnsOnlyActiveProducts()
    {
        await using var ctx = await SeedThreeProductsAsync();
        var c = CreateController(ctx);
        var result = await c.GetProducts(new ProductFilterDto(), pageNumber: 1, pageSize: 20);
        Assert.Equal(2, ReadTotalCount(result));
    }

    [Fact]
    public async Task GetList_IsActiveAll_ReturnsActiveAndInactive()
    {
        await using var ctx = await SeedThreeProductsAsync();
        var c = CreateController(ctx);
        var result = await c.GetProducts(new ProductFilterDto(), pageNumber: 1, pageSize: 20, isActive: "all");
        Assert.Equal(3, ReadTotalCount(result));
    }

    [Fact]
    public async Task GetList_IsActiveFalse_ReturnsOnlyInactive()
    {
        await using var ctx = await SeedThreeProductsAsync();
        var c = CreateController(ctx);
        var result = await c.GetProducts(new ProductFilterDto(), pageNumber: 1, pageSize: 20, isActive: "false");
        Assert.Equal(1, ReadTotalCount(result));
        Assert.Equal("Gamma", ReadFirstItemName(result));
    }

    [Fact]
    public async Task GetList_InvalidIsActive_ReturnsBadRequest()
    {
        await using var ctx = await SeedThreeProductsAsync();
        var c = CreateController(ctx);
        var result = await c.GetProducts(new ProductFilterDto(), pageNumber: 1, pageSize: 20, isActive: "nope");
        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task GetList_Pagination_WithIsActiveAll_RespectsSkip()
    {
        await using var ctx = await SeedThreeProductsAsync();
        var c = CreateController(ctx);
        var result = await c.GetProducts(new ProductFilterDto(), pageNumber: 2, pageSize: 1, isActive: "all");
        Assert.Equal(3, ReadTotalCount(result));
        Assert.Equal("Beta", ReadFirstItemName(result));
    }
}

/// <summary>
/// <see cref="EF.Functions.ILike"/> is PostgreSQL-only; the in-memory list tests above cover filter/pagination without search.
/// </summary>
[Collection("PostgreSqlReplay")]
[Trait("Category", "PostgreSql")]
public sealed class AdminProductsGetListNameSearchPostgreSqlTests
{
    private readonly PostgreSqlReplayFixture _fixture;

    public AdminProductsGetListNameSearchPostgreSqlTests(PostgreSqlReplayFixture fixture) => _fixture = fixture;

    [SkippableFact]
    public async Task GetList_NameSearch_WithIsActiveAll_FiltersByNameAndStatus()
    {
        Skip.IfNot(_fixture.HasDatabase, _fixture.SkipReason);

        await using var ctx = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseAppNpgsql(_fixture.ConnectionString).Options,
            TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));

        TenantTestDoubles.EnsurePlatformTenant(ctx);
        var taxGroupId = TenantTestDoubles.EnsureTaxGroup(ctx, SystemTenantIds.Platform);
        var catId = Guid.NewGuid();
        var token = catId.ToString("N")[..8];
        ctx.Categories.Add(new Category
        {
            TenantId = SystemTenantIds.Platform,
            Id = catId,
            Name = $"C-{token}",
            VatRate = 10m
        });

        var gammaName = $"Gamma-{token}";
        ctx.Products.AddRange(
            NewProduct(Guid.NewGuid(), $"Alpha-{token}", isActive: true, catId, taxGroupId),
            NewProduct(Guid.NewGuid(), $"Beta-{token}", isActive: true, catId, taxGroupId),
            NewProduct(Guid.NewGuid(), gammaName, isActive: false, catId, taxGroupId));
        await ctx.SaveChangesAsync();

        var controller = new AdminProductsController(
            ctx,
            Mock.Of<IGenericRepository<Product>>(),
            NullLogger<AdminProductsController>.Instance,
            TenantTestDoubles.SettingsResolverReturning(SystemTenantIds.Platform),
            Mock.Of<IWebHostEnvironment>(),
            Options.Create(new ProductMediaOptions()),
            new ProductImageThumbnailService(
                Options.Create(new ProductMediaOptions()),
                NullLogger<ProductImageThumbnailService>.Instance),
            Mock.Of<IDemoProductImportService>(),
            NullCurrentTenantAccessor.Instance,
            new AdminProductListService(ctx, TenantTestDoubles.SettingsResolverReturning(SystemTenantIds.Platform)),
            Mock.Of<IProductService>(),
            Mock.Of<IProductExportService>(),
            Mock.Of<KasseAPI_Final.Services.Operations.IOperationLogService>(),
            Mock.Of<IProductPriceHistoryService>(),
            Mock.Of<IPriceChangeService>());

        var result = await controller.GetProducts(new ProductFilterDto(), pageNumber: 1, pageSize: 20, name: gammaName, isActive: "all");
        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(1, data.GetProperty("pagination").GetProperty("totalCount").GetInt32());
        Assert.Equal(gammaName, data.GetProperty("items")[0].GetProperty("name").GetString());
    }

    private static Product NewProduct(Guid id, string name, bool isActive, Guid categoryId, Guid taxGroupId) => new()
    {
        Id = id,
        TenantId = SystemTenantIds.Platform,
        Name = name,
        Description = "-",
        Price = 1m,
        CategoryId = categoryId,
        TaxGroupId = taxGroupId,
        Category = "C",
        StockQuantity = 1,
        MinStockLevel = 0,
        Unit = "Stk",
        TaxType = TaxTypes.Reduced,
        TaxRate = TaxTypes.GetTaxRate(TaxTypes.Reduced),
        Barcode = $"bc-{id:N}",
        IsFiscalCompliant = true,
        IsTaxable = true,
        RksvProductType = RksvProductTypes.Standard,
        IsActive = isActive
    };
}
