namespace KasseAPI_Final.Services.OperationalRuns;

/// <summary>
/// Stale kurtarma sayaçları ve recoverability kanıt yaşı gauge (Prometheus).
/// </summary>
public interface IDrOperationalObservabilityMetrics
{
    void IncrementStaleRunRecovery(string runKind, string phase);

    /// <summary>Kanıt yoksa -1 gönderilir (Grafana’da filtre).</summary>
    void SetRecoverabilityProofAgeSeconds(string kind, double? ageSeconds);
}
