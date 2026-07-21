using System.Reflection;
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
        Assert.False(LegacyRouteDeprecationFilter.TryGetLegacyInfo("/api/pos/list", out _, out _));
    }

    [Fact]
    public void NormalizePathForMetric_replaces_guid_segments()
    {
        var id = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
        var path = $"/api/Payment/{id}/receipt";
        var n = LegacyRouteDeprecationFilter.NormalizePathForMetric(path);
        Assert.Equal("/api/Payment/{id}/receipt", n);
    }

    [Theory]
    [InlineData("KasseAPI_Final.Controllers.PaymentController")]
    [InlineData("KasseAPI_Final.Controllers.CartController")]
    [InlineData("KasseAPI_Final.Controllers.ProductController")]
    public void Legacy_dual_route_controllers_are_marked_Obsolete(string controllerTypeName)
    {
        var controllerType = typeof(LegacyRouteDeprecationFilter).Assembly.GetType(controllerTypeName);
        Assert.NotNull(controllerType);

        var obsolete = controllerType!.GetCustomAttribute<ObsoleteAttribute>();
        Assert.NotNull(obsolete);
        Assert.False(obsolete!.IsError);
        Assert.Contains("/api/pos", obsolete.Message, StringComparison.Ordinal);
        Assert.Contains("2026-09-30", obsolete.Message, StringComparison.Ordinal);
    }
}
