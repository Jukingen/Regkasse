using Microsoft.AspNetCore.Http;

namespace KasseAPI_Final.Tenancy;

/// <inheritdoc />
/// <remarks>
/// Prefers the ambient <see cref="ICurrentTenantAccessor"/> (subdomain, dev header, impersonation).
/// Falls back to <see cref="IAuthTenantSnapshotProvider"/> (JWT <c>tenant_id</c>, else legacy default).
/// </remarks>
public sealed class SettingsTenantResolver : ISettingsTenantResolver
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IAuthTenantSnapshotProvider _authTenantSnapshotProvider;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public SettingsTenantResolver(
        IHttpContextAccessor httpContextAccessor,
        IAuthTenantSnapshotProvider authTenantSnapshotProvider,
        ICurrentTenantAccessor tenantAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
        _authTenantSnapshotProvider = authTenantSnapshotProvider;
        _tenantAccessor = tenantAccessor;
    }

    public async Task<Guid> ResolveEffectiveTenantIdAsync(CancellationToken cancellationToken = default)
    {
        if (_tenantAccessor.TenantId is Guid ambient && ambient != Guid.Empty)
            return ambient;

        var user = _httpContextAccessor.HttpContext?.User;
        var snapshot = await _authTenantSnapshotProvider.GetSnapshotAsync(user, cancellationToken).ConfigureAwait(false);
        return Guid.Parse(snapshot.TenantId);
    }
}
