using KasseAPI_Final.Models.Backup;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Inputs for a rough backup duration estimate (operator UI hint — not a SLA).
/// </summary>
public sealed class BackupTimeEstimateRequest
{
    /// <summary>Expected payload size in bytes (0 when unknown).</summary>
    public long DataSizeBytes { get; init; }

    /// <summary>Pipeline / work step count (default: official projector step count).</summary>
    public int StepCount { get; init; } = BackupPipelineProjector.PipelineStepKeysOrdered.Count;

    /// <summary>Historical average of succeeded runs (seconds); preferred over heuristic when set.</summary>
    public double? AverageSucceededDurationSeconds { get; init; }

    public DateTime? StartedAtUtc { get; init; }
    public DateTime? RequestedAtUtc { get; init; }
    public BackupRunStatus? Status { get; init; }

    /// <summary>Clock for remaining-time math; defaults to <see cref="DateTime.UtcNow"/>.</summary>
    public DateTime? UtcNow { get; init; }
}

/// <summary>Result of <see cref="IBackupTimeEstimator"/> — indicative only.</summary>
public sealed class BackupTimeEstimateResult
{
    public const string SourceHistorical = "historical";
    public const string SourceHeuristic = "heuristic";

    public TimeSpan EstimatedTotal { get; init; }

    /// <summary>Null when the run is not in progress or remaining cannot be derived.</summary>
    public TimeSpan? EstimatedRemaining { get; init; }

    /// <summary><see cref="SourceHistorical"/> or <see cref="SourceHeuristic"/>.</summary>
    public string Source { get; init; } = SourceHeuristic;

    public double EstimatedTotalSeconds => EstimatedTotal.TotalSeconds;

    public double? EstimatedRemainingSeconds => EstimatedRemaining?.TotalSeconds;
}

/// <summary>Estimates backup duration from historical averages and/or size + step heuristic.</summary>
public interface IBackupTimeEstimator
{
    /// <summary>
    /// Size + step heuristic (sketch formula with byte-safe units):
    /// <c>seconds ≈ (dataSizeBytes / 1024 / 1000) + (stepCount * 2)</c>, clamped.
    /// </summary>
    TimeSpan EstimateTime(long dataSizeBytes, int stepCount);

    /// <summary>
    /// Prefer historical average total when available; otherwise heuristic.
    /// Remaining time is derived for queued / running / awaiting-verification runs.
    /// </summary>
    BackupTimeEstimateResult Estimate(BackupTimeEstimateRequest request);
}

/// <inheritdoc />
public sealed class BackupTimeEstimator : IBackupTimeEstimator
{
    public const int MinEstimateSeconds = 15;
    public const int MaxEstimateSeconds = 6 * 60 * 60;

    /// <inheritdoc />
    public TimeSpan EstimateTime(long dataSizeBytes, int stepCount)
    {
        if (stepCount < 0)
            throw new ArgumentOutOfRangeException(nameof(stepCount));
        if (dataSizeBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(dataSizeBytes));

        // Sketch used dataSize/1000; treat size as KiB so ~1s per MiB-ish + 2s per step.
        var sizeComponent = dataSizeBytes <= 0
            ? 0
            : dataSizeBytes / 1024.0 / 1000.0;
        var seconds = sizeComponent + (stepCount * 2.0);
        seconds = Math.Clamp(seconds, MinEstimateSeconds, MaxEstimateSeconds);
        return TimeSpan.FromSeconds(seconds);
    }

    /// <inheritdoc />
    public BackupTimeEstimateResult Estimate(BackupTimeEstimateRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        string source;
        TimeSpan total;
        if (request.AverageSucceededDurationSeconds is > 0)
        {
            var hist = Math.Clamp(
                request.AverageSucceededDurationSeconds.Value,
                MinEstimateSeconds,
                MaxEstimateSeconds);
            total = TimeSpan.FromSeconds(hist);
            source = BackupTimeEstimateResult.SourceHistorical;
        }
        else
        {
            total = EstimateTime(request.DataSizeBytes, request.StepCount);
            source = BackupTimeEstimateResult.SourceHeuristic;
        }

        var remaining = ComputeRemaining(
            request.Status,
            request.StartedAtUtc,
            request.RequestedAtUtc,
            total,
            request.UtcNow ?? DateTime.UtcNow);

        return new BackupTimeEstimateResult
        {
            EstimatedTotal = total,
            EstimatedRemaining = remaining,
            Source = source
        };
    }

    private static TimeSpan? ComputeRemaining(
        BackupRunStatus? status,
        DateTime? startedAtUtc,
        DateTime? requestedAtUtc,
        TimeSpan total,
        DateTime utcNow)
    {
        if (status is null)
            return null;

        if (status is BackupRunStatus.Queued)
        {
            // Full estimate while waiting; queue wait is not modeled separately.
            return total;
        }

        if (status is BackupRunStatus.Running or BackupRunStatus.AwaitingVerification)
        {
            var anchor = startedAtUtc ?? requestedAtUtc;
            if (anchor is null)
                return total;

            var elapsed = utcNow - EnsureUtc(anchor.Value);
            if (elapsed < TimeSpan.Zero)
                elapsed = TimeSpan.Zero;

            var left = total - elapsed;
            return left < TimeSpan.Zero ? TimeSpan.Zero : left;
        }

        return null;
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(value, DateTimeKind.Utc)
            : value.ToUniversalTime();
}
