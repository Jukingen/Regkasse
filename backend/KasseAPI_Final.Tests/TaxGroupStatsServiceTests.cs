using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TaxGroupStatsServiceTests
{
    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tax_group_stats_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    [Fact]
    public async Task GetStatsAsync_ComputesProductShareAndRevenueByRate()
    {
        var tenantId = SystemTenantIds.Platform;
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Legacy", Slug = "legacy", IsActive = true });

        var reducedId = Guid.NewGuid();
        var standardId = Guid.NewGuid();
        db.TaxGroups.AddRange(
            new TaxGroup
            {
                Id = reducedId,
                TenantId = tenantId,
                Name = "Ermäßigt",
                Rate = 10m,
                IsActive = true,
                IsSystem = true,
                Color = "#52c41a",
                Icon = "🥬",
                GroupType = TaxGroupType.Reduced,
                CreatedAt = DateTime.UtcNow,
            },
            new TaxGroup
            {
                Id = standardId,
                TenantId = tenantId,
                Name = "Normalsatz",
                Rate = 20m,
                IsActive = true,
                IsSystem = true,
                Color = "#1677ff",
                Icon = "🧾",
                GroupType = TaxGroupType.Standard,
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

        db.Products.AddRange(
            MakeProduct(tenantId, categoryId, reducedId, "Bread", 10m),
            MakeProduct(tenantId, categoryId, reducedId, "Milk", 10m),
            MakeProduct(tenantId, categoryId, standardId, "Wine", 20m));

        var receiptId = Guid.NewGuid();
        var issued = new DateTime(2026, 3, 15, 12, 0, 0, DateTimeKind.Utc);
        db.Receipts.Add(new Receipt
        {
            ReceiptId = receiptId,
            TenantId = tenantId,
            PaymentId = Guid.NewGuid(),
            IssuedAt = issued,
            ReceiptNumber = "R-1",
            CashRegisterId = Guid.NewGuid(),
            SubTotal = 150m,
            TaxTotal = 20m,
            GrandTotal = 170m,
            CreatedAt = issued,
        });
        db.ReceiptTaxLines.AddRange(
            new ReceiptTaxLine
            {
                LineId = Guid.NewGuid(),
                TenantId = tenantId,
                ReceiptId = receiptId,
                TaxRate = 10m,
                TaxType = 2,
                NetAmount = 100m,
                TaxAmount = 10m,
                GrossAmount = 110m,
            },
            new ReceiptTaxLine
            {
                LineId = Guid.NewGuid(),
                TenantId = tenantId,
                ReceiptId = receiptId,
                TaxRate = 20m,
                TaxType = 1,
                NetAmount = 50m,
                TaxAmount = 10m,
                GrossAmount = 60m,
            });
        await db.SaveChangesAsync();

        var sut = new TaxGroupStatsService(db, NullLogger<TaxGroupStatsService>.Instance);
        var report = await sut.GetStatsAsync(
            tenantId,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(3, report.TotalProducts);
        Assert.Equal(170m, report.TotalRevenue);

        var reduced = Assert.Single(report.Groups, g => g.Id == reducedId);
        Assert.Equal(2, reduced.ProductCount);
        Assert.Equal(66.67m, reduced.Percentage);
        Assert.Equal(110m, reduced.Revenue);

        var standard = Assert.Single(report.Groups, g => g.Id == standardId);
        Assert.Equal(1, standard.ProductCount);
        Assert.Equal(33.33m, standard.Percentage);
        Assert.Equal(60m, standard.Revenue);
    }

    private static Product MakeProduct(
        Guid tenantId,
        Guid categoryId,
        Guid taxGroupId,
        string name,
        decimal taxRate) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CategoryId = categoryId,
            TaxGroupId = taxGroupId,
            Name = name,
            Category = "Cat",
            Price = 1m,
            TaxRate = taxRate,
            TaxType = TaxTypes.FromRate(taxRate),
            Unit = "pcs",
            Barcode = $"BC-{name}",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };
}
