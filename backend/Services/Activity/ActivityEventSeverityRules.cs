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
            ActivityEventType.OnlineOrderPushedToPos => ActivitySeverityNames.Warning,
            ActivityEventType.OnlineOrderPaid => ActivitySeverityNames.Info,
            ActivityEventType.OnlineOrderStatusChanged => ActivitySeverityNames.Info,
            ActivityEventType.OnlineOrderConfirmed => ActivitySeverityNames.Warning,
            ActivityEventType.DigitalServiceRequested => ActivitySeverityNames.Info,
            ActivityEventType.DataAccessDeleteRequested => ActivitySeverityNames.Warning,
            ActivityEventType.DataExportReady => ActivitySeverityNames.Info,
            ActivityEventType.RoleCreated => ActivitySeverityNames.Warning,
            ActivityEventType.RoleDeleted => ActivitySeverityNames.Critical,
            ActivityEventType.RolePermissionsUpdated => ActivitySeverityNames.Warning,
            ActivityEventType.UserPermissionOverridesChanged => ActivitySeverityNames.Info,
            ActivityEventType.SystemPermissionChange => ActivitySeverityNames.Critical,
            ActivityEventType.PermissionRequested => ActivitySeverityNames.Info,
            ActivityEventType.PermissionRequestApproved => ActivitySeverityNames.Info,
            ActivityEventType.PermissionRequestRejected => ActivitySeverityNames.Info,
            ActivityEventType.UserPermissionOverrideExpiringSoon => ActivitySeverityNames.Warning,
            ActivityEventType.UserPermissionOverrideExpired => ActivitySeverityNames.Info,
            ActivityEventType.TenantSettingsChangeRequested => ActivitySeverityNames.Warning,
            ActivityEventType.TenantSettingsChangeApproved => ActivitySeverityNames.Warning,
            ActivityEventType.TenantSettingsChangeRejected => ActivitySeverityNames.Info,
            ActivityEventType.TenantSettingsChangeReverted => ActivitySeverityNames.Warning,
            ActivityEventType.RiskAnomalyDetected => ActivitySeverityNames.Warning,
            ActivityEventType.RiskScoreResolved => ActivitySeverityNames.Info,
            ActivityEventType.CriticalActionApprovalRequested => ActivitySeverityNames.Warning,
            ActivityEventType.CriticalActionApprovalApproved => ActivitySeverityNames.Info,
            ActivityEventType.CriticalActionApprovalRejected => ActivitySeverityNames.Info,
            ActivityEventType.MaintenanceUpcoming => ActivitySeverityNames.Warning,
            ActivityEventType.MaintenanceForceDisplayEnabled => ActivitySeverityNames.Warning,
            ActivityEventType.MaintenanceStarted => ActivitySeverityNames.Critical,
            ActivityEventType.TseFailoverActivated => ActivitySeverityNames.Critical,
            ActivityEventType.TseFailoverNoBackup => ActivitySeverityNames.Critical,
            ActivityEventType.TseFailoverReverted => ActivitySeverityNames.Warning,
            ActivityEventType.TseFailoverStarted => ActivitySeverityNames.Critical,
            ActivityEventType.TseFailoverFailed => ActivitySeverityNames.Critical,
            ActivityEventType.TseFailoverBackupLowHealth => ActivitySeverityNames.Warning,
            ActivityEventType.TseCertificateExpiringSoon => ActivitySeverityNames.Warning,
            ActivityEventType.TseCertificateExpired => ActivitySeverityNames.Critical,
            ActivityEventType.TseCertificateRenewed => ActivitySeverityNames.Info,
            ActivityEventType.TseCertificateRenewalScheduled => ActivitySeverityNames.Info,
            ActivityEventType.TsePerformanceSlow => ActivitySeverityNames.Warning,
            ActivityEventType.TsePerformanceHighErrorRate => ActivitySeverityNames.Warning,
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
