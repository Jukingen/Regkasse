using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class KassenSicherheitHostOptionsValidatorTests
{
    private static KassenSicherheitHostOptionsValidator Sut() =>
        new(NullLogger<KassenSicherheitHostOptionsValidator>.Instance);

    [Theory]
    [InlineData("https://kassensichv-middleware.fiskaly.com/api/v2")]
    [InlineData("https://kassensichv.fiskaly.com/api/v2")]
    public void Validate_KassenSichvHost_Succeeds(string apiBaseUrl)
    {
        var result = Sut().Validate(null, new KassenSicherheitOptions { ApiBaseUrl = apiBaseUrl });

        Assert.False(result.Failed);
    }

    [Theory]
    [InlineData("https://rksv.fiskaly.com/api/v1")]
    [InlineData("https://example.com/rksv.fiskaly.com/proxy")]
    public void Validate_RksvHost_Fails(string apiBaseUrl)
    {
        var result = Sut().Validate(null, new KassenSicherheitOptions { ApiBaseUrl = apiBaseUrl });

        Assert.True(result.Failed);
        Assert.Contains("rksv.fiskaly.com", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Fiskaly:ApiBaseUrl", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_LegacyApiFiskalyHost_Fails()
    {
        var result = Sut().Validate(null, new KassenSicherheitOptions
        {
            ApiBaseUrl = "https://api.fiskaly.com/v1"
        });

        Assert.True(result.Failed);
        Assert.Contains("api.fiskaly.com", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }
}
