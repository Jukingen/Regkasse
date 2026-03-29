using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Saklama politikası için periyodik bilgi günlüğü; silme veya dosya işlemi yapmaz.
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
            if (o.RetentionPolicyMode != BackupRetentionPolicyMode.Disabled)
            {
                _logger.LogInformation(
                    "Backup retention policy heartbeat: mode={Mode}, artifactRetentionDays={Days}. No automated artifact deletion is performed by this service.",
                    o.RetentionPolicyMode,
                    o.ArtifactRetentionDays);
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
