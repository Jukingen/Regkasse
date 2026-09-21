using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Paket 30-d: OSS destination STANDARD rates. <c>EL</c> aliases to seeded <c>GR</c>.
/// Unknown codes return null — no AT fallback.
/// </summary>
public sealed class OssVatRateRegistryTests
{
    private static readonly IOssVatRateRegistry Registry = new OssVatRateRegistry();

    [Theory]
    [InlineData("AT", 20)]
    [InlineData("DE", 19)]
    [InlineData("FR", 20)]
    [InlineData("IT", 22)]
    [InlineData("NL", 21)]
    [InlineData("GR", 24)]
    [InlineData("EL", 24)]
    [InlineData("el", 24)]
    public void GetStandardRate_ReturnsSeededStandard(string code, double expectedPercent)
    {
        Assert.Equal((decimal)expectedPercent, Registry.GetStandardRate(code));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("XX")]
    public void GetStandardRate_UnknownOrBlank_ReturnsNull(string? code)
    {
        Assert.Null(Registry.GetStandardRate(code!));
    }

    [Fact]
    public void GetAll_ReturnsFourteenSeededRows_WithoutElAlias()
    {
        var rows = Registry.GetAll();

        Assert.Equal(14, rows.Count);
        Assert.All(rows, row => Assert.Equal(OssVatRateCodes.Standard, row.Code));
        Assert.DoesNotContain(rows, row => row.CountryCode == "EL");
        Assert.Equal(24m, rows.Single(row => row.CountryCode == "GR").Rate);
        var finland = rows.Single(row => row.CountryCode == "FI");
        Assert.Equal(25.5m, finland.Rate);
        Assert.Equal(new DateOnly(2024, 9, 1), finland.EffectiveFrom);
        Assert.All(
            rows.Where(row => row.CountryCode != "FI"),
            row => Assert.Equal(new DateOnly(2024, 1, 1), row.EffectiveFrom));
    }
}
