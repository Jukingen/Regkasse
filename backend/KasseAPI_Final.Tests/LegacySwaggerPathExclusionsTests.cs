using KasseAPI_Final.Swagger;
using Xunit;

namespace KasseAPI_Final.Tests;

public class LegacySwaggerPathExclusionsTests
{
    [Theory]
    [InlineData("api/Cart/current")]
    [InlineData("api/Payment/methods")]
    [InlineData("api/Product/list")]
    [InlineData("api/CompanySettings/business-hours")]
    [InlineData("api/pos/company-profile")]
    [InlineData("api/pos/payment/card/intent")]
    [InlineData("api/FinanzOnline/submit-invoice")]
    public void ShouldExclude_ReturnsTrue_ForRetiredContractPaths(string relativePath) =>
        Assert.True(LegacySwaggerPathExclusions.ShouldExclude(relativePath));

    [Theory]
    [InlineData("api/pos/cart/current")]
    [InlineData("api/pos/payment")]
    [InlineData("api/pos/company")]
    [InlineData("api/pos/card-payment/intent")]
    [InlineData("api/company/settings")]
    [InlineData("api/company/settings/business-hours")]
    [InlineData("api/admin/payments")]
    [InlineData("api/admin/feedback")]
    [InlineData("api/health")]
    [InlineData("api/Auth/login")]
    [InlineData("api/FinanzOnline/config")]
    public void ShouldExclude_ReturnsFalse_ForCurrentPaths(string relativePath) =>
        Assert.False(LegacySwaggerPathExclusions.ShouldExclude(relativePath));
}
