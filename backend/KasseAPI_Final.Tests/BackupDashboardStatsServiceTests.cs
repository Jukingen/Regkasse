using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupDashboardStatsServiceTests
{
    private static (AppDbContext Db, IBackupDashboardStatsService Sut) CreateSut(
        string dbName,
        DateTime utcNow)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped<IBackupRunQueryService, BackupRunQueryService>();
        services.AddSingleton<TimeProvider>(_ => new FakeTimeProvider(utcNow));
        services.AddSingleton<IBackupOperationalReadiness>(_ =>
        {
            var m = new Mock<IBackupOperationalReadiness>();
            m.Setup(x => x.GetConfigurationHealth()).Returns(new BackupConfigurationHealthSnapshot
            {
                Level = BackupConfigurationHealthLevel.Healthy,
                EffectiveAdapterKind = BackupExecutionAdapterKind.PgDump,
            });
            m.Setup(x => x.GetArtifactPipelinePolicy()).Returns(new BackupArtifactPipelinePolicySnapshot
            {
                ExternalArchiveRootConfigured = true,
                EffectiveAdapterKind = BackupExecutionAdapterKind.PgDump,
            });
            return m.Object;
        });
        services.AddScoped<IBackupDashboardStatsService, BackupDashboardStatsService>();
        var sp = services.BuildServiceProvider();
        return (sp.GetRequiredService<AppDbContext>(), sp.GetRequiredService<IBackupDashboardStatsService>());
    }

    [Fact]
    public async Task GetAsync_ComputesSuccessRateAndRpo_FromTerminalRuns()
    {
        var now = new DateTime(2026, 5, 27, 12, 0, 0, DateTimeKind.Utc);
        var (db, sut) = CreateSut(nameof(GetAsync_ComputesSuccessRateAndRpo_FromTerminalRuns), now);

        db.BackupRuns.AddRange(
            new BackupRun
            {
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "PgDump",
                RequestedAt = now.AddDays(-2),
                StartedAt = now.AddDays(-2),
                CompletedAt = now.AddDays(-2).AddMinutes(5),
                Artifacts =
                {
                    new BackupArtifact
                    {
                        ArtifactType = BackupArtifactType.LogicalDump,
                        StorageDescriptor = "x.dump",
                        ByteSize = 1024,
                    },
                },
            },
            new BackupRun
            {
                Status = BackupRunStatus.Failed,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "PgDump",
                RequestedAt = now.AddDays(-1),
                CompletedAt = now.AddDays(-1),
            });
        await db.SaveChangesAsync();

        var stats = await sut.GetAsync();

        Assert.Equal(50, stats.SuccessRate30DaysPercent);
        Assert.Equal(2, stats.TerminalRuns30Days);
        Assert.NotNull(stats.RpoHours);
        Assert.True(stats.RpoHours < 72);
        Assert.Equal(1024, stats.BackupSizeBytes);
        Assert.Equal(2, stats.History30Days.Count);
        Assert.Equal(1, stats.History30Days.Count(p => p.Success == 1));
        Assert.Equal(1, stats.History30Days.Count(p => p.Failed == 1));
    }

    [Fact]
    public async Task GetAsync_RtoMinutes_FromSucceededRestoreDrills()
    {
        var now = new DateTime(2026, 5, 27, 12, 0, 0, DateTimeKind.Utc);
        var (db, sut) = CreateSut(nameof(GetAsync_RtoMinutes_FromSucceededRestoreDrills), now);

        var started = now.AddHours(-2);
        var completed = started.AddMinutes(20);
        db.RestoreVerificationRuns.Add(new RestoreVerificationRun
        {
            Status = RestoreVerificationStatus.Succeeded,
            TriggerSource = RestoreVerificationTriggerSource.Scheduled,
            RequestedAt = started,
            StartedAt = started,
            CompletedAt = completed,
        });
        await db.SaveChangesAsync();

        var stats = await sut.GetAsync();

        Assert.Equal(20, stats.RtoMinutes);
    }

    [Fact]
    public async Task GetAsync_ManagerScope_ExcludesOtherTenantTerminalRuns()
    {
        var now = new DateTime(2026, 7, 7, 12, 0, 0, DateTimeKind.Utc);
        var (db, sut) = CreateSut(nameof(GetAsync_ManagerScope_ExcludesOtherTenantTerminalRuns), now);
        var tenantA = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var tenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        db.BackupRuns.AddRange(
            new BackupRun
            {
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                TenantId = tenantA,
                RequestedAt = now.AddDays(-1),
                StartedAt = now.AddDays(-1),
                CompletedAt = now.AddDays(-1).AddMinutes(5),
            },
            new BackupRun
            {
                Status = BackupRunStatus.Failed,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                TenantId = tenantB,
                RequestedAt = now.AddHours(-1),
                CompletedAt = now.AddHours(-1),
            });
        await db.SaveChangesAsync();

        var scope = new BackupRunAccessScope(IsSuperAdmin: false, tenantA, "manager-1");
        var stats = await sut.GetAsync(scope);

        Assert.Equal(100, stats.SuccessRate30DaysPercent);
        Assert.Equal(1, stats.TerminalRuns30Days);
        Assert.Equal(1, stats.SucceededRuns30Days);
        Assert.Single(stats.History30Days);
    }

    private sealed class FakeTimeProvider(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }
}
