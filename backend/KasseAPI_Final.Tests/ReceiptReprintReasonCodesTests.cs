using KasseAPI_Final.DTOs;
using Xunit;

namespace KasseAPI_Final.Tests;

public class ReceiptReprintReasonCodesTests
{
    [Theory]
    [InlineData("CUSTOMER_REQUEST", true)]
    [InlineData("OTHER", true)]
    [InlineData("invalid", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValid_respects_catalog(string? code, bool expected)
    {
        Assert.Equal(expected, ReceiptReprintReasonCodes.IsValid(code));
    }
}
