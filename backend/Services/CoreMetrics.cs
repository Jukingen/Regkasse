using Prometheus;

namespace KasseAPI_Final.Services;

/// <summary>
/// Core fiscal/replay metrics for Grafana dashboards. Exposed via Prometheus /metrics endpoint.
/// </summary>
public interface ICoreMetrics
{
    void RecordReplayTotal(int count = 1);
    void RecordReplayFailed(int count = 1);
    void RecordReplayDuplicate(int count = 1);
    void RecordAdvisoryLockWaitSeconds(double seconds);
    void RecordPayloadHashMismatch(int count = 1);
    /// <summary>Replay resolved an offline intent via structural fallback (hash path did not match).</summary>
    void RecordStructuralFallbackResolved(int count = 1);
    /// <summary>Structural fallback found multiple matches; resolved was skipped (ambiguous).</summary>
    void RecordStructuralFallbackAmbiguous(int count = 1);
    /// <summary>Payload hash repair job: rows skipped due to conflict (report only).</summary>
    void RecordPayloadHashRepairConflict(int count = 1);
    /// <summary>Set completion percent (0..100) from last payload_hash analyze/repair cycle.</summary>
    void SetPayloadHashCompletionPercent(double percent);
    void RecordFinanzOnlineSubmit(int count = 1);
    void RecordFinanzOnlineFailed(FinanzOnlineFailureKind kind, int count = 1);
    /// <summary>Legacy alias route hits: <paramref name="legacyFamily"/> is payment|cart|product; <paramref name="routePattern"/> is normalized request path (GUIDs as {id}).</summary>
    void RecordLegacyRouteHit(string legacyFamily, string routePattern, string httpMethod, int count = 1);
}

/// <summary>
/// Prometheus-backed implementation of core metrics. Safe for concurrent use.
/// </summary>
public sealed class CoreMetrics : ICoreMetrics
{
    private readonly Counter _replayTotal;
    private readonly Counter _replayFailedTotal;
    private readonly Counter _replayDuplicateTotal;
    private readonly Histogram _advisoryLockWaitSeconds;
    private readonly Counter _payloadHashMismatchTotal;
    private readonly Counter _structuralFallbackResolvedTotal;
    private readonly Counter _structuralFallbackAmbiguousTotal;
    private readonly Counter _payloadHashRepairConflictTotal;
    private readonly Gauge _payloadHashCompletionPercent;
    private readonly Counter _finanzonlineSubmitTotal;
    private readonly Counter _finanzonlineSubmitFailedTotal;
    private readonly Counter _legacyRouteHitsTotal;

    public CoreMetrics()
    {
        _replayTotal = global::Prometheus.Metrics.CreateCounter(
            "replay_total",
            "Total number of offline replay items attempted (per item).");

        _replayFailedTotal = global::Prometheus.Metrics.CreateCounter(
            "replay_failed_total",
            "Total number of offline replay items that ended in failed state.");

        _replayDuplicateTotal = global::Prometheus.Metrics.CreateCounter(
            "replay_duplicate_total",
            "Total number of offline replay items that were already synced (idempotent duplicate).");

        _advisoryLockWaitSeconds = global::Prometheus.Metrics.CreateHistogram(
            "advisory_lock_wait_seconds",
            "Time spent waiting to acquire offline replay advisory lock (seconds).",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(0.001, 2, 14) // 1ms to ~8s
            });

        _payloadHashMismatchTotal = global::Prometheus.Metrics.CreateCounter(
            "payload_hash_mismatch_total",
            "Total number of payload_hash mismatches detected and aligned (lazy repair on replay or maintenance).");

        _structuralFallbackResolvedTotal = global::Prometheus.Metrics.CreateCounter(
            "structural_fallback_resolved_total",
            "Number of offline replay items resolved via structural payload match (hash path did not match).");
        _structuralFallbackAmbiguousTotal = global::Prometheus.Metrics.CreateCounter(
            "structural_fallback_ambiguous_total",
            "Number of times structural fallback found multiple matches and skipped resolution.");

        _payloadHashRepairConflictTotal = global::Prometheus.Metrics.CreateCounter(
            "payload_hash_repair_conflicts_total",
            "Number of rows skipped during repair due to (CashRegisterId, canonicalHash) conflict (report only).");
        _payloadHashCompletionPercent = global::Prometheus.Metrics.CreateGauge(
            "payload_hash_completion_percent",
            "Percentage of offline_transactions with aligned payload_hash (0-100). From last repair/analyze cycle.");

        _finanzonlineSubmitTotal = global::Prometheus.Metrics.CreateCounter(
            "finanzonline_submit_total",
            "Total number of FinanzOnline submission attempts.");

        _finanzonlineSubmitFailedTotal = global::Prometheus.Metrics.CreateCounter(
            "finanzonline_submit_failed_total",
            "Total number of FinanzOnline submission failures.",
            new CounterConfiguration { LabelNames = ["failure_kind"] });

        _legacyRouteHitsTotal = global::Prometheus.Metrics.CreateCounter(
            "legacy_route_hits_total",
            "Total number of requests hitting deprecated legacy API aliases (/api/Payment, /api/Cart, /api/Product).",
            new CounterConfiguration { LabelNames = ["legacy_family", "route_pattern", "http_method"] });
    }

    public void RecordReplayTotal(int count = 1)
    {
        if (count > 0)
            _replayTotal.Inc(count);
    }

    public void RecordReplayFailed(int count = 1)
    {
        if (count > 0)
            _replayFailedTotal.Inc(count);
    }

    public void RecordReplayDuplicate(int count = 1)
    {
        if (count > 0)
            _replayDuplicateTotal.Inc(count);
    }

    public void RecordAdvisoryLockWaitSeconds(double seconds)
    {
        if (seconds >= 0)
            _advisoryLockWaitSeconds.Observe(seconds);
    }

    public void RecordPayloadHashMismatch(int count = 1)
    {
        if (count > 0)
            _payloadHashMismatchTotal.Inc(count);
    }

    public void RecordStructuralFallbackResolved(int count = 1)
    {
        if (count > 0)
            _structuralFallbackResolvedTotal.Inc(count);
    }

    public void RecordStructuralFallbackAmbiguous(int count = 1)
    {
        if (count > 0)
            _structuralFallbackAmbiguousTotal.Inc(count);
    }

    public void RecordPayloadHashRepairConflict(int count = 1)
    {
        if (count > 0)
            _payloadHashRepairConflictTotal.Inc(count);
    }

    public void SetPayloadHashCompletionPercent(double percent)
    {
        var value = Math.Clamp(percent, 0, 100);
        _payloadHashCompletionPercent.Set(value);
    }

    public void RecordFinanzOnlineSubmit(int count = 1)
    {
        if (count > 0)
            _finanzonlineSubmitTotal.Inc(count);
    }

    public void RecordFinanzOnlineFailed(FinanzOnlineFailureKind kind, int count = 1)
    {
        if (count <= 0)
            return;
        var label = kind switch
        {
            FinanzOnlineFailureKind.Transient => "transient",
            FinanzOnlineFailureKind.Permanent => "permanent",
            _ => "unknown"
        };
        _finanzonlineSubmitFailedTotal.WithLabels(label).Inc(count);
    }

    public void RecordLegacyRouteHit(string legacyFamily, string routePattern, string httpMethod, int count = 1)
    {
        if (count <= 0)
            return;
        var family = string.IsNullOrWhiteSpace(legacyFamily) ? "unknown" : legacyFamily.Trim().ToLowerInvariant();
        var pattern = string.IsNullOrWhiteSpace(routePattern) ? "unknown" : routePattern;
        var method = string.IsNullOrWhiteSpace(httpMethod) ? "unknown" : httpMethod.ToUpperInvariant();
        _legacyRouteHitsTotal.WithLabels(family, pattern, method).Inc(count);
    }
}
