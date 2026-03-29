using Cronos;
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
    public const string BackupExecutionRealityPostgreSqlLogicalDump = "PostgreSqlLogicalDump";

    public const string BackupExecutionRealitySimulatedFake = "SimulatedFake";

    public const string BackupExecutionRealityProductionStubNoPostgreSql = "ProductionStubNoPostgreSqlBackup";

    public const string BackupExecutionRealityUnknown = "Unknown";

    /// <summary>Production, Staging, and any non-Development host profile.</summary>
    public static bool IsProductionLikeEnvironment(IHostEnvironment environment) =>
        !environment.IsDevelopment();

    public static string MapBackupExecutionReality(BackupExecutionAdapterKind kind) =>
        kind switch
        {
            BackupExecutionAdapterKind.PgDump => BackupExecutionRealityPostgreSqlLogicalDump,
            BackupExecutionAdapterKind.Fake => BackupExecutionRealitySimulatedFake,
            BackupExecutionAdapterKind.ProductionStub => BackupExecutionRealityProductionStubNoPostgreSql,
            _ => BackupExecutionRealityUnknown
        };

    /// <summary>Evaluates backup options without connection-string checks (tests / callers without <see cref="IConfiguration"/>).</summary>
    public static BackupConfigurationHealthSnapshot Evaluate(BackupOptions options, IHostEnvironment environment) =>
        Evaluate(options, environment, configuration: null, externalArchiveBackend: null);

    /// <param name="configuration">When non-null, <see cref="BackupExecutionAdapterKind.PgDump"/> triggers connection-string presence/parsing checks.</param>
    public static BackupConfigurationHealthSnapshot Evaluate(
        BackupOptions options,
        IHostEnvironment environment,
        IConfiguration? configuration) =>
        Evaluate(options, environment, configuration, externalArchiveBackend: null);

    /// <param name="externalArchiveBackend">Kayıtlı <see cref="IBackupArtifactExternalArchive"/> tanımlayıcısı; null ise <see cref="BackupExternalArchiveBackendDescriptors.AssumedWhenCallerOmitsRegistration"/>.</param>
    public static BackupConfigurationHealthSnapshot Evaluate(
        BackupOptions options,
        IHostEnvironment environment,
        IConfiguration? configuration,
        BackupExternalArchiveBackendDescriptor? externalArchiveBackend)
    {
        var archiveBackend = externalArchiveBackend ?? BackupExternalArchiveBackendDescriptors.AssumedWhenCallerOmitsRegistration;

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

        if (IsProductionLikeEnvironment(environment))
        {
            if (options.ExecutionAdapterKind == BackupExecutionAdapterKind.Fake)
            {
                if (!options.AcknowledgeFakeBackupAdapterOutsideDevelopment)
                {
                    Add(BackupConfigurationHealthLevel.Unhealthy,
                        "Backup:ExecutionAdapterKind=Fake is not allowed in production-like environments without Backup:AcknowledgeFakeBackupAdapterOutsideDevelopment=true (simulated artifacts only; no PostgreSQL logical backup).");
                }
                else
                {
                    Add(BackupConfigurationHealthLevel.Degraded,
                        "Backup:ExecutionAdapterKind=Fake in production-like environment — simulated artifacts only; operator set Backup:AcknowledgeFakeBackupAdapterOutsideDevelopment=true (no PostgreSQL logical backup).");
                }
            }

            if (options.ExecutionAdapterKind == BackupExecutionAdapterKind.ProductionStub)
            {
                if (!options.AcknowledgePhase1NoRealBackup)
                {
                    Add(BackupConfigurationHealthLevel.Unhealthy,
                        "Backup:ExecutionAdapterKind=ProductionStub requires Backup:AcknowledgePhase1NoRealBackup=true in production-like environments — ProductionStub does not run pg_dump or produce a real PostgreSQL logical backup.");
                }
                else
                {
                    Add(BackupConfigurationHealthLevel.Degraded,
                        "Backup:ExecutionAdapterKind=ProductionStub — no pg_dump / real PostgreSQL logical backup; operator set Backup:AcknowledgePhase1NoRealBackup=true; switch to PgDump for real dumps.");
                }
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

        // Harici arşiv immutability (DR): Development dışında zorunlu bayrak + operatör beyanı.
        if (!environment.IsDevelopment()
            && options.ExecutionAdapterKind == BackupExecutionAdapterKind.PgDump
            && !string.IsNullOrWhiteSpace(options.ExternalArchiveRoot)
            && options.RequireExternalArchiveImmutableTarget
            && !options.ExternalArchiveImmutabilityAcknowledged)
        {
            Add(BackupConfigurationHealthLevel.Unhealthy,
                "Backup:RequireExternalArchiveImmutableTarget=true requires Backup:ExternalArchiveImmutabilityAcknowledged=true after configuring an immutable external archive tier (e.g. object lock / WORM). The API cannot verify storage immutability.");
        }

        // Harici arşiv yolu var; immutable zorunluluğu kapalıysa operatör ya WORM beyanı ya da mutable kabulü vermeli.
        if (!environment.IsDevelopment()
            && options.ExecutionAdapterKind == BackupExecutionAdapterKind.PgDump
            && !string.IsNullOrWhiteSpace(options.ExternalArchiveRoot)
            && !options.RequireExternalArchiveImmutableTarget
            && !options.ExternalArchiveImmutabilityAcknowledged
            && !options.ExternalArchiveMutableTargetAccepted)
        {
            Add(BackupConfigurationHealthLevel.Degraded,
                "Backup: ExternalArchiveRoot is set for PgDump in a production-like environment, but operator disposition is missing — set Backup:ExternalArchiveImmutabilityAcknowledged=true after configuring an immutable tier, or set Backup:ExternalArchiveMutableTargetAccepted=true to explicitly accept a mutable external target, or enable Backup:RequireExternalArchiveImmutableTarget with acknowledgment for WORM/object-lock posture.");
        }

        if (!environment.IsDevelopment()
            && options.ExecutionAdapterKind == BackupExecutionAdapterKind.PgDump
            && !string.IsNullOrWhiteSpace(options.ExternalArchiveRoot)
            && options.RequireExternalArchiveImmutableTarget
            && options.ExternalArchiveImmutabilityAcknowledged
            && !archiveBackend.ApplicationEnforcesStorageImmutability)
        {
            Add(BackupConfigurationHealthLevel.Degraded,
                "Backup:RequireExternalArchiveImmutableTarget with ExternalArchiveImmutabilityAcknowledged is configuration attestation only for the current registered external archive backend (filesystem copy + post-copy SHA-256). The API does not verify WORM/object-lock. Ensure destination storage enforces immutability below this layer, or adopt a future object-lock-capable archive backend when available.");
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

        if (options.RetentionPolicyMode == BackupRetentionPolicyMode.ExecutionPlanned)
        {
            Add(BackupConfigurationHealthLevel.Degraded,
                "Backup:RetentionPolicyMode=ExecutionPlanned — automated artifact deletion is not implemented; Backup:RetentionArtifactDeletionEnabled must remain false until a retention job ships. Policy is recorded for operator planning only.");
        }

        if (options.ScheduledBackupEnabled)
        {
            var cron = options.GetEffectiveScheduledBackupCronExpression();
            if (string.IsNullOrWhiteSpace(cron))
            {
                Add(BackupConfigurationHealthLevel.Unhealthy,
                    "Backup:ScheduledBackupEnabled=true requires Backup:ScheduledBackupCron or legacy Backup:ScheduleCronPlaceholder.");
            }
            else if (!CronExpression.TryParse(cron, CronFormat.Standard, out _))
            {
                Add(BackupConfigurationHealthLevel.Unhealthy,
                    "Backup scheduled cron expression is invalid (CronFormat.Standard, five fields).");
            }
        }

        var reality = MapBackupExecutionReality(options.ExecutionAdapterKind);
        var realPg = options.ExecutionAdapterKind == BackupExecutionAdapterKind.PgDump;
        string? nonRealAckKey = null;
        if (IsProductionLikeEnvironment(environment))
        {
            nonRealAckKey = options.ExecutionAdapterKind switch
            {
                BackupExecutionAdapterKind.Fake when options.AcknowledgeFakeBackupAdapterOutsideDevelopment =>
                    "Backup:AcknowledgeFakeBackupAdapterOutsideDevelopment",
                BackupExecutionAdapterKind.ProductionStub when options.AcknowledgePhase1NoRealBackup =>
                    "Backup:AcknowledgePhase1NoRealBackup",
                _ => null
            };
        }

        var externalReadiness = BuildExternalArchiveReadiness(options, archiveBackend);

        return new BackupConfigurationHealthSnapshot
        {
            Level = level,
            Issues = issues,
            EffectiveAdapterKind = options.ExecutionAdapterKind,
            WorkerEnabled = options.WorkerEnabled,
            RealPostgreSqlLogicalDumpConfigured = realPg,
            BackupExecutionReality = reality,
            NonRealBackupAdapterAcknowledgmentConfigurationKey = nonRealAckKey,
            ReadinessNarrative = BuildReadinessNarrative(level, realPg, options, environment, nonRealAckKey),
            RetentionReadiness = BackupRetentionReadinessEvaluator.Build(options),
            ExternalArchiveReadiness = externalReadiness
        };
    }

    private static BackupExternalArchiveReadinessSnapshot BuildExternalArchiveReadiness(
        BackupOptions options,
        BackupExternalArchiveBackendDescriptor backend)
    {
        var pg = options.ExecutionAdapterKind == BackupExecutionAdapterKind.PgDump;
        var ext = !string.IsNullOrWhiteSpace(options.ExternalArchiveRoot);
        if (!pg || !ext)
        {
            return new BackupExternalArchiveReadinessSnapshot
            {
                RegisteredBackendKind = backend.BackendKind,
                ImmutabilityEnforcement = backend.ImmutabilityEnforcement,
                ApplicationEnforcesStorageImmutability = backend.ApplicationEnforcesStorageImmutability,
                ObjectStorageImmutabilityBackendImplemented = backend.ObjectStorageImmutabilityBackendImplemented,
                CapabilityOperatorNotes = Array.Empty<string>()
            };
        }

        var notes = new List<string>
        {
            $"Registered external archive backend: {backend.BackendKind}. {backend.CapabilitySummaryEnglish}"
        };

        if (!backend.ObjectStorageImmutabilityBackendImplemented)
        {
            notes.Add(
                "Native object-storage immutability integration (e.g. S3 Object Lock as the archive implementation) is not shipped in this API version; the active integration remains filesystem-oriented.");
        }

        if (options.ExternalArchiveImmutabilityAcknowledged && !backend.ApplicationEnforcesStorageImmutability)
        {
            notes.Add(
                "Backup:ExternalArchiveImmutabilityAcknowledged=true is operator attestation — the registered archive backend does not programmatically prove WORM/object-lock.");
        }

        return new BackupExternalArchiveReadinessSnapshot
        {
            RegisteredBackendKind = backend.BackendKind,
            ImmutabilityEnforcement = backend.ImmutabilityEnforcement,
            ApplicationEnforcesStorageImmutability = backend.ApplicationEnforcesStorageImmutability,
            ObjectStorageImmutabilityBackendImplemented = backend.ObjectStorageImmutabilityBackendImplemented,
            CapabilityOperatorNotes = notes
        };
    }

    private static string BuildReadinessNarrative(
        BackupConfigurationHealthLevel level,
        bool realPostgreSqlLogicalDumpConfigured,
        BackupOptions options,
        IHostEnvironment environment,
        string? nonRealAckKey)
    {
        var restricted = IsProductionLikeEnvironment(environment);
        if (realPostgreSqlLogicalDumpConfigured && level == BackupConfigurationHealthLevel.Healthy)
            return "Real PostgreSQL logical backup is configured (pg_dump -Fc); required paths and connection checks passed.";

        if (realPostgreSqlLogicalDumpConfigured)
            return "PostgreSQL logical dump adapter is selected but configuration is not fully healthy; review issues.";

        if (!restricted)
            return $"Development: backup adapter {options.ExecutionAdapterKind} does not perform production PostgreSQL logical dumps.";

        if (level == BackupConfigurationHealthLevel.Unhealthy
            && options.ExecutionAdapterKind is BackupExecutionAdapterKind.Fake or BackupExecutionAdapterKind.ProductionStub)
        {
            return "Unhealthy: non-real backup adapter in a production-like environment or missing explicit operator acknowledgment — correct configuration before relying on backups.";
        }

        if (nonRealAckKey != null)
            return $"No real PostgreSQL logical backup: adapter is {options.ExecutionAdapterKind}; explicit acknowledgment is set ({nonRealAckKey}).";

        return $"No real PostgreSQL logical backup: adapter is {options.ExecutionAdapterKind}.";
    }
}
