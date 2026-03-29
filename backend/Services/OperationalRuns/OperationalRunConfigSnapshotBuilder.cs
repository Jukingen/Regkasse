using System.Text.Json;
using System.Text.Json.Serialization;
using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.Services.OperationalRuns;

/// <summary>
/// Yedek ve restore drill satırlarına yazılan, gizli bilgi içermeyen yapılandırma anlık görüntüsü (JSON).
/// schemaVersion ile evrilebilir; yalnızca operasyonel bayraklar ve bağlantı <em>adları</em> (değerleri değil).
/// </summary>
public static class OperationalRunConfigSnapshotBuilder
{
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    /// <param name="capturePhase">Örn. backup_manual_enqueue, backup_run_start.</param>
    public static string SerializeBackup(BackupOptions options, string capturePhase, DateTime capturedAtUtc)
    {
        var utc = DateTime.SpecifyKind(capturedAtUtc, DateTimeKind.Utc);
        var payload = new BackupRunConfigSnapshotV1(
            SchemaVersion: CurrentSchemaVersion,
            Scope: "backup_run",
            CapturePhase: capturePhase,
            CapturedAtUtc: utc,
            ExecutionAdapterKind: options.ExecutionAdapterKind.ToString(),
            WorkerEnabled: options.WorkerEnabled,
            OrchestratorDistributedLockEnabled: options.OrchestratorDistributedLockEnabled,
            OrchestratorAdvisoryLockKey1: options.OrchestratorAdvisoryLockKey1,
            OrchestratorAdvisoryLockKey2: options.OrchestratorAdvisoryLockKey2,
            VerifyLogicalDumpFileOnDisk: options.VerifyLogicalDumpFileOnDisk,
            ArtifactStagingRootConfigured: !string.IsNullOrWhiteSpace(options.ArtifactStagingRoot),
            ExternalArchiveRootConfigured: !string.IsNullOrWhiteSpace(options.ExternalArchiveRoot),
            LogicalDumpConnectionStringName: NullIfWhiteSpace(options.LogicalDumpConnectionStringName),
            PgDumpTimeoutSeconds: options.PgDumpTimeoutSeconds,
            PgDumpExecutablePathConfigured: !string.IsNullOrWhiteSpace(options.PgDumpExecutablePath),
            DevelopmentForceVerificationFailure: options.DevelopmentForceVerificationFailure,
            AcknowledgePhase1NoRealBackup: options.AcknowledgePhase1NoRealBackup,
            AcknowledgeFakeBackupAdapterOutsideDevelopment: options.AcknowledgeFakeBackupAdapterOutsideDevelopment,
            ScheduledBackupEnabled: options.ScheduledBackupEnabled,
            ScheduledBackupCronConfigured: !string.IsNullOrWhiteSpace(options.GetEffectiveScheduledBackupCronExpression()),
            ScheduleCronPlaceholderConfigured: !string.IsNullOrWhiteSpace(options.ScheduleCronPlaceholder),
            RetentionDaysPlaceholder: options.RetentionDaysPlaceholder,
            RunLeaseTimeoutSeconds: (int)Math.Clamp(options.RunLeaseTimeout.TotalSeconds, 0, int.MaxValue),
            HeartbeatIntervalSeconds: (int)Math.Clamp(options.HeartbeatInterval.TotalSeconds, 0, int.MaxValue),
            StaleRecoveryScanIntervalSeconds: (int)Math.Clamp(options.StaleRecoveryScanInterval.TotalSeconds, 0, int.MaxValue),
            RetentionPolicyMode: options.RetentionPolicyMode.ToString(),
            ArtifactRetentionDays: options.ArtifactRetentionDays,
            RetentionArtifactDeletionEnabled: options.RetentionArtifactDeletionEnabled,
            RequireExternalArchiveImmutableTarget: options.RequireExternalArchiveImmutableTarget,
            ExternalArchiveImmutabilityAcknowledged: options.ExternalArchiveImmutabilityAcknowledged,
            ExternalArchiveMutableTargetAccepted: options.ExternalArchiveMutableTargetAccepted);

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    /// <param name="capturePhase">Örn. restore_manual_enqueue, restore_scheduled_enqueue, restore_run_start.</param>
    public static string SerializeRestore(RestoreVerificationOptions options, string capturePhase, DateTime capturedAtUtc)
    {
        var utc = DateTime.SpecifyKind(capturedAtUtc, DateTimeKind.Utc);
        var payload = new RestoreVerificationRunConfigSnapshotV1(
            SchemaVersion: CurrentSchemaVersion,
            Scope: "restore_verification_run",
            CapturePhase: capturePhase,
            CapturedAtUtc: utc,
            WorkerEnabled: options.WorkerEnabled,
            DumpFallbackDepth: options.DumpFallbackDepth,
            OrchestratorDistributedLockEnabled: options.OrchestratorDistributedLockEnabled,
            OrchestratorAdvisoryLockKey1: options.OrchestratorAdvisoryLockKey1,
            OrchestratorAdvisoryLockKey2: options.OrchestratorAdvisoryLockKey2,
            ScheduledWeeklyDrillEnabled: options.ScheduledWeeklyDrillEnabled,
            ScheduledProofCadenceDays: options.ScheduledProofCadenceDays,
            IsolatedPgRestoreEnabled: options.IsolatedPgRestoreEnabled,
            IsolatedRestoreAdminConnectionStringName: NullIfWhiteSpace(options.IsolatedRestoreAdminConnectionStringName),
            IsolatedPgRestoreTimeoutSeconds: options.IsolatedPgRestoreTimeoutSeconds,
            FiscalValidationConnectionStringName: NullIfWhiteSpace(options.FiscalValidationConnectionStringName),
            FiscalValidationScriptRelativePath: options.FiscalValidationScriptRelativePath.Trim(),
            IncludeLiveIntegrityChecks: options.IncludeLiveIntegrityChecks,
            IntegrityLookbackDays: options.IntegrityLookbackDays,
            PgRestoreExecutablePathConfigured: !string.IsNullOrWhiteSpace(options.PgRestoreExecutablePath),
            RunLeaseTimeoutSeconds: (int)Math.Clamp(options.RunLeaseTimeout.TotalSeconds, 0, int.MaxValue),
            HeartbeatIntervalSeconds: (int)Math.Clamp(options.HeartbeatInterval.TotalSeconds, 0, int.MaxValue),
            StaleRecoveryScanIntervalSeconds: (int)Math.Clamp(options.StaleRecoveryScanInterval.TotalSeconds, 0, int.MaxValue));

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static string? NullIfWhiteSpace(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return null;
        return s.Trim();
    }

    private sealed record BackupRunConfigSnapshotV1(
        int SchemaVersion,
        string Scope,
        string CapturePhase,
        DateTime CapturedAtUtc,
        string ExecutionAdapterKind,
        bool WorkerEnabled,
        bool OrchestratorDistributedLockEnabled,
        int OrchestratorAdvisoryLockKey1,
        int OrchestratorAdvisoryLockKey2,
        bool VerifyLogicalDumpFileOnDisk,
        bool ArtifactStagingRootConfigured,
        bool ExternalArchiveRootConfigured,
        string? LogicalDumpConnectionStringName,
        int PgDumpTimeoutSeconds,
        bool PgDumpExecutablePathConfigured,
        bool DevelopmentForceVerificationFailure,
        bool AcknowledgePhase1NoRealBackup,
        bool AcknowledgeFakeBackupAdapterOutsideDevelopment,
        bool ScheduledBackupEnabled,
        bool ScheduledBackupCronConfigured,
        bool ScheduleCronPlaceholderConfigured,
        int? RetentionDaysPlaceholder,
        int RunLeaseTimeoutSeconds,
        int HeartbeatIntervalSeconds,
        int StaleRecoveryScanIntervalSeconds,
        string RetentionPolicyMode,
        int? ArtifactRetentionDays,
        bool RetentionArtifactDeletionEnabled,
        bool RequireExternalArchiveImmutableTarget,
        bool ExternalArchiveImmutabilityAcknowledged,
        bool ExternalArchiveMutableTargetAccepted);

    private sealed record RestoreVerificationRunConfigSnapshotV1(
        int SchemaVersion,
        string Scope,
        string CapturePhase,
        DateTime CapturedAtUtc,
        bool WorkerEnabled,
        int DumpFallbackDepth,
        bool OrchestratorDistributedLockEnabled,
        int OrchestratorAdvisoryLockKey1,
        int OrchestratorAdvisoryLockKey2,
        bool ScheduledWeeklyDrillEnabled,
        int ScheduledProofCadenceDays,
        bool IsolatedPgRestoreEnabled,
        string? IsolatedRestoreAdminConnectionStringName,
        int IsolatedPgRestoreTimeoutSeconds,
        string? FiscalValidationConnectionStringName,
        string FiscalValidationScriptRelativePath,
        bool IncludeLiveIntegrityChecks,
        int IntegrityLookbackDays,
        bool PgRestoreExecutablePathConfigured,
        int RunLeaseTimeoutSeconds,
        int HeartbeatIntervalSeconds,
        int StaleRecoveryScanIntervalSeconds);
}
