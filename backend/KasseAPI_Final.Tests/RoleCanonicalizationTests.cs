using KasseAPI_Final.Auth;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>Role normalization: trim/empty only; no legacy alias mapping.</summary>
public class RoleCanonicalizationTests
{
    [Fact]
    public void GetCanonicalRole_Returns_Trimmed_AsIs()
    {
        Assert.Equal("Admin", RoleCanonicalization.GetCanonicalRole("Admin"));
        Assert.Equal("SuperAdmin", RoleCanonicalization.GetCanonicalRole("SuperAdmin"));
    }

    [Fact]
    public void GetCanonicalRole_Trims_Whitespace()
    {
        Assert.Equal("Admin", RoleCanonicalization.GetCanonicalRole("  Admin  "));
        Assert.Equal("SuperAdmin", RoleCanonicalization.GetCanonicalRole("  SuperAdmin  "));
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
