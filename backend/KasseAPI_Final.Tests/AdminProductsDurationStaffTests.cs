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

public sealed class AdminProductsDurationStaffTests
{
    [Fact]
    public async Task CreateAndGetById_PersistDurationMinutesAndStaffId()
    {
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsurePlatformTenant(ctx);
        var (catId, taxGroupId) = await SeedCatalogAsync(ctx);

        var repo = new GenericRepository<Product>(ctx, NullLogger<GenericRepository<Product>>.Instance);
        var controller = CreateController(ctx, repo);
        AttachManagerUser(controller);

        var createdId = Guid.NewGuid();
        var payload = NewProduct(createdId, "Schnitt", catId, $"bc-{createdId:N}", taxGroupId);
        payload.DurationMinutes = 45;
        payload.StaffId = "staff-anna";
        payload.TenantId = Guid.Empty;

        var createResult = await controller.Create(payload);
        Assert.IsType<OkObjectResult>(createResult);

        var stored = await ctx.Products.AsNoTracking().SingleAsync(p => p.Id == createdId);
        Assert.Equal(45, stored.DurationMinutes);
        Assert.Equal("staff-anna", stored.StaffId);

        var getResult = await controller.GetById(createdId);
        var getOk = Assert.IsType<OkObjectResult>(getResult);
        var json = JsonSerializer.Serialize(
            getOk.Value,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        using var doc = JsonDocument.Parse(json);
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(45, data.GetProperty("durationMinutes").GetInt32());
        Assert.Equal("staff-anna", data.GetProperty("staffId").GetString());
    }

    [Fact]
    public async Task Update_PersistsDurationMinutesAndStaffId()
    {
        await using var ctx = CreateContext();
        TenantTestDoubles.EnsurePlatformTenant(ctx);
        var (catId, taxGroupId) = await SeedCatalogAsync(ctx);

        var productId = Guid.NewGuid();
        ctx.Products.Add(NewProduct(productId, "Föhnen", catId, $"bc-{productId:N}", taxGroupId));
        await ctx.SaveChangesAsync();

        var controller = CreateController(ctx);
        AttachManagerUser(controller);

        var payload = NewProduct(productId, "Föhnen", catId, $"bc-{productId:N}", taxGroupId);
        payload.DurationMinutes = 30;
        payload.StaffId = "staff-ben";
        payload.TenantId = Guid.Empty;

        var result = await controller.Update(productId, payload);
        Assert.IsType<OkObjectResult>(result);

        var updated = await ctx.Products.AsNoTracking().SingleAsync(p => p.Id == productId);
        Assert.Equal(30, updated.DurationMinutes);
        Assert.Equal("staff-ben", updated.StaffId);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"AdminProductsDuration_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }

    private static AdminProductsController CreateController(
        AppDbContext ctx,
        IGenericRepository<Product>? productRepository = null)
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
            productRepository ?? Mock.Of<IGenericRepository<Product>>(),
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

    private static async Task<(Guid CategoryId, Guid TaxGroupId)> SeedCatalogAsync(AppDbContext ctx)
    {
        var catId = Guid.NewGuid();
        ctx.Categories.Add(new Category
        {
            TenantId = SystemTenantIds.Platform,
            Id = catId,
            Key = $"key-{catId:N}"[..20],
            Name = "Services",
            VatRate = 20m,
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

    private static Product NewProduct(Guid id, string name, Guid categoryId, string barcode, Guid taxGroupId) => new()
    {
        Id = id,
        TenantId = SystemTenantIds.Platform,
        Name = name,
        Price = 12m,
        CategoryId = categoryId,
        Category = "Services",
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
