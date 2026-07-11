using KasseAPI_Final.Tenancy;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DevTenantSlugAliasesTests
{
    [Theory]
    [InlineData("test_cafe", "dev")]
    [InlineData("test-cafe", "dev")]
    [InlineData("cafe", "dev")]
    [InlineData("dev", "dev")]
    [InlineData("test_bar", "prod")]
    [InlineData("test-bar", "prod")]
    [InlineData("bar", "prod")]
    [InlineData("prod", "prod")]
    public void ResolveCanonical_maps_legacy_dev_slugs(string input, string expected)
    {
        Assert.Equal(expected, DevTenantSlugAliases.ResolveCanonical(input));
    }
}
