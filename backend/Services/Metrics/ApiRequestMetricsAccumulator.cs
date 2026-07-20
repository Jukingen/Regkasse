namespace KasseAPI_Final.Services.Metrics;

/// <summary>
/// Process-local API request aggregates for the FA metrics summary (complements Prometheus series).
/// </summary>
public sealed class ApiRequestMetricsAccumulator
{
    private long _totalRequests;
    private long _totalErrors;
    private long _totalDurationMs;

    public void Record(long durationMs, bool isError)
    {
        Interlocked.Increment(ref _totalRequests);
        Interlocked.Add(ref _totalDurationMs, Math.Max(0, durationMs));
        if (isError)
            Interlocked.Increment(ref _totalErrors);
    }

    public (long TotalRequests, long TotalErrors, double AvgResponseTimeMs, double ErrorRatePercent) Snapshot()
    {
        var requests = Interlocked.Read(ref _totalRequests);
        var errors = Interlocked.Read(ref _totalErrors);
        var duration = Interlocked.Read(ref _totalDurationMs);
        var avg = requests > 0 ? (double)duration / requests : 0;
        var errorRate = requests > 0 ? 100.0 * errors / requests : 0;
        return (requests, errors, avg, errorRate);
    }
}
