using KasseAPI_Final.Models.Backup;
using KasseAPI_Final.Models.RestoreVerification;

namespace KasseAPI_Final.Services.OperationalRuns;

/// <summary>
/// Prometheus etiketleri: tutarlı küçük harf / snake değerleri.
/// </summary>
public static class OperationalRunMetricLabels
{
    public static string BackupTrigger(BackupTriggerSource s) => s switch
    {
        BackupTriggerSource.Manual => "manual",
        BackupTriggerSource.Scheduled => "scheduled",
        BackupTriggerSource.OperatorApi => "operator_api",
        _ => "unknown"
    };

    public static string RestoreTrigger(RestoreVerificationTriggerSource s) => s switch
    {
        RestoreVerificationTriggerSource.Manual => "manual",
        RestoreVerificationTriggerSource.Scheduled => "scheduled",
        _ => "unknown"
    };

    public static string FormatBackupRunStatus(BackupRunStatus s) => s switch
    {
        BackupRunStatus.Queued => "queued",
        BackupRunStatus.Running => "running",
        BackupRunStatus.AwaitingVerification => "awaiting_verification",
        BackupRunStatus.Succeeded => "succeeded",
        BackupRunStatus.Failed => "failed",
        BackupRunStatus.VerificationFailed => "verification_failed",
        BackupRunStatus.Cancelled => "cancelled",
        _ => "unknown"
    };

    public static string FormatRestoreVerificationStatus(RestoreVerificationStatus s) => s switch
    {
        RestoreVerificationStatus.Queued => "queued",
        RestoreVerificationStatus.Running => "running",
        RestoreVerificationStatus.Succeeded => "succeeded",
        RestoreVerificationStatus.Failed => "failed",
        _ => "unknown"
    };
}
