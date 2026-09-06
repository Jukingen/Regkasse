using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupChainServiceTests
{
    [Fact]
    public async Task GetChainAsync_groups_full_and_incrementals()
    {
        var tenantId = Guid.NewGuid();
        await using var db = CreateDb();
        db.Tenants.Add(new Tenant { Id = tenantId, Name = "T", Slug = "t", IsActive = true, CreatedAt = DateTime.UtcNow });
        var fullAt = new DateTime(2026, 9, 1, 2, 0, 0, DateTimeKind.Utc);
        var incrAt = new DateTime(2026, 9, 2, 3, 0, 0, DateTimeKind.Utc);
        var full = SucceededTenant(tenantId, fullAt, incremental: false);
        var incr = SucceededTenant(tenantId, incrAt, incremental: true, since: fullAt);
        db.BackupRuns.AddRange(full, incr, SucceededSystem(new DateTime(2026, 9, 1, 2, 10, 0, DateTimeKind.Utc)));
        await db.SaveChangesAsync();

        var wal = new Mock<IWalArchiveService>();
        wal.Setup(w => w.GetStatus()).Returns(new WalArchiveStatusDto { Enabled = false });
        var sut = new BackupChainService(db, wal.Object);
        var chain = await sut.GetChainAsync(
            tenantId,
            new BackupRunAccessScope(true, tenantId, "sa"),
            CancellationToken.None);

        Assert.NotNull(chain.FullBackup);
        Assert.Equal(full.Id, chain.FullBackup!.RunId);
        Assert.Single(chain.Incrementals);
        Assert.Equal(incr.Id, chain.Incrementals[0].RunId);
        Assert.Single(chain.SystemBackups);
        Assert.True(chain.RestorePointAvailable);
    }

    private static BackupRun SucceededTenant(Guid tenantId, DateTime completed, bool incremental, DateTime? since = null)
    {
        var json = incremental && since.HasValue
            ? BackupIncrementalPackageMetadata.MergeIntoConfigSnapshot("{}", since.Value)
            : """{"packageKind":"full"}""";
        return new BackupRun
        {
            Id = Guid.NewGuid(),
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.Tenant,
            TenantId = tenantId,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = completed.AddMinutes(-5),
            CompletedAt = completed,
            ConfigSnapshotJson = json
        };
    }

    private static BackupRun SucceededSystem(DateTime completed) => new()
    {
        Id = Guid.NewGuid(),
        Status = BackupRunStatus.Succeeded,
        Strategy = BackupStrategyKind.System,
        TriggerSource = BackupTriggerSource.Scheduled,
        AdapterKind = "Fake",
        RequestedAt = completed.AddMinutes(-5),
        CompletedAt = completed
    };

    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"chain_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(opts, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }
}
