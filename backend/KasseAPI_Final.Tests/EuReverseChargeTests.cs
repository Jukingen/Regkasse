using System.Globalization;
using System.Text.Json.Nodes;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.EInvoicing;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Strategies.Austria;
using KasseAPI_Final.Services.Countries.Strategies.EuDefault;
using KasseAPI_Final.Services.Countries.Vat;
using KasseAPI_Final.Services.FeatureFlags;
using KasseAPI_Final.Tests.CountryBaseline;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Paket 12-b reverse charge pins, Paket 12-c AT cross-regime routing, and Paket 30-d OSS destination rates.
/// Does not call VIES.
/// </summary>
public sealed class EuReverseChargeTests
{
    private const string AtEuOssUnsupportedMessage = "AT tenant + EU_OSS is not supported";

    private static readonly ICountryProfileRegistry Profiles = new CountryProfileRegistry();
    private static readonly ICountryTaxTypeRegistry Rates = new CountryTaxTypeRegistry();
    private static readonly ITaxStrategyResolver TaxResolver = CountryStrategyWiring.CreateTaxResolver();
    private static readonly IInvoiceStrategyResolver InvoiceResolver = CountryStrategyWiring.CreateInvoiceResolver();
    private static readonly IVatIdValidator VatIds = new VatIdValidator(new DisabledViesClient());

    private static EuDefaultTaxStrategy EuTax() => new(Profiles, VatIds, featureFlags: null);

    private static EuDefaultInvoiceStrategy EuInvoice() => new(featureFlags: null);

    private static TaxCalculationContext EuContext(VatRegime regime, string? buyerVatId = null) => new()
    {
        CountryProfile = Profiles.Get(CountryProfileCodes.EuDefault),
        VatRegime = regime,
        TaxExempt = false,
        BuyerVatId = buyerVatId,
    };

    private static TaxCalculationContext AtContext(string? buyerVatId = null) => new()
    {
        CountryProfile = Profiles.Get(CountryProfileCodes.Austria),
        VatRegime = VatRegime.AT_RKSV_STANDARD,
        TaxExempt = false,
        BuyerVatId = buyerVatId,
    };

    private static CompanySettings EuCompany(VatRegime regime) => new()
    {
        Country = CountryProfileCodes.EuDefault,
        VatRegime = regime,
        CompanyName = "EU GmbH",
        CompanyAddress = "Brussels",
        CompanyTaxNumber = "FR12345678901",
    };

    private static CompanySettings AtCompany(VatRegime regime) => new()
    {
        Country = CountryProfileCodes.Austria,
        VatRegime = regime,
        CompanyName = "AT GmbH",
        CompanyAddress = "Vienna",
        CompanyTaxNumber = "ATU12345678",
    };

    private static TaxCalculationContext AtReverseChargeContext(string? buyerVatId) => new()
    {
        CountryProfile = Profiles.Get(CountryProfileCodes.Austria),
        VatRegime = VatRegime.EU_REVERSE_CHARGE,
        TaxExempt = false,
        BuyerVatId = buyerVatId,
    };

    private static IFeatureFlagService FlagsOff()
    {
        var mock = new Mock<IFeatureFlagService>();
        mock.Setup(f => f.IsEnabled(FeatureFlagNames.EInvoicingEn16931, It.IsAny<string?>()))
            .Returns(false);
        return mock.Object;
    }

    [Fact]
    public void ReverseCharge_ValidDeBuyerVatId_IsZeroRated()
    {
        var expected = CartMoneyHelper.ComputeLine(121m, 1, 0m);

        var result = EuTax().CalculateTax(
            [TaxLineItemInput.FromVatPercent(121m, 1, 21m)],
            EuContext(VatRegime.EU_REVERSE_CHARGE, "DE123456789"));

        Assert.Equal(expected, Assert.Single(result.Lines));
        Assert.Equal(CountryRateTaxSummary.Totals([expected]).TotalNet, result.Totals.TotalNet);
        Assert.Equal(CountryRateTaxSummary.Totals([expected]).TotalVat, result.Totals.TotalVat);
        Assert.Equal(CountryRateTaxSummary.Totals([expected]).TotalGross, result.Totals.TotalGross);
        Assert.Equal(0m, result.TaxSummary.Single().TaxRatePct);
        Assert.Equal(0m, result.TaxDetails["0"]);
        Assert.DoesNotContain(TaxTypes.Standard.ToString(), result.TaxDetails.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void ReverseCharge_MissingBuyerVatId_ThrowsVatIdShapeInvalid(string? buyerVatId)
    {
        var ex = Assert.Throws<VatIdShapeInvalidException>(() =>
            EuTax().CalculateTax(
                [TaxLineItemInput.FromVatPercent(121m, 1, 21m)],
                EuContext(VatRegime.EU_REVERSE_CHARGE, buyerVatId)));

        Assert.Equal(VatIdShapeInvalidException.Code, ex.ErrorCode);
    }

    [Theory]
    [InlineData("DE12")]
    [InlineData("AT12345678")]
    public void ReverseCharge_InvalidBuyerVatIdFormat_ThrowsVatIdShapeInvalid(string buyerVatId)
    {
        var ex = Assert.Throws<VatIdShapeInvalidException>(() =>
            EuTax().CalculateTax(
                [TaxLineItemInput.FromVatPercent(121m, 1, 21m)],
                EuContext(VatRegime.EU_REVERSE_CHARGE, buyerVatId)));

        Assert.Equal(VatIdShapeInvalidException.Code, ex.ErrorCode);
    }

    [Fact]
    public void DomesticAt_MatchesCountryBaselineStandard20Single()
    {
        var fixture = BaselineFixtureFile.Read("at-fiscal-chain.baseline.json");
        var row = fixture["vatCalculation"]!.AsArray()
            .Single(n => n!["case"]!.GetValue<string>() == "standard-20-single")!;

        var result = new AustriaTaxStrategy().CalculateTax(
            [TaxLineItemInput.FromTaxType(Money(row, "unitGross"), Qty(row), TaxTypes.Standard)],
            AtContext());

        var line = Assert.Single(result.Lines);
        Assert.Equal(Money(row, "lineNet"), line.LineNet);
        Assert.Equal(Money(row, "lineTax"), line.LineTax);
        Assert.Equal(Money(row, "lineGross"), line.LineGross);
        Assert.Equal(Money(row, "receiptTotalNet"), result.Totals.TotalNet);
        Assert.Equal(Money(row, "receiptTotalVat"), result.Totals.TotalVat);
        Assert.Equal(Money(row, "receiptTotalGross"), result.Totals.TotalGross);
    }

    [Theory]
    [InlineData("FR12345678901", 20)]
    [InlineData("IT12345678901", 22)]
    [InlineData("DE123456789", 19)]
    public void EuOss_UsesDestinationRate_FromOssRegistry(string buyerVatId, int expectedPercent)
    {
        var destination = CountryPaymentTaxLineMapper.ResolveOssDestinationCountry(
            VatRegime.EU_OSS,
            buyerVatId);
        var rate = (decimal)expectedPercent;
        var expected = CartMoneyHelper.ComputeLine(121m, 1, rate);

        var result = EuTax().CalculateTax(
            [TaxLineItemInput.FromVatPercent(121m, 1, 0m)],
            EuContext(VatRegime.EU_OSS, buyerVatId) with { DestinationCountry = destination });

        Assert.Equal(expected, Assert.Single(result.Lines));
        Assert.Equal(rate, result.TaxSummary.Single().TaxRatePct);
        Assert.Equal(expected.LineTax, result.TaxDetails[rate.ToString(CultureInfo.InvariantCulture)]);
    }

    [Fact]
    public void EuOss_UnknownDestination_ThrowsRateMissing()
    {
        var destination = CountryPaymentTaxLineMapper.ResolveOssDestinationCountry(
            VatRegime.EU_OSS,
            "XX123456789");

        var ex = Assert.Throws<ArgumentException>(() =>
            EuTax().CalculateTax(
                [TaxLineItemInput.FromVatPercent(121m, 1, 0m)],
                EuContext(VatRegime.EU_OSS, "XX123456789") with { DestinationCountry = destination }));

        Assert.Equal("OSS destination rate missing: XX", ex.Message);
    }

    [Fact]
    public void EuOss_MissingBuyerVatId_Throws()
    {
        var destination = CountryPaymentTaxLineMapper.ResolveOssDestinationCountry(VatRegime.EU_OSS, null);

        var ex = Assert.Throws<ArgumentException>(() =>
            EuTax().CalculateTax(
                [TaxLineItemInput.FromVatPercent(121m, 1, 0m)],
                EuContext(VatRegime.EU_OSS) with { DestinationCountry = destination }));

        Assert.Equal("OSS requires BuyerVatId to determine destination country", ex.Message);
    }

    [Fact]
    public void NonEu_IsZeroRatedExport()
    {
        var expected = CartMoneyHelper.ComputeLine(100m, 2, 0m);

        var result = EuTax().CalculateTax(
            [TaxLineItemInput.FromVatPercent(100m, 2, 21m)],
            EuContext(VatRegime.NON_EU));

        Assert.Equal(expected, Assert.Single(result.Lines));
        Assert.Equal(0m, result.Totals.TotalVat);
        Assert.Equal(0m, result.TaxDetails["0"]);
    }

    [Fact]
    public void ReverseCharge_Disclosures_ComeFromInvoiceStrategy()
    {
        var reverseCharge = EuInvoice().GetMandatoryDisclosures(
            EuCompany(VatRegime.EU_REVERSE_CHARGE),
            customer: null);
        var keys = reverseCharge.Select(d => d.Key).ToArray();

        Assert.Contains("buyer.vatId", keys);
        Assert.Contains("invoice.taxBreakdown", keys);
        var rc = reverseCharge.Single(d => d.Key == "invoice.reverseCharge");
        Assert.Contains("Reverse charge", rc.LegalBasis, StringComparison.Ordinal);
        Assert.Equal(nameof(PaymentDetails.TaxAmount), rc.SourceField);

        var tax = EuTax().CalculateTax(
            [TaxLineItemInput.FromVatPercent(121m, 1, 21m)],
            EuContext(VatRegime.EU_REVERSE_CHARGE, "DE123456789"));
        Assert.Equal(0m, tax.TaxDetails["0"]);
        Assert.Equal(0m, tax.Totals.TotalVat);

        foreach (var regime in new[] { VatRegime.EU_OSS, VatRegime.NON_EU })
        {
            var other = EuInvoice().GetMandatoryDisclosures(EuCompany(regime), customer: null)
                .Select(d => d.Key)
                .ToArray();
            Assert.DoesNotContain("invoice.reverseCharge", other);
            Assert.DoesNotContain("buyer.vatId", other);
        }
    }

    [Fact]
    public void AtTenant_EuReverseCharge_ResolvesEuDefaultTaxStrategy()
    {
        var at = Profiles.Get(CountryProfileCodes.Austria);
        var tax = TaxResolver.Resolve(at, VatRegime.EU_REVERSE_CHARGE);
        var invoice = InvoiceResolver.Resolve(at, VatRegime.EU_REVERSE_CHARGE);

        Assert.IsType<EuDefaultTaxStrategy>(tax);
        Assert.IsType<EuDefaultInvoiceStrategy>(invoice);

        var result = tax.CalculateTax(
            [TaxLineItemInput.FromVatPercent(121m, 1, 21m)],
            AtReverseChargeContext("DE123456789"));

        Assert.Equal(0m, result.Totals.TotalVat);
        Assert.Equal(0m, result.TaxDetails["0"]);

        var keys = invoice.GetMandatoryDisclosures(AtCompany(VatRegime.EU_REVERSE_CHARGE), customer: null)
            .Select(d => d.Key)
            .ToArray();
        Assert.Contains("invoice.reverseCharge", keys);
        Assert.Contains("buyer.vatId", keys);

        foreach (var buyerVatId in new string?[] { null, "", "DE12" })
        {
            var ex = Assert.Throws<VatIdShapeInvalidException>(() =>
                tax.CalculateTax(
                    [TaxLineItemInput.FromVatPercent(121m, 1, 21m)],
                    AtReverseChargeContext(buyerVatId)));
            Assert.Equal(VatIdShapeInvalidException.Code, ex.ErrorCode);
        }
    }

    [Fact]
    public void AtTenant_AtRksvStandard_StillResolvesAustriaTaxStrategy()
    {
        var at = Profiles.Get(CountryProfileCodes.Austria);

        Assert.IsType<AustriaTaxStrategy>(TaxResolver.Resolve(at, VatRegime.AT_RKSV_STANDARD));
        Assert.IsType<AustriaInvoiceStrategy>(InvoiceResolver.Resolve(at, VatRegime.AT_RKSV_STANDARD));
    }

    [Fact]
    public void AtTenant_EuOss_ThrowsWithClearMessage()
    {
        var at = Profiles.Get(CountryProfileCodes.Austria);

        var taxEx = Assert.Throws<ArgumentException>(() => TaxResolver.Resolve(at, VatRegime.EU_OSS));
        var invoiceEx = Assert.Throws<ArgumentException>(() => InvoiceResolver.Resolve(at, VatRegime.EU_OSS));

        Assert.Equal(AtEuOssUnsupportedMessage, taxEx.Message);
        Assert.Equal(AtEuOssUnsupportedMessage, invoiceEx.Message);
    }

    [Fact]
    public void AtTenant_EuReverseCharge_WorksWhenEn16931FlagIsOff()
    {
        var flags = FlagsOff();
        var tax = new EuDefaultTaxStrategy(Profiles, VatIds, flags);
        var invoice = new EuDefaultInvoiceStrategy(flags);

        var result = tax.CalculateTax(
            [TaxLineItemInput.FromVatPercent(121m, 1, 21m)],
            AtReverseChargeContext("DE123456789"));

        Assert.Equal(0m, result.Totals.TotalVat);
        Assert.Equal(0m, result.TaxDetails["0"]);

        var keys = invoice.GetMandatoryDisclosures(AtCompany(VatRegime.EU_REVERSE_CHARGE), customer: null)
            .Select(d => d.Key)
            .ToArray();
        Assert.Contains("invoice.reverseCharge", keys);
        Assert.Contains("buyer.vatId", keys);
    }

    [Fact]
    public async Task AtTenant_EuReverseCharge_XmlBuilderStillGatedByFlag()
    {
        IEn16931XmlBuilder builder = new En16931UblXmlBuilder(FlagsOff());

        var ex = await Assert.ThrowsAsync<FeatureDisabledException>(() =>
            builder.BuildXmlAsync(new InvoiceDocumentDto { CountryCode = CountryProfileCodes.EuDefault }));

        Assert.Equal(FeatureDisabledException.Code, ex.ErrorCode);
        Assert.Equal(FeatureFlagNames.EInvoicingEn16931, ex.FeatureName);
    }

    private static decimal Money(JsonNode row, string name) =>
        decimal.Parse(row[name]!.GetValue<string>(), CultureInfo.InvariantCulture);

    private static int Qty(JsonNode row) => row["quantity"]!.GetValue<int>();
}
