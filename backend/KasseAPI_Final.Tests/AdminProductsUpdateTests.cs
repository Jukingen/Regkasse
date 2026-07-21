using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.Data.Repositories;
using KasseAPI_Final.Models;
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

public sealed class AdminProductsUpdateTests
{
    private static AppDbContext CreateContext(ICurrentTenantAccessor? tenantAccessor = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminProductsUpdate_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, tenantAccessor ?? TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
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
            TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary),
            new AdminProductListService(ctx, TenantTestDoubles.SettingsResolverReturning(LegacyDefaultTenantIds.Primary)),
            Mock.Of<IProductService>());

    private static void AttachManagerUser(AdminProductsController controller)
    {
        var identity = new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, Roles.Manager), new Claim(ClaimTypes.Name, "manager1")],
            "Test");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) },
        };
    }

    [Fact]
    public async Task Update_Product_WithModifierAssignments_DoesNotFail()
    {
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        var catId = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Id = catId, Name = "Drinks", VatRate = 20m });

        var productId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        ctx.ProductModifierGroups.Add(new ProductModifierGroup
        {
            Id = groupId,
            TenantId = LegacyDefaultTenantIds.Primary,
            Name = "Extras",
            IsActive = true,
        });
        ctx.Products.Add(NewProduct(productId, "Cola", catId, "bc-cola"));
        ctx.ProductModifierGroupAssignments.Add(new ProductModifierGroupAssignment
        {
            ProductId = productId,
            ModifierGroupId = groupId,
            TenantId = LegacyDefaultTenantIds.Primary,
            SortOrder = 0,
        });
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx);
        AttachManagerUser(controller);

        var payload = NewProduct(productId, "Cola Zero", catId, "bc-cola");
        payload.Price = 2.5m;
        payload.TenantId = Guid.Empty; // FE does not send tenant_id

        var result = await controller.Update(productId, payload);

        Assert.IsType<OkObjectResult>(result);
        var updated = await ctx.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
        Assert.Equal("Cola Zero", updated.Name);
        Assert.Equal(1, await ctx.ProductModifierGroupAssignments.CountAsync(a => a.ProductId == productId));
    }

    [Fact]
    public async Task Update_Product_PreservesIsSellableAddOn()
    {
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        var catId = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Id = catId, Name = "Addons", VatRate = 10m });

        var productId = Guid.NewGuid();
        var product = NewProduct(productId, "Extra Cheese", catId, "bc-cheese");
        product.IsSellableAddOn = true;
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx);
        AttachManagerUser(controller);

        var payload = NewProduct(productId, "Extra Cheese XL", catId, "bc-cheese");
        var result = await controller.Update(productId, payload);

        Assert.IsType<OkObjectResult>(result);
        var updated = await ctx.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
        Assert.True(updated.IsSellableAddOn);
    }

    [Fact]
    public async Task Update_Product_WithOnlyTurkishDescription_DoesNotNullCanonicalDescription()
    {
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        var catId = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Id = catId, Name = "Pizza", VatRate = 10m });

        var productId = Guid.NewGuid();
        var seeded = NewProduct(productId, "Bauern Pizza", catId, "DEMO-BAUERNPIZZA-050");
        seeded.Description = "Original";
        seeded.DescriptionDe = "Original DE";
        ctx.Products.Add(seeded);
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx);
        AttachManagerUser(controller);

        var payload = NewProduct(productId, "bauern-pizza", catId, "DEMO-BAUERNPIZZA-050");
        payload.DescriptionDe = null;
        payload.DescriptionEn = null;
        payload.DescriptionTr = "tesstt";
        payload.TenantId = Guid.Empty;

        var result = await controller.Update(productId, payload);

        Assert.IsType<OkObjectResult>(result);
        var updated = await ctx.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
        Assert.Equal("tesstt", updated.Description);
        Assert.Equal("tesstt", updated.DescriptionTr);
    }

    [Fact]
    public async Task Update_Product_WithDescriptionExceedingMaxLength_ReturnsBadRequest()
    {
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsureDefaultTenant(ctx);
        var catId = Guid.NewGuid();
        ctx.Categories.Add(new Category { TenantId = LegacyDefaultTenantIds.Primary, Id = catId, Name = "Pizza", VatRate = 10m });

        var productId = Guid.NewGuid();
        ctx.Products.Add(NewProduct(productId, "Margherita", catId, "bc-pizza"));
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx);
        AttachManagerUser(controller);

        var payload = NewProduct(productId, "Margherita", catId, "bc-pizza");
        payload.DescriptionDe = new string('x', 2001);

        var result = await controller.Update(productId, payload);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("2000", badRequest.Value?.ToString(), StringComparison.Ordinal);
    }

    private static Product NewProduct(Guid id, string name, Guid categoryId, string barcode) => new()
    {
        Id = id,
        TenantId = LegacyDefaultTenantIds.Primary,
        Name = name,
        Price = 1m,
        CategoryId = categoryId,
        Category = "Drinks",
        StockQuantity = 1,
        MinStockLevel = 0,
        Unit = "Stk",
        TaxType = TaxTypes.Standard,
        TaxRate = TaxTypes.GetTaxRate(TaxTypes.Standard),
        Barcode = barcode,
        IsFiscalCompliant = true,
        IsTaxable = true,
        RksvProductType = RksvProductTypes.Standard,
        IsActive = true,
        Description = string.Empty,
    };
}
