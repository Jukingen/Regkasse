using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class RksvFinanzOnlineSubmissionOptionsValidatorTests
{
    private static RksvFinanzOnlineSubmissionOptionsValidator Create(string environmentName)
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(environmentName);
        return new RksvFinanzOnlineSubmissionOptionsValidator(
            env.Object,
            Mock.Of<ILogger<RksvFinanzOnlineSubmissionOptionsValidator>>());
    }

    /// <summary>Complete Real configuration (what Production templates must provide).</summary>
    private static RksvFinanzOnlineSubmissionClientOptions CompleteReal() => new()
    {
        ClientKind = RksvFinanzOnlineSubmissionClientKind.Real,
        Enabled = true,
        AllowOutboundNetworkCalls = true,
        EndpointUrl = "https://finanzonline.bmf.gv.at/fonws/ws/rkdb",
        TimeoutSeconds = 120,
        ParticipantCredentialsConfigurationKey = "FinanzOnline:Participant",
    };

    [Fact]
    public void Validate_Development_AllowsFake()
    {
        var result = Create(Environments.Development).Validate(
            null,
            new RksvFinanzOnlineSubmissionClientOptions { ClientKind = RksvFinanzOnlineSubmissionClientKind.Fake });
        Assert.False(result.Failed);
    }

    [Fact]
    public void Validate_Development_AllowsIncompleteReal()
    {
        var result = Create(Environments.Development).Validate(
            null,
            new RksvFinanzOnlineSubmissionClientOptions { ClientKind = RksvFinanzOnlineSubmissionClientKind.Real });
        Assert.False(result.Failed);
    }

    [Fact]
    public void Validate_Production_RejectsFakeWithoutEscapeHatch()
    {
        var result = Create(Environments.Production).Validate(
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
    public void Validate_Production_RejectsNotImplemented()
    {
        var options = CompleteReal();
        options.ClientKind = RksvFinanzOnlineSubmissionClientKind.NotImplemented;

        var result = Create(Environments.Production).Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("NotImplemented", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Production_NotImplementedIgnoresEscapeHatch()
    {
        var options = CompleteReal();
        options.ClientKind = RksvFinanzOnlineSubmissionClientKind.NotImplemented;
        options.AllowFakeClientInProduction = true;

        var result = Create(Environments.Production).Validate(null, options);

        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_Production_RejectsDisabled()
    {
        var options = CompleteReal();
        options.Enabled = false;

        var result = Create(Environments.Production).Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("Enabled", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Production_RejectsOutboundCallsDisabled()
    {
        var options = CompleteReal();
        options.AllowOutboundNetworkCalls = false;

        var result = Create(Environments.Production).Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("AllowOutboundNetworkCalls", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_Production_RejectsMissingEndpointUrl(string? endpointUrl)
    {
        var options = CompleteReal();
        options.EndpointUrl = endpointUrl;

        var result = Create(Environments.Production).Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("EndpointUrl", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("http://finanzonline.bmf.gv.at/fonws/ws/rkdb")]
    [InlineData("not-a-uri")]
    public void Validate_Production_RejectsNonHttpsEndpointUrl(string endpointUrl)
    {
        var options = CompleteReal();
        options.EndpointUrl = endpointUrl;

        var result = Create(Environments.Production).Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("HTTPS", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void Validate_Production_RejectsMissingCredentialsKey(string? credentialsKey)
    {
        var options = CompleteReal();
        options.ParticipantCredentialsConfigurationKey = credentialsKey;

        var result = Create(Environments.Production).Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("ParticipantCredentialsConfigurationKey", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RksvFinanzOnlineSubmissionClientOptions.MinTimeoutSeconds - 1)]
    [InlineData(RksvFinanzOnlineSubmissionClientOptions.MaxTimeoutSeconds + 1)]
    public void Validate_Production_RejectsTimeoutOutsideSharedRange(int timeoutSeconds)
    {
        var options = CompleteReal();
        options.TimeoutSeconds = timeoutSeconds;

        var result = Create(Environments.Production).Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("TimeoutSeconds", result.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(RksvFinanzOnlineSubmissionClientOptions.MinTimeoutSeconds)]
    [InlineData(RksvFinanzOnlineSubmissionClientOptions.MaxTimeoutSeconds)]
    public void Validate_Production_AcceptsTimeoutAtSharedRangeBounds(int timeoutSeconds)
    {
        var options = CompleteReal();
        options.TimeoutSeconds = timeoutSeconds;

        var result = Create(Environments.Production).Validate(null, options);

        Assert.False(result.Failed);
    }

    [Fact]
    public void Validate_Production_ReportsAllViolationsTogether()
    {
        var result = Create(Environments.Production).Validate(
            null,
            new RksvFinanzOnlineSubmissionClientOptions());

        Assert.True(result.Failed);
        var message = result.FailureMessage;
        Assert.Contains("ClientKind", message, StringComparison.Ordinal);
        Assert.Contains("Enabled", message, StringComparison.Ordinal);
        Assert.Contains("AllowOutboundNetworkCalls", message, StringComparison.Ordinal);
        Assert.Contains("EndpointUrl", message, StringComparison.Ordinal);
        Assert.Contains("ParticipantCredentialsConfigurationKey", message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Production_AllowsCompleteReal()
    {
        var result = Create(Environments.Production).Validate(null, CompleteReal());
        Assert.False(result.Failed);
    }

    [Fact]
    public void Validate_Production_RejectsIncompleteReal()
    {
        var result = Create(Environments.Production).Validate(
            null,
            new RksvFinanzOnlineSubmissionClientOptions { ClientKind = RksvFinanzOnlineSubmissionClientKind.Real });
        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_Staging_RejectsRealWithoutEndpointUrl()
    {
        var options = CompleteReal();
        options.EndpointUrl = null;

        var result = Create(Environments.Staging).Validate(null, options);

        Assert.True(result.Failed);
        Assert.Contains("EndpointUrl", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Staging_RejectsFakeWithoutEscapeHatch()
    {
        var result = Create(Environments.Staging).Validate(
            null,
            new RksvFinanzOnlineSubmissionClientOptions { ClientKind = RksvFinanzOnlineSubmissionClientKind.Fake });
        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_Staging_AllowsCompleteReal()
    {
        var result = Create(Environments.Staging).Validate(null, CompleteReal());
        Assert.False(result.Failed);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Validate_NonDevelopment_AllowsFakeWithEscapeHatch(string environmentName)
    {
        var result = Create(environmentName).Validate(
            null,
            new RksvFinanzOnlineSubmissionClientOptions
            {
                ClientKind = RksvFinanzOnlineSubmissionClientKind.Fake,
                AllowFakeClientInProduction = true,
            });
        Assert.False(result.Failed);
    }

    [Fact]
    public void Validate_Production_EscapeHatchIsIgnoredForIncompleteReal()
    {
        var result = Create(Environments.Production).Validate(
            null,
            new RksvFinanzOnlineSubmissionClientOptions
            {
                ClientKind = RksvFinanzOnlineSubmissionClientKind.Real,
                AllowFakeClientInProduction = true,
            });
        Assert.True(result.Failed);
    }
}
