using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FinanzOnlineTransportPathKindResolverTests
{
    [Theory]
    [InlineData(true, "PROD", FinanzOnlineTransportPathKindResolver.Simulated)]
    [InlineData(true, "TEST", FinanzOnlineTransportPathKindResolver.Simulated)]
    [InlineData(false, "PROD", FinanzOnlineTransportPathKindResolver.RealProduction)]
    [InlineData(false, "TEST", FinanzOnlineTransportPathKindResolver.RealTest)]
    [InlineData(false, "test", FinanzOnlineTransportPathKindResolver.RealTest)]
    [InlineData(false, null, FinanzOnlineTransportPathKindResolver.RealTest)]
    public void Resolve_matches_expected(bool anySim, string? mode, string expected)
    {
        Assert.Equal(expected, FinanzOnlineTransportPathKindResolver.Resolve(anySim, mode));
    }
}
