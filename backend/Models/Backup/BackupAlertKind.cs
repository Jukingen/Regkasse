namespace KasseAPI_Final.Models.Backup;

/// <summary>
/// Contract for future Slack/email sinks; Phase 1: logged via <see cref="Services.Backup.IBackupAlertPublisher"/>.
/// </summary>
public enum BackupAlertKind
{
    BackupFailed = 0,
    VerificationFailed = 1,
    DuplicateExecutionPrevented = 2,
    AdapterUnavailable = 3,
    ScheduleMisconfigured = 4,
    RetentionNotYetEnforced = 5,

    /// <summary>Restore verification drill terminal failure (worker completed with Failed).</summary>
    RestoreVerificationFailed = 6,

    /// <summary>Lease reaper recovered a stuck backup or restore verification run.</summary>
    StaleRunRecovered = 7,

    /// <summary>Scheduled restore proof cadence exceeded, unhealthy config, or worker off while scheduling on.</summary>
    RestoreDrillOperationalRisk = 8,
}
