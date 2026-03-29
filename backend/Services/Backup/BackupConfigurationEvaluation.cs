using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Single place for backup config semantics: startup validator and admin status both use this.
/// </summary>
public static class BackupConfigurationEvaluation
{
    /// <summary>Evaluates backup options without connection-string checks (tests / callers without <see cref="IConfiguration"/>).</summary>
    public static BackupConfigurationHealthSnapshot Evaluate(BackupOptions options, IHostEnvironment environment) =>
        Evaluate(options, environment, configuration: null);

    /// <param name="configuration">When non-null, <see cref="BackupExecutionAdapterKind.PgDump"/> triggers connection-string presence/parsing checks.</param>
    public static BackupConfigurationHealthSnapshot Evaluate(
        BackupOptions options,
        IHostEnvironment environment,
        IConfiguration? configuration)
    {
        var issues = new List<string>();
        var level = BackupConfigurationHealthLevel.Healthy;

        void Add(BackupConfigurationHealthLevel minLevel, string message)
        {
            issues.Add(message);
            if ((int)minLevel > (int)level)
                level = minLevel;
        }

        if (options.OrchestratorPollingInterval < TimeSpan.FromSeconds(1))
            Add(BackupConfigurationHealthLevel.Unhealthy, "Backup:OrchestratorPollingInterval must be at least 00:00:01.");

        if (options.OrchestratorPollingInterval > TimeSpan.FromHours(24))
            Add(BackupConfigurationHealthLevel.Unhealthy, "Backup:OrchestratorPollingInterval exceeds 24 hours (misconfiguration).");

        if (!environment.IsDevelopment())
        {
            if (options.ExecutionAdapterKind == BackupExecutionAdapterKind.Fake)
                Add(BackupConfigurationHealthLevel.Unhealthy,
                    "Backup:ExecutionAdapterKind=Fake is not allowed outside Development (simulated artifacts only).");

            if (options.ExecutionAdapterKind == BackupExecutionAdapterKind.ProductionStub)
            {
                if (!options.AcknowledgePhase1NoRealBackup)
                    Add(BackupConfigurationHealthLevel.Unhealthy,
                        "Backup:ExecutionAdapterKind=ProductionStub in non-Development requires Backup:AcknowledgePhase1NoRealBackup=true (no real PostgreSQL backup is performed).");
                else
                    Add(BackupConfigurationHealthLevel.Degraded,
                        "Backup:ExecutionAdapterKind=ProductionStub — no pg_dump execution; switch to PgDump when ready.");
            }
        }

        if (options.ExecutionAdapterKind == BackupExecutionAdapterKind.PgDump
            && string.IsNullOrWhiteSpace(options.ArtifactStagingRoot))
        {
            Add(BackupConfigurationHealthLevel.Unhealthy,
                "Backup:ArtifactStagingRoot is required when ExecutionAdapterKind=PgDump.");
        }

        if (options.ExecutionAdapterKind == BackupExecutionAdapterKind.PgDump
            && !string.IsNullOrWhiteSpace(options.ArtifactStagingRoot)
            && !environment.IsDevelopment())
        {
            var root = options.ArtifactStagingRoot.Trim();
            if (!Path.IsPathRooted(root))
                Add(BackupConfigurationHealthLevel.Unhealthy,
                    "Backup:ArtifactStagingRoot must be an absolute path in non-Development when ExecutionAdapterKind=PgDump.");

            if (!options.VerifyLogicalDumpFileOnDisk)
                Add(BackupConfigurationHealthLevel.Unhealthy,
                    "Backup:VerifyLogicalDumpFileOnDisk must be true in non-Development when ExecutionAdapterKind=PgDump (Succeeded requires on-disk SHA-256 re-hash, not metadata-only).");

            if (string.IsNullOrWhiteSpace(options.ExternalArchiveRoot))
                Add(BackupConfigurationHealthLevel.Unhealthy,
                    "Backup:ExternalArchiveRoot is required in non-Development when ExecutionAdapterKind=PgDump (external copy + post-copy checksum is mandatory).");
        }

        if (options.ExecutionAdapterKind == BackupExecutionAdapterKind.PgDump
            && environment.IsDevelopment()
            && string.IsNullOrWhiteSpace(options.ExternalArchiveRoot))
        {
            Add(BackupConfigurationHealthLevel.Degraded,
                "Backup:ExternalArchiveRoot not set — PgDump runs skip external archive copy in Development; production requires ExternalArchiveRoot (operational health is not green).");
        }

        if (options.ExecutionAdapterKind == BackupExecutionAdapterKind.PgDump
            && !string.IsNullOrWhiteSpace(options.ExternalArchiveRoot)
            && !Path.IsPathRooted(options.ExternalArchiveRoot.Trim()))
        {
            Add(BackupConfigurationHealthLevel.Unhealthy,
                "Backup:ExternalArchiveRoot must be an absolute path when set.");
        }

        if (options.ExecutionAdapterKind == BackupExecutionAdapterKind.PgDump
            && configuration != null
            && !environment.IsDevelopment())
        {
            var connName = string.IsNullOrWhiteSpace(options.LogicalDumpConnectionStringName)
                ? "DefaultConnection"
                : options.LogicalDumpConnectionStringName.Trim();
            var cs = configuration.GetConnectionString(connName);
            if (string.IsNullOrWhiteSpace(cs))
            {
                Add(BackupConfigurationHealthLevel.Unhealthy,
                    $"Connection string '{connName}' is missing; required for Backup:ExecutionAdapterKind=PgDump in non-Development.");
            }
            else
            {
                try
                {
                    var b = new NpgsqlConnectionStringBuilder(cs);
                    if (string.IsNullOrWhiteSpace(b.Host) || string.IsNullOrWhiteSpace(b.Username)
                                                         || string.IsNullOrWhiteSpace(b.Database))
                        Add(BackupConfigurationHealthLevel.Unhealthy,
                            $"Connection string '{connName}' must include Host, Username, and Database for PgDump.");
                }
                catch (ArgumentException)
                {
                    Add(BackupConfigurationHealthLevel.Unhealthy,
                        $"Connection string '{connName}' is not a valid Npgsql connection string.");
                }
            }
        }

        if (!options.WorkerEnabled)
            Add(BackupConfigurationHealthLevel.Degraded, "Backup:WorkerEnabled=false — queued runs will not be processed.");

        if (!options.OrchestratorDistributedLockEnabled && !environment.IsDevelopment())
            Add(BackupConfigurationHealthLevel.Degraded,
                "Backup:OrchestratorDistributedLockEnabled=false — multiple API replicas can race on the same Queued backup row; enable PostgreSQL advisory lock or run a single worker instance.");

        if (options.DevelopmentForceVerificationFailure && !environment.IsDevelopment())
            Add(BackupConfigurationHealthLevel.Unhealthy, "Backup:DevelopmentForceVerificationFailure=true outside Development.");

        return new BackupConfigurationHealthSnapshot
        {
            Level = level,
            Issues = issues,
            EffectiveAdapterKind = options.ExecutionAdapterKind,
            WorkerEnabled = options.WorkerEnabled
        };
    }
}
