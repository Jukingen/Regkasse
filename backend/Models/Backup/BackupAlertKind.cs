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
}
