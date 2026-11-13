namespace KasseAPI_Final.Configuration;

/// <summary>Alert thresholds for offline order sync monitoring and activity notifications.</summary>
public class OfflineAlertRules
{
    public const string SectionName = "OfflineAlertRules";

    /// <summary>Tenant-wide pending <c>offline_orders</c> count before alert.</summary>
    public int MaxPendingOrders { get; set; } = 50;

    /// <summary>Pending order age (hours since creation) before alert.</summary>
    public int MaxPendingAgeHours { get; set; } = 48;

    /// <summary>Sync attempt count on a single order before alert.</summary>
    public int MaxSyncRetries { get; set; } = 5;

    /// <summary>Minimum acceptable sync success rate (0–100).</summary>
    public int MinSyncSuccessRate { get; set; } = 80;

    /// <summary>Background alert check interval (seconds).</summary>
    public int CheckIntervalSeconds { get; set; } = 300;
}
