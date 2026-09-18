using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Strategies.Switzerland;
using KasseAPI_Final.Services.Countries.Vat;
using KasseAPI_Final.Services.FeatureFlags;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class SwitzerlandTaxStrategyTests
{
    private static readonly ICountryProfileRegistry Profiles = new CountryProfileRegistry();
    private static readonly ICountryTaxTypeRegistry RateCatalog = new CountryTaxTypeRegistry();

    private static TaxCalculationContext ChContext() => new()
    {
        CountryProfile = Profiles.Get(CountryProfileCodes.Switzerland),
        VatRegime = VatRegime.CH_MWST_STANDARD,
        TaxExempt = false,
    };

    private static IFeatureFlagService Flags(bool enabled)
    {
        var mock = new Mock<IFeatureFlagService>();
        mock.Setup(f => f.IsEnabled(FeatureFlagNames.FiscalMwstCh, It.IsAny<string?>()))
            .Returns(enabled);
        return mock.Object;
    }

    private static SwitzerlandTaxStrategy Strategy(
        bool enabled = true,
        IVatIdValidator? vatIdValidator = null) =>
        new(
            RateCatalog,
            vatIdValidator ?? new VatIdValidator(new DisabledViesClient()),
            Flags(enabled));

    [Fact]
    public void CalculateTax_UsesCh81And26And38Rates_WithoutAustrianBuckets()
    {
        var expected81 = CartMoneyHelper.ComputeLine(108.1m, 1, 8.1m);
        var expected26 = CartMoneyHelper.ComputeLine(102.6m, 1, 2.6m);
        var expected38 = CartMoneyHelper.ComputeLine(103.8m, 1, 3.8m);

        var result = Strategy().CalculateTax(
            [
                TaxLineItemInput.FromVatPercent(108.1m, 1, 8.1m),
                TaxLineItemInput.FromVatPercent(102.6m, 1, 2.6m),
                TaxLineItemInput.FromVatPercent(103.8m, 1, 3.8m),
            ],
            ChContext());

        Assert.Equal(3, result.Lines.Count);
        Assert.Equal(expected81, result.Lines[0]);
        Assert.Equal(expected26, result.Lines[1]);
        Assert.Equal(expected38, result.Lines[2]);
        Assert.Equal(expected81.LineNet + expected26.LineNet + expected38.LineNet, result.Totals.TotalNet);
        Assert.Equal(expected81.LineTax + expected26.LineTax + expected38.LineTax, result.Totals.TotalVat);
        Assert.Equal(expected81.LineGross + expected26.LineGross + expected38.LineGross, result.Totals.TotalGross);

        Assert.Equal(8.1m, result.TaxSummary.Single(s => s.TaxRatePct == 8.1m).TaxRatePct);
        Assert.Equal(2.6m, result.TaxSummary.Single(s => s.TaxRatePct == 2.6m).TaxRatePct);
        Assert.Equal(3.8m, result.TaxSummary.Single(s => s.TaxRatePct == 3.8m).TaxRatePct);
        Assert.All(result.TaxSummary, s => Assert.Equal(0, s.TaxType));

        Assert.Equal(expected81.LineTax, result.TaxDetails[CountryTaxTypeCodes.Standard]);
        Assert.Equal(expected26.LineTax, result.TaxDetails[CountryTaxTypeCodes.Reduced1]);
        Assert.Equal(expected38.LineTax, result.TaxDetails[CountryTaxTypeCodes.Lodging]);
        Assert.DoesNotContain(TaxTypes.Standard.ToString(), result.TaxDetails.Keys);
    }

    [Fact]
    public void CalculateTax_RejectsRksvTaxTypeLines()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Strategy().CalculateTax(
                [TaxLineItemInput.FromTaxType(10m, 1, TaxTypes.Standard)],
                ChContext()));

        Assert.Contains("CountryTaxType", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CalculateTax_FlagOff_ThrowsFeatureDisabled()
    {
        var ex = Assert.Throws<FeatureDisabledException>(() =>
            Strategy(enabled: false).CalculateTax(
                [TaxLineItemInput.FromVatPercent(108.1m, 1, 8.1m)],
                ChContext()));

        Assert.Equal(FeatureDisabledException.Code, ex.ErrorCode);
        Assert.Equal(FeatureFlagNames.FiscalMwstCh, ex.FeatureName);
    }

    [Fact]
    public void ValidateVatId_DelegatesToIVatIdValidator_Once()
    {
        var profile = Profiles.Get(CountryProfileCodes.Switzerland);
        var validator = new Mock<IVatIdValidator>(MockBehavior.Strict);
        validator
            .Setup(v => v.Validate("CHE-123.456.789 MWST", profile))
            .Returns(VatIdValidationResult.Valid("CHE-123.456.789 MWST"));

        var result = Strategy(vatIdValidator: validator.Object)
            .ValidateVatId("CHE-123.456.789 MWST", profile);

        Assert.True(result.IsValid);
        Assert.Equal("CHE-123.456.789 MWST", result.VatId);
        validator.Verify(v => v.Validate("CHE-123.456.789 MWST", profile), Times.Once);
        validator.VerifyNoOtherCalls();
    }

    [Fact]
    public void ProjectFiscalTaxSets_ThrowsNotImplemented()
    {
        var ex = Assert.Throws<NotImplementedException>(() =>
            Strategy().ProjectFiscalTaxSets("{}", 0m));

        Assert.Contains(CountryStrategyDocs.Switzerland, ex.Message, StringComparison.Ordinal);
    }
}
