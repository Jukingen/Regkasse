using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupArtifactDownloadServiceTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"dl_{Guid.NewGuid():N}")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task Succeeded_run_with_staging_file_returns_ok()
    {
        using var db = CreateDb();
        var runId = Guid.NewGuid();
        var artId = Guid.NewGuid();
        var staging = Path.Combine(Path.GetTempPath(), "bk_dl_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        var fileName = "dump.sql";
        var full = Path.Combine(staging, fileName);
        await File.WriteAllTextAsync(full, "x");

        try
        {
            db.BackupRuns.Add(new BackupRun
            {
                Id = runId,
                Status = BackupRunStatus.Succeeded,
                TriggerSource = BackupTriggerSource.Manual,
                AdapterKind = "Fake",
                RequestedAt = DateTime.UtcNow,
                StartedAt = DateTime.UtcNow.AddSeconds(-2),
                CompletedAt = DateTime.UtcNow,
            });
            db.BackupArtifacts.Add(new BackupArtifact
            {
                Id = artId,
                BackupRunId = runId,
                ArtifactType = BackupArtifactType.LogicalDump,
                StorageDescriptor = fileName,
                CreatedAt = DateTime.UtcNow,
            });
            await db.SaveChangesAsync();

            var optMon = new Mock<IOptionsMonitor<BackupOptions>>();
            optMon.Setup(m => m.CurrentValue).Returns(new BackupOptions { ArtifactStagingRoot = staging });
            var svc = new BackupArtifactDownloadService(db, optMon.Object, NullLogger<BackupArtifactDownloadService>.Instance);
            var r = await svc.PrepareDownloadAsync(runId, artId);
            Assert.Equal(BackupArtifactDownloadPrepareStatus.Ok, r.Status);
            Assert.Equal(full, r.AbsolutePath);
            Assert.Equal(fileName, r.DownloadFileName);
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
                // best-effort
            }
        }
    }

    [Fact]
    public async Task Non_succeeded_run_returns_run_not_succeeded()
    {
        using var db = CreateDb();
        var runId = Guid.NewGuid();
        var artId = Guid.NewGuid();
        db.BackupRuns.Add(new BackupRun
        {
            Id = runId,
            Status = BackupRunStatus.Running,
            TriggerSource = BackupTriggerSource.Manual,
            AdapterKind = "Fake",
            RequestedAt = DateTime.UtcNow,
        });
        db.BackupArtifacts.Add(new BackupArtifact
        {
            Id = artId,
            BackupRunId = runId,
            ArtifactType = BackupArtifactType.LogicalDump,
            StorageDescriptor = "x.dump",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var optMon = new Mock<IOptionsMonitor<BackupOptions>>();
        optMon.Setup(m => m.CurrentValue).Returns(new BackupOptions { ArtifactStagingRoot = "/tmp" });
        var svc = new BackupArtifactDownloadService(db, optMon.Object, NullLogger<BackupArtifactDownloadService>.Instance);
        var r = await svc.PrepareDownloadAsync(runId, artId);
        Assert.Equal(BackupArtifactDownloadPrepareStatus.RunNotSucceeded, r.Status);
    }
}
