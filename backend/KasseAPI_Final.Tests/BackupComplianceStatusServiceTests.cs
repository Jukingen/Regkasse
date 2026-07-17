using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupComplianceStatusServiceTests
{
    [Fact]
    public void EvaluateRestoreReadiness_requires_sha256()
    {
        var run = new BackupRun
        {
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.System,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow,
            Artifacts =
            {
                new BackupArtifact
                {
                    ArtifactType = BackupArtifactType.LogicalDump,
                    StorageDescriptor = "x.dump",
                    ContentHashSha256 = "abc"
                }
            }
        };

        var (ok, reason) = BackupComplianceStatusService.EvaluateRestoreReadiness(run);
        Assert.False(ok);
        Assert.Equal("missing_sha256", reason);
    }

    [Fact]
    public void EvaluateRestoreReadiness_ok_for_system_with_hash()
    {
        var run = new BackupRun
        {
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.System,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow,
            Artifacts =
            {
                new BackupArtifact
                {
                    ArtifactType = BackupArtifactType.LogicalDump,
                    StorageDescriptor = "ok.dump",
                    ContentHashSha256 = new string('a', 64)
                }
            }
        };

        var (ok, reason) = BackupComplianceStatusService.EvaluateRestoreReadiness(run);
        Assert.True(ok);
        Assert.Equal("system_dump_hash_present", reason);
    }

    [Fact]
    public async Task GetAsync_aggregates_succeeded_runs()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"bcs_{Guid.NewGuid():N}")
            .Options;
        await using var db = new AppDbContext(options);

        db.BackupRuns.Add(new BackupRun
        {
            Id = Guid.NewGuid(),
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.System,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow,
            Artifacts =
            {
                new BackupArtifact
                {
                    ArtifactType = BackupArtifactType.LogicalDump,
                    StorageDescriptor = "a.dump",
                    ContentHashSha256 = new string('b', 64)
                }
            }
        });
        db.BackupRuns.Add(new BackupRun
        {
            Id = Guid.NewGuid(),
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.System,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow,
            Artifacts =
            {
                new BackupArtifact
                {
                    ArtifactType = BackupArtifactType.LogicalDump,
                    StorageDescriptor = "b.dump",
                    ContentHashSha256 = null
                }
            }
        });
        await db.SaveChangesAsync();

        var sut = new BackupComplianceStatusService(db, TimeProvider.System);
        var result = await sut.GetAsync(new BackupRunAccessScope(true, null, "sa"));

        Assert.Equal(2, result.Total);
        Assert.Equal(1, result.Compliant);
        Assert.Equal(1, result.NonCompliant);
        Assert.False(result.AllCompliant);
    }
}
