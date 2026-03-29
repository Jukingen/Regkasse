using Prometheus;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// backup_orchestrator_distributed_gate_total{outcome=...} ve tutma süresi histogramı.
/// </summary>
public sealed class PrometheusBackupOrchestratorMetrics : IBackupOrchestratorMetrics
{
    private readonly Counter _gateOutcomes;
    private readonly Histogram _lockHoldSeconds;

    public PrometheusBackupOrchestratorMetrics()
    {
        _gateOutcomes = Metrics.CreateCounter(
            "backup_orchestrator_distributed_gate_total",
            "Backup worker distributed gate attempts (PostgreSQL advisory lock or bypass). HTTP layer excluded.",
            new CounterConfiguration { LabelNames = new[] { "outcome" } });

        _lockHoldSeconds = Metrics.CreateHistogram(
            "backup_orchestrator_advisory_lock_hold_seconds",
            "Time the non-pooled advisory lock connection was held (covers dequeue + backup + verification).",
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
