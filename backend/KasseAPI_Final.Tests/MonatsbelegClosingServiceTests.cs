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

public sealed class MonatsbelegClosingServiceTests
{
    private static AppDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"MonatsbelegClosing_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static MonatsbelegClosingService CreateService(
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

        return new MonatsbelegClosingService(
            ctx,
            DailyClosingTestDoubles.Create(ctx),
            tse,
            Mock.Of<ITseKeyProvider>(p => p.GetCurrentCertificateThumbprint() == "thumb-test"),
            TenantTestDoubles.PrimaryTenantResolver,
            hostEnvironment,
            Options.Create(tseOptions),
            configuration,
            new RksvEnvironmentService(configuration, hostEnvironment),
            CreateMonatsbelegReportService(),
            Mock.Of<ILogger<MonatsbelegClosingService>>(),
            Mock.Of<IReportPdfCaptureService>());
    }

    private static IMonatsbelegReportService CreateMonatsbelegReportService()
    {
        var enricher = CreateReportEnricherMock();
        return new MonatsbelegReportService(enricher, new RksvReportTextService(enricher));
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
        mock.Setup(x => x.CreateMonthlyClosingSignatureAsync(
                It.IsAny<Guid>(),
                It.IsAny<string>(),
                It.IsAny<DateTime>(),
                It.IsAny<decimal>(),
                It.IsAny<int>(),
                It.IsAny<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction?>()))
            .ReturnsAsync("eyJhbGciOiJFUzI1NiJ9.eyJ.test.monats.closing");
        return mock;
    }

    [Fact]
    public async Task CreateMonatsbelegClosingAsync_PersistsAggregatedTotals_AndChainFields()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureDefaultTenant(ctx);

        var regId = Guid.NewGuid();
        var (year, month) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
        var day = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(year, month, 5);
        var dayPersist = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(day);

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

        ctx.DailyClosings.Add(new DailyClosing
        {
            TenantId = tenantId,
            CashRegisterId = regId,
            UserId = "u1",
            ClosingDate = dayPersist,
            ClosingType = "Daily",
            TotalAmount = 100m,
            TotalTaxAmount = 20m,
            TransactionCount = 4,
            TseSignature = "daily-sig",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
        });

        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.CreateMonatsbelegClosingAsync(
            "manager-1",
            new CreateMonatsbelegClosingRequest { CashRegisterId = regId, Year = year, Month = month });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.MonatsbelegId);
        Assert.Equal(100m, result.TotalGross);
        Assert.Equal(1, result.DailyClosingCount);
        Assert.False(string.IsNullOrEmpty(result.TseSignature));

        var row = await ctx.Set<Monatsbeleg>().SingleAsync();
        Assert.Equal(regId, row.CashRegisterId);
        Assert.Equal(year, row.Year);
        Assert.Equal(month, row.Month);
        Assert.Equal(100m, row.TotalGross);
        Assert.Equal(20m, row.TotalTax);
        Assert.Equal(4, row.TransactionCount);
        Assert.True(row.IsSimulated);
        Assert.Equal("Demo", row.Environment);
        Assert.Equal(1, row.SignatureChainLength);
    }

    [Fact]
    public async Task CreateMonatsbelegClosingAsync_LinksPreviousMonthSignature()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureDefaultTenant(ctx);

        var regId = Guid.NewGuid();
        var (year, month) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
        var prevMonth = month == 1 ? 12 : month - 1;
        var prevYear = month == 1 ? year - 1 : year;

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

        ctx.Set<Monatsbeleg>().Add(new Monatsbeleg
        {
            TenantId = tenantId,
            CashRegisterId = regId,
            Year = prevYear,
            Month = prevMonth,
            TotalGross = 50m,
            TotalTax = 10m,
            TransactionCount = 2,
            TseSignature = "prev-month-sig",
            Environment = "Demo",
            CreatedByUserId = "u1",
            CreatedAtUtc = DateTime.UtcNow.AddMonths(-1),
            UpdatedAtUtc = DateTime.UtcNow.AddMonths(-1),
        });

        SeedDailyClosing(ctx, tenantId, regId, year, month, 80m, 16m, 3);
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.CreateMonatsbelegClosingAsync(
            "manager-1",
            new CreateMonatsbelegClosingRequest { CashRegisterId = regId, Year = year, Month = month });

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("prev-month-sig", result.PreviousSignature);

        var row = await ctx.Set<Monatsbeleg>()
            .SingleAsync(m => m.Year == year && m.Month == month);
        Assert.Equal("prev-month-sig", row.PreviousSignature);
        Assert.Equal(2, row.SignatureChainLength);
    }

    [Fact]
    public async Task CreateMonatsbelegClosingAsync_DuplicateMonth_ReturnsFailure()
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

        ctx.Set<Monatsbeleg>().Add(new Monatsbeleg
        {
            TenantId = tenantId,
            CashRegisterId = regId,
            Year = year,
            Month = month,
            TotalGross = 1m,
            Environment = "Demo",
            CreatedByUserId = "u1",
        });
        await ctx.SaveChangesAsync();

        var svc = CreateService(ctx);
        var result = await svc.CreateMonatsbelegClosingAsync(
            "manager-1",
            new CreateMonatsbelegClosingRequest { CashRegisterId = regId, Year = year, Month = month });

        Assert.False(result.Success);
        Assert.Contains("already exists", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BuildReportDtoAsync_UsesDemoFooter_InDevelopment()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureDefaultTenant(ctx);

        var regId = Guid.NewGuid();
        var entity = new Monatsbeleg
        {
            TenantId = tenantId,
            CashRegisterId = regId,
            Year = 2026,
            Month = 6,
            TotalCash = 10m,
            TotalCard = 20m,
            TotalGross = 30m,
            TotalTax = 5m,
            TransactionCount = 2,
            TseSignature = "sig",
            IsSimulated = true,
            Environment = "Demo",
            CreatedByUserId = "u1",
        };
        ctx.Set<Monatsbeleg>().Add(entity);
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
        Assert.Equal("Monthly", report!.ClosingType);
        Assert.True(report.IsDemoFiscal);
        Assert.Contains("DEMO", report.RksvFooterLabel, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("NON_FISCAL_DEMO", report.QrPayload, StringComparison.Ordinal);
    }

    private static void SeedDailyClosing(
        AppDbContext ctx,
        Guid tenantId,
        Guid regId,
        int year,
        int month,
        decimal gross,
        decimal tax,
        int txCount)
    {
        var day = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(year, month, 3);
        ctx.DailyClosings.Add(new DailyClosing
        {
            TenantId = tenantId,
            CashRegisterId = regId,
            UserId = "u1",
            ClosingDate = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(day),
            ClosingType = "Daily",
            TotalAmount = gross,
            TotalTaxAmount = tax,
            TransactionCount = txCount,
            TseSignature = "daily",
            Status = "Completed",
            CreatedAt = DateTime.UtcNow,
        });
    }
}
