using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupRunHistoryFilterTests
{
    private static AppDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"backup_history_filter_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options);

    [Fact]
    public async Task GetHistoryAsync_Filters_By_Strategy_CreatedBy_And_Date()
    {
        var now = DateTime.UtcNow;
        await using var db = CreateDb();
        db.BackupRuns.AddRange(
            new BackupRun
            {
                Id = Guid.NewGuid(),
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Scheduled,
                AdapterKind = "Fake",
                Strategy = BackupStrategyKind.System,
                RequestedAt = now.AddDays(-2)
            },
            new BackupRun
            {
                Id = Guid.NewGuid(),
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                Strategy = BackupStrategyKind.Tenant,
                RequestedByUserId = "manager-1",
                RequestedAt = now.AddHours(-1)
            },
            new BackupRun
            {
                Id = Guid.NewGuid(),
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                Strategy = BackupStrategyKind.Tenant,
                RequestedByUserId = "manager-2",
                RequestedAt = now.AddDays(-10)
            });
        await db.SaveChangesAsync();

        var svc = new BackupRunQueryService(db);

        var cron = await svc.GetHistoryAsync(1, 20, null, new BackupRunHistoryFilter { CreatedBy = "system" });
        Assert.Equal(1, cron.TotalCount);
        Assert.Equal(BackupTriggerSource.Scheduled, cron.Items[0].TriggerSource);

        var byUser = await svc.GetHistoryAsync(1, 20, null, new BackupRunHistoryFilter { CreatedBy = "manager-1" });
        Assert.Equal(1, byUser.TotalCount);
        Assert.Equal("manager-1", byUser.Items[0].RequestedByUserId);

        var byType = await svc.GetHistoryAsync(
            1, 20, null, new BackupRunHistoryFilter { Strategy = BackupStrategyKind.Tenant });
        Assert.Equal(2, byType.TotalCount);

        var byDate = await svc.GetHistoryAsync(
            1, 20, null, new BackupRunHistoryFilter { FromUtc = now.AddDays(-3), ToUtc = now });
        Assert.Equal(2, byDate.TotalCount);
    }

    [Fact]
    public void ActorLabel_Scheduled_Is_SystemCron()
    {
        Assert.Equal(
            BackupRunActorLabels.SystemCron,
            BackupRunActorLabels.Resolve(BackupTriggerSource.Scheduled, "Ada", null));
        Assert.Equal(
            "Ada Lovelace",
            BackupRunActorLabels.Resolve(BackupTriggerSource.Manual, "Ada Lovelace", "u1"));
    }

    [Fact]
    public void OperatorDownloadFileName_Uses_Type_Tenant_Date()
    {
        var name = BackupArtifactFileNameBuilder.BuildOperatorDownloadFileName(
            BackupStrategyKind.Tenant,
            "Cafe Wien",
            new DateTime(2026, 9, 6, 15, 0, 0, DateTimeKind.Utc),
            "zip");
        Assert.Equal("backup_tenant_cafe_wien_20260906.zip", name);

        var system = BackupArtifactFileNameBuilder.BuildOperatorDownloadFileName(
            BackupStrategyKind.System,
            "ignored",
            new DateTime(2026, 9, 6, 15, 0, 0, DateTimeKind.Utc),
            "dump");
        Assert.Equal("backup_system_system_20260906.dump", system);
    }
}
