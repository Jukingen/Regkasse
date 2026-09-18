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

    [Fact]
    public async Task BuildXmlAsync_FlagOn_ThrowsNotImplemented()
    {
        var builder = new NotImplementedEn16931XmlBuilder(
            Flags(true, FeatureFlagNames.EInvoicingEn16931));

        var ex = await Assert.ThrowsAsync<NotImplementedException>(() =>
            builder.BuildXmlAsync(Document()));

        Assert.Contains("docs/EINVOICING_EU.md", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildXmlAsync_FlagOff_ThrowsFeatureDisabled()
    {
        var builder = new NotImplementedEn16931XmlBuilder(
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
