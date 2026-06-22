using KasseAPI_Final.Services.AdminTenants;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RegkTenantLicenseKeyFormatTests
{
    [Theory]
    [InlineData("REGK-AAAAA-BBBBB-CCCCC", true)]
    [InlineData("regk-aaaaa-bbbbb-ccccc", true)]
    [InlineData("REGK-ABCDE-12345-FGHIJ", true)]
    [InlineData("REGK-ABCDE-12345", false)]
    [InlineData("INVALID", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_MatchesRegkPattern(string? key, bool expected)
    {
        Assert.Equal(expected, RegkTenantLicenseKeyFormat.IsValid(key));
    }
}
