using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupChecksumVerificationServiceTests
{
    [Fact]
    public async Task Verify_WhenChecksumMatches_ReturnsSuccess()
    {
        var dir = Path.Combine(Path.GetTempPath(), "regkasse-checksum-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var fileName = "dump.bin";
            var path = Path.Combine(dir, fileName);
            await File.WriteAllBytesAsync(path, new byte[] { 1, 2, 3, 4 });
            var hash = await new BackupChecksumService().ComputeFileSha256HexAsync(path);

            await using var db = CreateDb();
            var runId = Guid.NewGuid();
            db.BackupRuns.Add(new BackupRun
            {
                Id = runId,
                Status = BackupRunStatus.Succeeded,
                Strategy = BackupStrategyKind.System,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "PgDump",
                RequestedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Artifacts =
                {
                    new BackupArtifact
                    {
                        ArtifactType = BackupArtifactType.LogicalDump,
                        StorageDescriptor = fileName,
                        ContentHashSha256 = hash,
                        CreatedAt = DateTime.UtcNow
                    }
                }
            });
            await db.SaveChangesAsync();

            var sut = CreateSut(db, dir);
            var result = await sut.VerifyChecksumAsync(runId);

            Assert.True(result.IsValid);
            Assert.Null(result.FailureReason);
            Assert.Single(result.Artifacts);
            Assert.Equal("passed", result.Artifacts[0].Status);
            Assert.Equal(1, await db.BackupVerifications.CountAsync(v => v.BackupRunId == runId));
            Assert.Equal(BackupVerificationStatus.Passed,
                (await db.BackupVerifications.SingleAsync(v => v.BackupRunId == runId)).Status);
            Assert.Equal(IBackupChecksumVerificationService.VerifierSourceOnDemandHttp, result.VerifierSource);
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [Fact]
    public async Task Verify_WhenChecksumMismatch_ReturnsFailure()
    {
        var dir = Path.Combine(Path.GetTempPath(), "regkasse-checksum-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var fileName = "dump.bin";
            var path = Path.Combine(dir, fileName);
            await File.WriteAllBytesAsync(path, new byte[] { 9, 9, 9 });

            await using var db = CreateDb();
            var runId = Guid.NewGuid();
            db.BackupRuns.Add(new BackupRun
            {
                Id = runId,
                Status = BackupRunStatus.Succeeded,
                Strategy = BackupStrategyKind.System,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "PgDump",
                RequestedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow,
                Artifacts =
                {
                    new BackupArtifact
                    {
                        ArtifactType = BackupArtifactType.LogicalDump,
                        StorageDescriptor = fileName,
                        ContentHashSha256 = new string('a', 64),
                        CreatedAt = DateTime.UtcNow
                    }
                }
            });
            await db.SaveChangesAsync();

            var sut = CreateSut(db, dir);
            var result = await sut.VerifyChecksumAsync(runId);

            Assert.False(result.IsValid);
            Assert.Equal("failed", result.Artifacts[0].Status);
            Assert.Equal(BackupVerificationStatus.Failed,
                (await db.BackupVerifications.SingleAsync()).Status);
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    [Fact]
    public async Task Verify_WhenHashMissing_ReturnsFailureWithReason()
    {
        await using var db = CreateDb();
        var runId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Succeeded,
            Strategy = BackupStrategyKind.Tenant,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = DateTime.UtcNow,
            Artifacts =
            {
                new BackupArtifact
                {
                    ArtifactType = BackupArtifactType.LogicalDump,
                    StorageDescriptor = "x.dump",
                    ContentHashSha256 = "short",
                    CreatedAt = DateTime.UtcNow
                }
            }
        });
        await db.SaveChangesAsync();

        var sut = CreateSut(db, Path.GetTempPath());
        var result = await sut.VerifyChecksumAsync(runId);

        Assert.False(result.IsValid);
        Assert.Equal("missing_hash", result.Artifacts[0].Status);
        Assert.False(string.IsNullOrWhiteSpace(result.FailureReason));
    }

    [Fact]
    public async Task Verify_WhenFileMissing_ReturnsFailureWithReason()
    {
        var dir = Path.Combine(Path.GetTempPath(), "regkasse-checksum-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            await using var db = CreateDb();
            var runId = Guid.NewGuid();
            db.BackupRuns.Add(new BackupRun
            {
                Id = runId,
                Status = BackupRunStatus.Succeeded,
                Strategy = BackupStrategyKind.System,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "PgDump",
                RequestedAt = DateTime.UtcNow,
                Artifacts =
                {
                    new BackupArtifact
                    {
                        ArtifactType = BackupArtifactType.LogicalDump,
                        StorageDescriptor = "missing.dump",
                        ContentHashSha256 = new string('b', 64),
                        CreatedAt = DateTime.UtcNow
                    }
                }
            });
            await db.SaveChangesAsync();

            var sut = CreateSut(db, dir);
            var result = await sut.VerifyChecksumAsync(runId);

            Assert.False(result.IsValid);
            Assert.Equal("missing_file", result.Artifacts[0].Status);
            Assert.False(string.IsNullOrWhiteSpace(result.FailureReason));
        }
        finally
        {
            TryDeleteDir(dir);
        }
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"checksum_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options, TenantTestDoubles.TenantAccessorReturning(LegacyDefaultTenantIds.Primary));
    }

    private static BackupChecksumVerificationService CreateSut(AppDbContext db, string stagingRoot)
    {
        var opts = Options.Create(new BackupOptions { ArtifactStagingRoot = stagingRoot });
        var monitor = new Mock<IOptionsMonitor<BackupOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(opts.Value);

        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);

        return new BackupChecksumVerificationService(
            db,
            new BackupChecksumService(),
            monitor.Object,
            env.Object,
            NullLogger<BackupChecksumVerificationService>.Instance);
    }

    private static void TryDeleteDir(string dir)
    {
        try
        {
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
