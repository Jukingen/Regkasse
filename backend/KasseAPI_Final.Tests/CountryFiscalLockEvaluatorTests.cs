using KasseAPI_Final.Configuration;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Countries;
using KasseAPI_Final.Services.Tse;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class CountryFiscalLockEvaluatorTests
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

    private static CountryFiscalLockOptionsValidator CreateValidator(
        string environmentName,
        IConfiguration configuration) =>
        new(
            Env(environmentName),
            configuration,
            NullLogger<CountryFiscalLockOptionsValidator>.Instance);

    private static IConfiguration SafeStubs() => Config(
        ("KassenSicherheit:Provider", "not-configured"),
        ("KassenSicherheit:AllowSimulatedTse", "false"),
        ("Mwst:UseTestEndpoint", "false"),
        ("QrRechnung:BuilderMode", "not-configured"));

    [Fact]
    public void Evaluate_Production_Rejects_De_Provider_Fake()
    {
        var config = Config(("KassenSicherheit:Provider", "fake"));
        var result = CountryFiscalLockEvaluator.Evaluate(Env(Environments.Production), config);
        Assert.False(result.Ok);
        Assert.Contains(CountryFiscalLockEvaluator.ReasonProviderFake, result.Reasons);
        Assert.DoesNotContain(CountryFiscalLockEvaluator.ReasonAllowSimulatedTse, result.Reasons);
    }

    [Theory]
    [InlineData("FAKE")]
    [InlineData("Fake")]
    public void Evaluate_Production_Rejects_De_Provider_Fake_CaseInsensitive(string provider)
    {
        var config = Config(("KassenSicherheit:Provider", provider));
        var result = CountryFiscalLockEvaluator.Evaluate(Env(Environments.Production), config);
        Assert.False(result.Ok);
        Assert.Contains(CountryFiscalLockEvaluator.ReasonProviderFake, result.Reasons);
    }

    [Fact]
    public void Evaluate_Production_Rejects_De_AllowSimulatedTse_True()
    {
        var config = Config(("KassenSicherheit:AllowSimulatedTse", "true"));
        var result = CountryFiscalLockEvaluator.Evaluate(Env(Environments.Production), config);
        Assert.False(result.Ok);
        Assert.Contains(CountryFiscalLockEvaluator.ReasonAllowSimulatedTse, result.Reasons);
        Assert.DoesNotContain(CountryFiscalLockEvaluator.ReasonProviderFake, result.Reasons);
    }

    [Fact]
    public void Evaluate_Production_Rejects_De_RealProvider_With_AllowSimulatedTse()
    {
        var config = Config(
            ("KassenSicherheit:Provider", "fiskaly_de"),
            ("KassenSicherheit:AllowSimulatedTse", "true"));
        var result = CountryFiscalLockEvaluator.Evaluate(Env(Environments.Production), config);
        Assert.False(result.Ok);
        Assert.DoesNotContain(CountryFiscalLockEvaluator.ReasonProviderFake, result.Reasons);
        Assert.Contains(CountryFiscalLockEvaluator.ReasonAllowSimulatedTse, result.Reasons);
    }

    [Fact]
    public void Evaluate_Production_Rejects_Ch_UseTestEndpoint()
    {
        var config = Config(("Mwst:UseTestEndpoint", "true"));
        var result = CountryFiscalLockEvaluator.Evaluate(Env(Environments.Production), config);
        Assert.False(result.Ok);
        Assert.Contains(CountryFiscalLockEvaluator.ReasonMwstTestEndpoint, result.Reasons);
    }

    [Theory]
    [InlineData("dryRun")]
    [InlineData("DRYRUN")]
    public void Evaluate_Production_Rejects_Ch_BuilderMode_DryRun(string mode)
    {
        var config = Config(("QrRechnung:BuilderMode", mode));
        var result = CountryFiscalLockEvaluator.Evaluate(Env(Environments.Production), config);
        Assert.False(result.Ok);
        Assert.Contains(CountryFiscalLockEvaluator.ReasonQrDryRun, result.Reasons);
    }

    [Fact]
    public void Evaluate_Production_SafeStubs_Succeed()
    {
        var result = CountryFiscalLockEvaluator.Evaluate(Env(Environments.Production), SafeStubs());
        Assert.True(result.Ok);
        Assert.True(result.LockApplies);
        Assert.True(result.IsSafe);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void Evaluate_Production_MissingKeys_Succeed()
    {
        var result = CountryFiscalLockEvaluator.Evaluate(Env(Environments.Production), Config());
        Assert.True(result.Ok);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void Evaluate_Production_Ignores_Unsafe_At_Tse_Config()
    {
        var config = Config(
            ("Tse:TseMode", "Demo"),
            ("Tse:Mode", "Fake"),
            ("Tse:SoftTseEnabled", "true"),
            ("RKSV:Mode", "Demo"),
            ("RKSV:TseMode", "Simulation"),
            ("KassenSicherheit:Provider", "not-configured"),
            ("KassenSicherheit:AllowSimulatedTse", "false"));
        var result = CountryFiscalLockEvaluator.Evaluate(Env(Environments.Production), config);
        Assert.True(result.Ok);
        Assert.Empty(result.Reasons);
    }

    [Fact]
    public void Validate_Development_Allows_Unsafe_De_And_Ch()
    {
        var config = Config(
            ("KassenSicherheit:Provider", "fake"),
            ("KassenSicherheit:AllowSimulatedTse", "true"),
            ("Mwst:UseTestEndpoint", "true"),
            ("QrRechnung:BuilderMode", "dryRun"));
        var eval = CountryFiscalLockEvaluator.Evaluate(Env(Environments.Development), config);
        Assert.False(eval.LockApplies);
        Assert.True(eval.Ok);
        var r = CreateValidator(Environments.Development, config).Validate(null, new CountryFiscalLockOptions());
        Assert.False(r.Failed);
    }

    [Fact]
    public void Evaluate_Staging_Rejects_Unsafe_De_And_Ch()
    {
        var config = Config(
            ("KassenSicherheit:Provider", "fake"),
            ("Mwst:UseTestEndpoint", "true"),
            ("QrRechnung:BuilderMode", "dryRun"));
        var result = CountryFiscalLockEvaluator.Evaluate(Env(Environments.Staging), config);
        Assert.True(result.LockApplies);
        Assert.False(result.Ok);
        Assert.Contains(CountryFiscalLockEvaluator.ReasonProviderFake, result.Reasons);
        Assert.Contains(CountryFiscalLockEvaluator.ReasonMwstTestEndpoint, result.Reasons);
        Assert.Contains(CountryFiscalLockEvaluator.ReasonQrDryRun, result.Reasons);
    }

    [Fact]
    public void Evaluate_Staging_StillLocks_When_At_Staging_OptOut_Would_Apply()
    {
        var config = Config(
            ("Tse:EnforceProductionLockInStaging", "false"),
            ("KassenSicherheit:AllowSimulatedTse", "true"));
        var result = CountryFiscalLockEvaluator.Evaluate(Env(Environments.Staging), config);
        Assert.False(result.Ok);
        Assert.Contains(CountryFiscalLockEvaluator.ReasonAllowSimulatedTse, result.Reasons);
    }

    [Fact]
    public void Validate_Production_FakeProvider_Fails()
    {
        var r = CreateValidator(
            Environments.Production,
            Config(("KassenSicherheit:Provider", "fake")))
            .Validate(null, new CountryFiscalLockOptions());
        Assert.True(r.Failed);
        Assert.Contains("KassenSicherheit:Provider", r.FailureMessage, StringComparison.Ordinal);
    }
}

/// <summary>
/// AT production lock remains on <see cref="TseProductionOptionsValidator"/> (byte-identical path).
/// </summary>
public sealed class CountryFiscalLockAtPathStillEnforcedTests
{
    [Fact]
    public void TseProductionOptionsValidator_Production_Still_Rejects_SoftTse()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RKSV:Mode"] = "Production",
                ["RKSV:TseMode"] = "Real",
            })
            .Build();
        var validator = new TseProductionOptionsValidator(
            env.Object,
            config,
            NullLogger<TseProductionOptionsValidator>.Instance);
        var opts = new TseOptions
        {
            TseMode = "Device",
            Mode = "Real",
            Provider = "fiskaly",
            SoftTseEnabled = true,
        };
        var r = validator.Validate(null, opts);
        Assert.True(r.Failed);
        Assert.Contains("SoftTseEnabled", r.FailureMessage, StringComparison.Ordinal);
    }
}
