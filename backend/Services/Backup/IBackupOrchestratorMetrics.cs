namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Prometheus sayaçları: yedek worker dağıtık kapısı (HTTP dışı).
/// </summary>
public interface IBackupOrchestratorMetrics
{
    void RecordGateOutcome(string outcome);

    void ObserveLockHoldSeconds(double seconds);

    /// <summary>Terminal yedek çalıştırması (Queued→terminal).</summary>
    void RecordBackupRunCompleted(string status, string triggerSource, double durationSeconds);
}
