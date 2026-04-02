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
        BackupExternalArchiveBackendDescriptor? externalArchiveBackend,
        BackupExecutionAdapterKind? effectiveExecutionAdapterKind = null,
        AdminBackupRuntimeExecutionMode adminRuntimeExecutionMode = AdminBackupRuntimeExecutionMode.InheritFromConfiguration)
    {
        var archiveBackend = externalArchiveBackend ?? BackupExternalArchiveBackendDescriptors.AssumedWhenCallerOmitsRegistration;
        var adapterKind = effectiveExecutionAdapterKind ?? options.ExecutionAdapterKind;

        var issues = new List<string>();
        var diagnostics = new List<BackupConfigurationDiagnostic>();
        var level = BackupConfigurationHealthLevel.Healthy;

        void AddIssue(BackupConfigurationHealthLevel minLevel, string message, BackupConfigurationDiagnostic? diagnostic = null)
        {
            issues.Add(message);
            if (diagnostic != null)
                diagnostics.Add(diagnostic);
            if ((int)minLevel > (int)level)
                level = minLevel;
        }

        if (options.OrchestratorPollingInterval < TimeSpan.FromSeconds(1))
            AddIssue(BackupConfigurationHealthLevel.Unhealthy, "Backup:OrchestratorPollingInterval must be at least 00:00:01.",
                new BackupConfigurationDiagnostic
                {
                    Code = BackupConfigurationDiagnosticCodes.OrchestratorPollingTooShort,
                    Severity = BackupConfigurationDiagnosticSeverity.Error,
                    Message = "Backup:OrchestratorPollingInterval must be at least 00:00:01.",
                    RelatedConfigurationKeys = new[] { "Backup:OrchestratorPollingInterval" }
                });

        if (options.OrchestratorPollingInterval > TimeSpan.FromHours(24))
            AddIssue(BackupConfigurationHealthLevel.Unhealthy, "Backup:OrchestratorPollingInterval exceeds 24 hours (misconfiguration).",
                new BackupConfigurationDiagnostic
                {
                    Code = BackupConfigurationDiagnosticCodes.OrchestratorPollingTooLong,
                    Severity = BackupConfigurationDiagnosticSeverity.Error,
                    Message = "Backup:OrchestratorPollingInterval exceeds 24 hours (misconfiguration).",
                    RelatedConfigurationKeys = new[] { "Backup:OrchestratorPollingInterval" }
                });

        if (IsProductionLikeEnvironment(environment))
        {
            if (adapterKind == BackupExecutionAdapterKind.Fake)
            {
                if (!options.AcknowledgeFakeBackupAdapterOutsideDevelopment)
                {
                    AddIssue(BackupConfigurationHealthLevel.Unhealthy,
                        "Backup:ExecutionAdapterKind=Fake is not allowed in production-like environments without Backup:AcknowledgeFakeBackupAdapterOutsideDevelopment=true (simulated artifacts only; no PostgreSQL logical backup).",
                        new BackupConfigurationDiagnostic
                        {
                            Code = BackupConfigurationDiagnosticCodes.FakeAdapterForbiddenProduction,
                            Severity = BackupConfigurationDiagnosticSeverity.Error,
                            Message =
                                "Backup:ExecutionAdapterKind=Fake is not allowed in production-like environments without Backup:AcknowledgeFakeBackupAdapterOutsideDevelopment=true (simulated artifacts only; no PostgreSQL logical backup).",
                            RelatedConfigurationKeys = new[] { "Backup:ExecutionAdapterKind", "Backup:AcknowledgeFakeBackupAdapterOutsideDevelopment" }
                        });
                }
                else
                {
                    AddIssue(BackupConfigurationHealthLevel.Degraded,
                        "Backup:ExecutionAdapterKind=Fake in production-like environment — simulated artifacts only; operator set Backup:AcknowledgeFakeBackupAdapterOutsideDevelopment=true (no PostgreSQL logical backup).",
                        new BackupConfigurationDiagnostic
                        {
                            Code = BackupConfigurationDiagnosticCodes.FakeAdapterAcknowledgedProduction,
                            Severity = BackupConfigurationDiagnosticSeverity.Warning,
                            Message =
                                "Backup:ExecutionAdapterKind=Fake in production-like environment — simulated artifacts only; operator set Backup:AcknowledgeFakeBackupAdapterOutsideDevelopment=true (no PostgreSQL logical backup).",
                            RelatedConfigurationKeys = new[] { "Backup:ExecutionAdapterKind", "Backup:AcknowledgeFakeBackupAdapterOutsideDevelopment" }
                        });
                }
            }

            if (adapterKind == BackupExecutionAdapterKind.ProductionStub)
            {
                if (!options.AcknowledgePhase1NoRealBackup)
                {
                    AddIssue(BackupConfigurationHealthLevel.Unhealthy,
                        "Backup:ExecutionAdapterKind=ProductionStub requires Backup:AcknowledgePhase1NoRealBackup=true in production-like environments — ProductionStub does not run pg_dump or produce a real PostgreSQL logical backup.",
                        new BackupConfigurationDiagnostic
                        {
                            Code = BackupConfigurationDiagnosticCodes.ProductionStubForbiddenProduction,
                            Severity = BackupConfigurationDiagnosticSeverity.Error,
                            Message =
                                "Backup:ExecutionAdapterKind=ProductionStub requires Backup:AcknowledgePhase1NoRealBackup=true in production-like environments — ProductionStub does not run pg_dump or produce a real PostgreSQL logical backup.",
                            RelatedConfigurationKeys = new[] { "Backup:ExecutionAdapterKind", "Backup:AcknowledgePhase1NoRealBackup" }
                        });
                }
                else
                {
                    AddIssue(BackupConfigurationHealthLevel.Degraded,
                        "Backup:ExecutionAdapterKind=ProductionStub — no pg_dump / real PostgreSQL logical backup; operator set Backup:AcknowledgePhase1NoRealBackup=true; switch to PgDump for real dumps.",
                        new BackupConfigurationDiagnostic
                        {
                            Code = BackupConfigurationDiagnosticCodes.ProductionStubAcknowledgedProduction,
                            Severity = BackupConfigurationDiagnosticSeverity.Warning,
                            Message =
                                "Backup:ExecutionAdapterKind=ProductionStub — no pg_dump / real PostgreSQL logical backup; operator set Backup:AcknowledgePhase1NoRealBackup=true; switch to PgDump for real dumps.",
                            RelatedConfigurationKeys = new[] { "Backup:ExecutionAdapterKind", "Backup:AcknowledgePhase1NoRealBackup" }
                        });
                }
            }
        }

        if (adapterKind == BackupExecutionAdapterKind.PgDump
            && string.IsNullOrWhiteSpace(options.ArtifactStagingRoot))
        {
            AddIssue(BackupConfigurationHealthLevel.Unhealthy,
                "Backup:ArtifactStagingRoot is required when ExecutionAdapterKind=PgDump.",
                new BackupConfigurationDiagnostic
                {
                    Code = BackupConfigurationDiagnosticCodes.PgDumpStagingRootMissing,
                    Severity = BackupConfigurationDiagnosticSeverity.Error,
                    Message = "Backup:ArtifactStagingRoot is required when ExecutionAdapterKind=PgDump.",
                    RelatedConfigurationKeys = new[] { "Backup:ArtifactStagingRoot", "Backup:ExecutionAdapterKind" }
                });
        }

        if (adapterKind == BackupExecutionAdapterKind.PgDump
            && !string.IsNullOrWhiteSpace(options.ArtifactStagingRoot)
            && !environment.IsDevelopment())
        {
            var root = options.ArtifactStagingRoot.Trim();
            if (!Path.IsPathRooted(root))
                AddIssue(BackupConfigurationHealthLevel.Unhealthy,
                    "Backup:ArtifactStagingRoot must be an absolute path in non-Development when ExecutionAdapterKind=PgDump.",
                    new BackupConfigurationDiagnostic
                    {
                        Code = BackupConfigurationDiagnosticCodes.PgDumpStagingRootNotAbsolute,
                        Severity = BackupConfigurationDiagnosticSeverity.Error,
                        Message =
                            "Backup:ArtifactStagingRoot must be an absolute path in non-Development when ExecutionAdapterKind=PgDump.",
                        RelatedConfigurationKeys = new[] { "Backup:ArtifactStagingRoot" }
                    });

            if (!options.VerifyLogicalDumpFileOnDisk)
                AddIssue(BackupConfigurationHealthLevel.Unhealthy,
                    "Backup:VerifyLogicalDumpFileOnDisk must be true in non-Development when ExecutionAdapterKind=PgDump (Succeeded requires on-disk SHA-256 re-hash, not metadata-only).",
                    new BackupConfigurationDiagnostic
                    {
                        Code = BackupConfigurationDiagnosticCodes.PgDumpVerifyOnDiskRequired,
                        Severity = BackupConfigurationDiagnosticSeverity.Error,
                        Message =
                            "Backup:VerifyLogicalDumpFileOnDisk must be true in non-Development when ExecutionAdapterKind=PgDump (Succeeded requires on-disk SHA-256 re-hash, not metadata-only).",
                        RelatedConfigurationKeys = new[] { "Backup:VerifyLogicalDumpFileOnDisk", "Backup:ExecutionAdapterKind" }
                    });

            if (string.IsNullOrWhiteSpace(options.ExternalArchiveRoot))
                AddIssue(BackupConfigurationHealthLevel.Unhealthy,
                    "Backup:ExternalArchiveRoot is required in non-Development when ExecutionAdapterKind=PgDump (external copy + post-copy checksum is mandatory).",
                    new BackupConfigurationDiagnostic
                    {
                        Code = BackupConfigurationDiagnosticCodes.PgDumpExternalArchiveRequired,
                        Severity = BackupConfigurationDiagnosticSeverity.Error,
                        Message =
                            "Backup:ExternalArchiveRoot is required in non-Development when ExecutionAdapterKind=PgDump (external copy + post-copy checksum is mandatory).",
                        RelatedConfigurationKeys = new[] { "Backup:ExternalArchiveRoot", "Backup:ExecutionAdapterKind" }
                    });
        }

        if (adapterKind == BackupExecutionAdapterKind.PgDump
            && environment.IsDevelopment()
            && string.IsNullOrWhiteSpace(options.ExternalArchiveRoot))
        {
            AddIssue(BackupConfigurationHealthLevel.Degraded,
                "Backup:ExternalArchiveRoot not set — PgDump runs skip external archive copy in Development; production requires ExternalArchiveRoot (operational health is not green).",
                new BackupConfigurationDiagnostic
                {
                    Code = BackupConfigurationDiagnosticCodes.DevExternalArchiveNotSet,
                    Severity = BackupConfigurationDiagnosticSeverity.Warning,
                    Message =
                        "Backup:ExternalArchiveRoot not set — PgDump runs skip external archive copy in Development; production requires ExternalArchiveRoot (operational health is not green).",
                    RelatedConfigurationKeys = new[] { "Backup:ExternalArchiveRoot" }
                });
        }

        if (adapterKind == BackupExecutionAdapterKind.PgDump
            && !string.IsNullOrWhiteSpace(options.ExternalArchiveRoot)
            && !Path.IsPathRooted(options.ExternalArchiveRoot.Trim()))
        {
            AddIssue(BackupConfigurationHealthLevel.Unhealthy,
                "Backup:ExternalArchiveRoot must be an absolute path when set.",
                new BackupConfigurationDiagnostic
                {
                    Code = BackupConfigurationDiagnosticCodes.ExternalArchiveRootNotAbsolute,
                    Severity = BackupConfigurationDiagnosticSeverity.Error,
                    Message = "Backup:ExternalArchiveRoot must be an absolute path when set.",
                    RelatedConfigurationKeys = new[] { "Backup:ExternalArchiveRoot" }
                });
        }

        // Harici arşiv immutability (DR): Development dışında zorunlu bayrak + operatör beyanı.
        if (!environment.IsDevelopment()
            && adapterKind == BackupExecutionAdapterKind.PgDump
            && !string.IsNullOrWhiteSpace(options.ExternalArchiveRoot)
            && options.RequireExternalArchiveImmutableTarget
            && !options.ExternalArchiveImmutabilityAcknowledged)
        {
            AddIssue(BackupConfigurationHealthLevel.Unhealthy,
                "Backup:RequireExternalArchiveImmutableTarget=true requires Backup:ExternalArchiveImmutabilityAcknowledged=true after configuring an immutable external archive tier (e.g. object lock / WORM). The API cannot verify storage immutability.",
                new BackupConfigurationDiagnostic
                {
                    Code = BackupConfigurationDiagnosticCodes.ExternalArchiveImmutabilityMismatch,
                    Severity = BackupConfigurationDiagnosticSeverity.Error,
                    Message =
                        "Backup:RequireExternalArchiveImmutableTarget=true requires Backup:ExternalArchiveImmutabilityAcknowledged=true after configuring an immutable external archive tier (e.g. object lock / WORM). The API cannot verify storage immutability.",
                    RelatedConfigurationKeys = new[]
                    {
                        "Backup:RequireExternalArchiveImmutableTarget",
                        "Backup:ExternalArchiveImmutabilityAcknowledged"
                    }
                });
        }

        // Harici arşiv yolu var; immutable zorunluluğu kapalıysa operatör ya WORM beyanı ya da mutable kabulü vermeli.
        if (!environment.IsDevelopment()
            && adapterKind == BackupExecutionAdapterKind.PgDump
            && !string.IsNullOrWhiteSpace(options.ExternalArchiveRoot)
            && !options.RequireExternalArchiveImmutableTarget
            && !options.ExternalArchiveImmutabilityAcknowledged
            && !options.ExternalArchiveMutableTargetAccepted)
        {
            AddIssue(BackupConfigurationHealthLevel.Degraded,
                "Backup: ExternalArchiveRoot is set for PgDump in a production-like environment, but operator disposition is missing — set Backup:ExternalArchiveImmutabilityAcknowledged=true after configuring an immutable tier, or set Backup:ExternalArchiveMutableTargetAccepted=true to explicitly accept a mutable external target, or enable Backup:RequireExternalArchiveImmutableTarget with acknowledgment for WORM/object-lock posture.",
                new BackupConfigurationDiagnostic
                {
                    Code = BackupConfigurationDiagnosticCodes.ExternalArchiveOperatorDispositionMissing,
                    Severity = BackupConfigurationDiagnosticSeverity.Warning,
                    Message =
                        "Backup: ExternalArchiveRoot is set for PgDump in a production-like environment, but operator disposition is missing — set Backup:ExternalArchiveImmutabilityAcknowledged=true after configuring an immutable tier, or set Backup:ExternalArchiveMutableTargetAccepted=true to explicitly accept a mutable external target, or enable Backup:RequireExternalArchiveImmutableTarget with acknowledgment for WORM/object-lock posture.",
                    RelatedConfigurationKeys = new[]
                    {
                        "Backup:ExternalArchiveImmutabilityAcknowledged",
                        "Backup:ExternalArchiveMutableTargetAccepted",
                        "Backup:RequireExternalArchiveImmutableTarget"
                    }
                });
        }

        if (!environment.IsDevelopment()
            && adapterKind == BackupExecutionAdapterKind.PgDump
            && !string.IsNullOrWhiteSpace(options.ExternalArchiveRoot)
            && options.RequireExternalArchiveImmutableTarget
            && options.ExternalArchiveImmutabilityAcknowledged
            && !archiveBackend.ApplicationEnforcesStorageImmutability)
        {
            AddIssue(BackupConfigurationHealthLevel.Degraded,
                "Backup:RequireExternalArchiveImmutableTarget with ExternalArchiveImmutabilityAcknowledged is configuration attestation only for the current registered external archive backend (filesystem copy + post-copy SHA-256). The API does not verify WORM/object-lock. Ensure destination storage enforces immutability below this layer, or adopt a future object-lock-capable archive backend when available.",
                new BackupConfigurationDiagnostic
                {
                    Code = BackupConfigurationDiagnosticCodes.ExternalArchiveImmutableAttestationOnly,
                    Severity = BackupConfigurationDiagnosticSeverity.Warning,
                    Message =
                        "Backup:RequireExternalArchiveImmutableTarget with ExternalArchiveImmutabilityAcknowledged is configuration attestation only for the current registered external archive backend (filesystem copy + post-copy SHA-256). The API does not verify WORM/object-lock. Ensure destination storage enforces immutability below this layer, or adopt a future object-lock-capable archive backend when available.",
                    RelatedConfigurationKeys = new[] { "Backup:RequireExternalArchiveImmutableTarget", "Backup:ExternalArchiveImmutabilityAcknowledged" }
                });
        }

        // PgDump requires a resolvable Npgsql connection string at runtime; surface missing/invalid CS in admin health
        // (Development uses Degraded so local dev still sees the issue without matching Production gate severity).
        if (adapterKind == BackupExecutionAdapterKind.PgDump && configuration != null)
        {
            var connName = string.IsNullOrWhiteSpace(options.LogicalDumpConnectionStringName)
                ? "DefaultConnection"
                : options.LogicalDumpConnectionStringName.Trim();
            var cs = configuration.GetConnectionString(connName);
            var strictConn = !environment.IsDevelopment();
            if (string.IsNullOrWhiteSpace(cs))
            {
                var msgMissing = strictConn
                    ? $"Connection string '{connName}' is missing; required for Backup:ExecutionAdapterKind=PgDump in non-Development."
                    : $"Development: connection string '{connName}' is missing — PgDump runs will fail until ConnectionStrings:{connName} is configured.";
                AddIssue(
                    strictConn ? BackupConfigurationHealthLevel.Unhealthy : BackupConfigurationHealthLevel.Degraded,
                    msgMissing,
                    new BackupConfigurationDiagnostic
                    {
                        Code = BackupConfigurationDiagnosticCodes.PgDumpConnectionStringMissing,
                        Severity = strictConn
                            ? BackupConfigurationDiagnosticSeverity.Error
                            : BackupConfigurationDiagnosticSeverity.Warning,
                        Message = msgMissing,
                        RelatedConfigurationKeys = new[] { $"ConnectionStrings:{connName}", "Backup:LogicalDumpConnectionStringName", "Backup:ExecutionAdapterKind" }
                    });
            }
            else
            {
                try
                {
                    var b = new NpgsqlConnectionStringBuilder(cs);
                    if (string.IsNullOrWhiteSpace(b.Host) || string.IsNullOrWhiteSpace(b.Username)
                                                         || string.IsNullOrWhiteSpace(b.Database))
                    {
                        var msgIncomplete = strictConn
                            ? $"Connection string '{connName}' must include Host, Username, and Database for PgDump."
                            : $"Development: connection string '{connName}' must include Host, Username, and Database for PgDump.";
                        AddIssue(
                            strictConn ? BackupConfigurationHealthLevel.Unhealthy : BackupConfigurationHealthLevel.Degraded,
                            msgIncomplete,
                            new BackupConfigurationDiagnostic
                            {
                                Code = BackupConfigurationDiagnosticCodes.PgDumpConnectionStringIncomplete,
                                Severity = strictConn
                                    ? BackupConfigurationDiagnosticSeverity.Error
                                    : BackupConfigurationDiagnosticSeverity.Warning,
                                Message = msgIncomplete,
                                RelatedConfigurationKeys = new[] { $"ConnectionStrings:{connName}", "Backup:LogicalDumpConnectionStringName" }
                            });
                    }
                }
                catch (ArgumentException)
                {
                    var msgInvalid = strictConn
                        ? $"Connection string '{connName}' is not a valid Npgsql connection string."
                        : $"Development: connection string '{connName}' is not a valid Npgsql connection string.";
                    AddIssue(
                        strictConn ? BackupConfigurationHealthLevel.Unhealthy : BackupConfigurationHealthLevel.Degraded,
                        msgInvalid,
                        new BackupConfigurationDiagnostic
                        {
                            Code = BackupConfigurationDiagnosticCodes.PgDumpConnectionStringInvalid,
                            Severity = strictConn
                                ? BackupConfigurationDiagnosticSeverity.Error
                                : BackupConfigurationDiagnosticSeverity.Warning,
                            Message = msgInvalid,
                            RelatedConfigurationKeys = new[] { $"ConnectionStrings:{connName}", "Backup:LogicalDumpConnectionStringName" }
                        });
                }
            }
        }

        if (!options.WorkerEnabled)
            AddIssue(BackupConfigurationHealthLevel.Degraded, "Backup:WorkerEnabled=false — queued runs will not be processed.",
                new BackupConfigurationDiagnostic
                {
                    Code = BackupConfigurationDiagnosticCodes.WorkerDisabled,
                    Severity = BackupConfigurationDiagnosticSeverity.Warning,
                    Message = "Backup:WorkerEnabled=false — queued runs will not be processed.",
                    RelatedConfigurationKeys = new[] { "Backup:WorkerEnabled" }
                });

        if (!options.OrchestratorDistributedLockEnabled && !environment.IsDevelopment())
            AddIssue(BackupConfigurationHealthLevel.Degraded,
                "Backup:OrchestratorDistributedLockEnabled=false — multiple API replicas can race on the same Queued backup row; enable PostgreSQL advisory lock or run a single worker instance.",
                new BackupConfigurationDiagnostic
                {
                    Code = BackupConfigurationDiagnosticCodes.OrchestratorLockDisabledNonDev,
                    Severity = BackupConfigurationDiagnosticSeverity.Warning,
                    Message =
                        "Backup:OrchestratorDistributedLockEnabled=false — multiple API replicas can race on the same Queued backup row; enable PostgreSQL advisory lock or run a single worker instance.",
                    RelatedConfigurationKeys = new[] { "Backup:OrchestratorDistributedLockEnabled" }
                });

        if (options.DevelopmentForceVerificationFailure && !environment.IsDevelopment())
            AddIssue(BackupConfigurationHealthLevel.Unhealthy, "Backup:DevelopmentForceVerificationFailure=true outside Development.",
                new BackupConfigurationDiagnostic
                {
                    Code = BackupConfigurationDiagnosticCodes.DevelopmentForceVerificationFailureNonDev,
                    Severity = BackupConfigurationDiagnosticSeverity.Error,
                    Message = "Backup:DevelopmentForceVerificationFailure=true outside Development.",
                    RelatedConfigurationKeys = new[] { "Backup:DevelopmentForceVerificationFailure" }
                });

        if (options.RetentionPolicyMode == BackupRetentionPolicyMode.ExecutionPlanned)
        {
            AddIssue(BackupConfigurationHealthLevel.Degraded,
                "Backup:RetentionPolicyMode=ExecutionPlanned — automated artifact deletion is not implemented; Backup:RetentionArtifactDeletionEnabled must remain false until a retention job ships. Policy is recorded for operator planning only.",
                new BackupConfigurationDiagnostic
                {
                    Code = BackupConfigurationDiagnosticCodes.RetentionExecutionPlanned,
                    Severity = BackupConfigurationDiagnosticSeverity.Warning,
                    Message =
                        "Backup:RetentionPolicyMode=ExecutionPlanned — automated artifact deletion is not implemented; Backup:RetentionArtifactDeletionEnabled must remain false until a retention job ships. Policy is recorded for operator planning only.",
                    RelatedConfigurationKeys = new[] { "Backup:RetentionPolicyMode", "Backup:RetentionArtifactDeletionEnabled" }
                });
        }

        if (options.ScheduledBackupEnabled)
        {
            var cron = options.GetEffectiveScheduledBackupCronExpression();
            if (string.IsNullOrWhiteSpace(cron))
            {
                AddIssue(BackupConfigurationHealthLevel.Unhealthy,
                    "Backup:ScheduledBackupEnabled=true requires Backup:ScheduledBackupCron or legacy Backup:ScheduleCronPlaceholder.",
                    new BackupConfigurationDiagnostic
                    {
                        Code = BackupConfigurationDiagnosticCodes.ScheduledBackupCronMissing,
                        Severity = BackupConfigurationDiagnosticSeverity.Error,
                        Message =
                            "Backup:ScheduledBackupEnabled=true requires Backup:ScheduledBackupCron or legacy Backup:ScheduleCronPlaceholder.",
                        RelatedConfigurationKeys = new[] { "Backup:ScheduledBackupEnabled", "Backup:ScheduledBackupCron", "Backup:ScheduleCronPlaceholder" }
                    });
            }
            else if (!CronExpression.TryParse(cron, CronFormat.Standard, out _))
            {
                AddIssue(BackupConfigurationHealthLevel.Unhealthy,
                    "Backup scheduled cron expression is invalid (CronFormat.Standard, five fields).",
                    new BackupConfigurationDiagnostic
                    {
                        Code = BackupConfigurationDiagnosticCodes.ScheduledBackupCronInvalid,
                        Severity = BackupConfigurationDiagnosticSeverity.Error,
                        Message = "Backup scheduled cron expression is invalid (CronFormat.Standard, five fields).",
                        RelatedConfigurationKeys = new[] { "Backup:ScheduledBackupCron" }
                    });
            }
        }

        var reality = MapBackupExecutionReality(adapterKind);
        var realPg = adapterKind == BackupExecutionAdapterKind.PgDump;
        string? nonRealAckKey = null;
        if (IsProductionLikeEnvironment(environment))
        {
            nonRealAckKey = adapterKind switch
            {
                BackupExecutionAdapterKind.Fake when options.AcknowledgeFakeBackupAdapterOutsideDevelopment =>
                    "Backup:AcknowledgeFakeBackupAdapterOutsideDevelopment",
                BackupExecutionAdapterKind.ProductionStub when options.AcknowledgePhase1NoRealBackup =>
                    "Backup:AcknowledgePhase1NoRealBackup",
                _ => null
            };
        }

        var externalReadiness = BuildExternalArchiveReadiness(options, archiveBackend, adapterKind);

        AppendDevelopmentAdapterDiagnostics(options, environment, level, diagnostics, adapterKind);

        return new BackupConfigurationHealthSnapshot
        {
            Level = level,
            Issues = issues,
            Diagnostics = diagnostics,
            EffectiveAdapterKind = adapterKind,
            ConfigurationExecutionAdapterKind = options.ExecutionAdapterKind,
            AdminRuntimeExecutionMode = adminRuntimeExecutionMode,
            WorkerEnabled = options.WorkerEnabled,
            RealPostgreSqlLogicalDumpConfigured = realPg,
            BackupExecutionReality = reality,
            NonRealBackupAdapterAcknowledgmentConfigurationKey = nonRealAckKey,
            ReadinessNarrative = BuildReadinessNarrative(level, realPg, options, environment, nonRealAckKey, adapterKind),
            RetentionReadiness = BackupRetentionReadinessEvaluator.Build(options),
            ExternalArchiveReadiness = externalReadiness
        };
    }

    /// <summary>
    /// Development ortamında gerçek pg_dump yoksa açık bilgi satırı (Fake/ProductionStub veya sağlıklı PgDump seçimi).
    /// </summary>
    private static void AppendDevelopmentAdapterDiagnostics(
        BackupOptions options,
        IHostEnvironment environment,
        BackupConfigurationHealthLevel level,
        List<BackupConfigurationDiagnostic> diagnostics,
        BackupExecutionAdapterKind adapterKind)
    {
        if (!environment.IsDevelopment())
            return;

        if (adapterKind == BackupExecutionAdapterKind.Fake)
        {
            diagnostics.Add(new BackupConfigurationDiagnostic
            {
                Code = BackupConfigurationDiagnosticCodes.DevAdapterFakeNoPgDump,
                Severity = BackupConfigurationDiagnosticSeverity.Information,
                Message =
                    "Development: Backup:ExecutionAdapterKind=Fake — no pg_dump; produced artifacts are simulated stubs. Set Backup:ExecutionAdapterKind=PgDump (and staging + connection string) for real PostgreSQL logical dumps.",
                RelatedConfigurationKeys = new[] { "Backup:ExecutionAdapterKind" }
            });
        }
        else if (adapterKind == BackupExecutionAdapterKind.ProductionStub)
        {
            diagnostics.Add(new BackupConfigurationDiagnostic
            {
                Code = BackupConfigurationDiagnosticCodes.DevAdapterProductionStubNoPgDump,
                Severity = BackupConfigurationDiagnosticSeverity.Information,
                Message =
                    "Development: Backup:ExecutionAdapterKind=ProductionStub — no pg_dump. Use Backup:ExecutionAdapterKind=PgDump for real PostgreSQL logical dumps.",
                RelatedConfigurationKeys = new[] { "Backup:ExecutionAdapterKind", "Backup:AcknowledgePhase1NoRealBackup" }
            });
        }
        else if (adapterKind == BackupExecutionAdapterKind.PgDump
                 && level == BackupConfigurationHealthLevel.Healthy)
        {
            diagnostics.Add(new BackupConfigurationDiagnostic
            {
                Code = BackupConfigurationDiagnosticCodes.RealDumpAdapterSelectedConfigHealthy,
                Severity = BackupConfigurationDiagnosticSeverity.Information,
                Message =
                    "Development: PgDump adapter passes static configuration checks (staging root, connection string shape). A real backup still requires a working pg_dump binary on the host — see BACKUP_SETUP_DEV_PG_DUMP_* diagnostics after startup probe.",
                RelatedConfigurationKeys = new[]
                {
                    "Backup:ExecutionAdapterKind",
                    "Backup:PgDumpExecutablePath",
                    "Backup:LogicalDumpConnectionStringName",
                    "ConnectionStrings"
                }
            });
        }
    }

    private static BackupExternalArchiveReadinessSnapshot BuildExternalArchiveReadiness(
        BackupOptions options,
        BackupExternalArchiveBackendDescriptor backend,
        BackupExecutionAdapterKind adapterKind)
    {
        var pg = adapterKind == BackupExecutionAdapterKind.PgDump;
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
        string? nonRealAckKey,
        BackupExecutionAdapterKind adapterKind)
    {
        var restricted = IsProductionLikeEnvironment(environment);
        if (realPostgreSqlLogicalDumpConfigured && level == BackupConfigurationHealthLevel.Healthy)
            return "Real PostgreSQL logical backup is configured (pg_dump -Fc); required paths and connection checks passed.";

        if (realPostgreSqlLogicalDumpConfigured)
            return "PostgreSQL logical dump adapter is selected but configuration is not fully healthy; review issues.";

        if (!restricted)
            return $"Development: backup adapter {adapterKind} does not perform production PostgreSQL logical dumps.";

        if (level == BackupConfigurationHealthLevel.Unhealthy
            && adapterKind is BackupExecutionAdapterKind.Fake or BackupExecutionAdapterKind.ProductionStub)
        {
            return "Unhealthy: non-real backup adapter in a production-like environment or missing explicit operator acknowledgment — correct configuration before relying on backups.";
        }

        if (nonRealAckKey != null)
            return $"No real PostgreSQL logical backup: adapter is {adapterKind}; explicit acknowledgment is set ({nonRealAckKey}).";

        return $"No real PostgreSQL logical backup: adapter is {adapterKind}.";
    }
}
