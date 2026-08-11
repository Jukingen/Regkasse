using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PriceChangeServiceTests
{
    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"price_change_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static PriceChangeService CreateSut(AppDbContext db, Mock<IAuditLogService>? audit = null)
    {
        audit ??= new Mock<IAuditLogService>();
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
                It.IsAny<KasseAPI_Final.Services.ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog { Id = Guid.NewGuid() });

        return new PriceChangeService(
            db,
            new ProductPriceHistoryService(db, NullLogger<ProductPriceHistoryService>.Instance),
            new RksvPriceChangeComplianceChecker(
                db,
                new TaxRegulationService(db, NullLogger<TaxRegulationService>.Instance),
                NullLogger<RksvPriceChangeComplianceChecker>.Instance),
            audit.Object,
            NullLogger<PriceChangeService>.Instance);
    }

    [Fact]
    public async Task ChangePriceAsync_UpdatesProductAndCreatesVersion()
    {
        var tenantId = SystemTenantIds.Platform;
        await using var db = CreateDb(tenantId);
        var (productId, taxGroupId) = await SeedProductAsync(db, tenantId, price: 3.5m, taxRate: 20m);

        var sut = CreateSut(db);
        var actor = Guid.NewGuid();
        var result = await sut.ChangePriceAsync(new PriceChangeRequest
        {
            TenantId = tenantId,
            ProductId = productId,
            NewPrice = 4.2m,
            NewTaxGroupId = taxGroupId,
            ChangedBy = actor,
            Reason = "List price update",
        });

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(3.5m, result.OldPrice);
        Assert.Equal(4.2m, result.NewPrice);
        Assert.Equal("1.0", result.Version);

        var product = await db.Products.SingleAsync(p => p.Id == productId);
        Assert.Equal(4.2m, product.Price);

        var history = await sut.GetPriceHistoryAsync(tenantId, productId);
        Assert.Single(history);
        Assert.Equal(4.2m, history[0].NewPrice);
        Assert.True(history[0].IsRksvCompliant);
    }

    [Fact]
    public async Task ChangePriceAsync_WithFiscalHistory_CreatesNewCatalogProduct()
    {
        var tenantId = SystemTenantIds.Platform;
        await using var db = CreateDb(tenantId);
        var (productId, taxGroupId) = await SeedProductAsync(db, tenantId, price: 3.5m, taxRate: 20m);

        db.OrderItems.Add(new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = "ORD-RKSV-1",
            ProductId = productId,
            ProductName = "Espresso",
            Quantity = 1,
            UnitPrice = 3.5m,
            TaxRate = 20m,
            TaxAmount = 0.58m,
            DiscountAmount = 0m,
            TotalAmount = 3.5m,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        var result = await sut.ChangePriceAsync(new PriceChangeRequest
        {
            TenantId = tenantId,
            ProductId = productId,
            NewPrice = 4.5m,
            NewTaxGroupId = taxGroupId,
            ChangedBy = Guid.NewGuid(),
            Reason = "RKSV catalog version",
        });

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.True(result.CreatedNewProductVersion);
        Assert.Equal(productId, result.ArchivedProductId);
        Assert.NotEqual(productId, result.ProductId);
        Assert.Equal(2, result.CatalogVersion);

        var archived = await db.Products.SingleAsync(p => p.Id == productId);
        Assert.False(archived.IsActive);
        Assert.NotNull(archived.ArchivedAt);
        Assert.Contains("__v1_", archived.Barcode, StringComparison.Ordinal);

        var successor = await db.Products.SingleAsync(p => p.Id == result.ProductId);
        Assert.True(successor.IsActive);
        Assert.Equal(4.5m, successor.Price);
        Assert.Equal(2, successor.Version);
        Assert.Equal(productId, successor.OriginalProductId);
        Assert.Equal("ESP-PC-1", successor.Barcode);
    }

    [Fact]
    public async Task ValidatePriceChangeAsync_WarnsWhenOrderHistoryExists()
    {
        var tenantId = SystemTenantIds.Platform;
        await using var db = CreateDb(tenantId);
        var (productId, taxGroupId) = await SeedProductAsync(db, tenantId, price: 3.5m, taxRate: 20m);

        db.OrderItems.Add(new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = "ORD-1",
            ProductId = productId,
            ProductName = "Espresso",
            Quantity = 1,
            UnitPrice = 3.5m,
            TaxRate = 20m,
            TaxAmount = 0.58m,
            DiscountAmount = 0m,
            TotalAmount = 3.5m,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        var validation = await sut.ValidatePriceChangeAsync(new PriceChangeRequest
        {
            TenantId = tenantId,
            ProductId = productId,
            NewPrice = 4.0m,
            NewTaxGroupId = taxGroupId,
            ChangedBy = Guid.NewGuid(),
        });

        Assert.True(validation.IsValid);
        Assert.True(validation.HasWarning);
        Assert.True(validation.HasFiscalHistory);
        Assert.True(validation.RequiresNewProductVersion);
        Assert.Contains("new product version", validation.WarningMessage, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(validation.Compliance);
        Assert.Contains(
            validation.Compliance!.Warnings,
            w => w.Code == RksvPriceChangeComplianceChecker.CodeRequiresNewVersion);
    }

    [Fact]
    public async Task ChangePriceAsync_RejectsUnchangedPrice()
    {
        var tenantId = SystemTenantIds.Platform;
        await using var db = CreateDb(tenantId);
        var (productId, taxGroupId) = await SeedProductAsync(db, tenantId, price: 3.5m, taxRate: 20m);

        var sut = CreateSut(db);
        var result = await sut.ChangePriceAsync(new PriceChangeRequest
        {
            TenantId = tenantId,
            ProductId = productId,
            NewPrice = 3.5m,
            NewTaxGroupId = taxGroupId,
            ChangedBy = Guid.NewGuid(),
        });

        Assert.False(result.Succeeded);
        Assert.Contains("unchanged", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(Guid ProductId, Guid TaxGroupId)> SeedProductAsync(
        AppDbContext db,
        Guid tenantId,
        decimal price,
        decimal taxRate)
    {
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Legacy", Slug = "legacy", IsActive = true });

        var taxGroupId = Guid.NewGuid();
        db.TaxGroups.Add(new TaxGroup
        {
            Id = taxGroupId,
            TenantId = tenantId,
            Name = "Normalsatz",
            Rate = taxRate,
            IsActive = true,
            IsSystem = true,
            GroupType = TaxGroupType.Standard,
            CreatedAt = DateTime.UtcNow,
        });

        var categoryId = Guid.NewGuid();
        db.Categories.Add(new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Key = "test",
            Name = "Test",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        var productId = Guid.NewGuid();
        db.Products.Add(new Product
        {
            Id = productId,
            TenantId = tenantId,
            CategoryId = categoryId,
            TaxGroupId = taxGroupId,
            Name = "Espresso",
            Category = "Test",
            Price = price,
            TaxRate = taxRate,
            TaxType = TaxTypes.FromRate(taxRate),
            Unit = "pcs",
            Barcode = "ESP-PC-1",
            IsActive = true,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            StockQuantity = 10,
        });
        await db.SaveChangesAsync();
        return (productId, taxGroupId);
    }
}
