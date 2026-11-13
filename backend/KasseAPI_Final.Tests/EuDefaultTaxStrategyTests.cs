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
        string? buyerVatId = null,
        string? destinationCountry = null) => new()
    {
        CountryProfile = Profiles.Get(CountryProfileCodes.EuDefault),
        VatRegime = regime,
        TaxExempt = false,
        BuyerVatId = buyerVatId,
        DestinationCountry = destinationCountry,
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

    [Theory]
    [InlineData("FR", 20)]
    [InlineData("IT", 22)]
    [InlineData("DE", 19)]
    public void CalculateTax_Oss_UsesDestinationStandardRate(string destinationCountry, int expectedPercent)
    {
        var rate = (decimal)expectedPercent;
        var expected = CartMoneyHelper.ComputeLine(121m, 1, rate);

        var result = Strategy().CalculateTax(
            [TaxLineItemInput.FromVatPercent(121m, 1, 0m)],
            EuContext(VatRegime.EU_OSS, destinationCountry: destinationCountry));

        Assert.Equal(expected, Assert.Single(result.Lines));
        Assert.Equal(rate, result.TaxSummary.Single().TaxRatePct);
        Assert.Equal(expected.LineTax, result.TaxDetails[rate.ToString(CultureInfo.InvariantCulture)]);
        Assert.DoesNotContain(TaxTypes.Standard.ToString(), result.TaxDetails.Keys);
    }

    [Fact]
    public void CalculateTax_Oss_UnknownDestination_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Strategy().CalculateTax(
                [TaxLineItemInput.FromVatPercent(121m, 1, 0m)],
                EuContext(VatRegime.EU_OSS, destinationCountry: "XX")));

        Assert.Equal("OSS destination rate missing: XX", ex.Message);
    }

    [Fact]
    public void CalculateTax_Oss_MissingBuyerVatId_Throws()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Strategy().CalculateTax(
                [TaxLineItemInput.FromVatPercent(121m, 1, 0m)],
                EuContext(VatRegime.EU_OSS)));

        Assert.Equal("OSS requires BuyerVatId to determine destination country", ex.Message);
    }

    [Fact]
    public void CalculateTax_Oss_RejectsRksvTaxTypeLines()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            Strategy().CalculateTax(
                [TaxLineItemInput.FromTaxType(10m, 1, TaxTypes.Standard)],
                EuContext(VatRegime.EU_OSS, destinationCountry: "FR")));

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
