using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Caching;
using KasseAPI_Final.Services.Metrics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PosProductsByBarcodeTests
{
    [Fact]
    public async Task KnownBarcode_Returns200WithListProductShape()
    {
        await using var ctx = CreateContext(SystemTenantIds.Platform);
        var product = await SeedProductAsync(ctx, SystemTenantIds.Platform, "4001234567890", "Milch");

        var result = await CreateController(ctx, SystemTenantIds.Platform).GetByBarcode("4001234567890", CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(product.Id, data.GetProperty("Id").GetGuid());
        Assert.Equal("Milch", data.GetProperty("Name").GetString());
        Assert.Equal("4001234567890", data.GetProperty("Barcode").GetString());
        Assert.False(data.GetProperty("HasVariants").GetBoolean());
        Assert.True(data.TryGetProperty("Price", out _));
    }

    [Fact]
    public async Task UnknownBarcode_Returns404BarcodeNotFound()
    {
        await using var ctx = CreateContext(SystemTenantIds.Platform);
        await SeedProductAsync(ctx, SystemTenantIds.Platform, "111", "Known");

        var result = await CreateController(ctx, SystemTenantIds.Platform).GetByBarcode("MISSING", CancellationToken.None);
        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(notFound.Value));
        Assert.Equal("BARCODE_NOT_FOUND", doc.RootElement.GetProperty("code").GetString());
    }

    [Fact]
    public async Task CrossTenantBarcode_Returns404()
    {
        var tenantA = Guid.NewGuid();
        await using var ctx = CreateContext(tenantA);
        ctx.Tenants.Add(new Tenant { Id = tenantA, Name = "A", Slug = $"a-{tenantA:N}"[..12] });
        var tenantB = Guid.NewGuid();
        ctx.Tenants.Add(new Tenant { Id = tenantB, Name = "B", Slug = $"b-{tenantB:N}"[..12] });
        await ctx.SaveChangesAsync();
        await SeedProductAsync(ctx, tenantB, "FOREIGN-9", "Other");

        var result = await CreateController(ctx, tenantA).GetByBarcode("FOREIGN-9", CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ProductWithVariants_ReturnsHasVariantsTrue()
    {
        await using var ctx = CreateContext(SystemTenantIds.Platform);
        var product = await SeedProductAsync(ctx, SystemTenantIds.Platform, "VAR-1", "Shirt");
        ctx.ProductVariants.Add(new ProductVariant
        {
            Id = Guid.NewGuid(),
            TenantId = SystemTenantIds.Platform,
            ProductId = product.Id,
            Sku = "SHIRT-M",
            VariantLabel = "M",
            PriceDelta = 0m,
            Stock = 2,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var result = await CreateController(ctx, SystemTenantIds.Platform).GetByBarcode("VAR-1", CancellationToken.None);
        var ok = Assert.IsType<OkObjectResult>(result);
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(ok.Value));
        Assert.True(doc.RootElement.GetProperty("data").GetProperty("HasVariants").GetBoolean());
    }

    private static AppDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"PosBarcode_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var ctx = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
        if (tenantId == SystemTenantIds.Platform)
            TenantTestDoubles.EnsurePlatformTenant(ctx);
        return ctx;
    }

    private static ProductController CreateController(AppDbContext ctx, Guid tenantId)
    {
        var products = new ProductService(
            ctx,
            new MemoryCacheService(
                new MemoryCache(new MemoryCacheOptions()),
                NullLogger<MemoryCacheService>.Instance,
                new CacheMetricsService()),
            Microsoft.Extensions.Options.Options.Create(new CacheSettings()));
        return new ProductController(
            ctx,
            new GenericRepository<Product>(ctx, NullLogger<GenericRepository<Product>>.Instance),
            NullLogger<ProductController>.Instance,
            TenantTestDoubles.SettingsResolverReturning(tenantId),
            tenantLimitGuard: null,
            productService: products);
    }

    private static async Task<Product> SeedProductAsync(AppDbContext ctx, Guid tenantId, string barcode, string name)
    {
        var categoryId = Guid.NewGuid();
        ctx.Categories.Add(new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Name = "Retail",
            Key = $"k-{categoryId:N}"[..12],
            VatRate = 20m,
            IsActive = true,
        });
        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CategoryId = categoryId,
            Name = name,
            Price = 2.5m,
            Category = "Retail",
            StockQuantity = 4,
            MinStockLevel = 0,
            Unit = "Stk",
            TaxType = TaxTypes.Standard,
            TaxRate = TaxTypes.GetTaxRate(TaxTypes.Standard),
            Barcode = barcode,
            IsFiscalCompliant = true,
            IsTaxable = true,
            RksvProductType = RksvProductTypes.Standard,
            IsActive = true,
        };
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();
        return product;
    }
}
