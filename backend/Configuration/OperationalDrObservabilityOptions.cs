namespace KasseAPI_Final.Configuration;

/// <summary>
/// Recoverability gauge yenileme ve kanıt / yapılandırma risk uyarıları için aralıklar.
/// </summary>
public sealed class OperationalDrObservabilityOptions
{
    public const string SectionName = "OperationalDr:Observability";

    /// <summary>Özet ve gauge güncelleme döngüsü.</summary>
    public TimeSpan RecoverabilityRefreshInterval { get; set; } = TimeSpan.FromMinutes(2);

    public bool EmitProofCadenceRiskAlerts { get; set; } = true;

    public TimeSpan ProofCadenceRiskAlertMinInterval { get; set; } = TimeSpan.FromHours(6);

    public bool EmitUnhealthyRestoreConfigAlerts { get; set; } = true;

    public TimeSpan UnhealthyRestoreConfigAlertMinInterval { get; set; } = TimeSpan.FromHours(6);

    public bool EmitWorkerDisabledScheduledDrillAlerts { get; set; } = true;

    public TimeSpan WorkerDisabledScheduledDrillAlertMinInterval { get; set; } = TimeSpan.FromHours(6);
}
