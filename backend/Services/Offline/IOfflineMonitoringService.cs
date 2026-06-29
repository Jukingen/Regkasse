using KasseAPI_Final.Services.Billing;

namespace KasseAPI_Final.Services.Offline;

public interface IOfflineMonitoringService
{
    /// <summary>Aggregate offline system health for the current tenant.</summary>
    Task<OfflineSystemStatus> GetSystemStatusAsync(CancellationToken ct = default);

    /// <summary>Statistics for full order snapshots (<c>offline_orders</c>).</summary>
    Task<OfflineOrderStats> GetOrderStatsAsync(CancellationToken ct = default);

    /// <summary>Statistics for legacy TSE payment intents (<c>offline_transactions</c>).</summary>
    Task<DTOs.OfflineTransactionStats> GetTransactionStatsAsync(CancellationToken ct = default);

    /// <summary>Detect threshold breaches and RKSV-relevant offline risks.</summary>
    Task<List<OfflineAnomaly>> CheckAnomaliesAsync(CancellationToken ct = default);

    /// <summary>Sync/recovery health across both offline pipelines.</summary>
    Task<SyncHealth> GetSyncHealthAsync(CancellationToken ct = default);
}
