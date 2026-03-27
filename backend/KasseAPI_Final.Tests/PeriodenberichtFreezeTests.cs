using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public class PeriodenberichtFreezeTests
{
    [Fact]
    public async Task FreezePeriodicAsync_PersistsFrozenRun_AndIsRetrievable()
    {
        await using var db = CreateDb();
        var audit = new Mock<IAuditLogService>();
        audit.Setup(x => x.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        var service = new OperationalReportingService(
            db,
            new Mock<ILogger<OperationalReportingService>>().Object,
            audit.Object);

        var frozen = await service.FreezePeriodicAsync(new FreezePeriodenberichtRequest
        {
            PeriodPreset = "custom",
            StartDate = new DateTime(2026, 3, 1),
            EndDate = new DateTime(2026, 3, 31),
            ActiveOnly = true,
            ExportProfileKey = "operationalPreview"
        }, "actor");

        Assert.NotEqual(Guid.Empty, frozen.Id);
        Assert.Equal("custom", frozen.PeriodPreset);
        Assert.Equal("operationalPreview", frozen.ExportProfileKey);
        Assert.False(string.IsNullOrWhiteSpace(frozen.QueryParametersHash));
        Assert.False(string.IsNullOrWhiteSpace(frozen.SnapshotHash));

        var reloaded = await service.GetFrozenPeriodenberichtByIdAsync(frozen.Id);
        Assert.NotNull(reloaded);
        Assert.Equal(frozen.Id, reloaded!.Id);
        Assert.Equal(frozen.SnapshotHash, reloaded.SnapshotHash);
    }

    [Fact]
    public async Task FreezePeriodicAsync_SameParametersMultipleTimes_CreatesDistinctRuns()
    {
        await using var db = CreateDb();
        var audit = new Mock<IAuditLogService>();
        audit.Setup(x => x.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>()))
            .ReturnsAsync(new AuditLog());

        var service = new OperationalReportingService(
            db,
            new Mock<ILogger<OperationalReportingService>>().Object,
            audit.Object);

        var req = new FreezePeriodenberichtRequest
        {
            PeriodPreset = "month",
            ActiveOnly = true,
        };

        var a = await service.FreezePeriodicAsync(req, "actor");
        var b = await service.FreezePeriodicAsync(req, "actor");

        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(a.QueryParametersHash, b.QueryParametersHash);
        var list = await service.ListFrozenPeriodenberichteAsync(null, null, null, 50);
        Assert.True(list.Count >= 2);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new AppDbContext(options);
    }
}
