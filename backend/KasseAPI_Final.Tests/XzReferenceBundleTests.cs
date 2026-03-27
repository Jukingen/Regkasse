using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services;
using KasseAPI_Final.Time;
using Xunit;

namespace KasseAPI_Final.Tests;

public class XzReferenceBundleTests
{
    [Fact]
    public async Task GetXzReferenceBundleAsync_ReturnsSchema_AndSections()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

        var audit = new Mock<IAuditLogService>();
        var service = new OperationalReportingService(
            db,
            new Mock<ILogger<OperationalReportingService>>().Object,
            audit.Object);

        var bundle = await service.GetXzReferenceBundleAsync(
            businessDate: new DateTime(2024, 6, 15),
            cashRegisterId: null,
            cashierId: null,
            paymentMethod: null,
            activeOnly: true);

        Assert.Equal("1.0", bundle.SchemaVersion);
        Assert.False(bundle.IsCurrentBusinessDay);
        Assert.Null(bundle.InterimXLike);
        Assert.NotNull(bundle.FullDayOperationalSummary);
        Assert.NotNull(bundle.ClosingReference);
        Assert.Contains(bundle.Parts, p => p.Kind == "full_day_operational");
        Assert.Contains(bundle.Parts, p => p.Kind == "closing_z_reference");
        Assert.NotEmpty(bundle.LegalDisclaimers);
    }

    [Fact]
    public async Task GetXzReferenceBundleAsync_Today_IncludesInterimPart()
    {
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

        var audit = new Mock<IAuditLogService>();
        var service = new OperationalReportingService(
            db,
            new Mock<ILogger<OperationalReportingService>>().Object,
            audit.Object);

        var today = PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();
        var bundle = await service.GetXzReferenceBundleAsync(
            businessDate: today,
            cashRegisterId: null,
            cashierId: null,
            paymentMethod: null,
            activeOnly: true);

        Assert.True(bundle.IsCurrentBusinessDay);
        Assert.NotNull(bundle.InterimXLike);
        Assert.Contains(bundle.Parts, p => p.Kind == "interim_x_like");
        Assert.NotNull(bundle.InterimVsFullDaySnapshot);
    }
}
