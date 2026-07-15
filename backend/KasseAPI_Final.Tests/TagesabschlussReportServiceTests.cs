using KasseAPI_Final.Data;
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

public sealed class TagesabschlussReportServiceTests
{
    private static TagesabschlussReportService CreateService(
        string environmentName,
        TseOptions? tseOptions = null,
        IConfiguration? configuration = null,
        AppDbContext? context = null,
        IDailyClosingReportService? dailyClosingReportService = null,
        IReportPdfService? reportPdfService = null,
        IReportPdfStorageService? reportPdfStorage = null,
        ICurrentUserService? currentUserService = null) =>
        new(
            Mock.Of<IHostEnvironment>(h => h.EnvironmentName == environmentName),
            Options.Create(tseOptions ?? new TseOptions { Mode = "Real", TseMode = "Device" }),
            configuration ?? new ConfigurationBuilder().Build(),
            new RksvEnvironmentService(
                configuration ?? new ConfigurationBuilder().Build(),
                Mock.Of<IHostEnvironment>(h => h.EnvironmentName == environmentName)),
            CreateReportEnricherMock(),
            CreateReportTextService(),
            context ?? CreateContext(),
            dailyClosingReportService ?? Mock.Of<IDailyClosingReportService>(),
            reportPdfService ?? Mock.Of<IReportPdfService>(),
            reportPdfStorage ?? Mock.Of<IReportPdfStorageService>(),
            currentUserService ?? Mock.Of<ICurrentUserService>());

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"TagesabschlussReport_{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

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

    private static IRksvReportTextService CreateReportTextService() =>
        new RksvReportTextService(CreateReportEnricherMock());

    [Fact]
    public void GetRksvFooter_ReturnsDemoFooter_InDevelopment()
    {
        var svc = CreateService(Environments.Development);
        var footer = svc.GetRksvFooter(TenantTestDoubles.HostEnvironmentReturning(Environments.Development));

        Assert.Contains("DEMO / NICHT FISKAL", footer, StringComparison.Ordinal);
        Assert.Contains("SIMULIERT", footer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Testzwecken", footer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetRksvFooter_ReturnsProductionFooter_InProduction()
    {
        var svc = CreateService(Environments.Production);
        var footer = svc.GetRksvFooter(TenantTestDoubles.ProductionHostEnvironment);

        Assert.Contains("Registrierkassensicherheitsverordnung", footer, StringComparison.Ordinal);
        Assert.Contains("fiskalisch gültig", footer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GEPRÜFT", footer, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GetRksvFooterForClosing_UsesTseDemoMode_InProductionHost()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["RKSV:Mode"] = "Demo" })
            .Build();
        var svc = CreateService(Environments.Production, configuration: config);

        var footer = svc.GetRksvFooterForClosing();

        Assert.Contains("DEMO / NICHT FISKAL", footer, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatFooter_MatchesServiceConstants()
    {
        Assert.Equal(RksvEnvironmentService.DemoFooter.Trim(), TagesabschlussReportService.FormatFooter(true));
        Assert.Equal(RksvEnvironmentService.ProductionFooter.Trim(), TagesabschlussReportService.FormatFooter(false));
    }

    [Theory]
    [InlineData(true, "TSE SIMULIERT")]
    [InlineData(false, "TSE AKTIV")]
    public void GetTseStatusBadge_ReturnsEnvironmentBadge(bool isSimulated, string expected)
    {
        var svc = CreateService(Environments.Development);
        Assert.Equal(expected, svc.GetTseStatusBadge(isSimulated));
        Assert.Equal(expected, TagesabschlussReportService.FormatTseStatusBadge(isSimulated));
    }

    [Fact]
    public void GenerateReport_IncludesPaymentTaxChainAndDemoFooter()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Company:Name"] = "Test GmbH",
                ["Company:Address"] = "Wien 1",
                ["Company:VatId"] = "ATU99999999",
            })
            .Build();
        var svc = CreateService(Environments.Development, configuration: config);

        var closing = new DailyClosing
        {
            Id = Guid.NewGuid(),
            CashRegisterId = Guid.Parse("aaaaaaaa111111112222222233333333"),
            UserId = "cashier-1",
            ClosingDate = DateTime.UtcNow,
            ClosingType = "Daily",
            TotalAmount = 120m,
            TotalTaxAmount = 20m,
            TransactionCount = 5,
            TseSignature = "eyJhbGciOiJFUzI1NiJ9.eyJ.test.signature",
            PreviousSignature = "prev.signature.value.here",
            SignatureChainLength = 3,
            IsSimulated = true,
            Environment = "Demo",
            Status = "Completed",
            CashRegister = new CashRegister
            {
                RegisterNumber = "K1",
                Location = "Front",
            },
            User = new ApplicationUser
            {
                Id = "cashier-1",
                UserName = "Max",
            },
        };

        var summary = new DailyClosingSummaryDto
        {
            PaymentBreakdown = PaymentBreakdown.FromAmounts(80m, 30m, 10m, 0m),
            TaxBreakdown = new DailyClosingTaxBreakdownDto
            {
                TaxAt20 = 16m,
                TaxAt10 = 4m,
                GrossAt0 = 0m,
            },
        };

        var report = svc.GenerateReport(closing, summary, shiftNumber: "CAFEBABE");

        Assert.Contains("Firmenname:", report, StringComparison.Ordinal);
        Assert.Contains("Test GmbH", report, StringComparison.Ordinal);
        Assert.Contains("TAGESABSCHLUSS", report, StringComparison.Ordinal);
        Assert.Contains("Mitarbeiter:", report, StringComparison.Ordinal);
        Assert.Contains("Max", report, StringComparison.Ordinal);
        Assert.Contains("Schicht-Nr.:", report, StringComparison.Ordinal);
        Assert.Contains("CAFEBABE", report, StringComparison.Ordinal);
        Assert.Contains("TSE:             TSE simuliert (Demo)", report, StringComparison.Ordinal);
        Assert.Contains("Datum:", report, StringComparison.Ordinal);
        Assert.Contains("Zeitraum:", report, StringComparison.Ordinal);
        Assert.Contains("Bargeld:", report, StringComparison.Ordinal);
        Assert.Contains("€ 80,00", report, StringComparison.Ordinal);
        Assert.Contains("MwSt. gesamt:", report, StringComparison.Ordinal);
        Assert.Contains("Transaktionen:   5", report, StringComparison.Ordinal);
        Assert.Contains("Startbeleg:", report, StringComparison.Ordinal);
        Assert.Contains("DEP-Export:", report, StringComparison.Ordinal);
        Assert.Contains("Verifizierung:", report, StringComparison.Ordinal);
        Assert.Contains("eyJhbGciOiJFUzI1NiJ9", report, StringComparison.Ordinal);
        Assert.Contains("DEMO / NICHT FISKAL", report, StringComparison.Ordinal);
        Assert.Contains("NON_FISCAL_DEMO_DAILY_", report, StringComparison.Ordinal);
        Assert.DoesNotContain("Signaturkette", report, StringComparison.Ordinal);
        Assert.DoesNotContain("EFR", report, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAndSavePdfAsync_PersistsViaReportPdfService()
    {
        var closingId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var storedId = Guid.NewGuid();
        await using var ctx = CreateContext();
        ctx.DailyClosings.Add(new DailyClosing
        {
            Id = closingId,
            TenantId = LegacyDefaultTenantIds.Primary,
            CashRegisterId = Guid.NewGuid(),
            UserId = userId.ToString("D"),
            ClosingDate = DateTime.UtcNow,
            ClosingType = "Daily",
            TotalAmount = 10m,
            TotalTaxAmount = 2m,
            TransactionCount = 1,
            Status = "Completed",
        });
        await ctx.SaveChangesAsync();

        var pdfBytes = "%PDF-1.4"u8.ToArray();
        var dailyClosingReport = new Mock<IDailyClosingReportService>();
        dailyClosingReport
            .Setup(s => s.TryGenerateClosingReportPdfAsync(
                closingId,
                null,
                "de",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(pdfBytes);

        var reportPdfService = new Mock<IReportPdfService>();
        reportPdfService
            .Setup(s => s.SavePdfAsync(
                ReportPdfTypes.Tagesabschluss,
                closingId,
                pdfBytes,
                It.IsAny<string>(),
                userId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(storedId);

        var svc = CreateService(
            Environments.Production,
            context: ctx,
            dailyClosingReportService: dailyClosingReport.Object,
            reportPdfService: reportPdfService.Object);

        var resultId = await svc.GenerateAndSavePdfAsync(closingId, userId);

        Assert.Equal(storedId, resultId);
        reportPdfService.Verify(
            s => s.SavePdfAsync(
                ReportPdfTypes.Tagesabschluss,
                closingId,
                pdfBytes,
                It.Is<string>(name => name.StartsWith("tagesabschluss_", StringComparison.Ordinal)),
                userId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
