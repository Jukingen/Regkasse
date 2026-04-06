using KasseAPI_Final.Services.AdminProducts;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class AdminProductListIsActiveFilterParserTests
{
    [Theory]
    [InlineData(null, AdminProductListIsActiveFilterMode.ActiveOnly)]
    [InlineData("", AdminProductListIsActiveFilterMode.ActiveOnly)]
    [InlineData("   ", AdminProductListIsActiveFilterMode.ActiveOnly)]
    [InlineData("true", AdminProductListIsActiveFilterMode.ActiveOnly)]
    [InlineData("TRUE", AdminProductListIsActiveFilterMode.ActiveOnly)]
    [InlineData("1", AdminProductListIsActiveFilterMode.ActiveOnly)]
    [InlineData("false", AdminProductListIsActiveFilterMode.InactiveOnly)]
    [InlineData("FALSE", AdminProductListIsActiveFilterMode.InactiveOnly)]
    [InlineData("0", AdminProductListIsActiveFilterMode.InactiveOnly)]
    [InlineData("all", AdminProductListIsActiveFilterMode.All)]
    [InlineData("ALL", AdminProductListIsActiveFilterMode.All)]
    public void TryParse_AcceptedValues_MapsToMode(string? input, AdminProductListIsActiveFilterMode expected)
    {
        var ok = AdminProductListIsActiveFilterParser.TryParse(input, out var mode, out var error);
        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(expected, mode);
    }

    [Theory]
    [InlineData("maybe")]
    [InlineData("yes")]
    [InlineData("2")]
    public void TryParse_Invalid_ReturnsFalse(string input)
    {
        var ok = AdminProductListIsActiveFilterParser.TryParse(input, out var mode, out var error);
        Assert.False(ok);
        Assert.NotNull(error);
        Assert.Equal(default, mode);
    }
}
