using System.Runtime.CompilerServices;
using KasseAPI_Final.Controllers;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Products;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ProductVariantServiceTests
{
    private static readonly ConditionalWeakTable<AppDbContext, ICurrentTenantAccessor> Accessors = new();

    [Fact]
    public async Task CreateAndList_AreScopedToTheProductTenant()
    {
        var tenantId = Guid.NewGuid();
        var dbName = NewDbName();
        await using var db = await CreateDbAsync(dbName, tenantId);
        var productId = await SeedProductAsync(db, tenantId);
        var audit = TicketAuditTestDoubles.Loose();
        var service = CreateService(db, audit);

        var created = Assert.IsType<ProductVariantMutation.Ok>(
            await service.CreateAsync(
                productId,
                new CreateProductVariantRequest
                {
                    Sku = "SKU-RED-M",
                    VariantLabel = "Rot / M",
                    PriceDelta = 2.50m,
                    Stock = 4,
                },
                "manager-1",
                CancellationToken.None));

        var listed = await service.ListAsync(productId, CancellationToken.None);
        Assert.NotNull(listed);
        var row = Assert.Single(listed);
        Assert.Equal(created.Variant.Id, row.Id);
        Assert.Equal("SKU-RED-M", row.Sku);
        Assert.Equal(2.50m, row.PriceDelta);
        Assert.Equal(4, row.Stock);
        Assert.True(row.IsActive);

        audit.Verify(a => a.LogSystemOperationAsync(
            "PRODUCT_VARIANT_CHANGED",
            "ProductVariant",
            "manager-1",
            "Manager",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            AuditLogStatus.Success,
            It.IsAny<string?>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>(),
            It.IsAny<ImpersonationAuditContext.Snapshot?>(),
            AuditEventType.ProductVariantChanged,
            created.Variant.Id,
            tenantId,
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>()), Times.Once);
        Assert.Equal(148, (int)AuditEventType.ProductVariantChanged);
    }

    [Fact]
    public async Task Create_DuplicateSkuInTenant_Returns409()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(NewDbName(), tenantId);
        var first = await SeedProductAsync(db, tenantId, "Shirt");
        var second = await SeedProductAsync(db, tenantId, "Jacket");
        var service = CreateService(db);

        Assert.IsType<ProductVariantMutation.Ok>(
            await service.CreateAsync(
                first,
                new CreateProductVariantRequest { Sku = "SHARED", VariantLabel = "Blau" },
                "manager-1",
                CancellationToken.None));

        var duplicate = Assert.IsType<ProductVariantMutation.Error>(
            await service.CreateAsync(
                second,
                new CreateProductVariantRequest { Sku = " shared ", VariantLabel = "Schwarz" },
                "manager-1",
                CancellationToken.None));
        Assert.Equal(StatusCodes.Status409Conflict, duplicate.StatusCode);
        Assert.Equal("SKU_DUPLICATE", duplicate.Code);
    }

    [Fact]
    public async Task Create_DuplicateLabelOnProduct_Returns409()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(NewDbName(), tenantId);
        var productId = await SeedProductAsync(db, tenantId);
        var service = CreateService(db);

        Assert.IsType<ProductVariantMutation.Ok>(
            await service.CreateAsync(
                productId,
                new CreateProductVariantRequest { Sku = "SKU-1", VariantLabel = "Rot / M" },
                "manager-1",
                CancellationToken.None));

        var duplicate = Assert.IsType<ProductVariantMutation.Error>(
            await service.CreateAsync(
                productId,
                new CreateProductVariantRequest { Sku = "SKU-2", VariantLabel = " rot / m " },
                "manager-1",
                CancellationToken.None));
        Assert.Equal(StatusCodes.Status409Conflict, duplicate.StatusCode);
        Assert.Equal("VARIANT_LABEL_DUPLICATE", duplicate.Code);
    }

    [Fact]
    public async Task CrossTenant_GetPutDelete_Return404()
    {
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var dbName = NewDbName();
        await using var ownerDb = await CreateDbAsync(dbName, owner);
        var productId = await SeedProductAsync(ownerDb, owner);
        var created = Assert.IsType<ProductVariantMutation.Ok>(
            await CreateService(ownerDb).CreateAsync(
                productId,
                new CreateProductVariantRequest { Sku = "SKU-1", VariantLabel = "Rot" },
                "manager-1",
                CancellationToken.None));

        await using var otherDb = await CreateDbAsync(dbName, other);
        var controller = new AdminProductVariantsController(CreateService(otherDb));

        var listed = await controller.List(productId, CancellationToken.None);
        Assert.IsType<NotFoundResult>(listed.Result);

        var updated = await controller.Update(
            productId,
            created.Variant.Id,
            new UpdateProductVariantRequest { Sku = "SKU-2", VariantLabel = "Blau", IsActive = true },
            CancellationToken.None);
        Assert.IsType<NotFoundResult>(updated.Result);

        var deleted = await controller.Deactivate(productId, created.Variant.Id, CancellationToken.None);
        Assert.IsType<NotFoundResult>(deleted.Result);

        await using var stillOwner = await CreateDbAsync(dbName, owner);
        var row = await stillOwner.ProductVariants.SingleAsync();
        Assert.True(row.IsActive);
        Assert.Equal("SKU-1", row.Sku);
    }

    [Fact]
    public async Task Deactivate_SetsInactive_AndKeepsTheRow()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(NewDbName(), tenantId);
        var productId = await SeedProductAsync(db, tenantId);
        var service = CreateService(db);
        var created = Assert.IsType<ProductVariantMutation.Ok>(
            await service.CreateAsync(
                productId,
                new CreateProductVariantRequest { Sku = "SKU-1", VariantLabel = "Rot" },
                "manager-1",
                CancellationToken.None));

        var deactivated = Assert.IsType<ProductVariantMutation.Ok>(
            await service.DeactivateAsync(productId, created.Variant.Id, "manager-1", CancellationToken.None));
        Assert.False(deactivated.Variant.IsActive);

        var stored = await db.ProductVariants.SingleAsync();
        Assert.False(stored.IsActive);
        Assert.Equal(created.Variant.Id, stored.Id);
    }

    [Fact]
    public async Task Update_WritesVariantChanged_AndStockAdjust_Writes149()
    {
        var tenantId = Guid.NewGuid();
        await using var db = await CreateDbAsync(NewDbName(), tenantId);
        var productId = await SeedProductAsync(db, tenantId);
        var audit = TicketAuditTestDoubles.Loose();
        var service = CreateService(db, audit);
        var created = Assert.IsType<ProductVariantMutation.Ok>(
            await service.CreateAsync(
                productId,
                new CreateProductVariantRequest { Sku = "SKU-1", VariantLabel = "Rot", Stock = 3 },
                "manager-1",
                CancellationToken.None));

        var updated = Assert.IsType<ProductVariantMutation.Ok>(
            await service.UpdateAsync(
                productId,
                created.Variant.Id,
                new UpdateProductVariantRequest
                {
                    Sku = "SKU-1",
                    VariantLabel = "Rot / L",
                    PriceDelta = 1m,
                    Stock = 3,
                    IsActive = true,
                },
                "manager-1",
                CancellationToken.None));
        Assert.Equal("Rot / L", updated.Variant.VariantLabel);

        var adjusted = Assert.IsType<ProductVariantMutation.Ok>(
            await service.AdjustStockAsync(productId, created.Variant.Id, -2, "manager-1", CancellationToken.None));
        Assert.Equal(1, adjusted.Variant.Stock);

        audit.Verify(a => a.LogSystemOperationAsync(
            "PRODUCT_VARIANT_CHANGED",
            "ProductVariant",
            "manager-1",
            "Manager",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            AuditLogStatus.Success,
            It.IsAny<string?>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>(),
            It.IsAny<ImpersonationAuditContext.Snapshot?>(),
            AuditEventType.ProductVariantChanged,
            created.Variant.Id,
            tenantId,
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>()), Times.Exactly(2));

        audit.Verify(a => a.LogSystemOperationAsync(
            "PRODUCT_VARIANT_STOCK_ADJUSTED",
            "ProductVariant",
            "manager-1",
            "Manager",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            AuditLogStatus.Success,
            It.IsAny<string?>(),
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>(),
            It.IsAny<ImpersonationAuditContext.Snapshot?>(),
            AuditEventType.ProductVariantStockAdjusted,
            created.Variant.Id,
            tenantId,
            It.IsAny<object?>(),
            It.IsAny<object?>(),
            It.IsAny<string?>()), Times.Once);
        Assert.Equal(149, (int)AuditEventType.ProductVariantStockAdjusted);
    }

    private static ProductVariantService CreateService(AppDbContext db, Mock<IAuditLogService>? audit = null) =>
        new(db, (audit ?? TicketAuditTestDoubles.Loose()).Object, NullLogger<ProductVariantService>.Instance);

    private static async Task<Guid> SeedProductAsync(AppDbContext db, Guid tenantId, string name = "Shirt")
    {
        if (!await db.Tenants.AnyAsync(tenant => tenant.Id == tenantId))
        {
            db.Tenants.Add(new Tenant
            {
                Id = tenantId,
                Name = $"Tenant {tenantId:N}",
                Slug = $"t-{tenantId:N}"[..12],
                Status = TenantStatuses.Active,
                IsActive = true,
            });
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = name,
            Price = 19.90m,
            TaxType = 1,
            TaxRate = 20m,
            Category = "Retail",
            CategoryId = Guid.NewGuid(),
            TaxGroupId = Guid.NewGuid(),
            Unit = "pcs",
            Barcode = $"BC-{Guid.NewGuid():N}"[..12],
            StockQuantity = 10,
            IsActive = true,
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        return product.Id;
    }

    private static async Task<AppDbContext> CreateDbAsync(string dbName, Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var accessor = TenantTestDoubles.TenantAccessorReturning(tenantId);
        var db = new AppDbContext(options, accessor);
        Accessors.Add(db, accessor);
        await db.Database.EnsureCreatedAsync();
        return db;
    }

    private static string NewDbName() => $"ProductVariants_{Guid.NewGuid():N}";
}
