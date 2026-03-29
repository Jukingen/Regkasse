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
    private readonly IBackupArtifactExternalArchive _externalArchive;

    public BackupOperationalReadinessService(
        IOptionsMonitor<BackupOptions> options,
        IHostEnvironment environment,
        IConfiguration configuration,
        IBackupArtifactExternalArchive externalArchive)
    {
        _options = options;
        _environment = environment;
        _configuration = configuration;
        _externalArchive = externalArchive;
    }

    public BackupConfigurationHealthSnapshot GetConfigurationHealth() =>
        BackupConfigurationEvaluation.Evaluate(
            _options.CurrentValue,
            _environment,
            _configuration,
            _externalArchive.BackendDescriptor);

    public BackupArtifactPipelinePolicySnapshot GetArtifactPipelinePolicy() =>
        BackupArtifactPipelinePolicyEvaluator.Evaluate(
            _options.CurrentValue,
            _environment,
            _externalArchive.BackendDescriptor);
}
