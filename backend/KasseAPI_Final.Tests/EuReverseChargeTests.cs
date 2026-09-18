using System.Globalization;
using System.Text.Json.Nodes;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Strategies.Austria;
using KasseAPI_Final.Services.Countries.Strategies.EuDefault;
using KasseAPI_Final.Services.Countries.Vat;
using KasseAPI_Final.Tests.CountryBaseline;
using Xunit;

namespace KasseAPI_Final.Tests;

/// <summary>
/// Paket 12-b: EU reverse charge + OSS placeholder pins. Does not call VIES.
/// </summary>
public sealed class EuReverseChargeTests
{
    private static readonly ICountryProfileRegistry Profiles = new CountryProfileRegistry();
    private static readonly ICountryTaxTypeRegistry Rates = new CountryTaxTypeRegistry();
    private static readonly ITaxStrategyResolver TaxResolver = CountryStrategyWiring.CreateTaxResolver();
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

    [Fact]
    public void EuOss_CurrentlyUsesAtRates_TemporaryUntilPaket30d()
    {
        // TEMP: OSS uses AT rates as placeholder until real OSS table (Paket 30-d).
        var line = CountryPaymentTaxLineMapper.FromProductTaxType(
            Profiles.Get(CountryProfileCodes.EuDefault),
            121m,
            1,
            TaxTypes.Standard,
            Rates);

        Assert.Equal(TaxTypes.GetTaxRate(TaxTypes.Standard), line.VatRatePercent);
        Assert.Equal(20m, line.VatRatePercent);

        var expected = CartMoneyHelper.ComputeLine(121m, 1, line.VatRatePercent!.Value);
        var result = EuTax().CalculateTax([line], EuContext(VatRegime.EU_OSS));

        Assert.Equal(expected, Assert.Single(result.Lines));
        var ossKey = line.VatRatePercent.Value.ToString(CultureInfo.InvariantCulture);
        Assert.Equal(expected.LineTax, result.TaxDetails[ossKey]);
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
    public void AtTenant_EuReverseChargeRegime_StillResolvesAustriaTaxStrategy()
    {
        var at = Profiles.Get(CountryProfileCodes.Austria);
        var strategy = TaxResolver.Resolve(at, VatRegime.EU_REVERSE_CHARGE);
        Assert.IsType<AustriaTaxStrategy>(strategy);

        var expected = new AustriaTaxStrategy().CalculateTax(
            [TaxLineItemInput.FromTaxType(121m, 1, TaxTypes.Standard)],
            AtContext());

        var withBuyerVatId = strategy.CalculateTax(
            [TaxLineItemInput.FromTaxType(121m, 1, TaxTypes.Standard)],
            new TaxCalculationContext
            {
                CountryProfile = at,
                VatRegime = VatRegime.EU_REVERSE_CHARGE,
                TaxExempt = false,
                BuyerVatId = "DE123456789",
            });

        Assert.Equal(expected.Totals.TotalVat, withBuyerVatId.Totals.TotalVat);
        Assert.Equal(expected.Totals.TotalGross, withBuyerVatId.Totals.TotalGross);
        Assert.NotEqual(0m, withBuyerVatId.Totals.TotalVat);
    }

    private static decimal Money(JsonNode row, string name) =>
        decimal.Parse(row[name]!.GetValue<string>(), CultureInfo.InvariantCulture);

    private static int Qty(JsonNode row) => row["quantity"]!.GetValue<int>();
}
