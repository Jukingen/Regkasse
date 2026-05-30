using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

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

    private static BackupRunService CreateService(AppDbContext db)
    {
        var config = new ConfigurationBuilder().Build();
        var optionsMock = new Mock<IOptionsMonitor<BackupOptions>>();
        optionsMock.Setup(m => m.CurrentValue).Returns(new BackupOptions());
        return new BackupRunService(db, config, optionsMock.Object, NullLogger<BackupRunService>.Instance);
    }

    private static AppDbContext CreateDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"backup_run_svc_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(opts);
    }
}
