using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DepExportStatisticsServiceTests
{
    private static readonly Guid TenantId = SystemTenantIds.Platform;

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DepExportStats_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, new FixedTenantAccessor(TenantId));
    }

    private sealed class FixedTenantAccessor(Guid tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
        public string? TenantSlug { get; set; }
    }

    private static DepExportStatisticsService CreateSut(
        AppDbContext db,
        DateTime utcNow,
        IDepExportRequirementService? requirements = null)
    {
        var time = new FakeTimeProvider(utcNow);
        requirements ??= Mock.Of<IDepExportRequirementService>(r =>
            r.GetNextRequirementAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()) ==
            Task.FromResult<DepExportRequirement?>(null));
        return new DepExportStatisticsService(db, requirements, time);
    }

    private sealed class FakeTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));
    }

    [Fact]
    public void ClassifyPeriodType_UsesWindowLength()
    {
        Assert.Equal("YearlyWindow", DepExportStatisticsService.ClassifyPeriodType(
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc)));
        Assert.Equal("MonthlyWindow", DepExportStatisticsService.ClassifyPeriodType(
            new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 3, 31, 0, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public async Task GetStatisticsAsync_ComputesSuccessRateAndStorage()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        db.DepExportHistories.AddRange(
            new DepExportHistory
            {
                TenantId = TenantId,
                CashRegisterId = Guid.NewGuid(),
                FromUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ToUtc = new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc),
                ExportedAt = new DateTime(2026, 2, 1, 10, 0, 0, DateTimeKind.Utc),
                ExportedByUserId = "u",
                FileName = "a.json",
                FileSizeBytes = 2 * 1024 * 1024,
                SignatureCount = 1,
                GroupCount = 1,
                Status = DepExportStatus.Completed.ToString(),
            },
            new DepExportHistory
            {
                TenantId = TenantId,
                CashRegisterId = Guid.NewGuid(),
                FromUtc = new DateTime(2026, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                ToUtc = new DateTime(2026, 2, 28, 0, 0, 0, DateTimeKind.Utc),
                ExportedAt = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc),
                ExportedByUserId = "u",
                FileName = "b.json",
                FileSizeBytes = 0,
                SignatureCount = 0,
                GroupCount = 0,
                Status = DepExportStatus.Failed.ToString(),
                ScheduleId = Guid.NewGuid(),
            });
        await db.SaveChangesAsync();

        var sut = CreateSut(db, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        var stats = await sut.GetStatisticsAsync(
            TenantId,
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(2, stats.TotalExports);
        Assert.Equal(1, stats.SuccessfulExports);
        Assert.Equal(1, stats.FailedExports);
        Assert.Equal(50, stats.SuccessRate);
        Assert.Equal(1, stats.ExportsByType["Manual"]);
        Assert.Equal(1, stats.ExportsByType["Scheduled"]);
        Assert.Equal(2, stats.TotalStorageUsedMb);
        Assert.NotNull(stats.LastExportDate);
    }

    [Fact]
    public async Task GetTrendAsync_FillsMonthlyBuckets()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        db.DepExportHistories.Add(new DepExportHistory
        {
            TenantId = TenantId,
            CashRegisterId = Guid.NewGuid(),
            FromUtc = new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2026, 5, 31, 0, 0, 0, DateTimeKind.Utc),
            ExportedAt = new DateTime(2026, 6, 15, 0, 0, 0, DateTimeKind.Utc),
            ExportedByUserId = "u",
            FileName = "c.json",
            FileSizeBytes = 100,
            SignatureCount = 1,
            GroupCount = 1,
            Status = DepExportStatus.Completed.ToString(),
        });
        await db.SaveChangesAsync();

        var sut = CreateSut(db, new DateTime(2026, 7, 10, 0, 0, 0, DateTimeKind.Utc));
        var trend = await sut.GetTrendAsync(TenantId, months: 3);

        Assert.Equal(3, trend.Count);
        Assert.Equal("2026-05", trend[0].Label);
        Assert.Equal("2026-07", trend[2].Label);
        Assert.Equal(1, trend.Single(p => p.Label == "2026-06").SuccessfulExports);
    }

    [Fact]
    public async Task GetForecastAsync_ProjectsThreeMonths()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);
        var requirements = new Mock<IDepExportRequirementService>();
        requirements
            .Setup(r => r.GetNextRequirementAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new DepExportRequirement
            {
                TenantId = TenantId,
                Title = "Yearly 2025",
                DueDate = new DateTime(2026, 8, 15, 0, 0, 0, DateTimeKind.Utc),
            });

        var sut = CreateSut(db, new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), requirements.Object);
        var forecast = await sut.GetForecastAsync(TenantId);

        Assert.Equal(3, forecast.Points.Count);
        Assert.Equal("Yearly 2025", forecast.NextRequirementTitle);
        Assert.Contains(forecast.Points, p => p.Label == "2026-08" && p.HasKnownDueDate);
    }
}
