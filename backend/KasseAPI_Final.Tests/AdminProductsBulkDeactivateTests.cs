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

public sealed class AdminProductsBulkDeactivateTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminProductsBulkDeactivate_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

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

    [Fact]
    public async Task BulkDeactivateProducts_SoftDeletesActiveOnly()
    {
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        var catId = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Id = catId, Name = "C", VatRate = 10m });

        var activeId = Guid.NewGuid();
        var inactiveId = Guid.NewGuid();
        var missingId = Guid.NewGuid();
        ctx.Products.AddRange(
            NewProduct(activeId, "Active", isActive: true, catId),
            NewProduct(inactiveId, "Inactive", isActive: false, catId));
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx);
        var result = await controller.BulkDeactivateProducts(new BulkDeactivateProductsRequest
        {
            ProductIds = [activeId, inactiveId, missingId],
        });

        var ok = Assert.IsType<OkObjectResult>(result);
        var json = JsonSerializer.Serialize(ok.Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(1, data.GetProperty("deactivated").GetInt32());
        Assert.Equal(1, data.GetProperty("alreadyInactive").GetInt32());
        Assert.Equal(1, data.GetProperty("notFound").GetInt32());

        var active = await ctx.Products.AsNoTracking().SingleAsync(p => p.Id == activeId);
        Assert.False(active.IsActive);
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
        IsActive = isActive,
    };
}
