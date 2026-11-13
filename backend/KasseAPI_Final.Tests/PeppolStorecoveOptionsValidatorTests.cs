using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class PeppolStorecoveOptionsValidatorTests
{
    private static PeppolStorecoveOptionsValidator Sut() =>
        new(NullLogger<PeppolStorecoveOptionsValidator>.Instance);

    [Fact]
    public void Live_WithReservedExit_FailsStartup()
    {
        var result = Sut().Validate(null, Options("LIVE", reservedExitEnabled: true));

        Assert.True(result.Failed);
        Assert.Equal(PeppolStorecoveOptionsValidator.LiveWithReservedExitReason, result.FailureMessage);
    }

    [Fact]
    public void Live_WithoutReservedExit_AllowsStartup()
    {
        var result = Sut().Validate(null, Options("LIVE", reservedExitEnabled: false));

        Assert.False(result.Failed);
    }

    [Fact]
    public void Test_AllowsStartup()
    {
        var result = Sut().Validate(null, Options("TEST", reservedExitEnabled: true));

        Assert.False(result.Failed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Unset_AllowsStartup(string? environment)
    {
        var result = Sut().Validate(null, Options(environment, reservedExitEnabled: true));

        Assert.False(result.Failed);
    }

    private static PeppolOptions Options(string? environment, bool reservedExitEnabled) =>
        new()
        {
            Storecove = new PeppolStorecoveOptions { Environment = environment ?? string.Empty },
            ReservedExit = new PeppolReservedExitOptions { Enabled = reservedExitEnabled },
        };
}
