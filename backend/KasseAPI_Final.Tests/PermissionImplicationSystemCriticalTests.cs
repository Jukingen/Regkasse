using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Security;
using Xunit;

namespace KasseAPI_Final.Tests;

public class PermissionImplicationSystemCriticalTests
{
    [Fact]
    public void IsSatisfied_SystemCritical_Satisfies_Any_Catalog_Permission()
    {
        var effective = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            AppPermissions.SystemCritical,
        };

        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.UserView, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.ReportExport, effective));
        Assert.True(PermissionImplication.IsSatisfied(AppPermissions.SystemCritical, effective));
    }

    [Fact]
    public void HasPermissionClaim_CompactSuperAdminJwt_Allows_Granular_Permission()
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim("role", Roles.SuperAdmin),
                new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.SystemCritical),
            },
            authenticationType: "Test");
        var principal = new ClaimsPrincipal(identity);

        Assert.True(principal.HasPermissionClaim(AppPermissions.PaymentCancel));
        Assert.True(principal.HasPermissionClaim(AppPermissions.SystemCritical));
        Assert.True(PermissionClaimHelper.PrincipalHasAppPermission(principal, AppPermissions.AuditView));
    }

    [Fact]
    public void HasPermissionClaim_Manager_Without_Claim_Denies()
    {
        var identity = new ClaimsIdentity(
            new[]
            {
                new Claim("role", Roles.Manager),
                new Claim(PermissionCatalog.PermissionClaimType, AppPermissions.UserView),
            },
            authenticationType: "Test");
        var principal = new ClaimsPrincipal(identity);

        Assert.True(principal.HasPermissionClaim(AppPermissions.UserView));
        Assert.False(principal.HasPermissionClaim(AppPermissions.SystemCritical));
        Assert.False(principal.HasPermissionClaim(AppPermissions.TenantImpersonate));
    }
}
