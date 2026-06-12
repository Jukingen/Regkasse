using System.Text.Json;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.AdminProducts;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminProductsDeactivateAllTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminProductsDeactivateAll_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, new CurrentTenantAccessor { TenantId = LegacyDefaultTenantIds.Primary });
    }

    private static void AttachHttpContext(AdminProductsController controller)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext(),
        };
    }

    private static AdminProductsController CreateController(AppDbContext ctx)
    {
        var controller = new AdminProductsController(
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
            new AdminProductListService(ctx, TenantTestDoubles.SettingsResolverReturning(LegacyDefaultTenantIds.Primary)));
        AttachHttpContext(controller);
        return controller;
    }

    [Fact]
    public async Task DeactivateAllProducts_RequiresConfirmPhrase()
    {
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        var controller = CreateController(ctx);

        var result = await controller.DeactivateAllProducts(new DeactivateAllProductsRequest
        {
            ConfirmPhrase = "wrong",
        });

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task DeactivateAllProducts_SoftDeactivatesAllActiveForTenant()
    {
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        var catId = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Id = catId, Name = "C", VatRate = 10m });

        var activeA = Guid.NewGuid();
        var activeB = Guid.NewGuid();
        var inactive = Guid.NewGuid();
        ctx.Products.AddRange(
            NewProduct(activeA, LegacyDefaultTenantIds.Primary, "A", isActive: true, catId),
            NewProduct(activeB, LegacyDefaultTenantIds.Primary, "B", isActive: true, catId),
            NewProduct(inactive, LegacyDefaultTenantIds.Primary, "I", isActive: false, catId));
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx);
        var result = await controller.DeactivateAllProducts(new DeactivateAllProductsRequest
        {
            ConfirmPhrase = AdminProductsController.DeactivateAllProductsConfirmPhrase,
        });

        var ok = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(200, ok.StatusCode);
        var json = JsonSerializer.Serialize(ok.Value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(2, data.GetProperty("deactivated").GetInt32());
        Assert.Equal(1, data.GetProperty("alreadyInactive").GetInt32());
        Assert.Equal(3, data.GetProperty("totalProducts").GetInt32());

        Assert.False((await ctx.Products.AsNoTracking().SingleAsync(p => p.Id == activeA)).IsActive);
        Assert.False((await ctx.Products.AsNoTracking().SingleAsync(p => p.Id == activeB)).IsActive);
        Assert.False((await ctx.Products.AsNoTracking().SingleAsync(p => p.Id == inactive)).IsActive);
    }

    private static Product NewProduct(Guid id, Guid tenantId, string name, bool isActive, Guid categoryId) => new()
    {
        Id = id,
        TenantId = tenantId,
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
