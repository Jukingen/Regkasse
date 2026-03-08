using KasseAPI_Final.Auth;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>Legacy role alias → canonical (Phase 7).</summary>
public class RoleCanonicalizationTests
{
    [Fact]
    public void GetCanonicalRole_Maps_Administrator_To_Admin()
    {
        Assert.Equal(RoleCanonicalization.Canonical.Admin, RoleCanonicalization.GetCanonicalRole("Administrator"));
        Assert.Equal(RoleCanonicalization.Canonical.Admin, RoleCanonicalization.GetCanonicalRole("administrator"));
    }

    [Fact]
    public void GetCanonicalRole_Leaves_Admin_Unchanged()
    {
        Assert.Equal(RoleCanonicalization.Canonical.Admin, RoleCanonicalization.GetCanonicalRole("Admin"));
    }

    [Fact]
    public void GetCanonicalRole_Leaves_SuperAdmin_Unchanged()
    {
        Assert.Equal(RoleCanonicalization.Canonical.SuperAdmin, RoleCanonicalization.GetCanonicalRole("SuperAdmin"));
    }

    [Fact]
    public void GetCanonicalRole_NullOrEmpty_Returns_Empty()
    {
        Assert.Equal("", RoleCanonicalization.GetCanonicalRole(null));
        Assert.Equal("", RoleCanonicalization.GetCanonicalRole(""));
        Assert.Equal("", RoleCanonicalization.GetCanonicalRole("   "));
    }

    [Fact]
    public void GetLegacyAliases_Contains_Administrator()
    {
        var aliases = RoleCanonicalization.GetLegacyAliases();
        Assert.Contains("Administrator", aliases);
    }
}
