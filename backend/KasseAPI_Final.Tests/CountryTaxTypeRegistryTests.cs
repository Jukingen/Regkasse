using System.Reflection;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Paket 13-c: country-scoped VAT rate seeds. Not wired into calculation.
/// Unknown codes must not fall back to Austria.
/// </summary>
public sealed class CountryTaxTypeRegistryTests
{
    private static readonly ICountryTaxTypeRegistry Registry = new CountryTaxTypeRegistry();

    [Fact]
    public void Austria_HasFiveLiveRates()
    {
        var at = Registry.Get(CountryProfileCodes.Austria);

        Assert.Equal(5, at.Count);
        Assert.All(at, row => Assert.Equal(CountryProfileCodes.Austria, row.CountryCode));
        Assert.Equal(20m, Rate(at, CountryTaxTypeCodes.Standard));
        Assert.Equal(10m, Rate(at, CountryTaxTypeCodes.Reduced1));
        Assert.Equal(13m, Rate(at, CountryTaxTypeCodes.Reduced2));
        Assert.Equal(0m, Rate(at, CountryTaxTypeCodes.Zero));
        Assert.Equal(4.9m, Rate(at, CountryTaxTypeCodes.ReducedNew));
        Assert.Equal(new DateOnly(2016, 1, 1), Row(at, CountryTaxTypeCodes.Standard).EffectiveFrom);
        Assert.Equal(new DateOnly(2026, 1, 1), Row(at, CountryTaxTypeCodes.ReducedNew).EffectiveFrom);
        Assert.All(at, row => Assert.Null(row.EffectiveTo));
    }

    [Fact]
    public void Germany_HasStandardAndReduced()
    {
        var de = Registry.Get(CountryProfileCodes.Germany);

        Assert.Equal(2, de.Count);
        Assert.Equal(19m, Rate(de, CountryTaxTypeCodes.Standard));
        Assert.Equal(7m, Rate(de, CountryTaxTypeCodes.Reduced1));
        Assert.All(de, row =>
        {
            Assert.Equal(CountryProfileCodes.Germany, row.CountryCode);
            Assert.Equal(new DateOnly(2021, 1, 1), row.EffectiveFrom);
            Assert.Null(row.EffectiveTo);
        });
    }

    [Fact]
    public void Switzerland_HasStandardReducedAndLodging()
    {
        var ch = Registry.Get(CountryProfileCodes.Switzerland);

        Assert.Equal(3, ch.Count);
        Assert.Equal(8.1m, Rate(ch, CountryTaxTypeCodes.Standard));
        Assert.Equal(2.6m, Rate(ch, CountryTaxTypeCodes.Reduced1));
        Assert.Equal(3.8m, Rate(ch, CountryTaxTypeCodes.Lodging));
        Assert.All(ch, row =>
        {
            Assert.Equal(CountryProfileCodes.Switzerland, row.CountryCode);
            Assert.Equal(new DateOnly(2024, 1, 1), row.EffectiveFrom);
            Assert.Null(row.EffectiveTo);
        });
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("XX")]
    [InlineData("AUT")]
    public void Get_UnknownOrBlank_ReturnsEmpty_WithoutAustriaFallback(string? code)
    {
        var rows = Registry.Get(code);

        Assert.Empty(rows);
        Assert.DoesNotContain(rows, r => r.CountryCode == CountryProfileCodes.Austria);
    }

    [Fact]
    public void Get_EuDefault_ReturnsEmpty()
    {
        Assert.Empty(Registry.Get(CountryProfileCodes.EuDefault));
        Assert.Empty(Registry.Get("eu_default"));
    }

    [Theory]
    [InlineData("at")]
    [InlineData(" AT ")]
    [InlineData("At")]
    public void Get_IsCaseInsensitiveAndTrims(string code)
    {
        var rows = Registry.Get(code);

        Assert.Equal(5, rows.Count);
        Assert.All(rows, row => Assert.Equal(CountryProfileCodes.Austria, row.CountryCode));
    }

    [Fact]
    public void CountryProfile_StillDoesNotCarryVatRates()
    {
        var rateLikeProperties = typeof(CountryProfile)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.Name.Contains("Rate", StringComparison.OrdinalIgnoreCase)
                        || p.Name.Contains("Percent", StringComparison.OrdinalIgnoreCase))
            .Select(p => p.Name)
            .ToList();

        Assert.Empty(rateLikeProperties);
    }

    [Fact]
    public void AustriaRates_MatchLiveTaxTypes()
    {
        var at = Registry.Get(CountryProfileCodes.Austria);

        Assert.Equal(
            TaxTypes.GetTaxRate(TaxTypes.Standard),
            Rate(at, CountryTaxTypeCodes.Standard));
        Assert.Equal(
            TaxTypes.GetTaxRate(TaxTypes.ReducedNew),
            Rate(at, CountryTaxTypeCodes.ReducedNew));
        Assert.Equal(
            TaxTypes.GetTaxRate(TaxTypes.Reduced),
            Rate(at, CountryTaxTypeCodes.Reduced1));
        Assert.Equal(
            TaxTypes.GetTaxRate(TaxTypes.Special),
            Rate(at, CountryTaxTypeCodes.Reduced2));
        Assert.Equal(
            TaxTypes.GetTaxRate(TaxTypes.ZeroRate),
            Rate(at, CountryTaxTypeCodes.Zero));
    }

    private static CountryTaxType Row(IReadOnlyList<CountryTaxType> rows, string code) =>
        Assert.Single(rows, r => r.Code == code);

    private static decimal Rate(IReadOnlyList<CountryTaxType> rows, string code) =>
        Row(rows, code).Rate;
}
