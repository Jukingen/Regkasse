using KasseAPI_Final.Models.Constants;
using AuthRoles = KasseAPI_Final.Authorization.Roles;
using AppRoles = KasseAPI_Final.Constants.Roles;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RoleAndTenantSlugConstantsTests
{
    [Fact]
    public void RoleConstants_MatchCanonicalNames()
    {
        Assert.Equal(AuthRoles.SuperAdmin, AppRoles.SuperAdmin);
        Assert.Equal("SuperAdmin", AppRoles.SuperAdmin);
        Assert.Equal(KasseAPI_Final.Authorization.RoleHierarchy.PlatformAdmin, AppRoles.PlatformAdmin);
        Assert.Equal("PlatformAdmin", AppRoles.PlatformAdmin);
        Assert.Equal("Manager", AppRoles.Manager);
        Assert.Equal("Cashier", AppRoles.Cashier);
        Assert.Equal("Kitchen", AppRoles.Kitchen);
        Assert.Equal("Waiter", AppRoles.Waiter);
        Assert.Equal("Accountant", AppRoles.Accountant);
        Assert.Equal("ReportViewer", AppRoles.ReportViewer);
    }

    [Fact]
    public void TenantSlugs_MatchDevAndPlatform()
    {
        Assert.Equal("dev", KasseAPI_Final.Constants.TenantSlugs.Dev);
        Assert.Equal("platform", KasseAPI_Final.Constants.TenantSlugs.Platform);
        Assert.Equal(KasseAPI_Final.Constants.TenantSlugs.Platform, SystemTenantIds.PlatformSlug);
    }
}
