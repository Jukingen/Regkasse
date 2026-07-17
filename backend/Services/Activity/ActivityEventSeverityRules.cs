using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Activity;

internal static class ActivityEventSeverityRules
{
    public static string DefaultFor(ActivityEventType type) =>
        type switch
        {
            ActivityEventType.LicenseExpired => ActivitySeverityNames.Critical,
            ActivityEventType.FinanzOnlineSubmissionFailed => ActivitySeverityNames.Error,
            ActivityEventType.BackupFailed => ActivitySeverityNames.Critical,
            ActivityEventType.RestoreDrillFailed => ActivitySeverityNames.Critical,
            ActivityEventType.OfflineQueueGrowing => ActivitySeverityNames.Warning,
            ActivityEventType.OfflineOrdersBacklogGrowing => ActivitySeverityNames.Warning,
            ActivityEventType.OfflineOrdersExpiringSoon => ActivitySeverityNames.Warning,
            ActivityEventType.OfflineSyncStalled => ActivitySeverityNames.Error,
            ActivityEventType.LicenseExpiringSoon => ActivitySeverityNames.Warning,
            ActivityEventType.SuspiciousHighValuePayment => ActivitySeverityNames.Error,
            ActivityEventType.SuspiciousMultipleStornos => ActivitySeverityNames.Warning,
            ActivityEventType.SuspiciousMultipleRefunds => ActivitySeverityNames.Error,
            ActivityEventType.SuspiciousUnusualTime => ActivitySeverityNames.Warning,
            ActivityEventType.SuspiciousSameCardMultiple => ActivitySeverityNames.Error,
            ActivityEventType.SuspiciousRapidTransactions => ActivitySeverityNames.Warning,
            ActivityEventType.DailyClosingBackdatedCreated => ActivitySeverityNames.Warning,
            ActivityEventType.DailyClosingPendingReminder => ActivitySeverityNames.Warning,
            _ => ActivitySeverityNames.Info,
        };

    public static bool MeetsMinimum(string severity, string minimum)
    {
        static int Rank(string s) => s switch
        {
            ActivitySeverityNames.Info => 0,
            ActivitySeverityNames.Warning => 1,
            ActivitySeverityNames.Error => 2,
            ActivitySeverityNames.Critical => 3,
            _ => 0,
        };

        return Rank(ActivitySeverityNames.NormalizeOrDefault(severity))
               >= Rank(ActivitySeverityNames.NormalizeOrDefault(minimum, ActivitySeverityNames.Warning));
    }
}
