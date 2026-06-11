using System.Text;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DailyClosingReportServiceTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DailyClosingReport_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    [Theory]
    [InlineData("de", "Tagesabschluss-Bericht")]
    [InlineData("en", "Daily Closing Report")]
    [InlineData("tr", "Günlük Kapanış Raporu")]
    [InlineData("en-GB", "Daily Closing Report")]
    [InlineData(null, "Tagesabschluss-Bericht")]
    public void GenerateDailyReportPdf_ReturnsValidPdf_AndResolvesTemplate(string? language, string expectedTitle)
    {
        var labels = DailyClosingReportTemplates.Resolve(language);
        Assert.Equal(expectedTitle, labels.Title);

        var svc = new DailyClosingReportService(CreateContext());
        var pdf = svc.GenerateDailyReportPdf(SampleReport(), language ?? "de");

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf[..4]));
        Assert.True(pdf.Length > 200);
    }

    [Fact]
    public async Task TryGenerateStoredDailyReportPdf_ReturnsNull_WhenShiftNotOwned()
    {
        await using var ctx = CreateContext();
        var closingId = Guid.NewGuid();
        SeedShiftWithClosing(ctx, closingId, "owner-1");
        await ctx.SaveChangesAsync();

        var svc = new DailyClosingReportService(ctx);
        var pdf = await svc.TryGenerateStoredDailyReportPdfAsync(closingId, "other-user", "de");

        Assert.Null(pdf);
    }

    [Fact]
    public async Task TryGenerateStoredDailyReportPdf_ReturnsPdf_ForOwner()
    {
        await using var ctx = CreateContext();
        var closingId = Guid.NewGuid();
        SeedShiftWithClosing(ctx, closingId, "cashier-1");
        await ctx.SaveChangesAsync();

        var svc = new DailyClosingReportService(ctx);
        var pdf = await svc.TryGenerateStoredDailyReportPdfAsync(closingId, "cashier-1", "en");

        Assert.NotNull(pdf);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf![..4]));
    }

    [Theory]
    [InlineData("de", "de")]
    [InlineData("EN", "en")]
    [InlineData("tr-TR", "tr")]
    [InlineData("fr", "de")]
    public void NormalizeLanguage_MapsKnownCodes(string input, string expected)
    {
        Assert.Equal(expected, DailyClosingReportTemplates.NormalizeLanguage(input));
    }

    private static PosDailyClosingReportDto SampleReport() => new()
    {
        BusinessDate = new DateTime(2026, 6, 11),
        RegisterNumber = "K1",
        TotalSales = 120.50m,
        TotalCash = 80m,
        TotalCard = 40.50m,
        CashCount = 78m,
        Difference = -2m,
        FiscalTotalAmount = 120.50m,
        FiscalTotalTaxAmount = 20.08m,
        FiscalTransactionCount = 15,
        TseSignature = "abc123signature",
    };

    private static void SeedShiftWithClosing(AppDbContext ctx, Guid closingId, string cashierId)
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        var registerId = Guid.NewGuid();

        ctx.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "POS-1",
            Location = "Front",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });

        ctx.DailyClosings.Add(new DailyClosing
        {
            Id = closingId,
            TenantId = tenantId,
            CashRegisterId = registerId,
            UserId = cashierId,
            ClosingDate = new DateTime(2026, 6, 11),
            ClosingType = "Daily",
            TotalAmount = 100m,
            TotalTaxAmount = 16.67m,
            TransactionCount = 10,
            TseSignature = "sig",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
        });

        ctx.CashierShifts.Add(new CashierShift
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = registerId,
            CashierId = cashierId,
            CashierName = "Test Cashier",
            StartBalance = 50m,
            TotalSales = 100m,
            TotalCash = 60m,
            TotalCard = 40m,
            CashCount = 58m,
            Difference = -2m,
            StartedAt = DateTime.UtcNow.AddHours(-8),
            EndedAt = DateTime.UtcNow,
            Status = CashierShiftStatuses.Completed,
            DailyClosingId = closingId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        });
    }
}
