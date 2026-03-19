namespace KasseAPI_Final.Services;

/// <summary>
/// Counters for FinanzOnline submit attempts (total and failed by FailureKind).
/// Exposed via GET /api/admin/finanzonline-reconciliation/metrics for ops/monitoring.
/// </summary>
public interface IFinanzOnlineMetrics
{
    void IncrementSubmitTotal();
    void IncrementSubmitFailed(FinanzOnlineFailureKind kind);
    FinanzOnlineMetricsSnapshot GetSnapshot();
}

public sealed class FinanzOnlineMetricsSnapshot
{
    public long SubmitTotal { get; init; }
    public long SubmitFailedTransient { get; init; }
    public long SubmitFailedPermanent { get; init; }
    public long SubmitFailedUnknown { get; init; }
    public long SubmitFailedTotal => SubmitFailedTransient + SubmitFailedPermanent + SubmitFailedUnknown;
}

/// <summary>
/// In-memory counters; safe for concurrent access. Resets on app restart.
/// When ICoreMetrics is provided, also records to Prometheus (finanzonline_submit_total, finanzonline_submit_failed_total).
/// </summary>
public sealed class FinanzOnlineMetrics : IFinanzOnlineMetrics
{
    private long _submitTotal;
    private long _submitFailedTransient;
    private long _submitFailedPermanent;
    private long _submitFailedUnknown;
    private readonly ICoreMetrics? _coreMetrics;

    public FinanzOnlineMetrics(ICoreMetrics? coreMetrics = null)
    {
        _coreMetrics = coreMetrics;
    }

    public void IncrementSubmitTotal()
    {
        Interlocked.Increment(ref _submitTotal);
        _coreMetrics?.RecordFinanzOnlineSubmit(1);
    }

    public void IncrementSubmitFailed(FinanzOnlineFailureKind kind)
    {
        switch (kind)
        {
            case FinanzOnlineFailureKind.Transient:
                Interlocked.Increment(ref _submitFailedTransient);
                break;
            case FinanzOnlineFailureKind.Permanent:
                Interlocked.Increment(ref _submitFailedPermanent);
                break;
            default:
                Interlocked.Increment(ref _submitFailedUnknown);
                break;
        }
        _coreMetrics?.RecordFinanzOnlineFailed(kind, 1);
    }

    public FinanzOnlineMetricsSnapshot GetSnapshot()
    {
        return new FinanzOnlineMetricsSnapshot
        {
            SubmitTotal = Interlocked.Read(ref _submitTotal),
            SubmitFailedTransient = Interlocked.Read(ref _submitFailedTransient),
            SubmitFailedPermanent = Interlocked.Read(ref _submitFailedPermanent),
            SubmitFailedUnknown = Interlocked.Read(ref _submitFailedUnknown)
        };
    }
}

/// <summary>
/// Optional sink for alerting when FinanzOnline failed count or per-register repeated failures exceed thresholds.
/// Implement and register to push to your monitoring (e.g. webhook, queue). Default: no-op.
/// </summary>
public interface IFinanzOnlineAlertSink
{
    void OnFailedCountThresholdExceeded(int failedCount, int threshold);
    void OnRegisterRepeatedFailure(Guid cashRegisterId, int failureCount);
}

/// <summary>
/// No-op implementation; use when no external alerting is configured.
/// </summary>
public sealed class NoOpFinanzOnlineAlertSink : IFinanzOnlineAlertSink
{
    public void OnFailedCountThresholdExceeded(int failedCount, int threshold) { }
    public void OnRegisterRepeatedFailure(Guid cashRegisterId, int failureCount) { }
}
