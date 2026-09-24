using System.Text.Json;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Strategies.Germany;
using KasseAPI_Final.Services.Countries.Vat;
using KasseAPI_Final.Services.FeatureFlags;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class GermanyTaxStrategyTests
{
    private static readonly ICountryProfileRegistry Profiles = new CountryProfileRegistry();
    private static readonly ICountryTaxTypeRegistry RateCatalog = new CountryTaxTypeRegistry();

    private static TaxCalculationContext DeContext() => new()
    {
        CountryProfile = Profiles.Get(CountryProfileCodes.Germany),
        VatRegime = VatRegime.DE_USTG_STANDARD,
        TaxExempt = false,
    };

    private static IFeatureFlagService Flags(bool enabled)
    {
        var mock = new Mock<IFeatureFlagService>();
        mock.Setup(f => f.IsEnabled(FeatureFlagNames.FiscalKassenSicherheitDe, It.IsAny<string?>()))
            .Returns(enabled);
        return mock.Object;
    }

    private static GermanyTaxStrategy Strategy(bool enabled = true) =>
        new(RateCatalog, new VatIdValidator(new DisabledViesClient()), Flags(enabled));

    [Fact]
    public void CalculateTax_UsesDe19And7Rates_WithoutAustrianBuckets()
    {
        var expected19 = CartMoneyHelper.ComputeLine(119m, 1, 19m);
        var expected7 = CartMoneyHelper.ComputeLine(107m, 1, 7m);

        var result = Strategy().CalculateTax(
            [
                TaxLineItemInput.FromVatPercent(119m, 1, 19m),
                TaxLineItemInput.FromVatPercent(107m, 1, 7m),
            ],
            DeContext());

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(expected19, result.Lines[0]);
        Assert.Equal(expected7, result.Lines[1]);
        Assert.Equal(expected19.LineNet + expected7.LineNet, result.Totals.TotalNet);
        Assert.Equal(expected19.LineTax + expected7.LineTax, result.Totals.TotalVat);
        Assert.Equal(expected19.LineGross + expected7.LineGross, result.Totals.TotalGross);

        Assert.Equal(19m, result.TaxSummary.Single(s => s.TaxRatePct == 19m).TaxRatePct);
        Assert.Equal(7m, result.TaxSummary.Single(s => s.TaxRatePct == 7m).TaxRatePct);
        Assert.All(result.TaxSummary, s => Assert.Equal(0, s.TaxType));

        Assert.Equal(expected19.LineTax, result.TaxDetails[CountryTaxTypeCodes.Standard]);
        Assert.Equal(expected7.LineTax, result.TaxDetails[CountryTaxTypeCodes.Reduced1]);
        Assert.DoesNotContain(TaxTypes.Standard.ToString(), result.TaxDetails.Keys);
    }

    [Fact]
    public void CalculateTax_RejectsRksvTaxTypeLines()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Strategy().CalculateTax(
                [TaxLineItemInput.FromTaxType(10m, 1, TaxTypes.Standard)],
                DeContext()));

        Assert.Contains("CountryTaxType", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CalculateTax_FlagOff_ThrowsFeatureDisabled()
    {
        var ex = Assert.Throws<FeatureDisabledException>(() =>
            Strategy(enabled: false).CalculateTax(
                [TaxLineItemInput.FromVatPercent(119m, 1, 19m)],
                DeContext()));

        Assert.Equal(FeatureDisabledException.Code, ex.ErrorCode);
        Assert.Equal(FeatureFlagNames.FiscalKassenSicherheitDe, ex.FeatureName);
    }

    [Fact]
    public void ProjectFiscalTaxSets_ThrowsNotImplemented_AtOnly()
    {
        var ex = Assert.Throws<NotImplementedException>(() =>
            Strategy().ProjectFiscalTaxSets("{}", 0m));

        Assert.Contains("AT-only", ex.Message, StringComparison.Ordinal);
        Assert.Contains("ProjectDeFiscalTaxSets", ex.Message, StringComparison.Ordinal);
        Assert.Contains(CountryStrategyDocs.Germany, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectDeFiscalTaxSets_MapsCalculateTaxDetails()
    {
        var calc = Strategy().CalculateTax(
            [
                TaxLineItemInput.FromVatPercent(119m, 1, 19m),
                TaxLineItemInput.FromVatPercent(107m, 1, 7m),
            ],
            DeContext());

        var json = JsonSerializer.Serialize(calc.TaxDetails);
        var projection = Strategy().ProjectDeFiscalTaxSets(json, calc.Totals.TotalGross);

        Assert.Equal(2, projection.AmountsPerVatRate.Count);
        Assert.Equal(DeVatRateNames.Normal, projection.AmountsPerVatRate[0].VatRate);
        Assert.Equal("119.00", projection.AmountsPerVatRate[0].Amount);
        Assert.Equal(DeVatRateNames.Reduced1, projection.AmountsPerVatRate[1].VatRate);
        Assert.Equal("107.00", projection.AmountsPerVatRate[1].Amount);
    }

    [Fact]
    public void ProjectDeFiscalTaxSets_Empty_YieldsNullZero()
    {
        var projection = Strategy().ProjectDeFiscalTaxSets("{}", 0m);

        var only = Assert.Single(projection.AmountsPerVatRate);
        Assert.Equal(new DeAmountPerVatRate(DeVatRateNames.Null, "0.00"), only);
    }

    [Fact]
    public void ProjectDeFiscalTaxSets_FlagOff_ThrowsFeatureDisabled()
    {
        var ex = Assert.Throws<FeatureDisabledException>(() =>
            Strategy(enabled: false).ProjectDeFiscalTaxSets("{}", 0m));

        Assert.Equal(FeatureFlagNames.FiscalKassenSicherheitDe, ex.FeatureName);
    }
}
