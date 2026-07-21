using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PitrServiceTests
{
    private sealed class FixedUtcTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedUtcTimeProvider(DateTime utcInstant) =>
            _utcNow = new DateTimeOffset(DateTime.SpecifyKind(utcInstant, DateTimeKind.Utc));

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }

    [Fact]
    public async Task GetPitrAvailabilityAsync_returns_unavailable_when_no_succeeded_backups()
    {
        await using var db = CreateDb();
        var svc = CreateSut(db, new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc));

        var result = await svc.GetPitrAvailabilityAsync(null, CancellationToken.None);

        Assert.False(result.IsAvailable);
        Assert.Contains("No successful backups", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetPitrAvailabilityAsync_returns_window_from_succeeded_backups()
    {
        await using var db = CreateDb();
        var t1 = new DateTime(2026, 5, 28, 2, 0, 0, DateTimeKind.Utc);
        var t2 = new DateTime(2026, 5, 29, 2, 0, 0, DateTimeKind.Utc);
        db.BackupRuns.AddRange(
            SucceededRun(t1),
            SucceededRun(t2));
        await db.SaveChangesAsync();

        var svc = CreateSut(db, new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc));
        var result = await svc.GetPitrAvailabilityAsync(null, CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.Equal(t1, result.EarliestRestorePointUtc);
        Assert.Equal(t2, result.LatestRestorePointUtc);
        Assert.Equal(2, result.SupportedTimePointsUtc.Count);
        Assert.False(result.WalArchivingEnabled);
    }

    [Fact]
    public async Task ValidateRestorePointAsync_selects_base_backup_and_full_backup_only_without_wal()
    {
        await using var db = CreateDb();
        var baseTime = new DateTime(2026, 5, 28, 2, 0, 0, DateTimeKind.Utc);
        var run = SucceededRun(baseTime);
        db.BackupRuns.Add(run);
        await db.SaveChangesAsync();

        var target = baseTime;
        var svc = CreateSut(db, new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc));

        var result = await svc.ValidateRestorePointAsync(null, target, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal(run.Id, result.BaseBackupId);
        Assert.Equal(PitrService.RecoveryMethodFullBackupOnly, result.RecoveryMethod);
        Assert.Equal(0, result.EstimatedDataLossSeconds);
    }

    [Fact]
    public async Task ValidateRestorePointAsync_uses_PITR_when_wal_declared()
    {
        await using var db = CreateDb();
        var baseTime = new DateTime(2026, 5, 28, 2, 0, 0, DateTimeKind.Utc);
        var run = SucceededRun(baseTime);
        db.BackupRuns.Add(run);
        await db.SaveChangesAsync();

        var opts = new BackupOptions { PitrWalArchivingDeclaredEnabled = true, PitrWalArchiveDeclaredLagMinutes = 10 };
        var target = baseTime.AddMinutes(5);
        var svc = CreateSut(db, new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc), opts);

        var result = await svc.ValidateRestorePointAsync(null, target, CancellationToken.None);

        Assert.True(result.IsValid);
        Assert.Equal(PitrService.RecoveryMethodPitr, result.RecoveryMethod);
    }

    [Fact]
    public async Task GetPitrAvailabilityAsync_filters_by_tenant_idempotency_hint()
    {
        await using var db = CreateDb();
        var tenantId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        db.BackupRuns.Add(SucceededRun(new DateTime(2026, 5, 28, 2, 0, 0, DateTimeKind.Utc)));
        db.BackupRuns.Add(new BackupRun
        {
            Id = Guid.NewGuid(),
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = DateTime.UtcNow,
            CompletedAt = new DateTime(2026, 5, 29, 2, 0, 0, DateTimeKind.Utc),
            IdempotencyKey = $"manual-tenant-{tenantId}-1"
        });
        await db.SaveChangesAsync();

        var svc = CreateSut(db, new DateTime(2026, 5, 30, 12, 0, 0, DateTimeKind.Utc));
        var result = await svc.GetPitrAvailabilityAsync(tenantId, CancellationToken.None);

        Assert.True(result.IsAvailable);
        Assert.Single(result.SupportedTimePointsUtc);
    }

    private static BackupRun SucceededRun(DateTime completedAt) => new()
    {
        Id = Guid.NewGuid(),
        Status = BackupRunStatus.Succeeded,
        Strategy = BackupStrategyKind.System,
        TriggerSource = BackupTriggerSource.Scheduled,
        AdapterKind = "Fake",
        RequestedAt = completedAt.AddMinutes(-5),
        StartedAt = completedAt.AddMinutes(-3),
        CompletedAt = completedAt,
    };

    private static PitrService CreateSut(AppDbContext db, DateTime utcNow, BackupOptions? options = null)
    {
        var optMock = new Mock<IOptionsMonitor<BackupOptions>>();
        optMock.Setup(o => o.CurrentValue).Returns(options ?? new BackupOptions());
        return new PitrService(db, optMock.Object, new FixedUtcTimeProvider(utcNow), Mock.Of<ILogger<PitrService>>());
    }

    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"pitr_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(opts, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }
}
