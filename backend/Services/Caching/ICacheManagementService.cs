namespace KasseAPI_Final.Services.Caching;

/// <summary>Super Admin operations for manual cache invalidation.</summary>
public interface ICacheManagementService
{
    Task<ClearCacheResult> ClearAsync(ClearCacheRequest request, CancellationToken cancellationToken = default);
}
