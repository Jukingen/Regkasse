using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tenancy;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Auth;

public sealed class AuthService : IAuthService
{
    private static readonly TenantLicenseValidator TenantLicenseValidator = new();

    /// <summary>Shown in API responses when login is blocked due to a deleted tenant (UI: de-DE).</summary>
    public const string TenantDisabledMessageDe = "Dieser Mandant wurde deaktiviert.";
    public const string TenantLicenseLockdownMessageDe = "Dieser Mandant ist wegen abgelaufener Lizenz gesperrt.";

    private readonly AppDbContext _db;
    private readonly ILoginTenantResolver _loginTenantResolver;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly TseOptions _tseOptions;
    private readonly IDevelopmentModeService _developmentMode;
    private readonly LicenseOptions _licenseOptions;

    public AuthService(
        AppDbContext db,
        ILoginTenantResolver loginTenantResolver,
        IHostEnvironment hostEnvironment,
        IOptions<TseOptions> tseOptions,
        IDevelopmentModeService developmentMode,
        IOptions<LicenseOptions> licenseOptions)
    {
        _db = db;
        _loginTenantResolver = loginTenantResolver;
        _hostEnvironment = hostEnvironment;
        _tseOptions = tseOptions.Value;
        _developmentMode = developmentMode;
        _licenseOptions = licenseOptions.Value;
    }

    public async Task<LoginTenantAccessResult> ResolveLoginTenantAccessAsync(
        string userId,
        bool isSuperAdmin,
        CancellationToken cancellationToken = default)
    {
        if (!isSuperAdmin && await IsLoginBlockedByDeletedMembershipAsync(userId, cancellationToken).ConfigureAwait(false))
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
            var tenant = await _db.Tenants.AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => new
                {
                    t.Status,
                    t.Name,
                    t.LicenseValidUntilUtc,
                })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (tenant != null
                && string.Equals(tenant.Status, TenantStatuses.Deleted, StringComparison.OrdinalIgnoreCase))
            {
                return LoginTenantAccessResult.Blocked(
                    TenantDisabledMessageDe,
                    LoginTenantBlockedException.CodeTenantDisabled);
            }

            if (!isSuperAdmin
                && tenant != null
                && !LicenseEnforcementPolicy.ShouldDisableEnforcement(
                    _hostEnvironment,
                    _tseOptions,
                    _developmentMode,
                    _licenseOptions))
            {
                var permissions = TenantLicenseValidator.GetPermissions(tenant.LicenseValidUntilUtc, isSuperAdmin: false);
                if (!permissions.CanAccess)
                {
                    return LoginTenantAccessResult.Blocked(
                        TenantLicenseLockdownMessageDe,
                        LoginTenantBlockedException.CodeTenantLicenseLockdown);
                }
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
