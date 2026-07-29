using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvFinanzOnlineSubmissionOptionsValidatorTests
{
    private static RksvFinanzOnlineSubmissionOptionsValidator Create(bool isProduction)
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(isProduction ? Environments.Production : Environments.Development);
        return new RksvFinanzOnlineSubmissionOptionsValidator(
            env.Object,
            Mock.Of<ILogger<RksvFinanzOnlineSubmissionOptionsValidator>>());
    }

    [Fact]
    public void Validate_Development_AllowsFake()
    {
        var result = Create(isProduction: false).Validate(
            null,
            new RksvFinanzOnlineSubmissionClientOptions { ClientKind = RksvFinanzOnlineSubmissionClientKind.Fake });
        Assert.False(result.Failed);
    }

    [Fact]
    public void Validate_Production_RejectsFakeWithoutEscapeHatch()
    {
        var result = Create(isProduction: true).Validate(
            null,
            new RksvFinanzOnlineSubmissionClientOptions
            {
                ClientKind = RksvFinanzOnlineSubmissionClientKind.Fake,
                AllowFakeClientInProduction = false,
            });
        Assert.True(result.Failed);
        Assert.Contains("Fake", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_Production_AllowsFakeWithEscapeHatch()
    {
        var result = Create(isProduction: true).Validate(
            null,
            new RksvFinanzOnlineSubmissionClientOptions
            {
                ClientKind = RksvFinanzOnlineSubmissionClientKind.Fake,
                AllowFakeClientInProduction = true,
            });
        Assert.False(result.Failed);
    }

    [Fact]
    public void Validate_Production_AllowsReal()
    {
        var result = Create(isProduction: true).Validate(
            null,
            new RksvFinanzOnlineSubmissionClientOptions { ClientKind = RksvFinanzOnlineSubmissionClientKind.Real });
        Assert.False(result.Failed);
    }
}
