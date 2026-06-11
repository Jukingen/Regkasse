using System.Text.Json;
using KasseAPI_Final.DTOs;
using Xunit;

namespace KasseAPI_Final.Tests;

public class PosVoucherValidateRequestTests
{
    [Fact]
    public void Deserialize_AcceptsCodeAlias()
    {
        var json = """{"code":"GUT-TEST-001"}""";
        var request = JsonSerializer.Deserialize<ValidateVoucherRequest>(json);
        Assert.NotNull(request);
        Assert.Equal("GUT-TEST-001", request!.VoucherCode);
    }

    [Fact]
    public void Deserialize_PrefersExplicitVoucherCode()
    {
        var json = """{"voucherCode":"AAA","code":"BBB"}""";
        var request = JsonSerializer.Deserialize<ValidateVoucherRequest>(json);
        Assert.NotNull(request);
        Assert.Equal("AAA", request!.VoucherCode);
    }

    [Fact]
    public void VoucherValidationResult_MapsFromSuccessResponse()
    {
        var response = VoucherValidateResponse.Valid(
            "Active",
            25.50m,
            25.50m,
            DateTime.UtcNow.AddDays(30),
            "****1234");

        var compact = VoucherValidationResult.FromSuccess(response);
        Assert.True(compact.IsValid);
        Assert.Equal(25.50m, compact.RemainingAmount);
        Assert.Equal("****1234", compact.Code);
    }
}
