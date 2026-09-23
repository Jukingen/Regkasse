using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Countries.KassenSicherheit;
using KasseAPI_Final.Services.FeatureFlags;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalyDeKassenSicherheitServiceTests
{
    private static IFeatureFlagService Flags(bool enabled)
    {
        var mock = new Mock<IFeatureFlagService>();
        mock.Setup(f => f.IsEnabled(FeatureFlagNames.FiscalKassenSicherheitDe, It.IsAny<string?>()))
            .Returns(enabled);
        return mock.Object;
    }

    private static FiskalyDeKassenSicherheitService Sut(
        bool flagOn,
        string provider,
        out Mock<IKassenSicherheitHttpClient> http)
    {
        http = new Mock<IKassenSicherheitHttpClient>(MockBehavior.Strict);
        return new FiskalyDeKassenSicherheitService(
            Flags(flagOn),
            Options.Create(new KassenSicherheitOptions { Provider = provider }),
            http.Object);
    }

    [Fact]
    public async Task StartTransaction_FlagOff_ThrowsFeatureDisabled()
    {
        var sut = Sut(flagOn: false, "fiskaly-de", out var http);
        var ex = await Assert.ThrowsAsync<FeatureDisabledException>(() =>
            sut.StartTransactionAsync(Start()));

        Assert.Equal(FeatureFlagNames.FiscalKassenSicherheitDe, ex.FeatureName);
        http.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StartTransaction_NotConfigured_IsNoOp()
    {
        var sut = Sut(flagOn: true, "not-configured", out var http);
        var result = await sut.StartTransactionAsync(Start());

        Assert.False(result.Completed);
        Assert.Equal("not-configured", result.Provider);
        http.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task StartTransaction_FiskalyDe_CallsHttpClient()
    {
        var sut = Sut(flagOn: true, "fiskaly-de", out var http);
        var request = Start();
        http.Setup(c => c.StartTransactionAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new KassenSicherheitTransactionResult(true, "tx-1", "ACTIVE", 1, null, "fiskaly-de"));

        var result = await sut.StartTransactionAsync(request);

        Assert.True(result.Completed);
        http.Verify(c => c.StartTransactionAsync(request, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SignAsync_FiskalyDe_DoesNotCallHttpClient()
    {
        var sut = Sut(flagOn: true, "fiskaly-de", out var http);
        var ex = await Assert.ThrowsAsync<NotImplementedException>(() =>
            sut.SignAsync(new KassenSicherheitSignRequest(Guid.NewGuid(), "payload")));

        Assert.Contains("StartTransactionAsync", ex.Message, StringComparison.Ordinal);
        http.VerifyNoOtherCalls();
    }

    private static KassenSicherheitStartTransactionRequest Start() =>
        new(Guid.NewGuid(), "tss-1", "client-1", "tx-1", 1);
}
