using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Backup;

public sealed class BackupOperationalReadinessService : IBackupOperationalReadiness
{
    private readonly IOptionsMonitor<BackupOptions> _options;
    private readonly IHostEnvironment _environment;
    private readonly IConfiguration _configuration;

    public BackupOperationalReadinessService(
        IOptionsMonitor<BackupOptions> options,
        IHostEnvironment environment,
        IConfiguration configuration)
    {
        _options = options;
        _environment = environment;
        _configuration = configuration;
    }

    public BackupConfigurationHealthSnapshot GetConfigurationHealth() =>
        BackupConfigurationEvaluation.Evaluate(_options.CurrentValue, _environment, _configuration);

    public BackupArtifactPipelinePolicySnapshot GetArtifactPipelinePolicy() =>
        BackupArtifactPipelinePolicyEvaluator.Evaluate(_options.CurrentValue, _environment);
}
