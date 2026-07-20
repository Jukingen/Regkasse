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
}
