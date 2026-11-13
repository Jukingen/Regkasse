using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class KassenSicherheitHostOptionsValidatorTests
{
    private static KassenSicherheitHostOptionsValidator Sut(string environment = "Development")
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(environment);
        return new KassenSicherheitHostOptionsValidator(
            NullLogger<KassenSicherheitHostOptionsValidator>.Instance,
            env.Object);
    }

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

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public void Validate_Simulation_OutsideDevelopment_Fails(string environment)
    {
        var result = Sut(environment).Validate(null, new KassenSicherheitOptions
        {
            Mode = KassenSicherheitOptions.ModeSimulation,
        });

        Assert.True(result.Failed);
        Assert.Equal(KassenSicherheitHostOptionsValidator.SimulationRejectedReason, result.FailureMessage);
    }

    [Fact]
    public void Validate_PilotMode_LiveEnvironment_Fails()
    {
        var result = Sut("Staging").Validate(null, new KassenSicherheitOptions
        {
            PilotMode = true,
            Environment = KassenSicherheitOptions.EnvironmentLive,
            ApiBaseUrl = "https://kassensichv-middleware.fiskaly.com/api/v2",
        });

        Assert.True(result.Failed);
        Assert.Equal(
            KassenSicherheitHostOptionsValidator.PilotLiveEnvironmentRejectedReason,
            result.FailureMessage);
    }

    [Fact]
    public void Validate_PilotMode_LiveHost_Fails()
    {
        var result = Sut("Staging").Validate(null, new KassenSicherheitOptions
        {
            PilotMode = true,
            Environment = KassenSicherheitOptions.EnvironmentTest,
            ApiBaseUrl = "https://kassensichv.fiskaly.com/api/v2",
        });

        Assert.True(result.Failed);
        Assert.Equal(
            KassenSicherheitHostOptionsValidator.PilotLiveHostRejectedReason,
            result.FailureMessage);
    }

    [Fact]
    public void Validate_PilotMode_TestHostAndTestEnvironment_Succeeds()
    {
        var result = Sut("Staging").Validate(null, new KassenSicherheitOptions
        {
            PilotMode = true,
            Environment = "test",
            ApiBaseUrl = "https://kassensichv-middleware.fiskaly.com/api/v2",
        });

        Assert.False(result.Failed);
    }

    [Fact]
    public void Validate_PilotMode_DoesNotChangeAustrianTseLock()
    {
        var pilot = Sut("Production").Validate(null, new KassenSicherheitOptions
        {
            PilotMode = true,
            Environment = KassenSicherheitOptions.EnvironmentTest,
            ApiBaseUrl = "https://kassensichv-middleware.fiskaly.com/api/v2",
        });
        Assert.False(pilot.Failed);

        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RKSV:Mode"] = "Production",
                ["RKSV:TseMode"] = "Real",
                ["KassenSicherheit:PilotMode"] = "true",
                ["KassenSicherheit:Environment"] = "TEST",
                ["KassenSicherheit:ApiBaseUrl"] = "https://kassensichv-middleware.fiskaly.com/api/v2",
            })
            .Build();
        var at = new TseProductionOptionsValidator(
            env.Object,
            config,
            NullLogger<TseProductionOptionsValidator>.Instance);
        var rejected = at.Validate(null, new TseOptions
        {
            TseMode = "Device",
            Mode = "Real",
            Provider = "fiskaly",
            SoftTseEnabled = true,
        });

        Assert.True(rejected.Failed);
        Assert.Contains("SoftTseEnabled", rejected.FailureMessage, StringComparison.Ordinal);
    }
}
