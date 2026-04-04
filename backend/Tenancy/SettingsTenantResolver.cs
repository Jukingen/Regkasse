using Microsoft.AspNetCore.Http;

namespace KasseAPI_Final.Tenancy;

/// <inheritdoc />
/// <remarks>
/// Delegates to <see cref="IAuthTenantSnapshotProvider"/> with the current HTTP user (if any).
/// Background work without <see cref="HttpContext"/> falls back to the legacy default tenant via the provider.
/// </remarks>
public sealed class SettingsTenantResolver : ISettingsTenantResolver
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthTenantSnapshotProvider _authTenantSnapshotProvider;

    public SettingsTenantResolver(
        IHttpContextAccessor httpContextAccessor,
        IAuthTenantSnapshotProvider authTenantSnapshotProvider)
    {
        _httpContextAccessor = httpContextAccessor;
        _authTenantSnapshotProvider = authTenantSnapshotProvider;
    }

    public async Task<Guid> ResolveEffectiveTenantIdAsync(CancellationToken cancellationToken = default)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        var snapshot = await _authTenantSnapshotProvider.GetSnapshotAsync(user, cancellationToken).ConfigureAwait(false);
        return Guid.Parse(snapshot.TenantId);
    }
}
