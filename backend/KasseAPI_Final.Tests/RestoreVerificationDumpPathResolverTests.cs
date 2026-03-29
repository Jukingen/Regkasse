using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Services.RestoreVerification;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RestoreVerificationDumpPathResolverTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"rv_dump_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    private static BackupRun RunSucc(Guid id, DateTime requestedAt) => new()
    {
        Id = id,
        Status = BackupRunStatus.Succeeded,
        TriggerSource = BackupTriggerSource.Manual,
        AdapterKind = "Fake",
        RequestedAt = requestedAt,
    };

    private static BackupArtifact LogicalDump(Guid runId, string descriptor, DateTime createdAt) => new()
    {
        Id = Guid.NewGuid(),
        BackupRunId = runId,
        ArtifactType = BackupArtifactType.LogicalDump,
        StorageDescriptor = descriptor,
        CreatedAt = createdAt,
    };

    [Fact]
    public async Task Latest_successful_backup_missing_file_falls_back_to_older_successful_run()
    {
        using var db = CreateDb();
        var staging = Path.Combine(Path.GetTempPath(), "rv_dump_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);

        try
        {
            var newer = Guid.NewGuid();
            var older = Guid.NewGuid();
            db.BackupRuns.Add(RunSucc(newer, new DateTime(2026, 3, 3, 0, 0, 0, DateTimeKind.Utc)));
            db.BackupRuns.Add(RunSucc(older, new DateTime(2026, 3, 2, 0, 0, 0, DateTimeKind.Utc)));
            db.BackupArtifacts.Add(LogicalDump(newer, "missing.dump", DateTime.UtcNow));
            db.BackupArtifacts.Add(LogicalDump(older, "present.dump", DateTime.UtcNow.AddMinutes(-1)));
            await db.SaveChangesAsync();

            var presentPath = Path.Combine(staging, "present.dump");
            await File.WriteAllTextAsync(presentPath, "x");

            var query = new BackupRunQueryService(db);
            var ids = await query.GetRecentSucceededRunIdsAsync(5, default);
            var opts = new BackupOptions { ArtifactStagingRoot = staging };

            var result = await RestoreVerificationDumpPathResolver.TryResolveAmongSucceededCandidatesAsync(
                db,
                opts,
                ids,
                NullLogger.Instance,
                default);

            Assert.NotNull(result);
            Assert.Equal(older, result.Value.backupRunId);
            Assert.Equal(presentPath, result.Value.absolutePath);
        }
        finally
        {
            try
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, recursive: true);
            }
            catch
            {
                // test cleanup best-effort
            }
        }
    }

    [Fact]
    public async Task Staging_missing_external_archive_present_succeeds()
    {
        using var db = CreateDb();
        var staging = Path.Combine(Path.GetTempPath(), "rv_dump_stg_" + Guid.NewGuid().ToString("N"));
        var external = Path.Combine(Path.GetTempPath(), "rv_dump_ext_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(external);

        try
        {
            var runId = Guid.NewGuid();
            db.BackupRuns.Add(RunSucc(runId, DateTime.UtcNow));
            db.BackupArtifacts.Add(LogicalDump(runId, "only_external.dump", DateTime.UtcNow));
            await db.SaveChangesAsync();

            var archiveDir = Path.Combine(external, runId.ToString("N"));
            Directory.CreateDirectory(archiveDir);
            var archivedFile = Path.Combine(archiveDir, "only_external.dump");
            await File.WriteAllTextAsync(archivedFile, "dump");

            var query = new BackupRunQueryService(db);
            var ids = await query.GetRecentSucceededRunIdsAsync(5, default);
            var opts = new BackupOptions
            {
                ArtifactStagingRoot = staging,
                ExternalArchiveRoot = external,
            };

            var result = await RestoreVerificationDumpPathResolver.TryResolveAmongSucceededCandidatesAsync(
                db,
                opts,
                ids,
                NullLogger.Instance,
                default);

            Assert.NotNull(result);
            Assert.Equal(runId, result.Value.backupRunId);
            Assert.Equal(archivedFile, result.Value.absolutePath);
        }
        finally
        {
            try
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, recursive: true);
                if (Directory.Exists(external))
                    Directory.Delete(external, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task All_recent_successful_backups_missing_files_returns_null()
    {
        using var db = CreateDb();
        var staging = Path.Combine(Path.GetTempPath(), "rv_dump_empty_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);

        try
        {
            var a = Guid.NewGuid();
            var b = Guid.NewGuid();
            db.BackupRuns.Add(RunSucc(a, DateTime.UtcNow));
            db.BackupRuns.Add(RunSucc(b, DateTime.UtcNow.AddHours(-1)));
            db.BackupArtifacts.Add(LogicalDump(a, "a.dump", DateTime.UtcNow));
            db.BackupArtifacts.Add(LogicalDump(b, "b.dump", DateTime.UtcNow));
            await db.SaveChangesAsync();

            var query = new BackupRunQueryService(db);
            var ids = await query.GetRecentSucceededRunIdsAsync(5, default);
            var opts = new BackupOptions { ArtifactStagingRoot = staging, ExternalArchiveRoot = null };

            var result = await RestoreVerificationDumpPathResolver.TryResolveAmongSucceededCandidatesAsync(
                db,
                opts,
                ids,
                NullLogger.Instance,
                default);

            Assert.Null(result);
        }
        finally
        {
            try
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, recursive: true);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task Fallback_depth_limits_how_far_back_resolver_searches()
    {
        using var db = CreateDb();
        var staging = Path.Combine(Path.GetTempPath(), "rv_dump_depth_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);

        try
        {
            var r1 = Guid.NewGuid();
            var r2 = Guid.NewGuid();
            var r3 = Guid.NewGuid();
            db.BackupRuns.Add(RunSucc(r1, new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc)));
            db.BackupRuns.Add(RunSucc(r2, new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc)));
            db.BackupRuns.Add(RunSucc(r3, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            foreach (var (id, name) in new[] { (r1, "one.dump"), (r2, "two.dump"), (r3, "three.dump") })
            {
                db.BackupArtifacts.Add(LogicalDump(id, name, DateTime.UtcNow));
            }

            await db.SaveChangesAsync();

            var onlyThird = Path.Combine(staging, "three.dump");
            await File.WriteAllTextAsync(onlyThird, "ok");

            var query = new BackupRunQueryService(db);
            var opts = new BackupOptions { ArtifactStagingRoot = staging };

            var idsDepth2 = await query.GetRecentSucceededRunIdsAsync(2, default);
            var shallow = await RestoreVerificationDumpPathResolver.TryResolveAmongSucceededCandidatesAsync(
                db,
                opts,
                idsDepth2,
                NullLogger.Instance,
                default);
            Assert.Null(shallow);

            var idsDepth3 = await query.GetRecentSucceededRunIdsAsync(3, default);
            var deep = await RestoreVerificationDumpPathResolver.TryResolveAmongSucceededCandidatesAsync(
                db,
                opts,
                idsDepth3,
                NullLogger.Instance,
                default);
            Assert.NotNull(deep);
            Assert.Equal(r3, deep.Value.backupRunId);
            Assert.Equal(onlyThird, deep.Value.absolutePath);
        }
        finally
        {
            try
            {
                if (Directory.Exists(staging))
                    Directory.Delete(staging, recursive: true);
            }
            catch
            {
            }
        }
    }
}
