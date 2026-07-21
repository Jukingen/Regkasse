using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class JahresbelegClosingServiceTests
{
    private static AppDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"JahresbelegClosing_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static JahresbelegClosingService CreateService(
        AppDbContext ctx,
        ITseService? tse = null,
        IHostEnvironment? hostEnvironment = null,
        TseOptions? tseOptions = null,
        IConfiguration? configuration = null)
    {
        tse ??= CreateTseMock().Object;
        hostEnvironment ??= TenantTestDoubles.ProductionHostEnvironment;
        tseOptions ??= new TseOptions { TseMode = "Demo", Mode = "Fake" };
        configuration ??= new ConfigurationBuilder().Build();

        return new JahresbelegClosingService(
            ctx,
            tse,
            Mock.Of<ITseKeyProvider>(p => p.GetCurrentCertificateThumbprint() == "thumb-test"),
            TenantTestDoubles.PrimaryTenantResolver,
            hostEnvironment,
            Options.Create(tseOptions),
            configuration,
            new RksvEnvironmentService(configuration, hostEnvironment),
            CreateJahresbelegReportService(),
            Mock.Of<ILogger<JahresbelegClosingService>>(),
            Mock.Of<IReportPdfCaptureService>());
    }

    private static IJahresbelegReportService CreateJahresbelegReportService()
    {
        var enricher = CreateReportEnricherMock();
        return new JahresbelegReportService(enricher, new RksvReportTextService(enricher));
    }

    private static ITagesabschlussReportEnricher CreateReportEnricherMock()
    {
        var mock = new Mock<ITagesabschlussReportEnricher>();
        mock.Setup(e => e.BuildContextForRegisterAsync(
                It.IsAny<Guid>(),
                It.IsAny<DateTime?>(),
                It.IsAny<DateTime?>(),
                It.IsAny<bool>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TagesabschlussCloudContext
            {
                CompanyName = "Test GmbH",
                CompanyAddress = "Wien",
                CompanyVatId = "ATU12345678",
            });
        return mock.Object;
    }

    private static Mock<ITseService> CreateTseMock()
    {
        var mock = new Mock<ITseService>();
        mock.Setup(x => x.CreateYearlyClosingSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<decimal>(),
                It.IsAny<int>(),
                It.IsAny<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?>()))
            .ReturnsAsync("eyJhbGciOiJFUzI1NiJ9.eyJ.test.jahr.closing");
        return mock;
    }

    [Fact]
    public async Task CreateJahresbelegClosingAsync_PersistsAggregatedTotals_AndMonthlyReferences()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureDefaultTenant(ctx);

        var regId = Guid.NewGuid();
        var (year, month) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();

        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = tenantId,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        ctx.CompanySettings.Add(new CompanySettings
        {
            TenantId = tenantId,
            CompanyName = "Test GmbH",
            CompanyAddress = "Wien",
            CompanyTaxNumber = "ATU12345678",
            BusinessHours = new Dictionary<string, string>(),
            UseDecemberMonatsbelegAsJahresbeleg = true,
        });

        SeedMonatsbelegMonths(ctx, tenantId, regId, year, monthsThrough: month, grossPerMonth: 100m);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.CreateJahresbelegClosingAsync(
            "manager-1",
            new CreateJahresbelegClosingRequest { CashRegisterId = regId, Year = year });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.JahresbelegId);
        Assert.Equal(100m * month, result.TotalGross);
        Assert.Equal(month, result.MonatsbelegCount);
        Assert.False(string.IsNullOrEmpty(result.TseSignature));

        var row = await ctx.Jahresbelege.SingleAsync();
        Assert.Equal(regId, row.CashRegisterId);
        Assert.Equal(year, row.Year);
        Assert.Equal(100m * month, row.TotalGross);
        Assert.Equal(100m * month * 0.2m, row.TotalTax);
        Assert.Equal(month * 2, row.TransactionCount);
        Assert.True(row.IsSimulated);
        Assert.Equal("Demo", row.Environment);
        Assert.Contains("\"month\":1", row.MonthlyReferences, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateJahresbelegClosingAsync_LinksPreviousYearSignature()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureDefaultTenant(ctx);

        var regId = Guid.NewGuid();
        var (year, month) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();

        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = tenantId,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        ctx.Jahresbelege.Add(new Jahresbeleg
        {
            TenantId = tenantId,
            CashRegisterId = regId,
            Year = year - 1,
            TotalGross = 500m,
            TotalTax = 100m,
            TransactionCount = 10,
            TseSignature = "prev-year-sig",
            Environment = "Demo",
            CreatedByUserId = "u1",
        });

        SeedMonatsbelegMonths(ctx, tenantId, regId, year, monthsThrough: month, grossPerMonth: 50m);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.CreateJahresbelegClosingAsync(
            "manager-1",
            new CreateJahresbelegClosingRequest { CashRegisterId = regId, Year = year });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("prev-year-sig", result.PreviousSignature);

        var row = await ctx.Jahresbelege.SingleAsync(j => j.Year == year);
        Assert.Equal("prev-year-sig", row.PreviousSignature);
        Assert.Equal(2, row.SignatureChainLength);
    }

    [Fact]
    public async Task CreateJahresbelegClosingAsync_DuplicateYear_ReturnsFailure()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureDefaultTenant(ctx);

        var regId = Guid.NewGuid();
        var (year, month) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();

        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = tenantId,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        ctx.Jahresbelege.Add(new Jahresbeleg
        {
            TenantId = tenantId,
            CashRegisterId = regId,
            Year = year,
            TotalGross = 1m,
            Environment = "Demo",
            CreatedByUserId = "u1",
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.CreateJahresbelegClosingAsync(
            "manager-1",
            new CreateJahresbelegClosingRequest { CashRegisterId = regId, Year = year });

        Assert.False(result.Success);
        Assert.Contains("already exists", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreateJahresbelegClosingAsync_MissingMonths_ReturnsFailure()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureDefaultTenant(ctx);

        var regId = Guid.NewGuid();
        var (year, month) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();

        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = tenantId,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        // Only seed month 1 when current month > 1
        if (month > 1)
        {
            ctx.Monatsbelege.Add(new Monatsbeleg
            {
                TenantId = tenantId,
                CashRegisterId = regId,
                Year = year,
                Month = 1,
                TotalGross = 100m,
                TotalTax = 20m,
                TransactionCount = 2,
                Environment = "Demo",
                CreatedByUserId = "u1",
            });
        }

        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.CreateJahresbelegClosingAsync(
            "manager-1",
            new CreateJahresbelegClosingRequest { CashRegisterId = regId, Year = year });

        Assert.False(result.Success);
        Assert.Contains("Missing Monatsbeleg", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildReportDtoAsync_UsesDemoFooter_InDevelopment()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureDefaultTenant(ctx);

        var regId = Guid.NewGuid();
        var entity = new Jahresbeleg
        {
            TenantId = tenantId,
            CashRegisterId = regId,
            Year = 2025,
            TotalCash = 100m,
            TotalCard = 200m,
            TotalGross = 300m,
            TotalTax = 50m,
            TransactionCount = 20,
            MonthlyReferences = "[{\"year\":2025,\"month\":12,\"id\":\"00000000-0000-0000-0000-000000000001\"}]",
            TseSignature = "sig",
            IsSimulated = true,
            Environment = "Demo",
            CreatedByUserId = "u1",
        };
        ctx.Jahresbelege.Add(entity);
        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = tenantId,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });
        await ctx.SaveChangesAsync();

        var devEnv = Mock.Of<IHostEnvironment>(h => h.EnvironmentName == Environments.Development);
        var svc = CreateService(ctx, hostEnvironment: devEnv);
        var report = await svc.BuildReportDtoAsync(entity.Id);

        Assert.NotNull(report);
        Assert.Equal("Yearly", report!.ClosingType);
        Assert.True(report.IsDemoFiscal);
        Assert.Contains("DEMO", report.RksvFooterLabel, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("NON_FISCAL_DEMO", report.QrPayload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateYearlySummaryPreviewAsync_FlagsDecemberMonatsbeleg()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureDefaultTenant(ctx);

        var regId = Guid.NewGuid();
        const int year = 2024;

        ctx.CashRegisters.Add(new CashRegister
        {
            TenantId = tenantId,
            Id = regId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 0,
            CurrentBalance = 0,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Open,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
        });

        ctx.CompanySettings.Add(new CompanySettings
        {
            TenantId = tenantId,
            CompanyName = "Test GmbH",
            CompanyAddress = "Wien",
            CompanyTaxNumber = "ATU12345678",
            BusinessHours = new Dictionary<string, string>(),
            UseDecemberMonatsbelegAsJahresbeleg = true,
        });

        for (var m = 1; m <= 12; m++)
        {
            ctx.Monatsbelege.Add(new Monatsbeleg
            {
                TenantId = tenantId,
                CashRegisterId = regId,
                Year = year,
                Month = m,
                TotalGross = 10m * m,
                TotalTax = 2m,
                TransactionCount = 1,
                Environment = "Demo",
                CreatedByUserId = "u1",
            });
        }

        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var preview = await svc.GenerateYearlySummaryPreviewAsync(tenantId, regId, year);

        Assert.Equal(12, preview.MonatsbelegCount);
        Assert.True(preview.IsDecemberMonatsbeleg);
        Assert.Empty(preview.MissingMonths);
        Assert.Equal(780m, preview.TotalGross);
    }

    private static void SeedMonatsbelegMonths(
        AppDbContext ctx,
        Guid tenantId,
        Guid regId,
        int year,
        int monthsThrough,
        decimal grossPerMonth)
    {
        for (var m = 1; m <= monthsThrough; m++)
        {
            ctx.Monatsbelege.Add(new Monatsbeleg
            {
                TenantId = tenantId,
                CashRegisterId = regId,
                Year = year,
                Month = m,
                TotalGross = grossPerMonth,
                TotalTax = grossPerMonth * 0.2m,
                TransactionCount = 2,
                Environment = "Demo",
                CreatedByUserId = "u1",
            });
        }
    }
}
