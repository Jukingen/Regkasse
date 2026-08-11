using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DepExportArchiveServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"DepExportArchive_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options, new FixedTenantAccessor(SystemTenantIds.Platform));
    }

    private sealed class FixedTenantAccessor(Guid tenantId) : ICurrentTenantAccessor
    {
        public Guid? TenantId { get; set; } = tenantId;
        public string? TenantSlug { get; set; }
    }

    private static DepExportArchiveService CreateService(
        AppDbContext db,
        string archiveRoot,
        DepExportArchiveOptions? options = null)
    {
        var opts = options ?? new DepExportArchiveOptions
        {
            Enabled = true,
            ArchiveRootRelativeDirectory = archiveRoot,
            RetentionYears = 7,
            AutoArchiveOnComplete = true,
            PurgeEnabled = true,
        };
        var monitor = new Mock<IOptionsMonitor<DepExportArchiveOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(opts);

        var env = new Mock<IHostEnvironment>();
        env.SetupGet(e => e.ContentRootPath).Returns(Path.GetTempPath());

        return new DepExportArchiveService(
            db,
            monitor.Object,
            env.Object,
            Mock.Of<IDepExportAuditService>(),
            NullLogger<DepExportArchiveService>.Instance);
    }

    private static async Task<DepExportHistory> SeedCompletedAsync(
        AppDbContext db,
        string? storagePath = null,
        DateTime? fromUtc = null)
    {
        TenantTestDoubles.EnsurePlatformTenant(db);
        var row = new DepExportHistory
        {
            TenantId = SystemTenantIds.Platform,
            CashRegisterId = Guid.NewGuid(),
            FromUtc = fromUtc ?? new DateTime(2025, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            ToUtc = new DateTime(2025, 3, 31, 0, 0, 0, DateTimeKind.Utc),
            ExportedAt = DateTime.UtcNow,
            ExportedByUserId = "user-1",
            FileName = "dep-export_test.json",
            FileSizeBytes = 12,
            SignatureCount = 1,
            GroupCount = 1,
            Status = DepExportStatus.Completed.ToString(),
            StoragePath = storagePath,
        };
        db.DepExportHistories.Add(row);
        await db.SaveChangesAsync();
        return row;
    }

    [Fact]
    public async Task ArchiveExportAsync_WritesJsonAndChecksum_WhenNoStoragePath()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dep-arch-{Guid.NewGuid():N}");
        try
        {
            await using var db = CreateDb();
            var row = await SeedCompletedAsync(db);
            var service = CreateService(db, root);
            const string json = """{"Belege-Gruppe":[]}""";

            var result = await service.ArchiveExportAsync(row.Id, json);

            Assert.True(result.Success);
            Assert.False(string.IsNullOrWhiteSpace(result.ArchivePath));
            Assert.True(File.Exists(result.ArchivePath!));
            Assert.Equal(64, result.Checksum!.Length);
            Assert.NotNull(result.RetentionUntil);
            Assert.True(result.RetentionUntil > DateTime.UtcNow.AddYears(6));

            await db.Entry(row).ReloadAsync();
            Assert.NotNull(row.ArchivedAt);
            Assert.Equal(result.Checksum, row.ArchiveChecksum);
            Assert.Equal(result.ArchivePath, row.ArchivePath);
            Assert.Contains(Path.Combine(SystemTenantIds.Platform.ToString("D"), "2025"), row.ArchivePath);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ArchiveExportAsync_CopiesExistingStorageFile()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dep-arch-{Guid.NewGuid():N}");
        var source = Path.Combine(Path.GetTempPath(), $"dep-src-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(source, """{"Belege-Gruppe":[{"Signaturzertifikat":"x"}]}""");
            await using var db = CreateDb();
            var row = await SeedCompletedAsync(db, storagePath: source);
            var service = CreateService(db, root);

            var result = await service.ArchiveExportAsync(row.Id);

            Assert.True(result.Success);
            Assert.True(File.Exists(result.ArchivePath!));
            Assert.Equal(
                await File.ReadAllTextAsync(source),
                await File.ReadAllTextAsync(result.ArchivePath!));
        }
        finally
        {
            if (File.Exists(source)) File.Delete(source);
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task ArchiveExportAsync_IsIdempotent()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dep-arch-{Guid.NewGuid():N}");
        try
        {
            await using var db = CreateDb();
            var row = await SeedCompletedAsync(db);
            var service = CreateService(db, root);

            var first = await service.ArchiveExportAsync(row.Id, "{}");
            var second = await service.ArchiveExportAsync(row.Id, "{}");

            Assert.True(first.Success);
            Assert.True(second.Success);
            Assert.True(second.AlreadyArchived);
            Assert.Equal(first.Checksum, second.Checksum);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task PurgeOldExportsAsync_DeletesFileAndMarksPurged()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dep-arch-{Guid.NewGuid():N}");
        try
        {
            await using var db = CreateDb();
            var row = await SeedCompletedAsync(db);
            var service = CreateService(db, root, new DepExportArchiveOptions
            {
                Enabled = true,
                ArchiveRootRelativeDirectory = root,
                RetentionYears = 7,
                PurgeEnabled = true,
            });

            var archived = await service.ArchiveExportAsync(row.Id, """{"x":1}""");
            Assert.True(archived.Success);

            await db.Entry(row).ReloadAsync();
            row.ArchivedAt = DateTime.UtcNow.AddYears(-8);
            row.RetentionUntil = DateTime.UtcNow.AddDays(-1);
            await db.SaveChangesAsync();

            var purge = await service.PurgeOldExportsAsync(retentionYears: 7);

            Assert.Equal(1, purge.PurgedCount);
            Assert.False(File.Exists(archived.ArchivePath!));
            await db.Entry(row).ReloadAsync();
            Assert.NotNull(row.PurgedAt);
            Assert.Contains("Retention", row.PurgeReason ?? "", StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task GetArchiveReportAsync_AggregatesCounts()
    {
        var root = Path.Combine(Path.GetTempPath(), $"dep-arch-{Guid.NewGuid():N}");
        try
        {
            await using var db = CreateDb();
            var a = await SeedCompletedAsync(db);
            var b = await SeedCompletedAsync(db);
            var service = CreateService(db, root);
            await service.ArchiveExportAsync(a.Id, "{}");

            var report = await service.GetArchiveReportAsync(SystemTenantIds.Platform);

            Assert.Equal(2, report.TotalCompletedExports);
            Assert.Equal(1, report.ArchivedCount);
            Assert.Equal(1, report.PendingArchiveCount);
            Assert.Equal(7, report.RetentionYears);
            Assert.True(report.TotalArchivedSizeBytes >= 0);
            Assert.NotNull(report.OldestArchivedExportAt);
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }
}
