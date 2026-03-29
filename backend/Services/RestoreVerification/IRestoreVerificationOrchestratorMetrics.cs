namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>Prometheus: restore verification worker dağıtık kapı (HTTP dışı).</summary>
public interface IRestoreVerificationOrchestratorMetrics
{
    void RecordGateOutcome(string outcome);

    void ObserveLockHoldSeconds(double seconds);
}
