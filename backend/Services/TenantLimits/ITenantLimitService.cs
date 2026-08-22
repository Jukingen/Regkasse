using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Limits;

/// <summary>Reads, updates, and evaluates per-tenant operational caps.</summary>
public interface ITenantLimitService
{
    Task<TenantLimits> GetLimitsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<TenantLimits> UpdateLimitsAsync(
        Guid tenantId,
        UpdateTenantLimitsRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>True when <paramref name="currentValue"/> is still below the named cap.</summary>
    Task<bool> CheckLimitAsync(
        Guid tenantId,
        string limitKey,
        int currentValue,
        CancellationToken cancellationToken = default);

    Task<int> GetLimitValueAsync(
        Guid tenantId,
        string limitKey,
        CancellationToken cancellationToken = default);

    /// <summary>Sets a single named cap and invalidates the tenant-limits cache.</summary>
    Task<TenantLimits> SetLimitValueAsync(
        Guid tenantId,
        string limitKey,
        decimal value,
        CancellationToken cancellationToken = default);

    Task ResetLimitsAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
