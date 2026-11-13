using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FiskalyHostOptionsValidatorTests
{
    private static FiskalyHostOptionsValidator Sut() =>
        new(NullLogger<FiskalyHostOptionsValidator>.Instance);

    [Fact]
    public void Validate_RksvHost_Succeeds()
    {
        var opts = new FiskalyOptions
        {
            BaseUrl = "https://rksv.fiskaly.com/api/v1"
        };

        var result = Sut().Validate(null, opts);

        Assert.False(result.Failed);
    }

    [Theory]
    [InlineData("https://kassensichv-middleware.fiskaly.com/api/v2")]
    [InlineData("https://kassensichv.fiskaly.com/api/v2")]
    [InlineData("https://example.com/kassensichv/proxy")]
    public void Validate_KassenSichvHost_Fails(string baseUrl)
    {
        var opts = new FiskalyOptions { BaseUrl = baseUrl };

        var result = Sut().Validate(null, opts);

        Assert.True(result.Failed);
        Assert.Contains("kassensichv", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rksv.fiskaly.com", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("KassenSicherheit:ApiBaseUrl", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_LegacyApiFiskalyHost_SucceedsWithWarningOnly()
    {
        var opts = new FiskalyOptions
        {
            BaseUrl = "https://api.fiskaly.com/v1"
        };

        var result = Sut().Validate(null, opts);

        Assert.False(result.Failed);
    }
}
