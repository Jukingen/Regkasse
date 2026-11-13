using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class BackupTimeEstimatorTests
{
    private readonly BackupTimeEstimator _sut = new();

    [Fact]
    public void EstimateTime_matches_size_and_step_heuristic()
    {
        // 100 MiB ≈ 100 * 1024 KiB → /1000 ≈ 102.4s + 8*2 = 16 → ~118.4
        var span = _sut.EstimateTime(100L * 1024 * 1024, stepCount: 8);
        Assert.InRange(span.TotalSeconds, 118, 119);
    }

    [Fact]
    public void EstimateTime_clamps_to_minimum_when_tiny()
    {
        var span = _sut.EstimateTime(0, stepCount: 1);
        Assert.Equal(BackupTimeEstimator.MinEstimateSeconds, span.TotalSeconds);
    }

    [Fact]
    public void EstimateTime_rejects_negative_inputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _sut.EstimateTime(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => _sut.EstimateTime(0, -1));
    }

    [Fact]
    public void Estimate_prefers_historical_average_over_heuristic()
    {
        var result = _sut.Estimate(new BackupTimeEstimateRequest
        {
            DataSizeBytes = 500L * 1024 * 1024,
            StepCount = 8,
            AverageSucceededDurationSeconds = 90,
            Status = BackupRunStatus.Queued
        });

        Assert.Equal(BackupTimeEstimateResult.SourceHistorical, result.Source);
        Assert.Equal(90, result.EstimatedTotalSeconds);
        Assert.Equal(90, result.EstimatedRemainingSeconds);
    }

    [Fact]
    public void Estimate_remaining_decreases_after_start()
    {
        var started = new DateTime(2026, 7, 17, 10, 0, 0, DateTimeKind.Utc);
        var now = started.AddSeconds(30);
        var result = _sut.Estimate(new BackupTimeEstimateRequest
        {
            AverageSucceededDurationSeconds = 120,
            Status = BackupRunStatus.Running,
            StartedAtUtc = started,
            UtcNow = now
        });

        Assert.Equal(90, result.EstimatedRemainingSeconds);
    }

    [Fact]
    public void Estimate_terminal_status_has_no_remaining()
    {
        var result = _sut.Estimate(new BackupTimeEstimateRequest
        {
            AverageSucceededDurationSeconds = 60,
            Status = BackupRunStatus.Succeeded
        });

        Assert.Null(result.EstimatedRemainingSeconds);
        Assert.Equal(60, result.EstimatedTotalSeconds);
    }

    [Fact]
    public void Estimate_uses_heuristic_when_no_history()
    {
        var result = _sut.Estimate(new BackupTimeEstimateRequest
        {
            DataSizeBytes = 0,
            StepCount = 8,
            Status = BackupRunStatus.Queued
        });

        Assert.Equal(BackupTimeEstimateResult.SourceHeuristic, result.Source);
        // 0 size + 8*2 = 16s (above MinEstimateSeconds)
        Assert.Equal(16, result.EstimatedTotalSeconds);
    }
}
