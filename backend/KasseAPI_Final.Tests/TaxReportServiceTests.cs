using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TaxReportServiceTests
{
    private static AppDbContext CreateDb(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"tax_report_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static Receipt MakeReceipt(Guid receiptId, Guid tenantId, DateTime issued) => new()
    {
        ReceiptId = receiptId,
        TenantId = tenantId,
        PaymentId = Guid.NewGuid(),
        ReceiptNumber = $"R-{receiptId:N}"[..12],
        IssuedAt = issued,
        CashRegisterId = Guid.NewGuid(),
        SubTotal = 0,
        TaxTotal = 0,
        GrandTotal = 0,
        CreatedAt = issued,
    };

    [Fact]
    public async Task GetReportAsync_AggregatesByRate()
    {
        var tenantId = SystemTenantIds.Platform;
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Legacy", Slug = "legacy", IsActive = true });

        var receiptA = Guid.NewGuid();
        var receiptB = Guid.NewGuid();
        var issued = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);

        db.Receipts.AddRange(MakeReceipt(receiptA, tenantId, issued), MakeReceipt(receiptB, tenantId, issued));

        db.ReceiptTaxLines.AddRange(
            new ReceiptTaxLine
            {
                LineId = Guid.NewGuid(),
                TenantId = tenantId,
                ReceiptId = receiptA,
                TaxType = TaxTypes.Standard,
                TaxRate = 20m,
                NetAmount = 100m,
                TaxAmount = 20m,
                GrossAmount = 120m,
            },
            new ReceiptTaxLine
            {
                LineId = Guid.NewGuid(),
                TenantId = tenantId,
                ReceiptId = receiptA,
                TaxType = TaxTypes.Reduced,
                TaxRate = 10m,
                NetAmount = 50m,
                TaxAmount = 5m,
                GrossAmount = 55m,
            },
            new ReceiptTaxLine
            {
                LineId = Guid.NewGuid(),
                TenantId = tenantId,
                ReceiptId = receiptB,
                TaxType = TaxTypes.Standard,
                TaxRate = 20m,
                NetAmount = 10m,
                TaxAmount = 2m,
                GrossAmount = 12m,
            });
        await db.SaveChangesAsync();

        var sut = new TaxReportService(db, NullLogger<TaxReportService>.Instance);
        var report = await sut.GetReportAsync(
            tenantId,
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(2, report.TaxGroups.Count);
        var standard = Assert.Single(report.TaxGroups, g => g.Rate == 20m);
        Assert.Equal(110m, standard.NetRevenue);
        Assert.Equal(22m, standard.TaxAmount);
        Assert.Equal(2, standard.TransactionCount);
        Assert.Equal(160m, report.TotalNetRevenue);
        Assert.Equal(27m, report.TotalTaxAmount);
    }

    [Fact]
    public async Task GetTrendAsync_BucketsByDayAndRate()
    {
        var tenantId = SystemTenantIds.Platform;
        await using var db = CreateDb(tenantId);
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "Legacy", Slug = "legacy", IsActive = true });
        var receiptId = Guid.NewGuid();
        var issued = new DateTime(2026, 3, 10, 8, 0, 0, DateTimeKind.Utc);
        db.Receipts.Add(MakeReceipt(receiptId, tenantId, issued));
        db.ReceiptTaxLines.Add(new ReceiptTaxLine
        {
            LineId = Guid.NewGuid(),
            TenantId = tenantId,
            ReceiptId = receiptId,
            TaxType = TaxTypes.Reduced,
            TaxRate = 10m,
            NetAmount = 20m,
            TaxAmount = 2m,
            GrossAmount = 22m,
        });
        await db.SaveChangesAsync();

        var sut = new TaxReportService(db, NullLogger<TaxReportService>.Instance);
        var trend = await sut.GetTrendAsync(
            tenantId,
            new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));

        var point = Assert.Single(trend);
        Assert.Equal(10m, point.Rate);
        Assert.Equal(2m, point.Amount);
        Assert.Equal(new DateTime(2026, 3, 10, 0, 0, 0, DateTimeKind.Utc), point.Date);
    }
}
