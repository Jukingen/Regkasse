using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public class CustomerQrPayloadParserTests
{
    [Fact]
    public void Parse_RkCustomerNumber_ReturnsByNumber()
    {
        var result = CustomerQrPayloadParser.Parse("RK:C:CUST-42");
        Assert.True(result.Ok);
        Assert.Equal("CUST-42", result.CustomerNumber);
    }

    [Fact]
    public void Parse_RkCustomerGuid_ReturnsById()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var result = CustomerQrPayloadParser.Parse($"RK:CU:{id}");
        Assert.True(result.Ok);
        Assert.Equal(id, result.CustomerId);
    }

    [Fact]
    public void Parse_PlainNumber_ReturnsByNumber()
    {
        var result = CustomerQrPayloadParser.Parse("A1001");
        Assert.True(result.Ok);
        Assert.Equal("A1001", result.CustomerNumber);
    }

    [Fact]
    public void Parse_Empty_ReturnsInvalid()
    {
        var result = CustomerQrPayloadParser.Parse("   ");
        Assert.False(result.Ok);
    }

    [Fact]
    public void Parse_CustomerPrefixGuid_ReturnsById()
    {
        var id = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var result = CustomerQrPayloadParser.Parse($"customer:{id}");
        Assert.True(result.Ok);
        Assert.Equal(id, result.CustomerId);
    }

    [Fact]
    public void Parse_CustomerPrefixEmail_ReturnsByEmail()
    {
        var result = CustomerQrPayloadParser.Parse("customer:max@example.com");
        Assert.True(result.Ok);
        Assert.Equal("max@example.com", result.Email);
    }
}
