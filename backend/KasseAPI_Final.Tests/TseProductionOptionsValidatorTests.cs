using KasseAPI_Final.HealthChecks;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Tse;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class TseProductionOptionsValidatorTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.ToDictionary(p => p.Key, p => p.Value))
            .Build();

    private static IHostEnvironment Env(string name)
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(name);
        return env.Object;
    }

    private static TseProductionOptionsValidator CreateValidator(
        string environmentName,
        IConfiguration? configuration = null) =>
        new(
            Env(environmentName),
            configuration ?? Config(("RKSV:Mode", "Production"), ("RKSV:TseMode", "Real")),
            NullLogger<TseProductionOptionsValidator>.Instance);

    private static TseOptions SafeOptions() => new()
    {
        TseMode = "Device",
        Mode = "Real",
        Provider = "fiskaly",
        AllowSimulatedDailyClosing = false,
        AllowUnsafeFiscalModesInProduction = false,
    };

    [Theory]
    [InlineData("Off")]
    [InlineData("Demo")]
    [InlineData("off")]
    [InlineData("demo")]
    public void Validate_Production_Rejects_TseMode_OffOrDemo(string tseMode)
    {
        var opts = SafeOptions();
        opts.TseMode = tseMode;
        var r = CreateValidator(Environments.Production).Validate(null, opts);
        Assert.True(r.Failed);
        Assert.Contains("Tse:TseMode", r.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Production_Rejects_Mode_Fake()
    {
        var opts = SafeOptions();
        opts.Mode = "Fake";
        var r = CreateValidator(Environments.Production).Validate(null, opts);
        Assert.True(r.Failed);
        Assert.Contains("Tse:Mode=Fake", r.FailureMessage, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("fake")]
    [InlineData("soft")]
    [InlineData("SOFT")]
    [InlineData("")]
    [InlineData("unknown")]
    public void Validate_Production_Rejects_Provider_Not_Real_Vendor(string provider)
    {
        var opts = SafeOptions();
        opts.Provider = provider;
        var r = CreateValidator(Environments.Production).Validate(null, opts);
        Assert.True(r.Failed);
        Assert.Contains("Provider", r.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Production_Rejects_Rksv_Simulation_And_NonProduction_Mode()
    {
        var config = Config(("RKSV:Mode", "Demo"), ("RKSV:TseMode", "Simulation"));
        var r = CreateValidator(Environments.Production, config).Validate(null, SafeOptions());
        Assert.True(r.Failed);
        Assert.Contains("RKSV:Mode", r.FailureMessage, StringComparison.Ordinal);
        Assert.Contains("RKSV:TseMode=Simulation", r.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Production_Rejects_FinanzOnline_Simulation()
    {
        var config = Config(
            ("RKSV:Mode", "Production"),
            ("RKSV:TseMode", "Real"),
            ("FinanzOnline:Session:UseSimulation", "true"));
        var r = CreateValidator(Environments.Production, config).Validate(null, SafeOptions());
        Assert.True(r.Failed);
        Assert.Contains("FinanzOnline:UseSimulation", r.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Production_Rejects_AllowSimulatedDailyClosing()
    {
        var opts = SafeOptions();
        opts.AllowSimulatedDailyClosing = true;
        var r = CreateValidator(Environments.Production).Validate(null, opts);
        Assert.True(r.Failed);
        Assert.Contains("AllowSimulatedDailyClosing", r.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_Production_SafeConfig_Succeeds()
    {
        var r = CreateValidator(Environments.Production).Validate(null, SafeOptions());
        Assert.False(r.Failed);
    }

    [Fact]
    public void Validate_Development_Allows_Demo_And_Fake()
    {
        var opts = new TseOptions
        {
            TseMode = "Demo",
            Mode = "Fake",
            Provider = "soft",
            AllowSimulatedDailyClosing = true,
        };
        var config = Config(("RKSV:Mode", "Demo"), ("RKSV:TseMode", "Simulation"));
        var r = CreateValidator(Environments.Development, config).Validate(null, opts);
        Assert.False(r.Failed);
    }

    [Fact]
    public void Validate_Production_EscapeHatch_Allows_Unsafe_Modes()
    {
        var opts = SafeOptions();
        opts.TseMode = "Demo";
        opts.Mode = "Fake";
        opts.Provider = "soft";
        opts.AllowUnsafeFiscalModesInProduction = true;
        var r = CreateValidator(Environments.Production).Validate(null, opts);
        Assert.False(r.Failed);
    }

    [Fact]
    public void Validate_Staging_Enforces_Lock_By_Default()
    {
        var opts = SafeOptions();
        opts.TseMode = "Off";
        var r = CreateValidator(Environments.Staging).Validate(null, opts);
        Assert.True(r.Failed);
    }

    [Fact]
    public void Validate_Staging_Can_Disable_Lock()
    {
        var opts = SafeOptions();
        opts.TseMode = "Demo";
        opts.EnforceProductionLockInStaging = false;
        var r = CreateValidator(Environments.Staging).Validate(null, opts);
        Assert.False(r.Failed);
    }
}

public sealed class TseFiscalConfigHealthCheckTests
{
    private static TseFiscalConfigHealthCheck CreateCheck(
        string environmentName,
        TseOptions options,
        IConfiguration? configuration = null)
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(environmentName);
        var monitor = new Mock<IOptionsMonitor<TseOptions>>();
        monitor.Setup(m => m.CurrentValue).Returns(options);
        var config = configuration
                     ?? new ConfigurationBuilder()
                         .AddInMemoryCollection(new Dictionary<string, string?>
                         {
                             ["RKSV:Mode"] = "Production",
                             ["RKSV:TseMode"] = "Real",
                         })
                         .Build();
        return new TseFiscalConfigHealthCheck(env.Object, config, monitor.Object);
    }

    [Fact]
    public async Task Check_Development_IsHealthy()
    {
        var check = CreateCheck(
            Environments.Development,
            new TseOptions { TseMode = "Demo", Mode = "Fake", Provider = "soft" });
        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Check_Production_Safe_IsHealthy()
    {
        var check = CreateCheck(
            Environments.Production,
            new TseOptions { TseMode = "Device", Mode = "Real", Provider = "fiskaly" });
        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task Check_Production_Unsafe_IsUnhealthy()
    {
        var check = CreateCheck(
            Environments.Production,
            new TseOptions { TseMode = "Off", Mode = "Real", Provider = "fiskaly" });
        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.DoesNotContain("ApiKey", result.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ApiSecret", result.Description ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Check_Production_EscapeHatch_IsDegraded()
    {
        var check = CreateCheck(
            Environments.Production,
            new TseOptions
            {
                TseMode = "Demo",
                Mode = "Fake",
                Provider = "soft",
                AllowUnsafeFiscalModesInProduction = true,
            });
        var result = await check.CheckHealthAsync(new HealthCheckContext());
        Assert.Equal(HealthStatus.Degraded, result.Status);
    }
}

public sealed class TseFiscalConfigLockEvaluatorTests
{
    [Fact]
    public void Evaluate_Ok_When_EscapeHatch_With_Violations()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var config = new ConfigurationBuilder().Build();
        var opts = new TseOptions
        {
            TseMode = "Off",
            AllowUnsafeFiscalModesInProduction = true,
        };
        var result = TseFiscalConfigLockEvaluator.Evaluate(env.Object, config, opts);
        Assert.True(result.Ok);
        Assert.True(result.EscapeHatchActive);
        Assert.False(result.IsSafe);
        Assert.NotEmpty(result.Reasons);
    }
}
