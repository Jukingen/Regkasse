using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupStorageCostServiceTests
{
    private static AppDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(name).Options);

    private static IOptionsMonitor<BackupOptions> Options(BackupOptions opts) =>
        new StaticOptionsMonitor(opts);

    private sealed class StaticOptionsMonitor(BackupOptions value) : IOptionsMonitor<BackupOptions>
    {
        public BackupOptions CurrentValue { get; } = value;
        public BackupOptions Get(string? name) => CurrentValue;
        public IDisposable OnChange(Action<BackupOptions, string?> listener) => NullDisposable.Instance;
    }

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose() { }
    }

    [Fact]
    public async Task GetAsync_computes_usage_and_tier_costs()
    {
        await using var db = CreateDb(nameof(GetAsync_computes_usage_and_tier_costs));
        var runHot = Guid.NewGuid();
        var runCold = Guid.NewGuid();
        db.BackupRuns.AddRange(
            new BackupRun
            {
                Id = runHot,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                Strategy = BackupStrategyKind.System,
                RequestedAt = DateTime.UtcNow.AddDays(-1),
                CompletedAt = DateTime.UtcNow.AddDays(-1),
            },
            new BackupRun
            {
                Id = runCold,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Scheduled,
                AdapterKind = "Fake",
                Strategy = BackupStrategyKind.System,
                RequestedAt = DateTime.UtcNow.AddDays(-40),
                CompletedAt = DateTime.UtcNow.AddDays(-40),
            });
        // 1 GiB hot + 1 GiB cold
        var oneGib = (long)BackupStorageCostService.BytesPerGiB;
        db.BackupArtifacts.AddRange(
            new BackupArtifact
            {
                Id = Guid.NewGuid(),
                BackupRunId = runHot,
                ArtifactType = BackupArtifactType.LogicalDump,
                StorageDescriptor = "hot.dump",
                ByteSize = oneGib,
                StorageTier = BackupStorageTier.Hot,
                ContentHashSha256 = new string('a', 64),
                CreatedAt = DateTime.UtcNow.AddDays(-1),
            },
            new BackupArtifact
            {
                Id = Guid.NewGuid(),
                BackupRunId = runCold,
                ArtifactType = BackupArtifactType.LogicalDump,
                StorageDescriptor = "cold.dump",
                ByteSize = oneGib,
                StorageTier = BackupStorageTier.Cold,
                ContentHashSha256 = new string('b', 64),
                CreatedAt = DateTime.UtcNow.AddDays(-40),
            });
        await db.SaveChangesAsync();

        var sut = new BackupStorageCostService(
            db,
            Options(new BackupOptions
            {
                StorageCostHotEurPerGbMonth = 0.02m,
                StorageCostWarmEurPerGbMonth = 0.01m,
                StorageCostColdEurPerGbMonth = 0.005m,
                SmartRetentionEnabled = false,
                StorageTierManagementEnabled = true,
            }));

        var dto = await sut.GetAsync();

        Assert.Equal(2, dto.BackupCount);
        Assert.Equal(1, Assert.Single(dto.Tiers, t => t.Name == "Hot").ArtifactCount);
        Assert.Equal(0, Assert.Single(dto.Tiers, t => t.Name == "Warm").ArtifactCount);
        Assert.Equal(1, Assert.Single(dto.Tiers, t => t.Name == "Cold").ArtifactCount);
        Assert.Equal(0.02d, Assert.Single(dto.Tiers, t => t.Name == "Hot").CostEur, 3);
        Assert.Equal(0.005d, Assert.Single(dto.Tiers, t => t.Name == "Cold").CostEur, 3);
        Assert.Equal(0.025d, dto.MonthlyCostEur, 3);
        Assert.True(dto.RetentionSavingsPercent > 0);
        Assert.NotEmpty(dto.Recommendations);
        Assert.Contains(dto.Recommendations, r => r.Code is "enable_smart_retention" or "healthy" or "tier_savings_low");
    }

    [Fact]
    public async Task GetAsync_respects_tenant_access_scope()
    {
        await using var db = CreateDb(nameof(GetAsync_respects_tenant_access_scope));
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var runA = Guid.NewGuid();
        var runB = Guid.NewGuid();
        db.BackupRuns.AddRange(
            new BackupRun
            {
                Id = runA,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                Strategy = BackupStrategyKind.Tenant,
                TenantId = tenantA,
                RequestedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
            },
            new BackupRun
            {
                Id = runB,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                Strategy = BackupStrategyKind.Tenant,
                TenantId = tenantB,
                RequestedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
            });
        db.BackupArtifacts.AddRange(
            new BackupArtifact
            {
                Id = Guid.NewGuid(),
                BackupRunId = runA,
                ArtifactType = BackupArtifactType.LogicalDump,
                StorageDescriptor = "a.dump",
                ByteSize = 5L * 1024 * 1024, // 5 MiB
                StorageTier = BackupStorageTier.Hot,
                CreatedAt = DateTime.UtcNow,
            },
            new BackupArtifact
            {
                Id = Guid.NewGuid(),
                BackupRunId = runB,
                ArtifactType = BackupArtifactType.LogicalDump,
                StorageDescriptor = "b.dump",
                ByteSize = 9L * 1024 * 1024,
                StorageTier = BackupStorageTier.Hot,
                CreatedAt = DateTime.UtcNow,
            });
        await db.SaveChangesAsync();

        var sut = new BackupStorageCostService(db, Options(new BackupOptions()));
        var scope = new BackupRunAccessScope(
            IsSuperAdmin: false,
            CallerTenantId: tenantA,
            CallerUserId: null);

        var dto = await sut.GetAsync(scope);
        Assert.Equal(1, dto.BackupCount);
        Assert.Equal(5d, dto.AverageSizeMb);
    }
}
