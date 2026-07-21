using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Rksv;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using KasseAPI_Final.Tse;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class MonatsbelegServiceTests
{
    private static AppDbContext CreateContext(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"MonatsbelegService_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(tenantId));
    }

    private static MonatsbelegService CreateService(
        AppDbContext ctx,
        ITseService? tse = null,
        IHostEnvironment? hostEnvironment = null)
    {
        tse ??= CreateTseMock().Object;
        hostEnvironment ??= TenantTestDoubles.ProductionHostEnvironment;
        var configuration = new ConfigurationBuilder().Build();

        return new MonatsbelegService(
            ctx,
            DailyClosingTestDoubles.Create(ctx),
            tse,
            Mock.Of<ITseKeyProvider>(p => p.GetCurrentCertificateThumbprint() == "thumb-test"),
            new RksvEnvironmentService(configuration, hostEnvironment),
            Mock.Of<ICurrentUserService>(u => u.GetCurrentUserId() == Guid.NewGuid()),
            Mock.Of<ILogger<MonatsbelegService>>());
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
            .ReturnsAsync("eyJhbGciOiJFUzI1NiJ9.eyJ.test.monats.rksv");
        return mock;
    }

    [Fact]
    public async Task CreateMonatsbelegAsync_PersistsAggregatedTotals_AndSignatureChain()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureDefaultTenant(ctx);

        var regId = Guid.NewGuid();
        var (currentYear, currentMonth) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
        var year = currentMonth == 1 ? currentYear - 1 : currentYear;
        var month = currentMonth == 1 ? 12 : currentMonth - 1;
        var day = PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(year, month, 5);
        var dayPersist = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(day);

        ctx.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Status = RegisterStatus.Open,
        });
        ctx.DailyClosings.Add(new DailyClosing
        {
            CashRegisterId = regId,
            ClosingDate = dayPersist,
            ClosingType = "Daily",
            TotalAmount = 120m,
            TotalTaxAmount = 20m,
            TransactionCount = 3,
            Status = "Completed",
        });
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var result = await service.CreateMonatsbelegAsync(regId, year, month);

        Assert.True(result.TotalGross > 0);
        Assert.Equal(3, result.TransactionCount);
        Assert.Equal(1, result.DailyClosingCount);
        Assert.False(string.IsNullOrEmpty(result.TseSignature));
        Assert.Equal(1, result.SignatureChainLength);

        var stored = await ctx.Monatsbelege.SingleAsync();
        Assert.Equal(result.Id, stored.Id);
        Assert.Equal("Monthly", (await ctx.DailyClosings.SingleAsync(d => d.ClosingType == "Monthly")).ClosingType);
    }

    [Fact]
    public async Task CreateMonatsbelegAsync_ThrowsWhenDuplicate()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureDefaultTenant(ctx);

        var regId = Guid.NewGuid();
        var (currentYear, currentMonth) = PostgreSqlUtcDateTime.GetViennaCurrentYearMonth();
        var year = currentMonth == 1 ? currentYear - 1 : currentYear;
        var month = currentMonth == 1 ? 12 : currentMonth - 1;
        var dayPersist = PostgreSqlUtcDateTime.ViennaCalendarAnchorToPersistUtc(
            PostgreSqlUtcDateTime.ViennaCalendarDateMidnightUnspecified(year, month, 1));

        ctx.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Status = RegisterStatus.Open,
        });
        ctx.DailyClosings.Add(new DailyClosing
        {
            CashRegisterId = regId,
            ClosingDate = dayPersist,
            ClosingType = "Daily",
            TotalAmount = 50m,
            TotalTaxAmount = 8m,
            TransactionCount = 1,
            Status = "Completed",
        });
        ctx.Monatsbelege.Add(new Monatsbeleg
        {
            CashRegisterId = regId,
            Year = year,
            Month = month,
            TotalGross = 50m,
            Environment = "Demo",
            CreatedByUserId = "test",
        });
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CreateMonatsbelegAsync(regId, year, month));
    }

    [Fact]
    public async Task GetMonatsbelegHistoryAsync_ReturnsOrderedSummaries()
    {
        var tenantId = LegacyDefaultTenantIds.Primary;
        await using var ctx = CreateContext(tenantId);
        TenantTestDoubles.EnsureDefaultTenant(ctx);

        var regId = Guid.NewGuid();
        ctx.CashRegisters.Add(new CashRegister
        {
            Id = regId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Status = RegisterStatus.Open,
        });
        ctx.Monatsbelege.AddRange(
            new Monatsbeleg
            {
                CashRegisterId = regId,
                Year = 2025,
                Month = 11,
                TotalGross = 100m,
                TotalTax = 16m,
                TransactionCount = 2,
                TseSignature = "sig-a",
                Environment = "Demo",
                CreatedByUserId = "test",
                CreatedAtUtc = new DateTime(2025, 12, 1, 0, 0, 0, DateTimeKind.Utc),
            },
            new Monatsbeleg
            {
                CashRegisterId = regId,
                Year = 2025,
                Month = 12,
                TotalGross = 200m,
                TotalTax = 32m,
                TransactionCount = 4,
                TseSignature = "sig-b",
                Environment = "Demo",
                CreatedByUserId = "test",
                CreatedAtUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            });
        await ctx.SaveChangesAsync();

        var service = CreateService(ctx);
        var history = await service.GetMonatsbelegHistoryAsync(regId, 2025);

        Assert.Equal(2, history.Count);
        Assert.Equal(12, history[0].Month);
        Assert.Equal(11, history[1].Month);
        Assert.True(history[0].HasSignature);
    }
}
