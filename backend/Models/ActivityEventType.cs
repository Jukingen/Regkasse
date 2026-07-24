namespace KasseAPI_Final.Models;

/// <summary>
/// Admin activity feed / notification event kinds (FA bell, email, webhook).
/// </summary>
public enum ActivityEventType
{
    UserCreated = 0,
    UserUpdated = 1,
    UserDeleted = 2,
    CashRegisterOpened = 10,
    CashRegisterClosed = 11,
    CashRegisterDecommissioned = 12,
    LicenseExpiringSoon = 20,
    LicenseExpired = 21,
    OfflineQueueGrowing = 30,
    OfflineOrdersBacklogGrowing = 31,
    OfflineOrdersExpiringSoon = 32,
    OfflineSyncStalled = 33,
    FinanzOnlineSubmissionFailed = 40,
    BackupFailed = 50,
    BackupSucceeded = 51,
    RestoreDrillFailed = 52,
    RestoreDrillSucceeded = 53,
    SuspiciousHighValuePayment = 60,
    SuspiciousMultipleStornos = 61,
    SuspiciousMultipleRefunds = 62,
    SuspiciousUnusualTime = 63,
    SuspiciousSameCardMultiple = 64,
    SuspiciousRapidTransactions = 65,
    DailyClosingBackdatedCreated = 70,
    /// <summary>Evening reminder: Tagesabschluss still pending (no auto-close).</summary>
    DailyClosingPendingReminder = 71,
    /// <summary>Online order materialized into a POS cart (FA / kitchen alert).</summary>
    OnlineOrderPushedToPos = 80,
    /// <summary>Online order payment succeeded (Stripe / mock gateway).</summary>
    OnlineOrderPaid = 81,
    /// <summary>Online order lifecycle status changed (accepted / preparing / ready / …).</summary>
    OnlineOrderStatusChanged = 82,
    /// <summary>Customer confirmation dispatched (email/push) and staff inbox alert.</summary>
    OnlineOrderConfirmed = 83,
    /// <summary>Mandanten-Admin requested website or app creation (Super Admin queue).</summary>
    DigitalServiceRequested = 90,
    /// <summary>Mandanten-Admin requested GDPR non-RKSV data deletion (Super Admin notify).</summary>
    DataAccessDeleteRequested = 91,
    /// <summary>GDPR data export ZIP is ready; download link issued (7-day expiry).</summary>
    DataExportReady = 92,

    /// <summary>Custom role created (permission management).</summary>
    RoleCreated = 100,
    /// <summary>Custom role deleted.</summary>
    RoleDeleted = 101,
    /// <summary>Role permission set updated.</summary>
    RolePermissionsUpdated = 102,
    /// <summary>Per-user permission override created/changed/removed.</summary>
    UserPermissionOverridesChanged = 103,
    /// <summary>System-critical permission change (e.g. SuperAdmin-related or mass update).</summary>
    SystemPermissionChange = 104,
    /// <summary>User requested a temporary permission grant.</summary>
    PermissionRequested = 110,
    /// <summary>Permission request approved.</summary>
    PermissionRequestApproved = 111,
    /// <summary>Permission request rejected.</summary>
    PermissionRequestRejected = 112,
    /// <summary>Time-bound override expiring soon.</summary>
    UserPermissionOverrideExpiringSoon = 113,
    /// <summary>Time-bound override expired (processed by background job).</summary>
    UserPermissionOverrideExpired = 114,

    /// <summary>Super Admin requested a sensitive tenant settings change (pending approval).</summary>
    TenantSettingsChangeRequested = 120,
    /// <summary>Super Admin approved a pending tenant settings change.</summary>
    TenantSettingsChangeApproved = 121,
    /// <summary>Super Admin rejected a pending tenant settings change.</summary>
    TenantSettingsChangeRejected = 122,
    /// <summary>Super Admin reverted a previously approved tenant settings change.</summary>
    TenantSettingsChangeReverted = 123,

    /// <summary>Elevated risk score detected for a tenant user action (Medium+).</summary>
    RiskAnomalyDetected = 130,
    /// <summary>Risk score marked resolved by an operator.</summary>
    RiskScoreResolved = 131,

    /// <summary>Critical admin action awaiting Super Admin approval.</summary>
    CriticalActionApprovalRequested = 140,
    /// <summary>Super Admin approved a critical action request.</summary>
    CriticalActionApprovalApproved = 141,
    /// <summary>Super Admin rejected a critical action request.</summary>
    CriticalActionApprovalRejected = 142,

    /// <summary>Scheduled platform maintenance reminder (7d / 3d / 1h milestones).</summary>
    MaintenanceUpcoming = 150,
    /// <summary>Force-display enabled (~24h before scheduled start).</summary>
    MaintenanceForceDisplayEnabled = 151,
    /// <summary>Scheduled maintenance window has started (InProgress).</summary>
    MaintenanceStarted = 152,

    /// <summary>Primary TSE device failed health check and a backup was activated.</summary>
    TseFailoverActivated = 160,
    /// <summary>Primary TSE is unhealthy and no healthy backup device is available.</summary>
    TseFailoverNoBackup = 161,
    /// <summary>Signing role reverted from backup to the primary TSE device.</summary>
    TseFailoverReverted = 162,
    /// <summary>Automatic/manual failover attempt began (backup validated).</summary>
    TseFailoverStarted = 163,
    /// <summary>Failover attempt failed (backup unhealthy or unexpected error).</summary>
    TseFailoverFailed = 164,
    /// <summary>Backup TSE device health is degraded / low score.</summary>
    TseFailoverBackupLowHealth = 165,

    /// <summary>TSE signing certificate expires within the warning window.</summary>
    TseCertificateExpiringSoon = 170,

    /// <summary>TSE signing certificate ExpiresAt is in the past.</summary>
    TseCertificateExpired = 171,

    /// <summary>TSE certificate metadata was renewed / synced from key provider.</summary>
    TseCertificateRenewed = 172,

    /// <summary>Operator scheduled a TSE certificate renewal date.</summary>
    TseCertificateRenewalScheduled = 173,

    /// <summary>TSE device health probe latency exceeded slow/critical thresholds.</summary>
    TsePerformanceSlow = 180,

    /// <summary>TSE device health probe failure/error rate exceeded thresholds.</summary>
    TsePerformanceHighErrorRate = 181,

    /// <summary>Indicative TSE operating cost spiked vs baseline or daily average.</summary>
    TseCostAnomaly = 182,

    /// <summary>Heuristic predictive analytics flagged elevated TSE failure risk.</summary>
    TsePredictiveFailureRisk = 183,

    /// <summary>A TSE operational incident was opened.</summary>
    TseIncidentCreated = 184,

    /// <summary>A TSE operational incident was resolved or closed.</summary>
    TseIncidentResolved = 185,

    /// <summary>TSE operational SLA target(s) violated for a tenant lookback window.</summary>
    TseSlaViolation = 186,

    /// <summary>TSE signing capacity is near or over configured utilization thresholds.</summary>
    TseCapacityNearLimit = 187,

    /// <summary>TSE disaster-recovery simulation drill completed.</summary>
    TseDrDrillCompleted = 188,

    /// <summary>TSE auto-scaling recommended or soft-applied a device count change.</summary>
    TseAutoScaleRecommended = 189,

    /// <summary>Statistical TSE anomaly detected (baseline deviation; diagnostic only).</summary>
    TseAnomalyDetected = 190,

    /// <summary>TSE auto-healing applied a safe recovery action (re-probe / clear error / optional failover).</summary>
    TseAutoHealExecuted = 191,
}
