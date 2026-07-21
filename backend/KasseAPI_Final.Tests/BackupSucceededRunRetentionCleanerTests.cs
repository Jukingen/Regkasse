using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupSucceededRunRetentionCleanerTests
{
    private static AppDbContext CreateDb(string name)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(name)
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public async Task Flat_mode_deletes_tenant_runs_past_cutoff()
    {
        await using var db = CreateDb(nameof(Flat_mode_deletes_tenant_runs_past_cutoff));
        var oldId = Guid.NewGuid();
        var recentId = Guid.NewGuid();
        db.BackupRuns.AddRange(
            new BackupRun
            {
                Id = oldId,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                Strategy = BackupStrategyKind.Tenant,
                TenantId = Guid.NewGuid(),
                RequestedAt = DateTime.UtcNow.AddDays(-40),
                CompletedAt = DateTime.UtcNow.AddDays(-40),
            },
            new BackupRun
            {
                Id = recentId,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                Strategy = BackupStrategyKind.Tenant,
                TenantId = Guid.NewGuid(),
                RequestedAt = DateTime.UtcNow.AddDays(-5),
                CompletedAt = DateTime.UtcNow.AddDays(-5),
            });
        await db.SaveChangesAsync();

        var removed = await BackupSucceededRunRetentionCleaner.DeleteExpiredSucceededRunsAsync(
            db,
            new BackupOptions { SmartRetentionEnabled = false },
            new TestHostEnvironment(),
            NullLogger.Instance,
            tenantRetentionDays: 30,
            systemRetentionDays: 90);

        Assert.Equal(1, removed);
        await db.SaveChangesAsync();
        Assert.Null(await db.BackupRuns.FindAsync(oldId));
        Assert.NotNull(await db.BackupRuns.FindAsync(recentId));
    }

    [Fact]
    public async Task Smart_mode_keeps_daily_and_deletes_ancient()
    {
        await using var db = CreateDb(nameof(Smart_mode_keeps_daily_and_deletes_ancient));
        var dailyId = Guid.NewGuid();
        var ancientId = Guid.NewGuid();
        db.BackupRuns.AddRange(
            new BackupRun
            {
                Id = dailyId,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Scheduled,
                AdapterKind = "Fake",
                Strategy = BackupStrategyKind.System,
                RequestedAt = DateTime.UtcNow.AddDays(-2),
                CompletedAt = DateTime.UtcNow.AddDays(-2),
            },
            new BackupRun
            {
                Id = ancientId,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Scheduled,
                AdapterKind = "Fake",
                Strategy = BackupStrategyKind.System,
                RequestedAt = DateTime.UtcNow.AddDays(-(SmartRetentionService.YearlyRetentionYears * 365 + 30)),
                CompletedAt = DateTime.UtcNow.AddDays(-(SmartRetentionService.YearlyRetentionYears * 365 + 30)),
            });
        await db.SaveChangesAsync();

        var removed = await BackupSucceededRunRetentionCleaner.DeleteExpiredSucceededRunsAsync(
            db,
            new BackupOptions { SmartRetentionEnabled = true },
            new TestHostEnvironment(),
            NullLogger.Instance,
            tenantRetentionDays: 30,
            systemRetentionDays: 90,
            smartRetention: new SmartRetentionService());

        Assert.Equal(1, removed);
        await db.SaveChangesAsync();
        Assert.NotNull(await db.BackupRuns.FindAsync(dailyId));
        Assert.Null(await db.BackupRuns.FindAsync(ancientId));
    }

    [Fact]
    public async Task Smart_mode_does_not_thin_system_against_tenant_in_same_week()
    {
        await using var db = CreateDb(nameof(Smart_mode_does_not_thin_system_against_tenant_in_same_week));
        var tenantRunId = Guid.NewGuid();
        var systemRunId = Guid.NewGuid();
        // Both outside the daily window, same ISO week — pooled GFS would keep only the newer run.
        var older = DateTime.UtcNow.AddDays(-10);
        var newer = DateTime.UtcNow.AddDays(-9);
        db.BackupRuns.AddRange(
            new BackupRun
            {
                Id = tenantRunId,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                Strategy = BackupStrategyKind.Tenant,
                TenantId = Guid.NewGuid(),
                RequestedAt = older,
                CompletedAt = older,
            },
            new BackupRun
            {
                Id = systemRunId,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Scheduled,
                AdapterKind = "Fake",
                Strategy = BackupStrategyKind.System,
                RequestedAt = newer,
                CompletedAt = newer,
            });
        await db.SaveChangesAsync();

        var removed = await BackupSucceededRunRetentionCleaner.DeleteExpiredSucceededRunsAsync(
            db,
            new BackupOptions { SmartRetentionEnabled = true },
            new TestHostEnvironment(),
            NullLogger.Instance,
            tenantRetentionDays: 30,
            systemRetentionDays: 90,
            smartRetention: new SmartRetentionService());

        Assert.Equal(0, removed);
        await db.SaveChangesAsync();
        Assert.NotNull(await db.BackupRuns.FindAsync(tenantRunId));
        Assert.NotNull(await db.BackupRuns.FindAsync(systemRunId));
    }
}
