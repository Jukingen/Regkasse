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
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    private static async Task<AppDbContext> SeedThreeProductsAsync()
    {
        var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        var catId = Guid.NewGuid();
        ctx.Categories.Add(new Category
        {
            TenantId = LegacyDefaultTenantIds.Primary,
            Id = catId,
            Name = "C",
            VatRate = 10m
        });

        ctx.Products.AddRange(
            NewProduct(Guid.NewGuid(), "Alpha", isActive: true, catId),
            NewProduct(Guid.NewGuid(), "Beta", isActive: true, catId),
            NewProduct(Guid.NewGuid(), "Gamma", isActive: false, catId));

        await ctx.SaveChangesAsync();
        return ctx;
    }

    private static Product NewProduct(Guid id, string name, bool isActive, Guid categoryId) => new()
    {
        Id = id,
        TenantId = LegacyDefaultTenantIds.Primary,
        Name = name,
        Price = 1m,
        CategoryId = categoryId,
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
            TenantTestDoubles.SettingsResolverReturning(LegacyDefaultTenantIds.Primary),
            Mock.Of<IWebHostEnvironment>(),
            Options.Create(new ProductMediaOptions()),
            new ProductImageThumbnailService(
                Options.Create(new ProductMediaOptions()),
                NullLogger<ProductImageThumbnailService>.Instance),
            Mock.Of<IDemoProductImportService>(),
            NullCurrentTenantAccessor.Instance,
            new AdminProductListService(ctx, TenantTestDoubles.SettingsResolverReturning(LegacyDefaultTenantIds.Primary)),
            Mock.Of<IProductService>(),
            Mock.Of<IProductExportService>(),
            Mock.Of<KasseAPI_Final.Services.Operations.IOperationLogService>());

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
    public async Task GetList_NameSearch_WithIsActiveAll_FiltersByNameAndStatus()
    {
        await using var ctx = await SeedThreeProductsAsync();
        var c = CreateController(ctx);
        var result = await c.GetProducts(new ProductFilterDto(), pageNumber: 1, pageSize: 20, name: "Gamma", isActive: "all");
        Assert.Equal(1, ReadTotalCount(result));
        Assert.Equal("Gamma", ReadFirstItemName(result));
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
