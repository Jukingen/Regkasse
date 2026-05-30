using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupRunMetricsFormatterTests
{
    [Fact]
    public void ComputeDurationSeconds_returns_elapsed_when_started_and_completed()
    {
        var started = new DateTime(2026, 5, 30, 10, 0, 0, DateTimeKind.Utc);
        var completed = started.AddMinutes(2).AddSeconds(15);

        var seconds = BackupRunMetricsFormatter.ComputeDurationSeconds(started, completed);

        Assert.Equal(135, seconds);
        Assert.Equal("2m 15s", BackupRunMetricsFormatter.FormatDuration(seconds));
    }

    [Fact]
    public void FormatBytes_uses_invariant_units()
    {
        Assert.Equal("512 B", BackupRunMetricsFormatter.FormatBytes(512));
        Assert.Equal("1.5 KB", BackupRunMetricsFormatter.FormatBytes(1536));
        Assert.Equal("2.00 MB", BackupRunMetricsFormatter.FormatBytes(2 * 1024 * 1024));
    }

    [Fact]
    public void TryComputeCompressionRatio_reads_originalByteSize_from_metadata()
    {
        var artifacts = new[]
        {
            new BackupArtifact
            {
                ArtifactType = BackupArtifactType.LogicalDump,
                ByteSize = 100,
                MetadataJson = """{"originalByteSize":250}"""
            }
        };

        var ratio = BackupRunMetricsFormatter.TryComputeCompressionRatio(artifacts);

        Assert.Equal(250d, ratio);
    }

    [Fact]
    public void BackupRunMapper_populates_size_and_duration_when_artifacts_loaded()
    {
        var started = DateTime.UtcNow.AddMinutes(-5);
        var completed = DateTime.UtcNow;
        var run = new BackupRun
        {
            Id = Guid.NewGuid(),
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
                    ArtifactType = BackupArtifactType.LogicalDump,
                    ByteSize = 2048,
                    StorageDescriptor = "dump.bin",
                    CreatedAt = completed,
                }
            ]
        };

        var dto = BackupRunMapper.ToDto(run, includeChildren: true, materializedChildren: true);

        Assert.Equal(2048, dto.TotalSizeBytes);
        Assert.Equal("2.00 KB", dto.TotalSizeFormatted);
        Assert.NotNull(dto.DurationSeconds);
        Assert.NotNull(dto.DurationFormatted);
        Assert.Single(dto.Artifacts!);
        Assert.Equal("2.00 KB", dto.Artifacts![0].FormattedSize);
        Assert.Equal(completed, dto.Artifacts![0].CreatedAt);
    }
}
