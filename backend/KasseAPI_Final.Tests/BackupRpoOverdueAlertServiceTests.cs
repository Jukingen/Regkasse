using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupRpoOverdueAlertServiceTests
{
    [Fact]
    public async Task Check_WhenFreshBackup_NoAlert()
    {
        await using var db = CreateDb();
        db.BackupRuns.Add(new BackupRun
        {
            Id = Guid.NewGuid(),
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.System,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow.AddHours(-1),
            CompletedAt = DateTime.UtcNow.AddMinutes(-30),
        });
        await db.SaveChangesAsync();

        var alerts = new Mock<IBackupAlertPublisher>(MockBehavior.Strict);
        await using var provider = BuildProvider(db);
        var sut = CreateSut(
            provider,
            new BackupOptions
            {
                RpoOverdueAlertEnabled = true,
                WorkerEnabled = true,
                ScheduledBackupEnabled = true,
                ScheduledBackupCron = "0 2 * * *",
                AlertOnNoBackupDays = 2,
            },
            alerts.Object);

        await sut.CheckRpoAsync(CancellationToken.None);
        alerts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Check_WhenStaleBackup_PublishesAlert()
    {
        await using var db = CreateDb();
        var runId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.System,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow.AddDays(-5),
            CompletedAt = DateTime.UtcNow.AddDays(-4),
        });
        await db.SaveChangesAsync();

        var alerts = new Mock<IBackupAlertPublisher>();
        await using var provider = BuildProvider(db);
        var sut = CreateSut(
            provider,
            new BackupOptions
            {
                RpoOverdueAlertEnabled = true,
                WorkerEnabled = true,
                ScheduledBackupEnabled = true,
                ScheduledBackupCron = "0 2 * * *",
                AlertOnNoBackupDays = 2,
                RpoOverdueAlertMinInterval = TimeSpan.FromHours(12),
            },
            alerts.Object);

        await sut.CheckRpoAsync(CancellationToken.None);

        alerts.Verify(
            a => a.Publish(It.Is<BackupAlertEvent>(e =>
                e.Kind == BackupAlertKind.RpoOverdue && e.BackupRunId == runId)),
            Times.Once);
    }

    [Fact]
    public async Task Check_DedupPreventsSpam()
    {
        await using var db = CreateDb();
        var runId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.System,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "PgDump",
            RequestedAt = DateTime.UtcNow.AddDays(-5),
            CompletedAt = DateTime.UtcNow.AddDays(-4),
        });
        await db.SaveChangesAsync();

        var alerts = new Mock<IBackupAlertPublisher>();
        await using var provider = BuildProvider(db);
        var sut = CreateSut(
            provider,
            new BackupOptions
            {
                RpoOverdueAlertEnabled = true,
                WorkerEnabled = true,
                ScheduledBackupEnabled = true,
                ScheduledBackupCron = "0 2 * * *",
                AlertOnNoBackupDays = 2,
                RpoOverdueAlertMinInterval = TimeSpan.FromHours(12),
            },
            alerts.Object);

        await sut.CheckRpoAsync(CancellationToken.None);
        await sut.CheckRpoAsync(CancellationToken.None);

        alerts.Verify(
            a => a.Publish(It.Is<BackupAlertEvent>(e =>
                e.Kind == BackupAlertKind.RpoOverdue && e.BackupRunId == runId)),
            Times.Once);
    }

    [Fact]
    public async Task Check_WhenScheduledBackupDisabled_NoAlert()
    {
        await using var db = CreateDb();
        var alerts = new Mock<IBackupAlertPublisher>(MockBehavior.Strict);
        await using var provider = BuildProvider(db);
        var sut = CreateSut(
            provider,
            new BackupOptions
            {
                RpoOverdueAlertEnabled = true,
                WorkerEnabled = true,
                ScheduledBackupEnabled = false,
                ScheduledBackupCron = "0 2 * * *",
                AlertOnNoBackupDays = 2,
            },
            alerts.Object);

        await sut.CheckRpoAsync(CancellationToken.None);
        alerts.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Check_WhenDisabled_NoOp()
    {
        var alerts = new Mock<IBackupAlertPublisher>(MockBehavior.Strict);
        await using var provider = BuildProvider(CreateDb());
        var sut = CreateSut(provider, new BackupOptions { RpoOverdueAlertEnabled = false }, alerts.Object);
        await sut.CheckRpoAsync(CancellationToken.None);
        alerts.VerifyNoOtherCalls();
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"rpo_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(SystemTenantIds.Platform));
    }

    private static ServiceProvider BuildProvider(AppDbContext db)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        return services.BuildServiceProvider();
    }

    private static BackupRpoOverdueAlertService CreateSut(
        IServiceProvider provider,
        BackupOptions options,
        IBackupAlertPublisher alerts)
    {
        var monitor = new Mock<IOptionsMonitor<BackupOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(options);
        return new BackupRpoOverdueAlertService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            monitor.Object,
            alerts,
            NullLogger<BackupRpoOverdueAlertService>.Instance);
    }
}
