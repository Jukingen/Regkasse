using KasseAPI_Final.Data;
using KasseAPI_Final.Services.Reports;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PeakHoursAnalysisServiceTests
{
    [Fact]
    public async Task GetPeakHoursAsync_Returns7x24WithStats()
    {
        await using var db = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options);

        var service = new PeakHoursAnalysisService(db);
        var report = await service.GetPeakHoursAsync(
            DateTime.UtcNow.AddDays(-7),
            DateTime.UtcNow,
            null);

        Assert.Equal(7, report.Heatmap.Length);
        Assert.All(report.Heatmap, row => Assert.Equal(24, row.Length));
        Assert.NotNull(report.BusiestHour);
        Assert.NotNull(report.QuietestHour);
        Assert.Equal(24, report.RecommendedStaffingLevels.Count);
    }
}
