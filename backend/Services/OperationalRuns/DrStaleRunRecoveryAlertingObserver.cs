using System.Collections.Generic;
using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Services.Backup;
using Microsoft.Extensions.Logging;

namespace KasseAPI_Final.Services.OperationalRuns;

/// <summary>
/// Stale kurtarma: Prometheus sayacı + <see cref="IBackupAlertPublisher"/> (log/webhook zinciri).
/// </summary>
public sealed class DrStaleRunRecoveryAlertingObserver : IDrStaleRunRecoveryObserver
{
    private readonly IBackupAlertPublisher _alerts;
    private readonly IDrOperationalObservabilityMetrics _metrics;
    private readonly ILogger<DrStaleRunRecoveryAlertingObserver> _logger;

    public DrStaleRunRecoveryAlertingObserver(
        IBackupAlertPublisher alerts,
        IDrOperationalObservabilityMetrics metrics,
        ILogger<DrStaleRunRecoveryAlertingObserver> logger)
    {
        _alerts = alerts;
        _metrics = metrics;
        _logger = logger;
    }

    public void OnStaleBackupRunRecovered(Guid runId, string phase)
    {
        _metrics.IncrementStaleRunRecovery("backup", phase);
        _alerts.Publish(new BackupAlertEvent(
            BackupAlertKind.StaleRunRecovered,
            runId,
            null,
            $"Stale lease recovery: backup run finalized (phase={phase}).",
            new Dictionary<string, string>
            {
                ["runKind"] = "backup",
                ["phase"] = phase
            }));
    }

    public void OnStaleRestoreVerificationRunRecovered(Guid runId)
    {
        _metrics.IncrementStaleRunRecovery("restore_verification", "running");
        _alerts.Publish(new BackupAlertEvent(
            BackupAlertKind.StaleRunRecovered,
            null,
            null,
            "Stale lease recovery: restore verification run finalized.",
            new Dictionary<string, string> { ["runKind"] = "restore_verification", ["phase"] = "running" },
            RestoreVerificationRunId: runId));
    }
}
