using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.KassenSicherheit;
using KasseAPI_Final.Services.FeatureFlags;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class KassenSicherheitServiceTests
{
    private static IFeatureFlagService Flags(bool enabled)
    {
        var mock = new Mock<IFeatureFlagService>();
        mock.Setup(f => f.IsEnabled(FeatureFlagNames.FiscalKassenSicherheitDe, It.IsAny<string?>()))
            .Returns(enabled);
        return mock.Object;
    }

    private static IKassenSicherheitService Sut(bool flagOn, string provider) =>
        new NotImplementedKassenSicherheitService(
            Flags(flagOn),
            Options.Create(new KassenSicherheitOptions { Provider = provider }));

    private static readonly KassenSicherheitSignRequest Request = new(Guid.NewGuid(), "payload");

    [Fact]
    public async Task SignAsync_FlagOff_ThrowsFeatureDisabled()
    {
        var ex = await Assert.ThrowsAsync<FeatureDisabledException>(() =>
            Sut(flagOn: false, "not-configured").SignAsync(Request));

        Assert.Equal(FeatureDisabledException.Code, ex.ErrorCode);
        Assert.Equal(FeatureFlagNames.FiscalKassenSicherheitDe, ex.FeatureName);
    }

    [Fact]
    public async Task SignAsync_NotConfigured_IsNoOp()
    {
        var result = await Sut(flagOn: true, "not-configured").SignAsync(Request);

        Assert.False(result.Signed);
        Assert.Null(result.Signature);
        Assert.Equal("not-configured", result.Provider);
    }

    [Fact]
    public async Task SignAsync_ConfiguredProvider_ThrowsNotImplemented()
    {
        var ex = await Assert.ThrowsAsync<NotImplementedException>(() =>
            Sut(flagOn: true, "fiskaly_de").SignAsync(Request));

        Assert.Contains("docs/FISCAL_GERMANY.md", ex.Message, StringComparison.Ordinal);
    }
}
