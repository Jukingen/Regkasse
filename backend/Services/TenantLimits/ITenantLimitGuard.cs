using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Limits;

/// <summary>Enforces <c>tenant_limits</c> caps on create/sale paths. Throws <see cref="LimitExceededException"/>.</summary>
public interface ITenantLimitGuard
{
    Task EnsureCanCreateProductAsync(
        Guid tenantId,
        int additionalCount = 1,
        CancellationToken cancellationToken = default);

    Task EnsureCanCreateUserAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sale-only: daily transaction count, single-ticket amount, and daily revenue.
    /// Skip for storno/refund and offline fiscal replay.
    /// </summary>
    Task EnsureSaleWithinLimitsAsync(
        Guid tenantId,
        decimal amount,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tenant-strategy backup enqueue: succeeded-run count and estimated total LogicalDump size.
    /// System / deployment-wide backups are not counted.
    /// </summary>
    Task EnsureCanCreateBackupAsync(Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Offline queue create: Pending + NonFiscalPending rows for the tenant.
    /// Do not call when replaying an already-queued intent.
    /// </summary>
    Task EnsureCanQueueOfflineTransactionAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<TenantLimitUsageDto> GetUsageAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
