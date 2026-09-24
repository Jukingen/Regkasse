using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.EInvoicing;
using KasseAPI_Final.Services.FeatureFlags;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class En16931XmlBuilderTests
{
    private static IFeatureFlagService Flags(bool enabled, string featureName)
    {
        var mock = new Mock<IFeatureFlagService>();
        mock.Setup(f => f.IsEnabled(featureName, It.IsAny<string?>())).Returns(enabled);
        return mock.Object;
    }

    private static InvoiceDocumentDto Document() => new() { CountryCode = "EU_DEFAULT" };

    private static InvoiceDocumentDto Fixture(string category, decimal net, decimal tax, decimal vatPercent) => new()
    {
        CountryCode = "EU_DEFAULT",
        InvoiceNumber = "EU-2026-1",
        InvoiceDate = new DateTime(2026, 1, 15, 0, 0, 0, DateTimeKind.Utc),
        Currency = "EUR",
        SellerName = "Seller GmbH",
        SellerVatId = "ATU12345678",
        SellerStreet = "Hauptstrasse 1",
        SellerCity = "Wien",
        SellerPostalCode = "1010",
        SellerCountry = "AT",
        BuyerName = "Buyer BV",
        BuyerVatId = "DE123456789",
        BuyerStreet = "Berliner Strasse 2",
        BuyerCity = "Berlin",
        BuyerPostalCode = "10115",
        BuyerCountry = "DE",
        PerformanceDescription = "Goods",
        NetAmount = net,
        TaxAmount = tax,
        GrossAmount = net + tax,
        VatCategory = category,
        VatPercent = vatPercent,
        TaxExemptionReason = category == "AE" ? "Reverse charge" : null,
        TaxExemptionReasonCode = category == "AE" ? "VATEX-EU-AE" : null,
    };

    [Fact]
    public async Task BuildXmlAsync_StandardFixture_PassesSchematron()
    {
        var builder = new En16931UblXmlBuilder(Flags(true, FeatureFlagNames.EInvoicingEn16931));
        var xml = await builder.BuildXmlAsync(Fixture("S", 100m, 20m, 20m));

        Assert.Contains("urn:cen.eu:en16931:2017", xml, StringComparison.Ordinal);
        Assert.Contains(">380<", xml, StringComparison.Ordinal);
        Assert.Empty(En16931Schematron.Validate(xml));
    }

    [Fact]
    public async Task BuildXmlAsync_ReverseChargeFixture_PassesSchematron()
    {
        var builder = new En16931UblXmlBuilder(Flags(true, FeatureFlagNames.EInvoicingEn16931));
        var xml = await builder.BuildXmlAsync(Fixture("AE", 100m, 0m, 0m));

        Assert.Contains("VATEX-EU-AE", xml, StringComparison.Ordinal);
        Assert.Empty(En16931Schematron.Validate(xml));
    }

    [Fact]
    public async Task Schematron_MissingInvoiceNumber_FailsBr02()
    {
        var builder = new En16931UblXmlBuilder(Flags(true, FeatureFlagNames.EInvoicingEn16931));
        var xml = await builder.BuildXmlAsync(Fixture("S", 100m, 20m, 20m));
        xml = xml.Replace("<cbc:ID>EU-2026-1</cbc:ID>", "<cbc:ID></cbc:ID>", StringComparison.Ordinal);

        var failures = En16931Schematron.Validate(xml);
        Assert.Contains(failures, f => f.StartsWith("BR-02", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BuildXmlAsync_FlagOff_ThrowsFeatureDisabled()
    {
        var builder = new En16931UblXmlBuilder(
            Flags(false, FeatureFlagNames.EInvoicingEn16931));

        var ex = await Assert.ThrowsAsync<FeatureDisabledException>(() =>
            builder.BuildXmlAsync(Document()));

        Assert.Equal(FeatureDisabledException.Code, ex.ErrorCode);
        Assert.Equal(FeatureFlagNames.EInvoicingEn16931, ex.FeatureName);
    }

    [Fact]
    public async Task XrechnungBuilder_FlagOn_ThrowsNotImplemented()
    {
        var builder = new NotImplementedXrechnungXmlBuilder(
            Flags(true, FeatureFlagNames.EInvoicingXRechnung));

        var ex = await Assert.ThrowsAsync<NotImplementedException>(() =>
            builder.BuildXmlAsync(new InvoiceDocumentDto { CountryCode = "DE" }));

        Assert.Contains("docs/FISCAL_GERMANY.md", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task XrechnungBuilder_FlagOff_ThrowsFeatureDisabled()
    {
        var builder = new NotImplementedXrechnungXmlBuilder(
            Flags(false, FeatureFlagNames.EInvoicingXRechnung));

        var ex = await Assert.ThrowsAsync<FeatureDisabledException>(() =>
            builder.BuildXmlAsync(new InvoiceDocumentDto { CountryCode = "DE" }));

        Assert.Equal(FeatureDisabledException.Code, ex.ErrorCode);
        Assert.Equal(FeatureFlagNames.EInvoicingXRechnung, ex.FeatureName);
    }
}
