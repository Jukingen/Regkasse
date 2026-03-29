using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.Services.Backup;

/// <summary>
/// Saklama yapılandırmasının operatör yüzeyi; silme yürütmez.
/// </summary>
public static class BackupRetentionReadinessEvaluator
{
    public const string ExecutableStatusDisabled = "disabled";

    public const string ExecutableStatusReportOnly = "report_only_no_automated_enforcement";

    public const string ExecutableStatusExecutionPlannedPending = "execution_planned_pending_implementation";

    public const string ExecutableStatusDeletionFlagBlocked = "deletion_requested_not_implemented";

    public static BackupRetentionReadinessSnapshot Build(BackupOptions options)
    {
        var notes = new List<string>();
        var deletionRequested = options.RetentionArtifactDeletionEnabled;
        const bool deletionImplemented = false;

        switch (options.RetentionPolicyMode)
        {
            case BackupRetentionPolicyMode.Disabled:
                notes.Add("Retention policy is Disabled; ArtifactRetentionDays is not used.");
                return new BackupRetentionReadinessSnapshot
                {
                    Mode = options.RetentionPolicyMode,
                    ArtifactRetentionDays = null,
                    DeletionRequestedByConfiguration = deletionRequested,
                    AutomatedDeletionImplemented = deletionImplemented,
                    ExecutableStatus = ExecutableStatusDisabled,
                    OperatorNotes = notes
                };

            case BackupRetentionPolicyMode.ReportOnly:
                notes.Add(
                    $"Retention policy is ReportOnly: ArtifactRetentionDays={options.ArtifactRetentionDays} — the API records the window only; no automated artifact deletion runs.");
                return new BackupRetentionReadinessSnapshot
                {
                    Mode = options.RetentionPolicyMode,
                    ArtifactRetentionDays = options.ArtifactRetentionDays,
                    DeletionRequestedByConfiguration = deletionRequested,
                    AutomatedDeletionImplemented = deletionImplemented,
                    ExecutableStatus = ExecutableStatusReportOnly,
                    OperatorNotes = notes
                };

            case BackupRetentionPolicyMode.ExecutionPlanned:
                if (deletionRequested)
                {
                    notes.Add(
                        "Backup:RetentionArtifactDeletionEnabled=true is not supported; automated retention deletion is not implemented. This configuration must fail validation.");
                    return new BackupRetentionReadinessSnapshot
                    {
                        Mode = options.RetentionPolicyMode,
                        ArtifactRetentionDays = options.ArtifactRetentionDays,
                        DeletionRequestedByConfiguration = true,
                        AutomatedDeletionImplemented = deletionImplemented,
                        ExecutableStatus = ExecutableStatusDeletionFlagBlocked,
                        OperatorNotes = notes
                    };
                }

                notes.Add(
                    $"Retention policy is ExecutionPlanned with ArtifactRetentionDays={options.ArtifactRetentionDays}: automated deletion is not implemented; Backup:RetentionArtifactDeletionEnabled remains false until a retention job ships.");
                return new BackupRetentionReadinessSnapshot
                {
                    Mode = options.RetentionPolicyMode,
                    ArtifactRetentionDays = options.ArtifactRetentionDays,
                    DeletionRequestedByConfiguration = false,
                    AutomatedDeletionImplemented = deletionImplemented,
                    ExecutableStatus = ExecutableStatusExecutionPlannedPending,
                    OperatorNotes = notes
                };

            default:
                notes.Add("Unknown retention policy mode.");
                return new BackupRetentionReadinessSnapshot
                {
                    Mode = options.RetentionPolicyMode,
                    ArtifactRetentionDays = options.ArtifactRetentionDays,
                    DeletionRequestedByConfiguration = deletionRequested,
                    AutomatedDeletionImplemented = deletionImplemented,
                    ExecutableStatus = "unknown",
                    OperatorNotes = notes
                };
        }
    }
}
