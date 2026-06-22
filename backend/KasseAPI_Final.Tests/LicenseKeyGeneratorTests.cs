using KasseAPI_Final.Services.Billing;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class LicenseKeyGeneratorTests
{
    private readonly LicenseKeyGenerator _generator = new();

    [Fact]
    public void GenerateLicenseKey_ProducesExpectedShape()
    {
        var key = _generator.GenerateLicenseKey("Cafe", new DateTime(2026, 12, 31, 23, 59, 59, DateTimeKind.Utc));

        Assert.StartsWith("REGK-20261231-cafe-", key, StringComparison.Ordinal);
        Assert.True(_generator.ValidateLicenseKeyFormat(key));
    }

    [Fact]
    public void GenerateLicenseKey_NormalizesSlug()
    {
        var key = _generator.GenerateLicenseKey("  My_Cafe  ", new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.StartsWith("REGK-20260601-my-cafe-", key, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("REGK-20261231-cafe-A7F3K2D9", true)]
    [InlineData("REGK-20261231-my-cafe-shop-A7F3K2D9", true)]
    [InlineData("regk-20261231-cafe-a7f3k2d9", false)]
    [InlineData("REGK-20261331-cafe-A7F3K2D9", false)]
    [InlineData("REGK-20261231-admin-A7F3K2D9", false)]
    [InlineData("REGK-ABCDE-BBBBB-CCCCC", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void ValidateLicenseKeyFormat_MatchesBillingPattern(string? key, bool expected)
    {
        Assert.Equal(expected, _generator.ValidateLicenseKeyFormat(key!));
    }

    [Fact]
    public void GenerateLicenseKey_RejectsEmptySlug()
    {
        Assert.Throws<ArgumentException>(() =>
            _generator.GenerateLicenseKey("   ", DateTime.UtcNow.AddYears(1)));
    }
}
