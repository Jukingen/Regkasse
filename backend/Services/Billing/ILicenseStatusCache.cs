using KasseAPI_Final.Services.Caching;

namespace KasseAPI_Final.Services.Billing;

/// <summary>
/// Per-tenant billing license status cache (<c>license_status_{tenantId}</c>).
/// </summary>
public interface ILicenseStatusCache
{
    /// <summary>Cache-aside read: returns cached status or creates via <paramref name="factory"/>.</summary>
    Task<TenantLicenseStatus> GetOrCreateAsync(
        Guid tenantId,
        Func<CancellationToken, Task<TenantLicenseStatus>> factory,
        CancellationToken cancellationToken = default);

    /// <summary>Removes any cached license status entry for <paramref name="tenantId"/>.</summary>
    Task InvalidateLicenseCacheAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
