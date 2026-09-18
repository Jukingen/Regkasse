using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.QrRechnung;
using KasseAPI_Final.Services.FeatureFlags;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class QrRechnungBuilderTests
{
    private static IFeatureFlagService Flags(bool enabled)
    {
        var mock = new Mock<IFeatureFlagService>();
        mock.Setup(f => f.IsEnabled(FeatureFlagNames.EInvoicingQrRechnung, It.IsAny<string?>()))
            .Returns(enabled);
        return mock.Object;
    }

    private static QrRechnungParty Creditor() => new(
        Name: "CH GmbH",
        AddressLine1: "Bahnhofstrasse 1",
        AddressLine2: null,
        PostalCode: "8001",
        City: "Zürich",
        CountryCode: "CH");

    private static QrRechnungRequest Request(string iban, string currency = "CHF") => new(
        Iban: iban,
        Creditor: Creditor(),
        Debtor: new QrRechnungParty("Kunde AG", "Rue 2", null, "1200", "Genève", "CH"),
        Amount: 108.10m,
        Currency: currency,
        Reference: "RF18539007547034",
        AdditionalInfo: "Rechnung 1");

    [Fact]
    public async Task BuildPayloadAsync_ValidChIban_ReturnsSixShape()
    {
        var builder = new QrRechnungBuilder(Flags(true));
        var payload = await builder.BuildPayloadAsync(Request("CH93 0076 2011 6238 5295 7"));

        Assert.Equal("CH9300762011623852957", payload.Iban);
        Assert.Equal("CH GmbH", payload.Creditor.Name);
        Assert.Equal("Bahnhofstrasse 1", payload.Creditor.AddressLine1);
        Assert.Equal("8001", payload.Creditor.PostalCode);
        Assert.Equal("Zürich", payload.Creditor.City);
        Assert.Equal("CH", payload.Creditor.CountryCode);
        Assert.Equal("Kunde AG", payload.Debtor!.Name);
        Assert.Equal(108.10m, payload.Amount);
        Assert.Equal("CHF", payload.Currency);
        Assert.Equal("RF18539007547034", payload.Reference);
        Assert.Equal("Rechnung 1", payload.AdditionalInfo);
    }

    [Fact]
    public async Task BuildPayloadAsync_LiIban_IsAccepted()
    {
        var builder = new QrRechnungBuilder(Flags(true));
        var payload = await builder.BuildPayloadAsync(Request("li21 0880 0000 0219 9611 3"));

        Assert.Equal("LI2108800000021996113", payload.Iban);
    }

    [Fact]
    public async Task BuildPayloadAsync_NonChIban_ThrowsArgumentException()
    {
        var builder = new QrRechnungBuilder(Flags(true));
        var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
            builder.BuildPayloadAsync(Request("DE89370400440532013000")));

        Assert.Contains("CH", ex.Message, StringComparison.Ordinal);
        Assert.Contains("LI", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildPayloadAsync_FlagOff_ThrowsFeatureDisabled()
    {
        var builder = new QrRechnungBuilder(Flags(false));
        var ex = await Assert.ThrowsAsync<FeatureDisabledException>(() =>
            builder.BuildPayloadAsync(Request("CH9300762011623852957")));

        Assert.Equal(FeatureDisabledException.Code, ex.ErrorCode);
        Assert.Equal(FeatureFlagNames.EInvoicingQrRechnung, ex.FeatureName);
    }

    [Fact]
    public async Task BuildPdfAsync_FlagOn_ThrowsNotImplemented()
    {
        var builder = new QrRechnungBuilder(Flags(true));
        var ex = await Assert.ThrowsAsync<NotImplementedException>(() =>
            builder.BuildPdfAsync(Request("CH9300762011623852957")));

        Assert.Contains("docs/FISCAL_SWITZERLAND.md", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildPdfAsync_FlagOff_ThrowsFeatureDisabled()
    {
        var builder = new QrRechnungBuilder(Flags(false));
        var ex = await Assert.ThrowsAsync<FeatureDisabledException>(() =>
            builder.BuildPdfAsync(Request("CH9300762011623852957")));

        Assert.Equal(FeatureFlagNames.EInvoicingQrRechnung, ex.FeatureName);
    }
}
