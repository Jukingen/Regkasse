using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class StorageTierServiceTests
{
    private readonly StorageTierService _sut = new(NullLogger<StorageTierService>.Instance);
    private static readonly DateTime Now = new(2026, 7, 17, 12, 0, 0, DateTimeKind.Utc);

    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    [Theory]
    [InlineData(0, BackupStorageTier.Hot)]
    [InlineData(7, BackupStorageTier.Hot)]
    [InlineData(8, BackupStorageTier.Warm)]
    [InlineData(30, BackupStorageTier.Warm)]
    [InlineData(31, BackupStorageTier.Cold)]
    public void CalculateOptimalTier_classifies_by_age(int daysAgo, BackupStorageTier expected)
    {
        var tier = _sut.CalculateOptimalTier(Now.AddDays(-daysAgo), Now);
        Assert.Equal(expected, tier);
    }

    [Fact]
    public async Task MoveToOptimalTierAsync_persists_warm_on_artifacts()
    {
        await using var db = CreateDb(nameof(MoveToOptimalTierAsync_persists_warm_on_artifacts));
        var runId = Guid.NewGuid();
        var run = new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            Strategy = BackupStrategyKind.System,
            RequestedAt = Now.AddDays(-10),
            CompletedAt = Now.AddDays(-10),
        };
        db.BackupRuns.Add(run);
        db.BackupArtifacts.Add(new BackupArtifact
        {
            Id = Guid.NewGuid(),
            BackupRunId = runId,
            ArtifactType = BackupArtifactType.LogicalDump,
            StorageDescriptor = "dump.dump",
            LifecycleState = BackupArtifactLifecycleState.StagingVerified,
            StorageTier = BackupStorageTier.Hot,
            CreatedAt = Now.AddDays(-10),
        });
        await db.SaveChangesAsync();

        var result = await _sut.MoveToOptimalTierAsync(db, run, utcNow: Now);
        await db.SaveChangesAsync();

        Assert.Equal(BackupStorageTier.Warm, result.Tier);
        Assert.True(result.TierChanged);
        Assert.Equal(1, result.ArtifactsUpdated);
        Assert.Equal("staging", result.RecommendedLocation);

        var artifact = Assert.Single(await db.BackupArtifacts.Where(a => a.BackupRunId == runId).ToListAsync());
        Assert.Equal(BackupStorageTier.Warm, artifact.StorageTier);
    }

    [Fact]
    public async Task MoveToOptimalTierAsync_cold_recommends_external_archive()
    {
        await using var db = CreateDb(nameof(MoveToOptimalTierAsync_cold_recommends_external_archive));
        var run = new BackupRun
        {
            Id = Guid.NewGuid(),
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "Fake",
            Strategy = BackupStrategyKind.System,
            RequestedAt = Now.AddDays(-45),
            CompletedAt = Now.AddDays(-45),
        };
        db.BackupRuns.Add(run);
        await db.SaveChangesAsync();

        var result = await _sut.MoveToOptimalTierAsync(db, run, utcNow: Now);

        Assert.Equal(BackupStorageTier.Cold, result.Tier);
        Assert.Equal("external-archive", result.RecommendedLocation);
        Assert.False(result.TierChanged); // no artifacts
    }

    [Fact]
    public async Task ApplyOptimalTiersForSucceededRunsAsync_updates_multiple()
    {
        await using var db = CreateDb(nameof(ApplyOptimalTiersForSucceededRunsAsync_updates_multiple));
        var hotRun = Guid.NewGuid();
        var coldRun = Guid.NewGuid();
        db.BackupRuns.AddRange(
            new BackupRun
            {
                Id = hotRun,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                Strategy = BackupStrategyKind.Tenant,
                TenantId = Guid.NewGuid(),
                RequestedAt = Now.AddDays(-1),
                CompletedAt = Now.AddDays(-1),
            },
            new BackupRun
            {
                Id = coldRun,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                Strategy = BackupStrategyKind.System,
                RequestedAt = Now.AddDays(-40),
                CompletedAt = Now.AddDays(-40),
            });
        db.BackupArtifacts.AddRange(
            new BackupArtifact
            {
                Id = Guid.NewGuid(),
                BackupRunId = hotRun,
                ArtifactType = BackupArtifactType.LogicalDump,
                StorageDescriptor = "a.dump",
                StorageTier = BackupStorageTier.Cold, // wrong — should become Hot
                CreatedAt = Now.AddDays(-1),
            },
            new BackupArtifact
            {
                Id = Guid.NewGuid(),
                BackupRunId = coldRun,
                ArtifactType = BackupArtifactType.LogicalDump,
                StorageDescriptor = "b.dump",
                StorageTier = BackupStorageTier.Hot, // wrong — should become Cold
                CreatedAt = Now.AddDays(-40),
            });
        await db.SaveChangesAsync();

        var changed = await _sut.ApplyOptimalTiersForSucceededRunsAsync(db, utcNow: Now);
        await db.SaveChangesAsync();

        Assert.Equal(2, changed);
        Assert.Equal(
            BackupStorageTier.Hot,
            (await db.BackupArtifacts.SingleAsync(a => a.BackupRunId == hotRun)).StorageTier);
        Assert.Equal(
            BackupStorageTier.Cold,
            (await db.BackupArtifacts.SingleAsync(a => a.BackupRunId == coldRun)).StorageTier);
    }
}
