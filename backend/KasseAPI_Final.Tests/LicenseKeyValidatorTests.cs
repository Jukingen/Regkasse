using KasseAPI_Final.Services.License;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseKeyValidatorTests
{
    private readonly LicenseKeyValidator _sut = LicenseKeyValidator.Instance;

    [Theory]
    [InlineData("REGK-20990101-cafe-A7F3K2D9", true, false)]
    [InlineData("regk-20990101-CAFE-a7f3k2d9", true, false)]
    [InlineData("REGK-20990101-system-1R61EMER", false, true)]
    [InlineData("REGK-AAAAA-BBBBB-CCCCC", false, true)]
    [InlineData("regk-aaaaa-bbbbb-ccccc", false, true)]
    public void Parse_AcceptsUnifiedAndLegacy(string key, bool tenant, bool system)
    {
        var parsed = _sut.Parse(key);

        Assert.True(parsed.IsValid);
        Assert.True(_sut.IsValidFormat(key));
        Assert.Equal(tenant, parsed.IsTenant);
        Assert.Equal(system, parsed.IsSystem);
        Assert.Equal(tenant, _sut.IsTenantLicense(key));
        Assert.Equal(system, _sut.IsSystemLicense(key));
        Assert.False(string.IsNullOrWhiteSpace(_sut.Normalize(key)));
    }

    [Theory]
    [InlineData("not-a-key")]
    [InlineData("REGK-ABCDE-12345")]
    [InlineData("")]
    [InlineData(null)]
    public void Parse_RejectsInvalid(string? key)
    {
        var parsed = _sut.Parse(key);

        Assert.False(parsed.IsValid);
        Assert.False(_sut.IsValidFormat(key));
        Assert.Equal(LicenseKeyErrorCodes.InvalidFormat, parsed.ErrorCode);
    }

    [Fact]
    public void Normalize_RewritesUnifiedKeyCasing()
    {
        var normalized = _sut.Normalize("regk-20990101-Cafe-a7f3k2d9");

        Assert.Equal("REGK-20990101-cafe-A7F3K2D9", normalized);
    }
}
