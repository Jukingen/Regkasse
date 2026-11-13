using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class StorageAlertServiceTests
{
    private static (ServiceProvider Sp, AppDbContext Db) CreateScopeProvider(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        var sp = services.BuildServiceProvider();
        return (sp, sp.GetRequiredService<AppDbContext>());
    }

    private static IOptionsMonitor<BackupOptions> OptionsMonitor(BackupOptions value)
    {
        var mock = new Mock<IOptionsMonitor<BackupOptions>>();
        mock.Setup(m => m.CurrentValue).Returns(value);
        return mock.Object;
    }

    private static StorageAlertService CreateSut(
        IServiceScopeFactory scopeFactory,
        BackupOptions options,
        IBackupStagingDiskMonitor disk,
        IBackupAlertPublisher alerts) =>
        new(
            scopeFactory,
            OptionsMonitor(options),
            disk,
            alerts,
            NullLogger<StorageAlertService>.Instance);

    [Fact]
    public async Task CheckStorage_publishes_when_budget_reaches_80_percent_of_10_gb()
    {
        var (sp, db) = CreateScopeProvider($"storage_alert_budget_{Guid.NewGuid():N}");
        await using var _ = sp;

        var runId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = DateTime.UtcNow,
            Strategy = BackupStrategyKind.System,
        });
        db.BackupArtifacts.Add(new BackupArtifact
        {
            BackupRunId = runId,
            ArtifactType = BackupArtifactType.LogicalDump,
            StorageDescriptor = "dump.dump",
            ByteSize = BackupService.MaxStorageBytes * StorageAlertService.AlertThresholdPercent / 100L,
            LifecycleState = BackupArtifactLifecycleState.Staging,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var alerts = new Mock<IBackupAlertPublisher>();
        var disk = new Mock<IBackupStagingDiskMonitor>();
        disk.Setup(d => d.TryGetUsage(It.IsAny<string?>(), It.IsAny<int>())).Returns((BackupStagingDiskUsage?)null);

        var sut = CreateSut(sp.GetRequiredService<IServiceScopeFactory>(), new BackupOptions(), disk.Object, alerts.Object);
        await sut.CheckStorageAsync(CancellationToken.None);

        alerts.Verify(
            a => a.Publish(It.Is<BackupAlertEvent>(e =>
                e.Kind == BackupAlertKind.StoragePressure
                && e.Data != null
                && e.Data["reason"] == "storage_budget")),
            Times.Once);
    }

    [Fact]
    public async Task CheckStorage_does_not_publish_budget_alert_below_threshold()
    {
        var (sp, db) = CreateScopeProvider($"storage_alert_ok_{Guid.NewGuid():N}");
        await using var _ = sp;

        var runId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = DateTime.UtcNow,
            Strategy = BackupStrategyKind.System,
        });
        db.BackupArtifacts.Add(new BackupArtifact
        {
            BackupRunId = runId,
            ArtifactType = BackupArtifactType.LogicalDump,
            StorageDescriptor = "dump.dump",
            ByteSize = BackupService.MaxStorageBytes / 2,
            LifecycleState = BackupArtifactLifecycleState.Staging,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var alerts = new Mock<IBackupAlertPublisher>();
        var disk = new Mock<IBackupStagingDiskMonitor>();
        disk.Setup(d => d.TryGetUsage(It.IsAny<string?>(), It.IsAny<int>())).Returns((BackupStagingDiskUsage?)null);

        var sut = CreateSut(sp.GetRequiredService<IServiceScopeFactory>(), new BackupOptions(), disk.Object, alerts.Object);
        await sut.CheckStorageAsync(CancellationToken.None);

        alerts.Verify(a => a.Publish(It.IsAny<BackupAlertEvent>()), Times.Never);
    }

    [Fact]
    public async Task CheckStorage_publishes_when_staging_disk_alerts()
    {
        var (sp, _) = CreateScopeProvider($"storage_alert_disk_{Guid.NewGuid():N}");
        await using var _ = sp;

        var alerts = new Mock<IBackupAlertPublisher>();
        var disk = new Mock<IBackupStagingDiskMonitor>();
        disk.Setup(d => d.TryGetUsage("/backup/staging", 80))
            .Returns(new BackupStagingDiskUsage
            {
                RootPath = "/backup/staging",
                TotalBytes = 100,
                AvailableBytes = 10,
                UsedPercent = 90,
                Alert = true,
            });

        var sut = CreateSut(
            sp.GetRequiredService<IServiceScopeFactory>(),
            new BackupOptions { ArtifactStagingRoot = "/backup/staging", StagingDiskUsageAlertPercent = 80 },
            disk.Object,
            alerts.Object);
        await sut.CheckStorageAsync(CancellationToken.None);

        alerts.Verify(
            a => a.Publish(It.Is<BackupAlertEvent>(e =>
                e.Kind == BackupAlertKind.StoragePressure
                && e.Data != null
                && e.Data["reason"] == "staging_disk")),
            Times.Once);
    }

    [Fact]
    public async Task CheckStorage_ignores_failed_runs_in_budget_sum()
    {
        var (sp, db) = CreateScopeProvider($"storage_alert_failed_{Guid.NewGuid():N}");
        await using var _ = sp;

        var runId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Failed,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = DateTime.UtcNow,
            Strategy = BackupStrategyKind.System,
        });
        db.BackupArtifacts.Add(new BackupArtifact
        {
            BackupRunId = runId,
            ArtifactType = BackupArtifactType.LogicalDump,
            StorageDescriptor = "dump.dump",
            ByteSize = BackupService.MaxStorageBytes,
            LifecycleState = BackupArtifactLifecycleState.Staging,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var alerts = new Mock<IBackupAlertPublisher>();
        var disk = new Mock<IBackupStagingDiskMonitor>();
        disk.Setup(d => d.TryGetUsage(It.IsAny<string?>(), It.IsAny<int>())).Returns((BackupStagingDiskUsage?)null);

        var sut = CreateSut(sp.GetRequiredService<IServiceScopeFactory>(), new BackupOptions(), disk.Object, alerts.Object);
        await sut.CheckStorageAsync(CancellationToken.None);

        alerts.Verify(a => a.Publish(It.IsAny<BackupAlertEvent>()), Times.Never);
    }
}
