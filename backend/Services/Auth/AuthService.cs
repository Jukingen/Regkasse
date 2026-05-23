using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Auth;

public sealed class AuthService : IAuthService
{
    /// <summary>Shown in API responses when login is blocked due to a deleted tenant (UI: de-DE).</summary>
    public const string TenantDisabledMessageDe = "Dieser Mandant wurde deaktiviert.";

    private readonly AppDbContext _db;
    private readonly ILoginTenantResolver _loginTenantResolver;

    public AuthService(AppDbContext db, ILoginTenantResolver loginTenantResolver)
    {
        _db = db;
        _loginTenantResolver = loginTenantResolver;
    }

    public async Task<LoginTenantAccessResult> ResolveLoginTenantAccessAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (await IsLoginBlockedByDeletedMembershipAsync(userId, cancellationToken).ConfigureAwait(false))
        {
            return LoginTenantAccessResult.Blocked(
                TenantDisabledMessageDe,
                LoginTenantBlockedException.CodeTenantDisabled);
        }

        AuthTenantSnapshot snapshot;
        try
        {
            snapshot = await _loginTenantResolver
                .ResolveSnapshotForLoginAsync(userId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (LoginTenantBlockedException ex)
        {
            return LoginTenantAccessResult.Blocked(ex.Message, ex.ErrorCode);
        }

        if (Guid.TryParse(snapshot.TenantId, out var tenantId))
        {
            var status = await _db.Tenants.AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => t.Status)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (string.Equals(status, TenantStatuses.Deleted, StringComparison.OrdinalIgnoreCase))
            {
                return LoginTenantAccessResult.Blocked(
                    TenantDisabledMessageDe,
                    LoginTenantBlockedException.CodeTenantDisabled);
            }
        }

        return LoginTenantAccessResult.Ok(snapshot);
    }

    private async Task<bool> IsLoginBlockedByDeletedMembershipAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.UserTenantMemberships
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new
            {
                m.IsActive,
                TenantStatus = m.Tenant!.Status,
                TenantIsActive = m.Tenant!.IsActive,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rows.Count == 0)
            return false;

        var hasEligible = rows.Any(m =>
            m.IsActive
            && !string.Equals(m.TenantStatus, TenantStatuses.Deleted, StringComparison.OrdinalIgnoreCase)
            && m.TenantIsActive);

        if (hasEligible)
            return false;

        return rows.Any(m =>
            string.Equals(m.TenantStatus, TenantStatuses.Deleted, StringComparison.OrdinalIgnoreCase));
    }
}
