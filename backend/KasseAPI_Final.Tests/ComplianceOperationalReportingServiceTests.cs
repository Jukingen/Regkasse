using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ComplianceOperationalReportingServiceTests
{
    [Fact]
    public async Task GetDailyReconciliationAsync_ComputesExpectedCashFromOpeningAndSales()
    {
        var tenantId = Guid.NewGuid();
        var registerId = Guid.NewGuid();
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant { Id = tenantId, Slug = "test", Name = "Test", Status = TenantStatuses.Active });
        db.CashRegisters.Add(new CashRegister
        {
            Id = registerId,
            TenantId = tenantId,
            RegisterNumber = "K1",
            Location = "L",
            StartingBalance = 50m,
            CurrentBalance = 0m,
            LastBalanceUpdate = DateTime.UtcNow,
            Status = RegisterStatus.Closed,
        });
        await db.SaveChangesAsync();

        var day = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var dailyClosing = new Mock<IDailyClosingService>();
        dailyClosing
            .Setup(s => s.GenerateClosingSummaryAsync(tenantId, registerId, day, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DailyClosingSummaryDto
            {
                BusinessDate = day,
                CashRegisterId = registerId,
                TotalCash = 120m,
                TotalCard = 80m,
            });

        var tenantResolver = new Mock<ISettingsTenantResolver>();
        tenantResolver
            .Setup(r => r.ResolveEffectiveTenantIdAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(tenantId);

        var service = CreateService(db, dailyClosing.Object, tenantResolver.Object);
        var report = await service.GetDailyReconciliationAsync(day, registerId);

        Assert.Equal(50m + 120m, report.ExpectedCash);
        Assert.Equal(120m, report.CashTotal);
        Assert.False(report.IsReconciled);
        Assert.NotEmpty(report.DisclaimerDe);
    }

    [Fact]
    public async Task GetPeakHourHeatmapAsync_Returns7x24Grid()
    {
        await using var db = CreateDb();
        var tenantResolver = Mock.Of<ISettingsTenantResolver>();
        var service = CreateService(db, Mock.Of<IDailyClosingService>(), tenantResolver);

        var report = await service.GetPeakHourHeatmapAsync(
            DateTime.UtcNow.AddDays(-7),
            DateTime.UtcNow,
            null);

        Assert.Equal(7, report.Cells.Length);
        Assert.All(report.Cells, row => Assert.Equal(24, row.Length));
    }

    private static ComplianceOperationalReportingService CreateService(
        AppDbContext db,
        IDailyClosingService dailyClosing,
        ISettingsTenantResolver tenantResolver) =>
        new(
            db,
            dailyClosing,
            tenantResolver,
            new PeakHoursAnalysisService(db),
            new ProductMovementAnalysisService(db, tenantResolver));

    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);
}
