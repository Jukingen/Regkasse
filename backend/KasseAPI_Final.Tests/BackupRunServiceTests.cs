using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using Environments = Microsoft.Extensions.Hosting.Environments;

namespace KasseAPI_Final.Tests;

public sealed class BackupRunServiceTests
{
    [Fact]
    public async Task GetBackupRunAsync_populates_size_duration_and_metadata_compression_ratio()
    {
        var started = new DateTime(2026, 5, 30, 10, 0, 0, DateTimeKind.Utc);
        var completed = started.AddMinutes(3);
        var runId = Guid.NewGuid();

        await using var db = CreateDb();
        db.BackupRuns.Add(new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Succeeded,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = started,
            StartedAt = started,
            CompletedAt = completed,
            Artifacts =
            [
                new BackupArtifact
                {
                    Id = Guid.NewGuid(),
                    BackupRunId = runId,
                    ArtifactType = BackupArtifactType.LogicalDump,
                    ByteSize = 100,
                    MetadataJson = """{"originalByteSize":250}""",
                    StorageDescriptor = "dump.bin",
                    CreatedAt = completed,
                }
            ]
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var dto = await svc.GetBackupRunAsync(runId, new BackupRunDtoMappingOptions());

        Assert.NotNull(dto);
        Assert.Equal(100, dto!.TotalSizeBytes);
        Assert.Equal("100 B", dto.TotalSizeFormatted);
        Assert.Equal(180, dto.DurationSeconds);
        Assert.Equal("3m", dto.DurationFormatted);
        Assert.NotNull(dto.DurationFormatted);
        Assert.Equal(40d, dto.CompressionRatio);
        Assert.Single(dto.Artifacts!);
        Assert.Equal("100 B", dto.Artifacts![0].FormattedSize);
    }

    [Fact]
    public async Task EstimateOriginalDumpSizeAsync_returns_metadata_before_live_query()
    {
        var run = new BackupRun
        {
            AdapterKind = "PgDump",
            Artifacts =
            [
                new BackupArtifact
                {
                    ArtifactType = BackupArtifactType.LogicalDump,
                    MetadataJson = """{"databaseByteSize":999}"""
                }
            ]
        };

        await using var db = CreateDb();
        var svc = CreateService(db);

        var bytes = await svc.EstimateOriginalDumpSizeAsync(run, CancellationToken.None);

        Assert.Equal(999, bytes);
    }

    [Fact]
    public async Task GetBackupListAsync_returns_readable_logical_dump_rows_with_tenant_slug()
    {
        var tenantId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var artifactId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 7, 3, 15, 1, 0, DateTimeKind.Utc);

        await using var db = CreateDb();
        db.Tenants.Add(new Models.Tenant
        {
            Id = tenantId,
            Name = "Cafe",
            Slug = "dev",
            Status = Models.TenantStatuses.Active
        });
        db.BackupRuns.Add(new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.Tenant,
            TenantId = tenantId,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            IdempotencyKey = $"manual-tenant-{tenantId:D}-1734567890123",
            RequestedAt = createdAt,
            StartedAt = createdAt.AddMinutes(-3),
            CompletedAt = createdAt
        });
        db.BackupArtifacts.Add(new BackupArtifact
        {
            Id = artifactId,
            BackupRunId = runId,
            ArtifactType = BackupArtifactType.LogicalDump,
            StorageDescriptor = "backup_dev_20260703_150100.dump",
            ByteSize = 4096,
            CreatedAt = createdAt
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var items = await svc.GetBackupListAsync(tenantId, isSuperAdmin: false);

        var row = Assert.Single(items);
        Assert.Equal("backup_dev_20260703_150100.dump", row.FileName);
        Assert.Equal(4096, row.FileSize);
        Assert.Equal(createdAt, row.CreatedAt);
        Assert.Equal("dev", row.TenantSlug);
        Assert.True(row.IsFake);
        Assert.Equal(runId, row.BackupRunId);
        Assert.Equal(artifactId, row.ArtifactId);
        Assert.Equal(BackupRunStatus.Succeeded, row.Status);
        Assert.Equal(BackupStrategyKind.Tenant, row.Strategy);
        Assert.Equal(180, row.DurationSeconds);
        Assert.Equal("3m", row.DurationFormatted);
        Assert.Null(row.DownloadUrl);
    }

    [Fact]
    public async Task GetBackupListAsync_filters_by_tenant_idempotency_hint()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using var db = CreateDb();
        db.Tenants.AddRange(
            new Models.Tenant { Id = tenantA, Name = "A", Slug = "dev", Status = Models.TenantStatuses.Active },
            new Models.Tenant { Id = tenantB, Name = "B", Slug = "prod", Status = Models.TenantStatuses.Active });

        var runA = Guid.NewGuid();
        var runB = Guid.NewGuid();
        var systemRun = Guid.NewGuid();
        db.BackupRuns.AddRange(
            new BackupRun
            {
                Id = runA,
                Status = BackupRunStatus.Succeeded,
                Strategy = BackupStrategyKind.Tenant,
                TenantId = tenantA,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "PgDump",
                IdempotencyKey = $"manual-tenant-{tenantA:D}-1",
                RequestedAt = DateTime.UtcNow
            },
            new BackupRun
            {
                Id = runB,
                Status = BackupRunStatus.Succeeded,
                Strategy = BackupStrategyKind.Tenant,
                TenantId = tenantB,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "PgDump",
                IdempotencyKey = $"manual-tenant-{tenantB:D}-2",
                RequestedAt = DateTime.UtcNow
            },
            new BackupRun
            {
                Id = systemRun,
                Status = BackupRunStatus.Succeeded,
                Strategy = BackupStrategyKind.System,
                TenantId = null,
                TriggerSource = BackupTriggerSource.Scheduled,
                AdapterKind = "PgDump",
                RequestedAt = DateTime.UtcNow
            });
        db.BackupArtifacts.AddRange(
            new BackupArtifact
            {
                BackupRunId = runA,
                ArtifactType = BackupArtifactType.LogicalDump,
                StorageDescriptor = "backup_dev_20260703_150100.dump",
                CreatedAt = DateTime.UtcNow
            },
            new BackupArtifact
            {
                BackupRunId = runB,
                ArtifactType = BackupArtifactType.LogicalDump,
                StorageDescriptor = "backup_prod_20260702_230000.dump",
                CreatedAt = DateTime.UtcNow
            },
            new BackupArtifact
            {
                BackupRunId = systemRun,
                ArtifactType = BackupArtifactType.LogicalDump,
                StorageDescriptor = "backup_system.dump",
                CreatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var items = await svc.GetBackupListAsync(tenantA, isSuperAdmin: false);

        Assert.Single(items);
        Assert.Equal("dev", items[0].TenantSlug);
        Assert.Equal(BackupStrategyKind.Tenant, items[0].Strategy);
        Assert.False(items[0].IsFake);
    }

    [Fact]
    public async Task GetBackupListAsync_links_verification_manifest_on_same_run()
    {
        var runId = Guid.NewGuid();
        var dumpId = Guid.NewGuid();
        var manifestId = Guid.NewGuid();
        var createdAt = new DateTime(2026, 7, 3, 15, 1, 0, DateTimeKind.Utc);

        await using var db = CreateDb();
        db.BackupRuns.Add(new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.System,
            TriggerSource = BackupTriggerSource.Scheduled,
            AdapterKind = "PgDump",
            RequestedAt = createdAt,
            CompletedAt = createdAt
        });
        db.BackupArtifacts.Add(new BackupArtifact
        {
            Id = dumpId,
            BackupRunId = runId,
            ArtifactType = BackupArtifactType.LogicalDump,
            StorageDescriptor = "backup_scheduled_20260703_150100.dump",
            ByteSize = 1024,
            CreatedAt = createdAt
        });
        db.BackupArtifacts.Add(new BackupArtifact
        {
            Id = manifestId,
            BackupRunId = runId,
            ArtifactType = BackupArtifactType.VerificationManifest,
            StorageDescriptor = "backup_scheduled_20260703_150100_manifest.json",
            ByteSize = 256,
            CreatedAt = createdAt
        });
        await db.SaveChangesAsync();

        var svc = CreateService(db);
        var items = await svc.GetBackupListAsync(null, isSuperAdmin: true);

        var row = Assert.Single(items);
        Assert.Equal("backup_scheduled_20260703_150100.dump", row.FileName);
        Assert.Equal(BackupStrategyKind.System, row.Strategy);
        Assert.Equal(manifestId, row.ManifestArtifactId);
        Assert.Equal("backup_scheduled_20260703_150100_manifest.json", row.ManifestFileName);
        Assert.Equal(256, row.ManifestFileSize);
    }

    private static BackupRunService CreateService(AppDbContext db)
    {
        var config = new ConfigurationBuilder().Build();
        var optionsMock = new Mock<IOptionsMonitor<BackupOptions>>();
        optionsMock.Setup(m => m.CurrentValue).Returns(new BackupOptions());
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        return new BackupRunService(
            db,
            config,
            optionsMock.Object,
            env.Object,
            NullLogger<BackupRunService>.Instance);
    }

    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"backup_run_svc_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(opts, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }
}
