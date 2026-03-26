using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public class LegacyRouteDeprecationFilterTests
{
    [Theory]
    [InlineData("/api/Payment/methods", "payment", "/api/pos/payment/methods")]
    [InlineData("/api/Payment", "payment", "/api/pos/payment")]
    [InlineData("/api/Cart/current", "cart", "/api/pos/cart/current")]
    [InlineData("/api/Product/all", "product", "/api/pos/all")]
    public void TryGetLegacyInfo_maps_to_canonical(string path, string expectedFamily, string expectedCanonical)
    {
        var ok = LegacyRouteDeprecationFilter.TryGetLegacyInfo(path, out var family, out var canonical);
        Assert.True(ok);
        Assert.Equal(expectedFamily, family);
        Assert.Equal(expectedCanonical, canonical);
    }

    [Fact]
    public void TryGetLegacyInfo_returns_false_for_canonical_paths()
    {
        Assert.False(LegacyRouteDeprecationFilter.TryGetLegacyInfo("/api/pos/payment", out _, out _));
        Assert.False(LegacyRouteDeprecationFilter.TryGetLegacyInfo("/api/pos/cart/current", out _, out _));
    }

    [Fact]
    public void NormalizePathForMetric_replaces_guid_segments()
    {
        var id = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
        var path = $"/api/Payment/{id}/receipt";
        var n = LegacyRouteDeprecationFilter.NormalizePathForMetric(path);
        Assert.Equal("/api/Payment/{id}/receipt", n);
    }
}
