using KasseAPI_Final.Services.Backup;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class SmartRetentionServiceTests
{
    private readonly SmartRetentionService _sut = new();
    private static readonly DateTime Now = new(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(0, RetentionTier.Daily)]
    [InlineData(7, RetentionTier.Daily)]
    [InlineData(8, RetentionTier.Weekly)]
    [InlineData(28, RetentionTier.Weekly)]
    [InlineData(29, RetentionTier.Monthly)]
    [InlineData(360, RetentionTier.Monthly)]
    [InlineData(361, RetentionTier.Yearly)]
    [InlineData(2555, RetentionTier.Yearly)] // 7 * 365
    [InlineData(2556, RetentionTier.Delete)]
    public void CalculateRetentionPlan_classifies_by_age(int daysAgo, RetentionTier expected)
    {
        var backupDate = Now.AddDays(-daysAgo);
        var plan = _sut.CalculateRetentionPlan(backupDate, Now);
        Assert.Equal(expected, plan.Tier);
        Assert.Equal(expected == RetentionTier.Delete, plan.ShouldDelete);
    }

    [Fact]
    public async Task CalculateRetentionPlanAsync_matches_sync()
    {
        var backupDate = Now.AddDays(-10);
        var asyncPlan = await _sut.CalculateRetentionPlanAsync(backupDate);
        var syncPlan = _sut.CalculateRetentionPlan(backupDate);
        Assert.Equal(syncPlan.Tier, asyncPlan.Tier);
    }

    [Fact]
    public void SelectRunsToDelete_keeps_all_daily()
    {
        var candidates = Enumerable.Range(0, 5)
            .Select(i => new BackupRetentionCandidate(Guid.NewGuid(), Now.AddDays(-i)))
            .ToList();

        var delete = _sut.SelectRunsToDelete(candidates, Now);
        Assert.Empty(delete);
    }

    [Fact]
    public void SelectRunsToDelete_thins_weekly_to_newest_per_iso_week()
    {
        // Same ISO week, both inside weekly window (8–28 days).
        var olderInWeek = Now.AddDays(-15);
        var newerInWeek = Now.AddDays(-14);
        Assert.Equal(RetentionTier.Weekly, _sut.CalculateRetentionPlan(olderInWeek, Now).Tier);
        Assert.Equal(RetentionTier.Weekly, _sut.CalculateRetentionPlan(newerInWeek, Now).Tier);
        Assert.Equal(
            System.Globalization.ISOWeek.GetWeekOfYear(olderInWeek),
            System.Globalization.ISOWeek.GetWeekOfYear(newerInWeek));

        var keepId = Guid.NewGuid();
        var dropId = Guid.NewGuid();
        var candidates = new List<BackupRetentionCandidate>
        {
            new(dropId, olderInWeek),
            new(keepId, newerInWeek),
        };

        var delete = _sut.SelectRunsToDelete(candidates, Now);
        Assert.Contains(dropId, delete);
        Assert.DoesNotContain(keepId, delete);
    }

    [Fact]
    public void SelectRunsToDelete_thins_monthly_and_deletes_beyond_seven_years()
    {
        var monthlyKeep = Guid.NewGuid();
        var monthlyDrop = Guid.NewGuid();
        var ancient = Guid.NewGuid();

        var monthAnchor = Now.AddDays(-60); // monthly tier
        var candidates = new List<BackupRetentionCandidate>
        {
            new(monthlyDrop, monthAnchor.AddDays(-2)),
            new(monthlyKeep, monthAnchor),
            new(ancient, Now.AddDays(-(SmartRetentionService.YearlyRetentionYears * 365 + 10))),
        };

        var delete = _sut.SelectRunsToDelete(candidates, Now);
        Assert.Contains(monthlyDrop, delete);
        Assert.DoesNotContain(monthlyKeep, delete);
        Assert.Contains(ancient, delete);
    }

    [Fact]
    public void SelectRunsToDelete_keeps_one_yearly_per_calendar_year()
    {
        var year = Now.Year - 2;
        var older = new DateTime(year, 3, 1, 0, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(year, 11, 1, 0, 0, 0, DateTimeKind.Utc);
        // Ensure yearly tier ( > 360 days)
        Assert.True((Now.Date - newer.Date).Days > SmartRetentionService.MonthlyRetentionMonths * 30);

        var keepId = Guid.NewGuid();
        var dropId = Guid.NewGuid();
        var candidates = new List<BackupRetentionCandidate>
        {
            new(dropId, older),
            new(keepId, newer),
        };

        var delete = _sut.SelectRunsToDelete(candidates, Now);
        Assert.Contains(dropId, delete);
        Assert.DoesNotContain(keepId, delete);
    }
}
