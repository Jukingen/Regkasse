using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PosStornoReasonCodeMapperTests
{
    [Theory]
    [InlineData("CUSTOMER_REQUEST", CancellationReasonCode.CustomerRequest)]
    [InlineData("WRONG_ITEM", CancellationReasonCode.WrongItem)]
    [InlineData("CustomerRequest", CancellationReasonCode.CustomerRequest)]
    [InlineData("OTHER", CancellationReasonCode.Other)]
    public void Map_KnownCodes(string input, CancellationReasonCode expected)
    {
        Assert.Equal(expected, PosStornoReasonCodeMapper.Map(input));
    }

    [Fact]
    public void Map_UnknownCode_FallsBackToOther()
    {
        Assert.Equal(CancellationReasonCode.Other, PosStornoReasonCodeMapper.Map("UNKNOWN"));
    }
}
