using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class IncrementalBackupServiceTests
{
    private static IOptionsMonitor<BackupOptions> OptionsMonitor(BackupOptions value)
    {
        var mock = new Mock<IOptionsMonitor<BackupOptions>>();
        mock.Setup(m => m.CurrentValue).Returns(value);
        return mock.Object;
    }

    [Fact]
    public void NormalizeSinceUtc_rejects_future_watermark()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            IncrementalBackupService.NormalizeSinceUtc(DateTime.UtcNow.AddDays(2)));
    }

    [Fact]
    public async Task GetChangesSinceAsync_counts_only_rows_after_watermark()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"incr_count_{Guid.NewGuid():N}")
            .Options;
        await using var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "T",
            Slug = "t",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        });
        var since = DateTime.UtcNow.AddHours(-2);
        db.Products.AddRange(
            new Product
            {
                Id = Guid.NewGuid(),
                Name = "Old",
                TenantId = tenantId,
                Price = 1,
                IsActive = true,
                CreatedAt = since.AddHours(-5),
                // Product.UpdatedAt is required; keep it before the watermark.
                UpdatedAt = since.AddHours(-5)
            },
            new Product
            {
                Id = Guid.NewGuid(),
                Name = "New",
                TenantId = tenantId,
                Price = 2,
                IsActive = true,
                CreatedAt = since.AddMinutes(5),
                UpdatedAt = since.AddMinutes(5)
            });
        await db.SaveChangesAsync();

        var sut = new IncrementalBackupService(
            db,
            Mock.Of<IBackupManualTriggerService>(),
            Mock.Of<IBackupStagingDiskMonitor>(),
            OptionsMonitor(new BackupOptions()),
            NullLogger<IncrementalBackupService>.Instance);

        var summary = await sut.GetChangesSinceAsync(tenantId, since);
        Assert.Equal(1, summary.TableChangeCounts["products.json"]);
        Assert.Equal(1, summary.TotalChangedRows);
    }

    [Fact]
    public async Task CreateIncrementalBackupAsync_enqueues_with_incremental_watermark()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var since = DateTime.UtcNow.AddHours(-1);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"incr_enq_{Guid.NewGuid():N}")
            .Options;
        await using var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "T",
            Slug = "demo",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-1)
        });
        db.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            Name = "Changed",
            TenantId = tenantId,
            Price = 3,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var runId = Guid.NewGuid();
        var trigger = new Mock<IBackupManualTriggerService>();
        trigger
            .Setup(t => t.RequestManualBackupAsync(
                userId.ToString(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                BackupStrategyKind.Tenant,
                false,
                It.IsAny<CancellationToken>(),
                It.Is<DateTime?>(d => d.HasValue && Math.Abs((d.Value - since).TotalSeconds) < 2)))
            .ReturnsAsync(new BackupManualTriggerOutcome
            {
                Run = new BackupRun
                {
                    Id = runId,
                    Strategy = BackupStrategyKind.Tenant,
                    TenantId = tenantId,
                    Status = BackupRunStatus.Queued,
                    TriggerSource = BackupTriggerSource.Manual,
                    AdapterKind = "Fake",
                    RequestedAt = DateTime.UtcNow
                },
                Kind = BackupManualTriggerResultKind.NewRunQueued
            });

        var sut = new IncrementalBackupService(
            db,
            trigger.Object,
            Mock.Of<IBackupStagingDiskMonitor>(m =>
                m.TryGetUsage(It.IsAny<string?>(), It.IsAny<int>()) == null),
            OptionsMonitor(new BackupOptions()),
            NullLogger<IncrementalBackupService>.Instance);

        var result = await sut.CreateIncrementalBackupAsync(tenantId, userId, since);

        Assert.True(result.Succeeded);
        Assert.Equal(runId, result.BackupRunId);
        trigger.Verify(
            t => t.RequestManualBackupAsync(
                userId.ToString(),
                It.IsAny<string>(),
                It.Is<string?>(k => k != null && k.Contains("manual-tenant-incr", StringComparison.Ordinal)),
                It.IsAny<string?>(),
                BackupStrategyKind.Tenant,
                false,
                It.IsAny<CancellationToken>(),
                It.IsAny<DateTime?>()),
            Times.Once);
    }

    [Fact]
    public void MergeAndRead_incremental_metadata_round_trips()
    {
        var since = new DateTime(2026, 7, 1, 2, 0, 0, DateTimeKind.Utc);
        var json = """{"schemaVersion":4,"scope":"backup_run"}""";
        var merged = BackupIncrementalPackageMetadata.MergeIntoConfigSnapshot(json, since);
        Assert.True(BackupIncrementalPackageMetadata.TryReadIncrementalSinceUtc(merged, out var read));
        Assert.Equal(since, read);
    }
}

public sealed class TenantScopedBackupExporterIncrementalTests
{
    [Fact]
    public async Task ExportAsync_with_changedSinceUtc_excludes_older_products()
    {
        var tenantId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"incr_export_{Guid.NewGuid():N}")
            .Options;
        await using var db = new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));

        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "A",
            Slug = "a",
            IsActive = true,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        });
        var since = DateTime.UtcNow.AddHours(-1);
        db.Products.AddRange(
            new Product
            {
                Id = Guid.NewGuid(),
                Name = "Old",
                TenantId = tenantId,
                Price = 1,
                IsActive = true,
                CreatedAt = since.AddDays(-1),
                UpdatedAt = since.AddDays(-1)
            },
            new Product
            {
                Id = Guid.NewGuid(),
                Name = "New",
                TenantId = tenantId,
                Price = 2,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
        await db.SaveChangesAsync();

        var zipPath = Path.Combine(Path.GetTempPath(), $"tenant-incr-{Guid.NewGuid():N}.zip");
        try
        {
            var exporter = new TenantScopedBackupExporter();
            var result = await exporter.ExportAsync(db, tenantId, "a", zipPath, default, since);

            Assert.Equal("regkasse.tenant-backup.incremental.v1", result.Manifest.Format);
            Assert.Equal(1, result.Manifest.TableRowCounts["products.json"]);
        }
        finally
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);
        }
    }
}
