using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.EInvoicing;
using KasseAPI_Final.Services.Countries.Strategies;
using KasseAPI_Final.Services.Countries.Strategies.Germany;
using KasseAPI_Final.Services.FeatureFlags;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class GermanyInvoiceStrategyTests
{
    private static IFeatureFlagService Flags(bool enabled, string featureName)
    {
        var mock = new Mock<IFeatureFlagService>();
        mock.Setup(f => f.IsEnabled(featureName, It.IsAny<string?>())).Returns(enabled);
        return mock.Object;
    }

    private static CompanySettings DeCompany() => new()
    {
        Country = CountryProfileCodes.Germany,
        VatRegime = VatRegime.DE_USTG_STANDARD,
        CompanyName = "DE GmbH",
        CompanyAddress = "Berlin",
        CompanyTaxNumber = "DE123456789",
    };

    private static PaymentDetails Payment() => new()
    {
        ReceiptNumber = "RE-1",
        TotalAmount = 119m,
        TaxAmount = 19m,
        Notes = "Beratung",
        CustomerName = "Gast",
        CashierId = "c1",
        PaymentMethodRaw = "0",
        Steuernummer = "ATU12345678",
        CashRegisterId = Guid.NewGuid(),
    };

    [Fact]
    public void GetMandatoryDisclosures_ContainsUstgFields()
    {
        var strategy = new GermanyInvoiceStrategy(Flags(true, FeatureFlagNames.FiscalKassenSicherheitDe));
        var keys = strategy.GetMandatoryDisclosures(DeCompany(), null).Select(d => d.Key).ToArray();

        Assert.Contains("seller.taxNumber", keys);
        Assert.Contains("seller.vatId", keys);
        Assert.Contains("invoice.number", keys);
        Assert.Contains("invoice.date", keys);
        Assert.Contains("invoice.performanceDescription", keys);
        Assert.Contains("invoice.net", keys);
        Assert.Contains("invoice.tax", keys);
        Assert.Contains("invoice.gross", keys);
        Assert.All(
            strategy.GetMandatoryDisclosures(DeCompany(), null),
            d => Assert.Equal("UStG §14", d.LegalBasis));
    }

    [Fact]
    public async Task BuildInvoiceDocumentAsync_ReturnsStructuredShape_AndKeepsReceipt()
    {
        var strategy = new GermanyInvoiceStrategy(Flags(true, FeatureFlagNames.FiscalKassenSicherheitDe));
        var doc = await strategy.BuildInvoiceDocumentAsync(Payment(), DeCompany(), customer: null);

        Assert.Equal(CountryProfileCodes.Germany, doc.CountryCode);
        Assert.Equal("RE-1", doc.Receipt.ReceiptNumber);
        Assert.NotNull(doc.Structured);
        Assert.Equal("DE123456789", doc.Structured!.SellerVatId);
        Assert.Equal("DE123456789", doc.Structured.SellerTaxNumber);
        Assert.Equal("RE-1", doc.Structured.InvoiceNumber);
        Assert.Equal(100m, doc.Structured.NetAmount);
        Assert.Equal(19m, doc.Structured.TaxAmount);
        Assert.Equal(119m, doc.Structured.GrossAmount);
        Assert.Equal("Beratung", doc.Structured.PerformanceDescription);
    }

    [Fact]
    public void GetMandatoryDisclosures_FlagOff_ThrowsFeatureDisabled()
    {
        var strategy = new GermanyInvoiceStrategy(Flags(false, FeatureFlagNames.FiscalKassenSicherheitDe));
        var ex = Assert.Throws<FeatureDisabledException>(() =>
            strategy.GetMandatoryDisclosures(DeCompany(), null));

        Assert.Equal(FeatureFlagNames.FiscalKassenSicherheitDe, ex.FeatureName);
    }

    [Fact]
    public async Task AllocateReceiptNumberAsync_ThrowsNotImplemented()
    {
        var strategy = new GermanyInvoiceStrategy();
        var ex = await Assert.ThrowsAsync<NotImplementedException>(() =>
            strategy.AllocateReceiptNumberAsync(new ReceiptNumberAllocationContext
            {
                CashRegisterId = Guid.NewGuid(),
            }));

        Assert.Contains(CountryStrategyDocs.Germany, ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ZugferdBuilder_ThrowsNotImplemented_WhenFlagOn()
    {
        var builder = new NotImplementedZugferdXmlBuilder(
            Flags(true, FeatureFlagNames.EInvoicingZugferd));

        var ex = await Assert.ThrowsAsync<NotImplementedException>(() =>
            builder.BuildXmlAsync(new InvoiceDocumentDto { CountryCode = "DE" }));

        Assert.Contains("docs/FISCAL_GERMANY.md", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ZugferdBuilder_FlagOff_ThrowsFeatureDisabled()
    {
        var builder = new NotImplementedZugferdXmlBuilder(
            Flags(false, FeatureFlagNames.EInvoicingZugferd));

        var ex = await Assert.ThrowsAsync<FeatureDisabledException>(() =>
            builder.BuildXmlAsync(new InvoiceDocumentDto { CountryCode = "DE" }));

        Assert.Equal(FeatureFlagNames.EInvoicingZugferd, ex.FeatureName);
    }
}
