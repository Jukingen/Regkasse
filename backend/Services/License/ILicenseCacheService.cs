namespace KasseAPI_Final.Services.License;

/// <summary>
/// Invalidates backend license cache entries after activate / extend / revoke.
/// FA React Query keys are refreshed separately via <c>invalidateTenantLicenseQueries</c>.
/// </summary>
public interface ILicenseCacheService
{
    Task InvalidateAllAsync(string licenseKey, CancellationToken cancellationToken = default);

    Task InvalidateForTenantAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task InvalidateForSystemAsync(CancellationToken cancellationToken = default);
}
