using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AutomaticCleanupServiceTests
{
    private static (ServiceProvider Sp, AppDbContext Db, Mock<IAuditLogService> Audit) CreateProvider(string dbName)
    {
        var audit = new Mock<IAuditLogService>();
        audit.Setup(a => a.LogSystemOperationAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<AuditLogStatus>(),
                It.IsAny<string?>(),
                It.IsAny<object?>(),
                It.IsAny<object?>(),
                It.IsAny<string?>(),
                It.IsAny<ImpersonationAuditContext.Snapshot?>(),
                It.IsAny<AuditEventType?>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()))
            .ReturnsAsync(new AuditLog());

        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton(audit.Object);
        var sp = services.BuildServiceProvider();
        return (sp, sp.GetRequiredService<AppDbContext>(), audit);
    }

    private static IOptionsMonitor<BackupOptions> Options(BackupOptions opts)
    {
        var mock = new Mock<IOptionsMonitor<BackupOptions>>();
        mock.SetupGet(m => m.CurrentValue).Returns(opts);
        return mock.Object;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "tests";
        public string ContentRootPath { get; set; } = Path.GetTempPath();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public async Task RunCleanupPassAsync_smart_mode_deletes_beyond_seven_years_and_audits()
    {
        var (sp, db, audit) = CreateProvider(nameof(RunCleanupPassAsync_smart_mode_deletes_beyond_seven_years_and_audits));
        await using var _ = sp;

        await BackupSettingsEnsure.EnsureSingletonAsync(db);
        var settings = await db.BackupSettings.FirstAsync(x => x.Id == BackupSettings.SingletonId);
        settings.RetentionDays = 90;
        await db.SaveChangesAsync();

        var keepId = Guid.NewGuid();
        var dropId = Guid.NewGuid();
        db.BackupRuns.AddRange(
            new BackupRun
            {
                Id = keepId,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Scheduled,
                AdapterKind = "Fake",
                Strategy = BackupStrategyKind.System,
                RequestedAt = DateTime.UtcNow.AddDays(-2),
                CompletedAt = DateTime.UtcNow.AddDays(-2),
            },
            new BackupRun
            {
                Id = dropId,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Scheduled,
                AdapterKind = "Fake",
                Strategy = BackupStrategyKind.System,
                RequestedAt = DateTime.UtcNow.AddDays(-(SmartRetentionService.YearlyRetentionYears * 365 + 40)),
                CompletedAt = DateTime.UtcNow.AddDays(-(SmartRetentionService.YearlyRetentionYears * 365 + 40)),
            });
        await db.SaveChangesAsync();

        var sut = new AutomaticCleanupService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            Options(new BackupOptions
            {
                AutomaticCleanupEnabled = true,
                SmartRetentionEnabled = true,
                StorageTierManagementEnabled = false,
            }),
            new TestHostEnvironment(),
            new SmartRetentionService(),
            new StorageTierService(NullLogger<StorageTierService>.Instance),
            NullLogger<AutomaticCleanupService>.Instance);

        var result = await sut.RunCleanupPassAsync();

        Assert.Equal(1, result.RunsDeleted);
        Assert.Equal(0, result.TiersUpdated);
        db.ChangeTracker.Clear();
        Assert.NotNull(await db.BackupRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == keepId));
        Assert.Null(await db.BackupRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == dropId));
        Assert.Contains(
            audit.Invocations,
            i => i.Method.Name == nameof(IAuditLogService.LogSystemOperationAsync)
                 && i.Arguments.Count > 0
                 && Equals(i.Arguments[0], "BACKUP_AUTO_DELETED"));
    }

    [Fact]
    public async Task RunCleanupPassAsync_applies_storage_tiers_when_enabled()
    {
        var (sp, db, _) = CreateProvider(nameof(RunCleanupPassAsync_applies_storage_tiers_when_enabled));
        await using var _ = sp;

        await BackupSettingsEnsure.EnsureSingletonAsync(db);
        var runId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            Strategy = BackupStrategyKind.System,
            RequestedAt = DateTime.UtcNow.AddDays(-10),
            CompletedAt = DateTime.UtcNow.AddDays(-10),
        });
        db.BackupArtifacts.Add(new BackupArtifact
        {
            Id = Guid.NewGuid(),
            BackupRunId = runId,
            ArtifactType = BackupArtifactType.LogicalDump,
            StorageDescriptor = "x.dump",
            ByteSize = 1024,
            StorageTier = BackupStorageTier.Hot,
            CreatedAt = DateTime.UtcNow.AddDays(-10),
        });
        await db.SaveChangesAsync();

        var sut = new AutomaticCleanupService(
            sp.GetRequiredService<IServiceScopeFactory>(),
            Options(new BackupOptions
            {
                AutomaticCleanupEnabled = true,
                SmartRetentionEnabled = false,
                StorageTierManagementEnabled = true,
            }),
            new TestHostEnvironment(),
            new SmartRetentionService(),
            new StorageTierService(NullLogger<StorageTierService>.Instance),
            NullLogger<AutomaticCleanupService>.Instance);

        var result = await sut.RunCleanupPassAsync();

        Assert.Equal(0, result.RunsDeleted);
        Assert.Equal(1, result.TiersUpdated);
        db.ChangeTracker.Clear();
        var artifact = Assert.Single(await db.BackupArtifacts.AsNoTracking().Where(a => a.BackupRunId == runId).ToListAsync());
        Assert.Equal(BackupStorageTier.Warm, artifact.StorageTier);
    }
}
