using KasseAPI_Final.Swagger;
using Xunit;

namespace KasseAPI_Final.Tests;

public class LegacySwaggerPathExclusionsTests
{
    [Theory]
    [InlineData("api/Cart/current")]
    [InlineData("api/Payment/methods")]
    [InlineData("api/Product/list")]
    [InlineData("api/FinanzOnline/submit-invoice")]
    public void ShouldExclude_ReturnsTrue_ForRetiredContractPaths(string relativePath) =>
        Assert.True(LegacySwaggerPathExclusions.ShouldExclude(relativePath));

    [Theory]
    [InlineData("api/pos/cart/current")]
    [InlineData("api/pos/payment")]
    [InlineData("api/admin/payments")]
    [InlineData("api/FinanzOnline/config")]
    public void ShouldExclude_ReturnsFalse_ForCurrentPaths(string relativePath) =>
        Assert.False(LegacySwaggerPathExclusions.ShouldExclude(relativePath));
}
