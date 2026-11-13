using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Strategies.EuDefault;
using KasseAPI_Final.Services.FeatureFlags;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class EuDefaultInvoiceStrategyTests
{
    private static IFeatureFlagService Flags(bool enabled)
    {
        var mock = new Mock<IFeatureFlagService>();
        mock.Setup(f => f.IsEnabled(FeatureFlagNames.EInvoicingEn16931, It.IsAny<string?>()))
            .Returns(enabled);
        return mock.Object;
    }

    private static CompanySettings EuCompany(VatRegime regime = VatRegime.EU_OSS) => new()
    {
        Country = CountryProfileCodes.EuDefault,
        VatRegime = regime,
        CompanyName = "EU GmbH",
        CompanyAddress = "Brussels",
        CompanyTaxNumber = "FR12345678901",
    };

    private static PaymentDetails Payment() => new()
    {
        ReceiptNumber = "RE-EU-1",
        TotalAmount = 121m,
        TaxAmount = 21m,
        Notes = "Beratung",
        CustomerName = "Gast",
        CashierId = "c1",
        PaymentMethodRaw = "0",
        Steuernummer = "ATU12345678",
        CashRegisterId = Guid.NewGuid(),
    };

    [Fact]
    public void GetMandatoryDisclosures_ContainsEn16931Fields()
    {
        var strategy = new EuDefaultInvoiceStrategy(Flags(true));
        var disclosures = strategy.GetMandatoryDisclosures(EuCompany(), null);
        var keys = disclosures.Select(d => d.Key).ToArray();

        Assert.Contains("seller.name", keys);
        Assert.Contains("seller.vatId", keys);
        Assert.Contains("buyer.name", keys);
        Assert.Contains("invoice.number", keys);
        Assert.Contains("invoice.date", keys);
        Assert.Contains("invoice.currency", keys);
        Assert.Contains("invoice.lines", keys);
        Assert.Contains("invoice.net", keys);
        Assert.Contains("invoice.tax", keys);
        Assert.Contains("invoice.gross", keys);
        Assert.Contains("invoice.taxBreakdown", keys);
        Assert.DoesNotContain("buyer.vatId", keys);
        Assert.All(disclosures, d => Assert.Equal("EN 16931", d.LegalBasis));
    }

    [Fact]
    public void GetMandatoryDisclosures_ReverseCharge_IncludesBuyerVatId()
    {
        var strategy = new EuDefaultInvoiceStrategy(Flags(true));
        var keys = strategy
            .GetMandatoryDisclosures(EuCompany(VatRegime.EU_REVERSE_CHARGE), null)
            .Select(d => d.Key)
            .ToArray();

        Assert.Contains("buyer.vatId", keys);
        Assert.Contains("buyer.name", keys);
    }

    [Fact]
    public async Task BuildInvoiceDocumentAsync_ReturnsStructuredShape_AndKeepsReceipt()
    {
        var strategy = new EuDefaultInvoiceStrategy(Flags(true));
        var doc = await strategy.BuildInvoiceDocumentAsync(Payment(), EuCompany(), customer: null);

        Assert.Equal(CountryProfileCodes.EuDefault, doc.CountryCode);
        Assert.Equal("RE-EU-1", doc.Receipt.ReceiptNumber);
        Assert.NotNull(doc.Structured);
        Assert.Equal("FR12345678901", doc.Structured!.SellerVatId);
        Assert.Equal("FR12345678901", doc.Structured.SellerTaxNumber);
        Assert.Equal("RE-EU-1", doc.Structured.InvoiceNumber);
        Assert.Equal(100m, doc.Structured.NetAmount);
        Assert.Equal(21m, doc.Structured.TaxAmount);
        Assert.Equal(121m, doc.Structured.GrossAmount);
        Assert.Equal("EUR", doc.Structured.Currency);
        Assert.Equal("Beratung", doc.Structured.PerformanceDescription);
    }

    [Fact]
    public void GetMandatoryDisclosures_FlagOff_ThrowsFeatureDisabled()
    {
        var strategy = new EuDefaultInvoiceStrategy(Flags(false));
        var ex = Assert.Throws<FeatureDisabledException>(() =>
            strategy.GetMandatoryDisclosures(EuCompany(), null));

        Assert.Equal(FeatureFlagNames.EInvoicingEn16931, ex.FeatureName);
        Assert.Equal(FeatureDisabledException.Code, ex.ErrorCode);
    }

    [Fact]
    public async Task AllocateReceiptNumberAsync_ThrowsNotImplemented()
    {
        var strategy = new EuDefaultInvoiceStrategy();
        var ex = await Assert.ThrowsAsync<NotImplementedException>(() =>
            strategy.AllocateReceiptNumberAsync(new ReceiptNumberAllocationContext
            {
                CashRegisterId = Guid.NewGuid(),
            }));

        Assert.Contains(CountryStrategyDocs.EuDefault, ex.Message, StringComparison.Ordinal);
    }
}
