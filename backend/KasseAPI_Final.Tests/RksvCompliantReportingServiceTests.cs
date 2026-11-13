using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvCompliantReportingServiceTests
{
    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"rksv_report_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    [Fact]
    public async Task GenerateHistoricalReportAsync_UsesReceiptTaxLineRatesNotCatalog()
    {
        var tenantId = SystemTenantIds.Platform;
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Legacy", Slug = "legacy", IsActive = true });

        var taxGroupId = Guid.NewGuid();
        db.TaxGroups.Add(new TaxGroup
        {
            Id = taxGroupId,
            TenantId = tenantId,
            Name = "Normalsatz",
            Rate = 20m,
            IsActive = true,
            IsDefault = true,
            CreatedAt = DateTime.UtcNow,
        });

        var categoryId = Guid.NewGuid();
        db.Categories.Add(new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Key = "c1",
            Name = "Cat",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        // Catalog rate later changed to 10% — report must still use 20% from receipt line.
        var productId = Guid.NewGuid();
        db.Products.Add(new Product
        {
            Id = productId,
            TenantId = tenantId,
            CategoryId = categoryId,
            TaxGroupId = taxGroupId,
            Name = "Espresso",
            Category = "Cat",
            Price = 4m,
            TaxRate = 10m,
            TaxType = TaxTypes.Reduced,
            Unit = "pcs",
            Barcode = "ESP-RPT",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        var issued = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var receiptId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        db.Receipts.Add(new Receipt
        {
            ReceiptId = receiptId,
            TenantId = tenantId,
            PaymentId = paymentId,
            ReceiptNumber = "AT-TEST-001",
            IssuedAt = issued,
            CashRegisterId = Guid.NewGuid(),
            SubTotal = 100m,
            TaxTotal = 20m,
            GrandTotal = 120m,
            SignatureValue = "header.payload.signature",
            CreatedAt = issued,
        });
        db.ReceiptTaxLines.Add(new ReceiptTaxLine
        {
            LineId = Guid.NewGuid(),
            TenantId = tenantId,
            ReceiptId = receiptId,
            TaxType = TaxTypes.Standard,
            TaxRate = 20m,
            NetAmount = 100m,
            TaxAmount = 20m,
            GrossAmount = 120m,
        });
        await db.SaveChangesAsync();

        var sut = new RksvCompliantReportingService(db, NullLogger<RksvCompliantReportingService>.Instance);
        var report = await sut.GenerateHistoricalReportAsync(
            tenantId,
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        var txn = Assert.Single(report.Transactions);
        Assert.Equal(20m, txn.TaxRate);
        Assert.Equal(20m, txn.TaxAmount);
        Assert.Equal(100m, txn.Amount);
        Assert.Equal("Normalsatz", txn.TaxGroupName);
        Assert.Equal("header.payload.signature", txn.TseSignature);
        Assert.True(report.TaxBreakdown.ContainsKey(20m));
        Assert.Equal(20m, report.TaxBreakdown[20m]);
        Assert.Equal(100m, report.TotalNet);
        Assert.Equal(20m, report.TotalTax);
        Assert.Equal(120m, report.TotalGross);
        Assert.True(report.IsCompliant);
    }

    [Fact]
    public async Task GetTaxBreakdownForPeriodAsync_BucketsSingleDay()
    {
        var tenantId = SystemTenantIds.Platform;
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Legacy", Slug = "legacy", IsActive = true });

        var day = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc);
        var receiptId = Guid.NewGuid();
        db.Receipts.Add(new Receipt
        {
            ReceiptId = receiptId,
            TenantId = tenantId,
            PaymentId = Guid.NewGuid(),
            ReceiptNumber = "R-DAY",
            IssuedAt = day.AddHours(9),
            CashRegisterId = Guid.NewGuid(),
            SubTotal = 50m,
            TaxTotal = 5m,
            GrandTotal = 55m,
            SignatureValue = "a.b.c",
            CreatedAt = day,
        });
        db.ReceiptTaxLines.Add(new ReceiptTaxLine
        {
            LineId = Guid.NewGuid(),
            TenantId = tenantId,
            ReceiptId = receiptId,
            TaxType = TaxTypes.Reduced,
            TaxRate = 10m,
            NetAmount = 50m,
            TaxAmount = 5m,
            GrossAmount = 55m,
        });
        await db.SaveChangesAsync();

        var sut = new RksvCompliantReportingService(db, NullLogger<RksvCompliantReportingService>.Instance);
        var breakdown = await sut.GetTaxBreakdownForPeriodAsync(tenantId, day);

        Assert.Equal(day, breakdown.Date);
        Assert.Equal(1, breakdown.ReceiptCount);
        Assert.Equal(5m, breakdown.ByRate[10m]);
        Assert.Equal(50m, breakdown.TotalNet);
    }

    [Fact]
    public async Task GenerateHistoricalReportAsync_WarnsWhenTseMissing()
    {
        var tenantId = SystemTenantIds.Platform;
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Legacy", Slug = "legacy", IsActive = true });

        var receiptId = Guid.NewGuid();
        var issued = new DateTime(2026, 4, 1, 10, 0, 0, DateTimeKind.Utc);
        db.Receipts.Add(new Receipt
        {
            ReceiptId = receiptId,
            TenantId = tenantId,
            PaymentId = Guid.NewGuid(),
            ReceiptNumber = "R-NOSIG",
            IssuedAt = issued,
            CashRegisterId = Guid.NewGuid(),
            SubTotal = 10m,
            TaxTotal = 2m,
            GrandTotal = 12m,
            SignatureValue = null,
            CreatedAt = issued,
        });
        db.ReceiptTaxLines.Add(new ReceiptTaxLine
        {
            LineId = Guid.NewGuid(),
            TenantId = tenantId,
            ReceiptId = receiptId,
            TaxType = TaxTypes.Standard,
            TaxRate = 20m,
            NetAmount = 10m,
            TaxAmount = 2m,
            GrossAmount = 12m,
        });
        await db.SaveChangesAsync();

        var sut = new RksvCompliantReportingService(db, NullLogger<RksvCompliantReportingService>.Instance);
        var report = await sut.GenerateHistoricalReportAsync(
            tenantId,
            new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 2, 0, 0, 0, DateTimeKind.Utc));

        Assert.False(report.IsCompliant);
        Assert.Contains(report.Warnings, w => w.Code == "MISSING_TSE_SIGNATURE");
    }

    [Fact]
    public async Task GetPriceHistoryForProductAsync_ReturnsJournalAndVersions()
    {
        var tenantId = SystemTenantIds.Platform;
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Legacy", Slug = "legacy", IsActive = true });

        var taxGroupId = Guid.NewGuid();
        db.TaxGroups.Add(new TaxGroup
        {
            Id = taxGroupId,
            TenantId = tenantId,
            Name = "Normalsatz",
            Rate = 20m,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        var categoryId = Guid.NewGuid();
        db.Categories.Add(new Category
        {
            Id = categoryId,
            TenantId = tenantId,
            Key = "c",
            Name = "C",
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
            Name = "Latte",
            Category = "C",
            Price = 4m,
            TaxRate = 20m,
            TaxType = TaxTypes.Standard,
            Unit = "pcs",
            Barcode = "LATTE-1",
            IsActive = true,
            Version = 1,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var historySvc = new ProductPriceHistoryService(db, NullLogger<ProductPriceHistoryService>.Instance);
        await historySvc.EnsureInitialVersionAsync(tenantId, productId, 3.5m, taxGroupId, 20m, Guid.NewGuid());
        await historySvc.RecordChangeAsync(
            tenantId, productId, 3.5m, 4m, taxGroupId, taxGroupId, 20m, 20m, Guid.NewGuid(), "Raise");

        var sut = new RksvCompliantReportingService(db, NullLogger<RksvCompliantReportingService>.Instance);
        var report = await sut.GetPriceHistoryForProductAsync(tenantId, productId);

        Assert.Equal("Latte", report.ProductName);
        Assert.Equal(2, report.History.Count);
        Assert.Equal(2, report.Versions.Count);
        Assert.Contains(report.Versions, v => v.IsCurrent && v.Price == 4m);
    }
}
