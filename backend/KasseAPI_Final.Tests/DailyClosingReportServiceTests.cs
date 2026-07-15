using System.Text;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Moq;
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

    private static DailyClosingReportService CreateReportService(
        AppDbContext ctx,
        IReportPdfService? reportPdfService = null,
        ICurrentUserService? currentUserService = null) =>
        new(
            ctx,
            DailyClosingTestDoubles.Create(ctx),
            new MockQrImageService(),
            Mock.Of<IHostEnvironment>(h => h.EnvironmentName == Environments.Production),
            Options.Create(new TseOptions { Mode = "Real", TseMode = "Device" }),
            new ConfigurationBuilder().Build(),
            new RksvEnvironmentService(new ConfigurationBuilder().Build(),
                Mock.Of<IHostEnvironment>(h => h.EnvironmentName == Environments.Production)),
            CreateReportEnricherMock(),
            new RksvReportTextService(CreateReportEnricherMock()),
            Mock.Of<IReportPdfStorageService>(),
            reportPdfService ?? Mock.Of<IReportPdfService>(),
            currentUserService ?? Mock.Of<ICurrentUserService>());

    private static ITagesabschlussReportEnricher CreateReportEnricherMock()
    {
        var mock = new Mock<ITagesabschlussReportEnricher>();
        mock.Setup(e => e.BuildContextAsync(It.IsAny<DailyClosing>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TagesabschlussCloudContext());
        mock.Setup(e => e.BuildContextForRegisterAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TagesabschlussCloudContext());
        return mock.Object;
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

        var svc = CreateReportService(CreateContext());
        var pdf = svc.GenerateDailyReportPdf(SampleReport(), language ?? "de");

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf[..4]));
        Assert.True(pdf.Length > 200);
    }

    [Fact]
    public void GenerateDailyReportText_UsesUnifiedTemplate_ForMonatsbeleg()
    {
        var svc = CreateReportService(CreateContext());
        var report = SampleReport(closingType: "Monthly");

        var text = svc.GenerateDailyReportText(report);

        Assert.Contains("MONATSBELEG", text, StringComparison.Ordinal);
        Assert.Contains("Finanzübersicht:", text, StringComparison.Ordinal);
        Assert.Contains("RKSV / FinanzOnline:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("EFR", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryGenerateStoredDailyReportPdf_ReturnsNull_WhenShiftNotOwned()
    {
        await using var ctx = CreateContext();
        var closingId = Guid.NewGuid();
        SeedShiftWithClosing(ctx, closingId, "owner-1");
        await ctx.SaveChangesAsync();

        var svc = CreateReportService(ctx);
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

        var svc = CreateReportService(ctx);
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

    [Fact]
    public void GenerateDailyReportPdf_WithInflatedDemoMonatsbelegSignature_CompletesQuickly()
    {
        var longSig = DemoMonatsbelegSignatureFixture.Value;
        var report = new PosDailyClosingReportDto
        {
            ClosingType = "Monthly",
            BusinessDate = new DateTime(2026, 7, 1),
            RegisterNumber = "KASSE-001",
            TotalSales = 154m,
            FiscalTotalAmount = 154m,
            FiscalTotalTaxAmount = 13.97m,
            FiscalTransactionCount = 10,
            TseSignature = longSig,
            QrPayload = "NON_FISCAL_DEMO_DAILY_2026-07-01_154.00",
            IsDemoFiscal = true,
            TseStatusLabel = "TSE: SIMULIERT (NUR TEST)",
            TseStatusBadge = "TSE SIMULIERT",
            SnapshotDisclaimerDe = "DEMO / NICHT FISKAL",
        };

        var svc = CreateReportService(CreateContext());
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var pdf = svc.GenerateDailyReportPdf(report, "de");
        sw.Stop();

        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf[..4]));
        Assert.True(sw.ElapsedMilliseconds < 5000, $"PDF generation took {sw.ElapsedMilliseconds}ms");
    }

    private static readonly Lazy<string> DemoMonatsbelegSignatureFixture = new(() =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "monatsbeleg_sig.txt")).Trim());

    [Theory]
    [InlineData("Monthly", "Monatsabschluss-Bericht")]
    [InlineData("Yearly", "Jahresabschluss-Bericht")]
    public void GenerateDailyReportPdf_UsesClosingTypeTitle(string closingType, string expectedTitle)
    {
        var svc = CreateReportService(CreateContext());
        var baseReport = SampleReport();
        var dto = new PosDailyClosingReportDto
        {
            ClosingType = closingType,
            BusinessDate = baseReport.BusinessDate,
            RegisterNumber = baseReport.RegisterNumber,
            TotalSales = baseReport.TotalSales,
            TotalCash = baseReport.TotalCash,
            TotalCard = baseReport.TotalCard,
            CashCount = baseReport.CashCount,
            Difference = baseReport.Difference,
            FiscalTotalAmount = baseReport.FiscalTotalAmount,
            FiscalTotalTaxAmount = baseReport.FiscalTotalTaxAmount,
            FiscalTransactionCount = baseReport.FiscalTransactionCount,
            TseSignature = baseReport.TseSignature,
        };

        var labels = DailyClosingReportTemplates.Resolve("de", closingType);
        Assert.Equal(expectedTitle, labels.Title);

        var pdf = svc.GenerateDailyReportPdf(dto, "de");
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(pdf[..4]));
    }

    [Fact]
    public void Compose_UsesDemoDisclaimer_WhenDemoFiscal()
    {
        var closing = new DailyClosing
        {
            ClosingType = "Daily",
            ClosingDate = new DateTime(2026, 6, 11),
            TotalAmount = 100m,
            TotalTaxAmount = 16.67m,
            TransactionCount = 5,
            TseSignature = "demo.sig",
        };

        var demoEnv = new FiscalEnvironmentResolver.FiscalEnvironment(
            true,
            "Demo",
            "DEMO / NICHT FISKAL",
            "TSE: SIMULIERT (NUR TEST)",
            "TSE SIMULIERT");

        var report = DailyClosingReportComposer.Compose(
            closing,
            "K1",
            null,
            0m,
            0m,
            fiscalEnvironment: demoEnv);

        Assert.True(report.IsDemoFiscal);
        Assert.Equal("TSE SIMULIERT", report.TseStatusBadge);
        Assert.Equal("TSE: SIMULIERT (NUR TEST)", report.TseStatusLabel);
        Assert.Contains("DEMO", report.SnapshotDisclaimerDe, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("NON_FISCAL_DEMO", report.QrPayload, StringComparison.Ordinal);
    }

    [Fact]
    public void Compose_UsesDemoPaymentBreakdown_WhenDemoAndNoDaySummary()
    {
        var closing = new DailyClosing
        {
            ClosingType = "Daily",
            ClosingDate = new DateTime(2026, 6, 11),
            TotalAmount = 175m,
            TotalTaxAmount = 29.17m,
            TransactionCount = 3,
            TseSignature = "demo.sig",
        };

        var demoEnv = new FiscalEnvironmentResolver.FiscalEnvironment(
            true,
            "Demo",
            "DEMO / NICHT FISKAL",
            "TSE: SIMULIERT (NUR TEST)",
            "TSE SIMULIERT");

        var report = DailyClosingReportComposer.Compose(
            closing,
            "K1",
            daySummary: null,
            0m,
            0m,
            fiscalEnvironment: demoEnv);

        Assert.Equal(100m, report.PaymentBreakdown.Cash);
        Assert.Equal(50m, report.PaymentBreakdown.Card);
        Assert.Equal(25m, report.PaymentBreakdown.Voucher);
        Assert.Equal(175m, report.PaymentBreakdown.Total);
    }

    private static PosDailyClosingReportDto SampleReport(string closingType = "Daily") => new()
    {
        BusinessDate = new DateTime(2026, 6, 11),
        ClosingType = closingType,
        RegisterNumber = "K1",
        TotalSales = 120.50m,
        TotalCash = 80m,
        TotalCard = 40.50m,
        TotalVoucherRedemptions = 0m,
        TotalOtherPaymentMethods = 0m,
        CashCount = 78m,
        Difference = -2m,
        FiscalTotalAmount = 120.50m,
        FiscalTotalTaxAmount = 20.08m,
        FiscalTransactionCount = 15,
        TseSignature = "abc123signature",
    };

    private sealed class MockQrImageService : IQrImageService
    {
        public byte[]? GenerateQrCodeImage(string? payload) => null;
        public byte[]? GetQrPngFromExactPayload(string? payload) => null;
        public Task<byte[]?> GetQrPngAsync(Guid paymentId, CancellationToken ct = default) => Task.FromResult<byte[]?>(null);
        public Task<string?> GetQrSvgAsync(Guid paymentId, CancellationToken ct = default) => Task.FromResult<string?>(null);
    }

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
