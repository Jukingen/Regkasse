using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvPriceChangeComplianceCheckerTests
{
    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"rksv_price_compliance_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static RksvPriceChangeComplianceChecker CreateSut(AppDbContext db) =>
        new(
            db,
            new TaxRegulationService(db, NullLogger<TaxRegulationService>.Instance),
            NullLogger<RksvPriceChangeComplianceChecker>.Instance);

    [Fact]
    public async Task Check_WarnsRkSv001_WhenFiscalHistoryAndPriceChanges()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var db = CreateDb(tenantId);
        var (productId, taxGroupId) = await SeedAsync(db, tenantId, price: 3.5m, taxRate: 20m);
        await AddOrderLineAsync(db, productId);

        var sut = CreateSut(db);
        var result = await sut.CheckPriceChangeComplianceAsync(
            tenantId, productId, newPrice: 4.5m, newTaxGroupId: taxGroupId);

        Assert.True(result.IsCompliant);
        Assert.True(result.HasFiscalHistory);
        Assert.True(result.RequiresNewProductVersion);
        Assert.Contains(result.Warnings, w => w.Code == RksvPriceChangeComplianceChecker.CodeRequiresNewVersion);
        Assert.Contains(result.Requirements, r => r.Code == RksvPriceChangeComplianceChecker.CodeAuditTrailRequired);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Check_ErrorsRkSv002_WhenTaxRateInvalid()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var db = CreateDb(tenantId);
        var (productId, _) = await SeedAsync(db, tenantId, price: 3.5m, taxRate: 20m);

        var badGroupId = Guid.NewGuid();
        db.TaxGroups.Add(new TaxGroup
        {
            Id = badGroupId,
            TenantId = tenantId,
            Name = "Custom 7%",
            Rate = 7m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var sut = CreateSut(db);
        var result = await sut.CheckPriceChangeComplianceAsync(
            tenantId, productId, newPrice: 3.5m, newTaxGroupId: badGroupId);

        Assert.False(result.IsCompliant);
        Assert.Contains(result.Errors, e => e.Code == RksvPriceChangeComplianceChecker.CodeInvalidTaxRate);
        Assert.Contains("7", result.Errors[0].Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Check_AddsRkSv003_WhenPriceChanges()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var db = CreateDb(tenantId);
        var (productId, taxGroupId) = await SeedAsync(db, tenantId, price: 3.5m, taxRate: 20m);

        var sut = CreateSut(db);
        var result = await sut.CheckPriceChangeComplianceAsync(
            tenantId, productId, newPrice: 4.0m, newTaxGroupId: taxGroupId);

        Assert.True(result.IsCompliant);
        Assert.False(result.HasFiscalHistory);
        Assert.False(result.RequiresNewProductVersion);
        Assert.Contains(result.Requirements, r => r.Code == RksvPriceChangeComplianceChecker.CodeAuditTrailRequired);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task Check_ForceInPlace_KeepsWarningButNoNewVersion()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var db = CreateDb(tenantId);
        var (productId, taxGroupId) = await SeedAsync(db, tenantId, price: 3.5m, taxRate: 20m);
        await AddOrderLineAsync(db, productId);

        var sut = CreateSut(db);
        var result = await sut.CheckPriceChangeComplianceAsync(
            tenantId,
            productId,
            newPrice: 4.5m,
            newTaxGroupId: taxGroupId,
            forceInPlaceUpdate: true);

        Assert.True(result.IsCompliant);
        Assert.True(result.HasFiscalHistory);
        Assert.False(result.RequiresNewProductVersion);
        Assert.Contains(result.Warnings, w => w.Code == RksvPriceChangeComplianceChecker.CodeRequiresNewVersion);
    }

    private static async Task<(Guid ProductId, Guid TaxGroupId)> SeedAsync(
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
            Barcode = "ESP-COMP-1",
            IsActive = true,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
            StockQuantity = 10,
        });
        await db.SaveChangesAsync();
        return (productId, taxGroupId);
    }

    private static async Task AddOrderLineAsync(AppDbContext db, Guid productId)
    {
        db.OrderItems.Add(new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = "ORD-COMP-1",
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
    }
}
