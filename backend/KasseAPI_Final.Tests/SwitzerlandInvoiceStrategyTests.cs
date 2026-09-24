using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Strategies.Switzerland;
using KasseAPI_Final.Services.FeatureFlags;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class SwitzerlandInvoiceStrategyTests
{
    private static IFeatureFlagService Flags(bool enabled)
    {
        var mock = new Mock<IFeatureFlagService>();
        mock.Setup(f => f.IsEnabled(FeatureFlagNames.FiscalMwstCh, It.IsAny<string?>()))
            .Returns(enabled);
        return mock.Object;
    }

    private static CompanySettings ChCompany() => new()
    {
        Country = CountryProfileCodes.Switzerland,
        VatRegime = VatRegime.CH_MWST_STANDARD,
        CompanyName = "CH GmbH",
        CompanyAddress = "Zürich",
        CompanyTaxNumber = "CHE-123.456.789 MWST",
    };

    private static PaymentDetails Payment() => new()
    {
        ReceiptNumber = "RE-CH-1",
        TotalAmount = 108.1m,
        TaxAmount = 8.1m,
        Notes = "Beratung",
        CustomerName = "Gast",
        CashierId = "c1",
        PaymentMethodRaw = "0",
        Steuernummer = "ATU12345678",
        CashRegisterId = Guid.NewGuid(),
    };

    [Fact]
    public void GetMandatoryDisclosures_ContainsMwstFields()
    {
        var strategy = new SwitzerlandInvoiceStrategy(Flags(true));
        var disclosures = strategy.GetMandatoryDisclosures(ChCompany(), null);
        var keys = disclosures.Select(d => d.Key).ToArray();

        Assert.Contains("seller.vatId", keys);
        Assert.Contains("invoice.number", keys);
        Assert.Contains("invoice.date", keys);
        Assert.Contains("buyer.name", keys);
        Assert.Contains("buyer.address", keys);
        Assert.Contains("invoice.net", keys);
        Assert.Contains("invoice.tax", keys);
        Assert.Contains("invoice.gross", keys);
        Assert.Contains("invoice.mwstBreakdown", keys);
        Assert.Contains("invoice.kleinunternehmer", keys);
        Assert.All(disclosures, d => Assert.Equal("MWSTG", d.LegalBasis));
        Assert.Equal(nameof(CompanySettings.VatId), disclosures.Single(d => d.Key == "seller.vatId").SourceField);
        Assert.Equal(nameof(PaymentDetails.ReceiptNumber), disclosures.Single(d => d.Key == "invoice.number").SourceField);
        Assert.Equal(nameof(PaymentDetails.CreatedAt), disclosures.Single(d => d.Key == "invoice.date").SourceField);
    }

    [Fact]
    public async Task BuildInvoiceDocumentAsync_ReturnsStructuredShape_AndKeepsReceipt()
    {
        var strategy = new SwitzerlandInvoiceStrategy(Flags(true));
        var doc = await strategy.BuildInvoiceDocumentAsync(Payment(), ChCompany(), customer: null);

        Assert.Equal(CountryProfileCodes.Switzerland, doc.CountryCode);
        Assert.Equal("RE-CH-1", doc.Receipt.ReceiptNumber);
        Assert.NotNull(doc.Structured);
        Assert.Equal("CHE-123.456.789 MWST", doc.Structured!.SellerVatId);
        Assert.Equal("CHE-123.456.789 MWST", doc.Structured.SellerTaxNumber);
        Assert.Equal("RE-CH-1", doc.Structured.InvoiceNumber);
        Assert.Equal(100m, doc.Structured.NetAmount);
        Assert.Equal(8.1m, doc.Structured.TaxAmount);
        Assert.Equal(108.1m, doc.Structured.GrossAmount);
        Assert.Equal("CHF", doc.Structured.Currency);
        Assert.Equal("Beratung", doc.Structured.PerformanceDescription);
    }

    [Fact]
    public void GetMandatoryDisclosures_FlagOff_ThrowsFeatureDisabled()
    {
        var strategy = new SwitzerlandInvoiceStrategy(Flags(false));
        var ex = Assert.Throws<FeatureDisabledException>(() =>
            strategy.GetMandatoryDisclosures(ChCompany(), null));

        Assert.Equal(FeatureFlagNames.FiscalMwstCh, ex.FeatureName);
    }

    [Fact]
    public async Task AllocateReceiptNumberAsync_ThrowsNotImplemented()
    {
        var strategy = new SwitzerlandInvoiceStrategy();
        var ex = await Assert.ThrowsAsync<NotImplementedException>(() =>
            strategy.AllocateReceiptNumberAsync(new ReceiptNumberAllocationContext
            {
                CashRegisterId = Guid.NewGuid(),
            }));

        Assert.Contains(CountryStrategyDocs.Switzerland, ex.Message, StringComparison.Ordinal);
    }
}
