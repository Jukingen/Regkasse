using Prometheus;

namespace KasseAPI_Final.Services.OperationalRuns;

/// <summary>
/// <c>stale_run_recovery_total</c> ve <c>recoverability_proof_age_seconds</c>.
/// </summary>
public sealed class PrometheusDrOperationalObservabilityMetrics : IDrOperationalObservabilityMetrics
{
    private readonly Counter _staleRecoveries;
    private readonly Gauge _proofAge;

    public PrometheusDrOperationalObservabilityMetrics()
    {
        _staleRecoveries = global::Prometheus.Metrics.CreateCounter(
            "stale_run_recovery_total",
            "Lease-expired operational runs finalized by the stale reaper (backup or restore verification).",
            new CounterConfiguration { LabelNames = new[] { "run_kind", "phase" } });

        _proofAge = global::Prometheus.Metrics.CreateGauge(
            "recoverability_proof_age_seconds",
            "Seconds since last successful backup or restore-verification proof; -1 if no proof yet.",
            new GaugeConfiguration { LabelNames = new[] { "kind" } });
    }

    public void IncrementStaleRunRecovery(string runKind, string phase) =>
        _staleRecoveries.WithLabels(runKind, phase).Inc();

    public void SetRecoverabilityProofAgeSeconds(string kind, double? ageSeconds) =>
        _proofAge.WithLabels(kind).Set(ageSeconds ?? -1);
}
