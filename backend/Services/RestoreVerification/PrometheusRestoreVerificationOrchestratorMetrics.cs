using Prometheus;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// <c>restore_verification_orchestrator_distributed_gate_total</c> ve tutma süresi histogramı.
/// </summary>
public sealed class PrometheusRestoreVerificationOrchestratorMetrics : IRestoreVerificationOrchestratorMetrics
{
    private readonly Counter _gateOutcomes;
    private readonly Histogram _lockHoldSeconds;
    private readonly Counter _runTotal;
    private readonly Histogram _runDurationSeconds;
    private readonly Counter _scheduledEnqueueSuppressed;
    private readonly Counter _workerTickSuppressed;

    public PrometheusRestoreVerificationOrchestratorMetrics()
    {
        _gateOutcomes = Metrics.CreateCounter(
            "restore_verification_orchestrator_distributed_gate_total",
            "Restore verification worker distributed gate (PostgreSQL advisory lock; separate key pair from backup).",
            new CounterConfiguration { LabelNames = new[] { "outcome" } });

        _lockHoldSeconds = Metrics.CreateHistogram(
            "restore_verification_orchestrator_advisory_lock_hold_seconds",
            "Non-pooled advisory lock connection held (weekly enqueue check + single drill run).",
            new HistogramConfiguration
            {
                Buckets = Histogram.ExponentialBuckets(1, 2, 20)
            });

        _runTotal = Metrics.CreateCounter(
            "restore_verification_run_total",
            "Terminal restore verification drill runs (Queued→Succeeded or Failed).",
            new CounterConfiguration { LabelNames = new[] { "status", "trigger_source" } });

        _runDurationSeconds = Metrics.CreateHistogram(
            "restore_verification_run_duration_seconds",
            "Wall time from StartedAt to CompletedAt for terminal restore verification runs.",
            new HistogramConfiguration
            {
                LabelNames = new[] { "status", "trigger_source" },
                Buckets = Histogram.ExponentialBuckets(1, 2, 20)
            });

        _scheduledEnqueueSuppressed = Metrics.CreateCounter(
            "restore_verification_scheduled_enqueue_suppressed_total",
            "Scheduled weekly drill was not enqueued (unhealthy configuration, etc.).",
            new CounterConfiguration { LabelNames = new[] { "reason" } });

        _workerTickSuppressed = Metrics.CreateCounter(
            "restore_verification_worker_tick_suppressed_total",
            "Worker tick skipped before dequeue (e.g. distributed gate could not connect).",
            new CounterConfiguration { LabelNames = new[] { "reason" } });
    }

    public void RecordGateOutcome(string outcome) =>
        _gateOutcomes.WithLabels(outcome).Inc();

    public void ObserveLockHoldSeconds(double seconds) =>
        _lockHoldSeconds.Observe(seconds);

    public void RecordRestoreVerificationRunCompleted(string status, string triggerSource, double durationSeconds)
    {
        _runTotal.WithLabels(status, triggerSource).Inc();
        _runDurationSeconds.WithLabels(status, triggerSource).Observe(durationSeconds);
    }

    public void RecordScheduledEnqueueSuppressed(string reason) =>
        _scheduledEnqueueSuppressed.WithLabels(reason).Inc();

    public void RecordWorkerTickSuppressed(string reason) =>
        _workerTickSuppressed.WithLabels(reason).Inc();
}
