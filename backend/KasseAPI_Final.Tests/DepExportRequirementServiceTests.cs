using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DepExportRequirementServiceTests
{
    private static readonly Guid TenantId = SystemTenantIds.Platform;

    private static AppDbContext CreateDb(Guid? tenantId = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DepExportReq_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, new FixedTenantAccessor(tenantId ?? TenantId));
    }

    private sealed class FixedTenantAccessor(Guid tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
        public string? TenantSlug { get; set; }
    }

    private sealed class FixedTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    private static DepExportRequirementService CreateSut(AppDbContext db, DateTime utcNow) =>
        new(db, new FixedTimeProvider(utcNow));

    [Fact]
    public async Task GetRequirementsAsync_IncludesLegalYearlyRequirement()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);

        var sut = CreateSut(db, new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc));
        var requirements = await sut.GetRequirementsAsync(TenantId);

        var yearly = Assert.Single(requirements, r => r.Category == DepExportRequirementCategories.Yearly);
        Assert.Equal(DepExportRequirementTypes.Legal, yearly.RequirementType);
        Assert.Equal(2025, yearly.PeriodStart!.Value.Year);
        Assert.Equal(new DateTime(2026, 1, 31, 0, 0, 0, DateTimeKind.Utc), yearly.DueDate);
        Assert.False(yearly.IsCompleted);
        Assert.Equal(5, yearly.Priority);
    }

    [Fact]
    public async Task GetRequirementsAsync_MarksYearlyCompleted_WhenHistoryCoversYear()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);

        db.DepExportHistories.Add(new DepExportHistory
        {
            TenantId = TenantId,
            CashRegisterId = Guid.NewGuid(),
            FromUtc = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            ExportedAt = new DateTime(2026, 1, 10, 0, 0, 0, DateTimeKind.Utc),
            ExportedByUserId = "user-1",
            FileName = "dep-export.json",
            FileSizeBytes = 10,
            SignatureCount = 1,
            GroupCount = 1,
            Status = DepExportStatus.Completed.ToString(),
        });
        await db.SaveChangesAsync();

        var sut = CreateSut(db, new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc));
        var requirements = await sut.GetRequirementsAsync(TenantId);

        var yearly = Assert.Single(requirements, r => r.Category == DepExportRequirementCategories.Yearly);
        Assert.True(yearly.IsCompleted);

        var status = await sut.GetComplianceStatusAsync(TenantId);
        Assert.True(status.IsCompliant);
        Assert.Equal(0, status.LegalIncompleteCount);
    }

    [Fact]
    public async Task GetRequirementsAsync_AddsUrgent_Within30DaysOfDeadline()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);

        var sut = CreateSut(db, new DateTime(2026, 1, 15, 12, 0, 0, DateTimeKind.Utc));
        var requirements = await sut.GetRequirementsAsync(TenantId);

        Assert.Contains(requirements, r => r.Category == DepExportRequirementCategories.Urgent);
        Assert.Contains(requirements, r => r.Category == DepExportRequirementCategories.Yearly);
    }

    [Fact]
    public async Task GetNextRequirementAsync_ReturnsHighestPriorityIncomplete()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);

        var sut = CreateSut(db, new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc));
        var next = await sut.GetNextRequirementAsync(TenantId);

        Assert.NotNull(next);
        Assert.Equal(DepExportRequirementCategories.Yearly, next!.Category);
        Assert.Equal(5, next.Priority);
    }

    [Fact]
    public async Task EnsurePeriodsAsync_CreatesYearlyAndQuarterlyRows()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);

        var sut = CreateSut(db, new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc));
        await sut.EnsurePeriodsAsync(TenantId);

        var periods = await db.DepExportCompliancePeriods.Where(p => p.TenantId == TenantId).ToListAsync();
        Assert.Contains(periods, p => p.PeriodType == DepExportPeriodTypes.Yearly);
        Assert.Contains(periods, p => p.PeriodType == DepExportPeriodTypes.Quarterly);

        var yearly = Assert.Single(periods, p => p.PeriodType == DepExportPeriodTypes.Yearly);
        Assert.Equal(DepExportPeriodStatuses.Overdue, yearly.Status);

        var quarterly = Assert.Single(periods, p => p.PeriodType == DepExportPeriodTypes.Quarterly);
        Assert.Equal(DepExportPeriodStatuses.Pending, quarterly.Status);
    }

    [Fact]
    public async Task TryCompletePeriodsForExportAsync_MarksCoveredPeriodCompleted()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);

        var sut = CreateSut(db, new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc));
        await sut.EnsurePeriodsAsync(TenantId);

        await sut.TryCompletePeriodsForExportAsync(
            TenantId,
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            "user-1",
            "dep.json",
            "abc123",
            Guid.NewGuid());

        var yearly = await db.DepExportCompliancePeriods.SingleAsync(p =>
            p.PeriodType == DepExportPeriodTypes.Yearly);
        Assert.Equal(DepExportPeriodStatuses.Completed, yearly.Status);
        Assert.Equal("dep.json", yearly.FileName);
        Assert.Equal("abc123", yearly.FileHash);
        Assert.Equal("user-1", yearly.ExportedBy);
    }

    [Fact]
    public async Task GetCurrentPeriodAsync_PrefersYearlyOverQuarterly()
    {
        await using var db = CreateDb();
        TenantTestDoubles.EnsurePlatformTenant(db);

        var sut = CreateSut(db, new DateTime(2026, 7, 15, 12, 0, 0, DateTimeKind.Utc));
        var current = await sut.GetCurrentPeriodAsync(TenantId);

        Assert.NotNull(current);
        Assert.Equal(DepExportPeriodTypes.Yearly, current!.PeriodType);
    }

    [Fact]
    public void ExportCoversPeriod_RequiresFullCoverage()
    {
        var start = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = new DateTime(2025, 12, 31, 0, 0, 0, DateTimeKind.Utc);

        Assert.True(DepExportRequirementService.ExportCoversPeriod(
            start, end, start, end));
        Assert.False(DepExportRequirementService.ExportCoversPeriod(
            start.AddDays(1), end, start, end));
        Assert.False(DepExportRequirementService.ExportCoversPeriod(
            start, end.AddDays(-1), start, end));
    }
}
