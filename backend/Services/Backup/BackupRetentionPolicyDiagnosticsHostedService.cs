using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Saklama politikası için periyodik bilgi günlüğü; silme veya dosya işlemi yapmaz (yalnızca gözlemlenebilirlik iskeleti).
/// </summary>
public sealed class BackupRetentionPolicyDiagnosticsHostedService : BackgroundService
{
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly ILogger<BackupRetentionPolicyDiagnosticsHostedService> _logger;

    public BackupRetentionPolicyDiagnosticsHostedService(
        IOptionsMonitor<BackupOptions> options,
        ILogger<BackupRetentionPolicyDiagnosticsHostedService> logger)
    {
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var o = _options.CurrentValue;
            var readiness = BackupRetentionReadinessEvaluator.Build(o);
            if (o.RetentionPolicyMode != BackupRetentionPolicyMode.Disabled)
            {
                _logger.LogInformation(
                    "Backup retention policy heartbeat: mode={Mode}, artifactRetentionDays={Days}, executableStatus={ExecutableStatus}, automatedDeletionImplemented={DeletionImplemented}, retentionArtifactDeletionEnabled={DeletionFlag}. No artifact deletion is performed by this hosted service.",
                    o.RetentionPolicyMode,
                    o.ArtifactRetentionDays,
                    readiness.ExecutableStatus,
                    readiness.AutomatedDeletionImplemented,
                    o.RetentionArtifactDeletionEnabled);
                foreach (var line in readiness.OperatorNotes)
                    _logger.LogInformation("Backup retention policy note: {Note}", line);
            }
            else if (o.RetentionArtifactDeletionEnabled)
            {
                _logger.LogWarning(
                    "Backup:RetentionArtifactDeletionEnabled is true while retention mode is Disabled — this should fail options validation at startup.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
