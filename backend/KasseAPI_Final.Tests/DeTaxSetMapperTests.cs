using System.Globalization;
using System.Text.Json;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries.Strategies.Germany;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class DeTaxSetMapperTests
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Fact]
    public void MapFromLinePercents_Maps19_7_0_ToNamedEnums()
    {
        var projection = DeTaxSetMapper.MapFromLinePercents(
        [
            (19m, 119m),
            (7m, 107m),
            (0m, 50m),
        ]);

        Assert.Equal(3, projection.AmountsPerVatRate.Count);
        Assert.Equal(new DeAmountPerVatRate(DeVatRateNames.Normal, "119.00"), projection.AmountsPerVatRate[0]);
        Assert.Equal(new DeAmountPerVatRate(DeVatRateNames.Reduced1, "107.00"), projection.AmountsPerVatRate[1]);
        Assert.Equal(new DeAmountPerVatRate(DeVatRateNames.Null, "50.00"), projection.AmountsPerVatRate[2]);
    }

    [Fact]
    public void MapFromLinePercents_AggregatesSameRate_AndDropsZeroGross()
    {
        var projection = DeTaxSetMapper.MapFromLinePercents(
        [
            (19m, 10m),
            (19m, 5.555m),
            (7m, 0m),
        ]);

        var only = Assert.Single(projection.AmountsPerVatRate);
        Assert.Equal(DeVatRateNames.Normal, only.VatRate);
        Assert.Equal("15.56", only.Amount); // 10 + 5.56 AwayFromZero
    }

    [Theory]
    [InlineData(13)]
    [InlineData(4.9)]
    [InlineData(10.7)]
    [InlineData(20)]
    public void MapFromLinePercents_RejectsUnsupportedRates(decimal percent)
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            DeTaxSetMapper.MapFromLinePercents([(percent, 10m)]));

        Assert.Contains(percent.ToString(Invariant), ex.Message, StringComparison.Ordinal);
        Assert.Contains("13", ex.Message, StringComparison.Ordinal);
        Assert.Contains("4.9", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MapFromLinePercents_Empty_YieldsNullZero()
    {
        var projection = DeTaxSetMapper.MapFromLinePercents([]);

        var only = Assert.Single(projection.AmountsPerVatRate);
        Assert.Equal(new DeAmountPerVatRate(DeVatRateNames.Null, "0.00"), only);
    }

    [Fact]
    public void MapFromLinePercents_AllZeroGross_YieldsNullZero()
    {
        var projection = DeTaxSetMapper.MapFromLinePercents([(19m, 0m), (7m, 0m)]);

        var only = Assert.Single(projection.AmountsPerVatRate);
        Assert.Equal(new DeAmountPerVatRate(DeVatRateNames.Null, "0.00"), only);
    }

    [Fact]
    public void MapFromTaxDetailsJson_MapsStandardAndReduced1()
    {
        // tax 19 on 119 gross @19%; tax 7 on 107 gross @7%
        var json = JsonSerializer.Serialize(new Dictionary<string, decimal>
        {
            [CountryTaxTypeCodes.Standard] = 19m,
            [CountryTaxTypeCodes.Reduced1] = 7m,
        });

        var projection = DeTaxSetMapper.MapFromTaxDetailsJson(json, totalGross: 226m);

        Assert.Equal(2, projection.AmountsPerVatRate.Count);
        Assert.Equal(new DeAmountPerVatRate(DeVatRateNames.Normal, "119.00"), projection.AmountsPerVatRate[0]);
        Assert.Equal(new DeAmountPerVatRate(DeVatRateNames.Reduced1, "107.00"), projection.AmountsPerVatRate[1]);
    }

    [Fact]
    public void MapFromTaxDetailsJson_ZeroCode_UsesTotalGross()
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, decimal>
        {
            [CountryTaxTypeCodes.Zero] = 0m,
        });

        var projection = DeTaxSetMapper.MapFromTaxDetailsJson(json, totalGross: 42.5m);

        var only = Assert.Single(projection.AmountsPerVatRate);
        Assert.Equal(new DeAmountPerVatRate(DeVatRateNames.Null, "42.50"), only);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    public void MapFromTaxDetailsJson_Empty_YieldsNullZero(string? json)
    {
        var projection = DeTaxSetMapper.MapFromTaxDetailsJson(json, totalGross: 99m);

        var only = Assert.Single(projection.AmountsPerVatRate);
        Assert.Equal(new DeAmountPerVatRate(DeVatRateNames.Null, "0.00"), only);
    }

    [Theory]
    [InlineData("REDUCED_2")]
    [InlineData("REDUCED_NEW")]
    [InlineData("2")]
    [InlineData("LODGING")]
    [InlineData("UNKNOWN")]
    public void MapFromTaxDetailsJson_RejectsUnsupportedCodes(string code)
    {
        var json = $"{{\"{code}\":1.0}}";

        var ex = Assert.Throws<ArgumentException>(() =>
            DeTaxSetMapper.MapFromTaxDetailsJson(json, totalGross: 10m));

        Assert.Contains(code, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MapFromTaxDetailsJson_DoesNotEmitDeprecatedDecimalVatRates()
    {
        var projection = DeTaxSetMapper.MapFromLinePercents([(19m, 11.9m)]);

        var only = Assert.Single(projection.AmountsPerVatRate);
        Assert.Equal(DeVatRateNames.Normal, only.VatRate);
        Assert.DoesNotContain("\"19\"", only.VatRate, StringComparison.Ordinal);
    }
}
