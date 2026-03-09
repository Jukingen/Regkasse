using KasseAPI_Final.Auth;
using KasseAPI_Final.Authorization;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>Role normalization: trim/empty only; no legacy alias mapping.</summary>
public class RoleCanonicalizationTests
{
    [Fact]
    public void GetCanonicalRole_Leaves_Admin_Unchanged()
    {
        Assert.Equal(Roles.Admin, RoleCanonicalization.GetCanonicalRole("Admin"));
    }

    [Fact]
    public void GetCanonicalRole_Leaves_SuperAdmin_Unchanged()
    {
        Assert.Equal(Roles.SuperAdmin, RoleCanonicalization.GetCanonicalRole("SuperAdmin"));
    }

    [Fact]
    public void GetCanonicalRole_Trims_Whitespace()
    {
        Assert.Equal(Roles.Admin, RoleCanonicalization.GetCanonicalRole("  Admin  "));
    }

    [Fact]
    public void GetCanonicalRole_NullOrEmpty_Returns_Empty()
    {
        Assert.Equal("", RoleCanonicalization.GetCanonicalRole(null));
        Assert.Equal("", RoleCanonicalization.GetCanonicalRole(""));
        Assert.Equal("", RoleCanonicalization.GetCanonicalRole("   "));
    }

    [Fact]
    public void GetCanonicalRole_UnknownRole_Returns_AsIs()
    {
        Assert.Equal("CustomRole", RoleCanonicalization.GetCanonicalRole("CustomRole"));
    }
}
