using System.Globalization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Strategies.EuDefault;
using KasseAPI_Final.Services.Countries.Vat;
using KasseAPI_Final.Services.FeatureFlags;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class EuDefaultTaxStrategyTests
{
    private static readonly ICountryProfileRegistry Profiles = new CountryProfileRegistry();

    private static TaxCalculationContext EuContext(
        VatRegime regime,
        string? buyerVatId = null) => new()
    {
        CountryProfile = Profiles.Get(CountryProfileCodes.EuDefault),
        VatRegime = regime,
        TaxExempt = false,
        BuyerVatId = buyerVatId,
    };

    private static IFeatureFlagService Flags(bool enabled)
    {
        var mock = new Mock<IFeatureFlagService>();
        mock.Setup(f => f.IsEnabled(FeatureFlagNames.EInvoicingEn16931, It.IsAny<string?>()))
            .Returns(enabled);
        return mock.Object;
    }

    private static EuDefaultTaxStrategy Strategy(
        bool enabled = true,
        IVatIdValidator? vatIdValidator = null) =>
        new(
            Profiles,
            vatIdValidator ?? new VatIdValidator(new DisabledViesClient()),
            Flags(enabled));

    [Theory]
    [InlineData("ATU12345678")]
    [InlineData("DE123456789")]
    [InlineData("CHE-123.456.789 MWST")]
    [InlineData("FR12345678901")]
    public void CalculateTax_ReverseCharge_ValidBuyerVatId_IsZeroRated(string buyerVatId)
    {
        var expected = CartMoneyHelper.ComputeLine(121m, 1, 0m);

        var result = Strategy().CalculateTax(
            [TaxLineItemInput.FromVatPercent(121m, 1, 21m)],
            EuContext(VatRegime.EU_REVERSE_CHARGE, buyerVatId));

        Assert.Equal(expected, Assert.Single(result.Lines));
        Assert.Equal(expected.LineNet, result.Totals.TotalNet);
        Assert.Equal(0m, result.Totals.TotalVat);
        Assert.Equal(expected.LineGross, result.Totals.TotalGross);
        Assert.Equal(0m, result.TaxSummary.Single().TaxRatePct);
        Assert.Equal(0m, result.TaxDetails["0"]);
        Assert.DoesNotContain(TaxTypes.Standard.ToString(), result.TaxDetails.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("XX")]
    [InlineData("AT12345678")]
    public void CalculateTax_ReverseCharge_MissingOrInvalidBuyerVatId_ThrowsVatIdShapeInvalid(
        string? buyerVatId)
    {
        var ex = Assert.Throws<VatIdShapeInvalidException>(() =>
            Strategy().CalculateTax(
                [TaxLineItemInput.FromVatPercent(121m, 1, 21m)],
                EuContext(VatRegime.EU_REVERSE_CHARGE, buyerVatId)));

        Assert.Equal(VatIdShapeInvalidException.Code, ex.ErrorCode);
        Assert.Equal(VatIdValidationResult.InvalidShapeErrorCode, ex.ErrorCode);
    }

    [Fact]
    public void CalculateTax_Oss_UsesLineVatPercent_AsInvariantKey()
    {
        var expected21 = CartMoneyHelper.ComputeLine(121m, 1, 21m);
        var expected10 = CartMoneyHelper.ComputeLine(110m, 1, 10m);

        var result = Strategy().CalculateTax(
            [
                TaxLineItemInput.FromVatPercent(121m, 1, 21m),
                TaxLineItemInput.FromVatPercent(110m, 1, 10m),
            ],
            EuContext(VatRegime.EU_OSS));

        Assert.Equal(2, result.Lines.Count);
        Assert.Equal(expected21, result.Lines[0]);
        Assert.Equal(expected10, result.Lines[1]);
        Assert.Equal(expected21.LineNet + expected10.LineNet, result.Totals.TotalNet);
        Assert.Equal(expected21.LineTax + expected10.LineTax, result.Totals.TotalVat);
        Assert.Equal(expected21.LineGross + expected10.LineGross, result.Totals.TotalGross);
        Assert.Equal(21m, result.TaxSummary.Single(s => s.TaxRatePct == 21m).TaxRatePct);
        Assert.Equal(10m, result.TaxSummary.Single(s => s.TaxRatePct == 10m).TaxRatePct);
        Assert.All(result.TaxSummary, s => Assert.Equal(0, s.TaxType));
        Assert.Equal(expected21.LineTax, result.TaxDetails[21m.ToString(CultureInfo.InvariantCulture)]);
        Assert.Equal(expected10.LineTax, result.TaxDetails[10m.ToString(CultureInfo.InvariantCulture)]);
        Assert.DoesNotContain(TaxTypes.Standard.ToString(), result.TaxDetails.Keys);
    }

    [Fact]
    public void CalculateTax_Oss_RejectsRksvTaxTypeLines()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Strategy().CalculateTax(
                [TaxLineItemInput.FromTaxType(10m, 1, TaxTypes.Standard)],
                EuContext(VatRegime.EU_OSS)));

        Assert.Contains("VAT percent", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void CalculateTax_NonEu_IsZeroRatedExport()
    {
        var expected = CartMoneyHelper.ComputeLine(100m, 2, 0m);

        var result = Strategy().CalculateTax(
            [TaxLineItemInput.FromVatPercent(100m, 2, 21m)],
            EuContext(VatRegime.NON_EU));

        Assert.Equal(expected, Assert.Single(result.Lines));
        Assert.Equal(0m, result.Totals.TotalVat);
        Assert.Equal(0m, result.TaxDetails["0"]);
    }

    [Fact]
    public void CalculateTax_FlagOff_ThrowsFeatureDisabled()
    {
        var ex = Assert.Throws<FeatureDisabledException>(() =>
            Strategy(enabled: false).CalculateTax(
                [TaxLineItemInput.FromVatPercent(121m, 1, 21m)],
                EuContext(VatRegime.NON_EU)));

        Assert.Equal(FeatureDisabledException.Code, ex.ErrorCode);
        Assert.Equal(FeatureFlagNames.EInvoicingEn16931, ex.FeatureName);
    }

    [Fact]
    public void DetermineInvoiceFields_ReverseCharge_RequiresCustomerVatId()
    {
        var company = new CompanySettings
        {
            Country = CountryProfileCodes.EuDefault,
            VatRegime = VatRegime.EU_REVERSE_CHARGE,
            CompanyTaxNumber = "FR12345678901",
        };

        var fields = Strategy().DetermineInvoiceFields(company, new Customer { TaxNumber = "DE123456789" });

        Assert.True(fields.RequiresCustomerVatId);
        Assert.Equal("DE123456789", fields.CustomerVatId);
    }

    [Fact]
    public void DetermineInvoiceFields_Oss_DoesNotRequireCustomerVatId()
    {
        var company = new CompanySettings
        {
            Country = CountryProfileCodes.EuDefault,
            VatRegime = VatRegime.EU_OSS,
        };

        var fields = Strategy().DetermineInvoiceFields(company, customer: null);

        Assert.False(fields.RequiresCustomerVatId);
    }

    [Fact]
    public void ValidateVatId_DelegatesToIVatIdValidator_Once()
    {
        var profile = Profiles.Get(CountryProfileCodes.EuDefault);
        var validator = new Mock<IVatIdValidator>(MockBehavior.Strict);
        validator
            .Setup(v => v.Validate("FR12345678901", profile))
            .Returns(VatIdValidationResult.Valid("FR12345678901"));

        var result = Strategy(vatIdValidator: validator.Object)
            .ValidateVatId("FR12345678901", profile);

        Assert.True(result.IsValid);
        Assert.Equal("FR12345678901", result.VatId);
        validator.Verify(v => v.Validate("FR12345678901", profile), Times.Once);
        validator.VerifyNoOtherCalls();
    }

    [Fact]
    public void ProjectFiscalTaxSets_ThrowsNotImplemented()
    {
        var ex = Assert.Throws<NotImplementedException>(() =>
            Strategy().ProjectFiscalTaxSets("{}", 0m));

        Assert.Contains(CountryStrategyDocs.EuDefault, ex.Message, StringComparison.Ordinal);
    }
}
