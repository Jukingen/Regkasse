using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Limits;

/// <summary>Cache-Aside for <see cref="TenantLimits"/> snapshots.</summary>
public interface ITenantLimitCacheService
{
    Task<TenantLimits> GetOrCreateAsync(
        Guid tenantId,
        Func<CancellationToken, Task<TenantLimits>> factory,
        CancellationToken cancellationToken = default);

    Task InvalidateAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
