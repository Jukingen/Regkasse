using System.Security.Claims;
using KasseAPI_Final.Data;
using KasseAPI_Final.Services;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Tenancy;

/// <inheritdoc />
public sealed class AuthTenantSnapshotProvider : IAuthTenantSnapshotProvider
{
    private readonly AppDbContext _db;

    public AuthTenantSnapshotProvider(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public Task<AuthTenantSnapshot> GetSnapshotAsync(ClaimsPrincipal? user, CancellationToken cancellationToken = default) =>
        ResolveForTokenIssuanceAsync(persistedSessionTenantId: null, user, cancellationToken);

    /// <inheritdoc />
    public async Task<AuthTenantSnapshot> ResolveForTokenIssuanceAsync(
        string? persistedSessionTenantId,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken = default)
    {
        if (Guid.TryParse(persistedSessionTenantId, out var fromSession) && fromSession != Guid.Empty)
        {
            var rowSession = await _db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == fromSession, cancellationToken)
                .ConfigureAwait(false);
            if (rowSession != null)
            {
                return new AuthTenantSnapshot(fromSession.ToString("D"), rowSession.Name, null, null);
            }
        }

        var rawClaim = user?.FindFirst(ScopeCheckService.TenantIdClaim)?.Value;
        if (Guid.TryParse(rawClaim, out var fromJwt) && fromJwt != Guid.Empty)
        {
            var rowJwt = await _db.Tenants.AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == fromJwt, cancellationToken)
                .ConfigureAwait(false);
            if (rowJwt != null)
            {
                return new AuthTenantSnapshot(fromJwt.ToString("D"), rowJwt.Name, null, null);
            }
        }

        var primary = LegacyDefaultTenantIds.Primary;
        var rowDefault = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == primary, cancellationToken)
            .ConfigureAwait(false);

        return new AuthTenantSnapshot(primary.ToString("D"), rowDefault?.Name, null, null);
    }
}
