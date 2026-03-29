using Prometheus;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// <c>restore_verification_orchestrator_distributed_gate_total</c> ve tutma süresi histogramı.
/// </summary>
public sealed class PrometheusRestoreVerificationOrchestratorMetrics : IRestoreVerificationOrchestratorMetrics
{
    private readonly Counter _gateOutcomes;
    private readonly Histogram _lockHoldSeconds;

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
    }

    public void RecordGateOutcome(string outcome) =>
        _gateOutcomes.WithLabels(outcome).Inc();

    public void ObserveLockHoldSeconds(double seconds) =>
        _lockHoldSeconds.Observe(seconds);
}
