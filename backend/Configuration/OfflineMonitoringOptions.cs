namespace KasseAPI_Final.Configuration;

/// <summary>Thresholds for offline order/transaction monitoring and activity alerts.</summary>
public sealed class OfflineMonitoringOptions
{
    public const string SectionName = "OfflineMonitoring";

    /// <summary>Pending <c>offline_orders</c> per register before backlog anomaly.</summary>
    public int OrderQueueAlertThreshold { get; set; } = 10;

    /// <summary>Pending <c>offline_transactions</c> per register before backlog anomaly.</summary>
    public int TransactionQueueAlertThreshold { get; set; } = 10;

    /// <summary>Hours before expiry to raise warning anomaly.</summary>
    public int ExpiryWarningHours { get; set; } = 24;

    /// <summary>Hours before expiry to raise critical anomaly.</summary>
    public int ExpiryCriticalHours { get; set; } = 6;

    /// <summary>Pending order with no sync attempt older than this is considered stalled.</summary>
    public int StalledSyncHours { get; set; } = 2;

    /// <summary>Background alert sweep interval (minutes).</summary>
    public int MonitorIntervalMinutes { get; set; } = 5;

    /// <summary>TSE offline transaction count as % of max before cap-warning anomaly (default 80%).</summary>
    public int TseOfflineCapWarningPercent { get; set; } = 80;

    /// <summary>Tenant-wide NonFiscalPending count warning threshold for TSE offline queue UI/alerts.</summary>
    public int TseOfflineQueueWarningThreshold { get; set; } = 30;

    /// <summary>Tenant-wide NonFiscalPending count critical threshold (default matches TSE per-register cap 50).</summary>
    public int TseOfflineQueueCriticalThreshold { get; set; } = 50;
}
