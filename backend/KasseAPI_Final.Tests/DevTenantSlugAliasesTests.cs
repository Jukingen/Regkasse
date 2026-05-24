using KasseAPI_Final.Tenancy;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DevTenantSlugAliasesTests
{
    [Theory]
    [InlineData("test_cafe", "cafe")]
    [InlineData("test-cafe", "cafe")]
    [InlineData("test_bar", "bar")]
    [InlineData("test-bar", "bar")]
    [InlineData("cafe", "cafe")]
    [InlineData("dev", "dev")]
    public void ResolveCanonical_maps_legacy_dev_slugs(string input, string expected)
    {
        Assert.Equal(expected, DevTenantSlugAliases.ResolveCanonical(input));
    }
}
