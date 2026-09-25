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

public sealed class DeReceiptPayloadMapperTests
{
    private static readonly ICountryProfileRegistry Profiles = new CountryProfileRegistry();
    private static readonly ICountryTaxTypeRegistry RateCatalog = new CountryTaxTypeRegistry();

    private static GermanyTaxStrategy Strategy(bool enabled = true)
    {
        var flags = new Mock<IFeatureFlagService>();
        flags.Setup(f => f.IsEnabled(FeatureFlagNames.FiscalKassenSicherheitDe, It.IsAny<string?>()))
            .Returns(enabled);
        return new GermanyTaxStrategy(RateCatalog, new VatIdValidator(new DisabledViesClient()), flags.Object);
    }

    private static DeReceiptPayloadMapper Mapper(bool enabled = true) => new(Strategy(enabled));

    [Fact]
    public void FromPayment_Cash_Maps19And7ToReceiptAndCashEur()
    {
        var strategy = Strategy();
        var calc = strategy.CalculateTax(
            [
                TaxLineItemInput.FromVatPercent(119m, 1, 19m),
                TaxLineItemInput.FromVatPercent(107m, 1, 7m),
            ],
            new TaxCalculationContext
            {
                CountryProfile = Profiles.Get(CountryProfileCodes.Germany),
                VatRegime = VatRegime.DE_USTG_STANDARD,
            });

        var payload = new DeReceiptPayloadMapper(strategy).FromPayment(Payment(
            JsonSerializer.Serialize(calc.TaxDetails),
            calc.Totals.TotalGross,
            "0",
            "DE-dev-1-1"));

        Assert.Equal(DeReceiptTypes.Receipt, payload.StandardV1.Receipt.ReceiptType);
        Assert.Equal(
            [
                new DeAmountPerVatRate(DeVatRateNames.Normal, "119.00"),
                new DeAmountPerVatRate(DeVatRateNames.Reduced1, "107.00"),
            ],
            payload.StandardV1.Receipt.AmountsPerVatRate);
        var payment = Assert.Single(payload.StandardV1.Receipt.AmountsPerPaymentType);
        Assert.Equal(new DeAmountPerPaymentType(DePaymentTypes.Cash, "226.00", DePaymentTypes.Eur), payment);
        Assert.Null(payload.Raw);
        Assert.Equal("DE-dev-1-1", payload.Belegnummer);
        Assert.DoesNotContain(payload.StandardV1.Receipt.AmountsPerVatRate, row =>
            row.VatRate is "STANDARD" or "ZERO" or "SPECIAL" or "REDUCED_2");
    }

    [Fact]
    public void FromPayment_EmptyTaxDetails_IsNullZeroReceipt()
    {
        var payload = Mapper().FromPayment(Payment("{}", 0m, "0", "DE-dev-1-2"));

        Assert.Equal(DeReceiptTypes.Receipt, payload.StandardV1.Receipt.ReceiptType);
        var only = Assert.Single(payload.StandardV1.Receipt.AmountsPerVatRate);
        Assert.Equal(new DeAmountPerVatRate(DeVatRateNames.Null, "0.00"), only);
        var payment = Assert.Single(payload.StandardV1.Receipt.AmountsPerPaymentType);
        Assert.Equal(DePaymentTypes.Cash, payment.PaymentType);
        Assert.Equal("0.00", payment.Amount);
        Assert.Null(payload.Raw);
    }

    [Fact]
    public void FromPayment_CardRaw_IsNonCash()
    {
        var payload = Mapper().FromPayment(Payment("{}", 0m, "1", "DE-dev-1-3"));
        var payment = Assert.Single(payload.StandardV1.Receipt.AmountsPerPaymentType);
        Assert.Equal(DePaymentTypes.NonCash, payment.PaymentType);
        Assert.Equal(DePaymentTypes.Eur, payment.CurrencyCode);
    }

    [Fact]
    public void FromPayment_EmptyRaw_IsNonCash()
    {
        var payload = Mapper().FromPayment(Payment("{}", 0m, "", "DE-dev-1-4"));
        var payment = Assert.Single(payload.StandardV1.Receipt.AmountsPerPaymentType);
        Assert.Equal(DePaymentTypes.NonCash, payment.PaymentType);
    }

    [Fact]
    public void FromPayment_FlagOff_ThrowsFeatureDisabled()
    {
        var ex = Assert.Throws<FeatureDisabledException>(() =>
            Mapper(enabled: false).FromPayment(Payment("{}", 0m, "0", "DE-dev-1-5")));

        Assert.Equal(FeatureFlagNames.FiscalKassenSicherheitDe, ex.FeatureName);
    }

    [Fact]
    public void FromPayment_ThirteenPercent_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            Mapper().FromPayment(Payment("""{"13":1.00}""", 1m, "0", "DE-dev-1-6")));
    }

    private static PaymentDetails Payment(string taxDetailsJson, decimal total, string rawMethod, string belegNr) =>
        new()
        {
            TaxDetails = JsonDocument.Parse(taxDetailsJson),
            TotalAmount = total,
            PaymentMethodRaw = rawMethod,
            ReceiptNumber = belegNr,
        };
}
