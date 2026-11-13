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
        return new AppDbContext(options, tenantAccessor ?? TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }

    private static AdminProductsController CreateController(AppDbContext ctx)
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

        return new(
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
            TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform),
            new AdminProductListService(ctx, TenantTestDoubles.SettingsResolverReturning(SystemTenantIds.Platform)),
            Mock.Of<IProductService>(),
            Mock.Of<IProductExportService>(),
            Mock.Of<KasseAPI_Final.Services.Operations.IOperationLogService>(),
            priceHistory,
            priceChange);
    }

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

    private static async Task<(Guid CategoryId, Guid TaxGroupId)> SeedCatalogAsync(AppDbContext ctx, string categoryName, decimal vatRate)
    {
        var catId = Guid.NewGuid();
        ctx.Categories.Add(new Category
        {
            TenantId = SystemTenantIds.Platform,
            Id = catId,
            Key = $"key-{catId:N}"[..20],
            Name = categoryName,
            VatRate = vatRate,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        var taxGroupId = Guid.NewGuid();
        ctx.TaxGroups.Add(new TaxGroup
        {
            Id = taxGroupId,
            TenantId = SystemTenantIds.Platform,
            Name = "Normalsatz",
            Rate = 20m,
            IsActive = true,
            IsSystem = true,
            IsDefault = true,
            GroupType = TaxGroupType.Standard,
            AustrianCode = "A",
            CreatedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();
        return (catId, taxGroupId);
    }

    [Fact]
    public async Task Update_Product_WithModifierAssignments_DoesNotFail()
    {
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsurePlatformTenant(ctx);
        var (catId, taxGroupId) = await SeedCatalogAsync(ctx, "Drinks", 20m);

        var productId = Guid.NewGuid();
        var groupId = Guid.NewGuid();
        ctx.ProductModifierGroups.Add(new ProductModifierGroup
        {
            Id = groupId,
            TenantId = SystemTenantIds.Platform,
            Name = "Extras",
            IsActive = true,
        });
        ctx.Products.Add(NewProduct(productId, "Cola", catId, "bc-cola", taxGroupId));
        ctx.ProductModifierGroupAssignments.Add(new ProductModifierGroupAssignment
        {
            ProductId = productId,
            ModifierGroupId = groupId,
            TenantId = SystemTenantIds.Platform,
            SortOrder = 0,
        });
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx);
        AttachManagerUser(controller);

        var payload = NewProduct(productId, "Cola Zero", catId, "bc-cola", taxGroupId);
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
        TenantTestDoubles.EnsurePlatformTenant(ctx);
        var (catId, taxGroupId) = await SeedCatalogAsync(ctx, "Addons", 10m);

        var productId = Guid.NewGuid();
        var product = NewProduct(productId, "Extra Cheese", catId, "bc-cheese", taxGroupId);
        product.IsSellableAddOn = true;
        ctx.Products.Add(product);
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx);
        AttachManagerUser(controller);

        var payload = NewProduct(productId, "Extra Cheese XL", catId, "bc-cheese", taxGroupId);
        var result = await controller.Update(productId, payload);

        Assert.IsType<OkObjectResult>(result);
        var updated = await ctx.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
        Assert.True(updated.IsSellableAddOn);
    }

    [Fact]
    public async Task Update_Product_WithOnlyTurkishDescription_DoesNotNullCanonicalDescription()
    {
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsurePlatformTenant(ctx);
        var (catId, taxGroupId) = await SeedCatalogAsync(ctx, "Pizza", 10m);

        var productId = Guid.NewGuid();
        var seeded = NewProduct(productId, "Bauern Pizza", catId, "DEMO-BAUERNPIZZA-050", taxGroupId);
        seeded.Description = "Original";
        seeded.DescriptionDe = "Original DE";
        ctx.Products.Add(seeded);
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx);
        AttachManagerUser(controller);

        var payload = NewProduct(productId, "bauern-pizza", catId, "DEMO-BAUERNPIZZA-050", taxGroupId);
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
        TenantTestDoubles.EnsurePlatformTenant(ctx);
        var (catId, taxGroupId) = await SeedCatalogAsync(ctx, "Pizza", 10m);

        var productId = Guid.NewGuid();
        ctx.Products.Add(NewProduct(productId, "Margherita", catId, "bc-pizza", taxGroupId));
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx);
        AttachManagerUser(controller);

        var payload = NewProduct(productId, "Margherita", catId, "bc-pizza", taxGroupId);
        payload.DescriptionDe = new string('x', 2001);

        var result = await controller.Update(productId, payload);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("2000", badRequest.Value?.ToString(), StringComparison.Ordinal);
    }

    private static Product NewProduct(Guid id, string name, Guid categoryId, string barcode, Guid taxGroupId) => new()
    {
        Id = id,
        TenantId = SystemTenantIds.Platform,
        Name = name,
        Price = 1m,
        CategoryId = categoryId,
        Category = "Drinks",
        StockQuantity = 1,
        MinStockLevel = 0,
        Unit = "Stk",
        TaxType = TaxTypes.Standard,
        TaxRate = TaxTypes.GetTaxRate(TaxTypes.Standard),
        TaxGroupId = taxGroupId,
        Barcode = barcode,
        IsFiscalCompliant = true,
        IsTaxable = true,
        RksvProductType = RksvProductTypes.Standard,
        IsActive = true,
        Description = string.Empty,
    };
}
