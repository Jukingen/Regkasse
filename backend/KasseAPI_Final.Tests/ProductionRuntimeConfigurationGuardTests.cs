using KasseAPI_Final.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class ProductionRuntimeConfigurationGuardTests
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

    private static (string Key, string? Value)[] SafeProductionPairs() =>
    [
        ("Security:Csrf:Enabled", "true"),
        ("FinanzOnline:Session:UseSimulation", "false"),
        ("FinanzOnline:Registrierkassen:UseSimulation", "false"),
        ("FinanzOnline:TransmissionQuery:UseSimulation", "false"),
        ("FinanzOnline:Mode", "Production"),
        ("Backup:ExecutionAdapterKind", "PgDump"),
        ("PaymentGateway:Provider", "None"),
        ("TwoFactorAuth:Enabled", "true"),
        ("RateLimiting:Enabled", "true"),
        ("Redis:Enabled", "true"),
        ("Redis:ConnectionString", "redis:6379"),
    ];

    [Fact]
    public void ThrowIfUnsafe_no_ops_outside_Production()
    {
        var unsafeDev = Config(("Security:Csrf:Enabled", "false"), ("PaymentGateway:Provider", "Mock"));
        ProductionRuntimeConfigurationGuard.ThrowIfUnsafe(Env(Environments.Development), unsafeDev);
        ProductionRuntimeConfigurationGuard.ThrowIfUnsafe(Env(Environments.Staging), unsafeDev);
    }

    [Fact]
    public void ThrowIfUnsafe_accepts_safe_Production_config()
    {
        ProductionRuntimeConfigurationGuard.ThrowIfUnsafe(
            Env(Environments.Production),
            Config(SafeProductionPairs()));
    }

    [Fact]
    public void CollectViolations_reports_each_unsafe_default()
    {
        var errors = ProductionRuntimeConfigurationGuard.CollectViolations(Config());
        Assert.DoesNotContain(ProductionRuntimeConfigurationGuard.CsrfMustBeEnabled, errors);
        Assert.DoesNotContain(ProductionRuntimeConfigurationGuard.TwoFactorMustBeEnabled, errors);
        Assert.Contains(ProductionRuntimeConfigurationGuard.BackupMustUsePgDump, errors);
        Assert.Contains(ProductionRuntimeConfigurationGuard.PaymentGatewayMockNotAllowed, errors);
        Assert.Contains(ProductionRuntimeConfigurationGuard.RateLimitingMustBeEnabled, errors);
        Assert.Contains(ProductionRuntimeConfigurationGuard.RedisMustBeEnabled, errors);
    }

    [Fact]
    public void ThrowIfUnsafe_Production_rejects_FON_simulation()
    {
        var pairs = SafeProductionPairs().Select(p => p).ToList();
        pairs.RemoveAll(p => p.Key == "FinanzOnline:Session:UseSimulation");
        pairs.Add(("FinanzOnline:Session:UseSimulation", "true"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ProductionRuntimeConfigurationGuard.ThrowIfUnsafe(
                Env(Environments.Production),
                Config(pairs.ToArray())));
        Assert.Contains(ProductionRuntimeConfigurationGuard.FonSimulationNotAllowed, ex.Message);
    }

    [Fact]
    public void ThrowIfUnsafe_Production_rejects_CSRF_off()
    {
        var pairs = SafeProductionPairs().Select(p => p).ToList();
        pairs.RemoveAll(p => p.Key == "Security:Csrf:Enabled");
        pairs.Add(("Security:Csrf:Enabled", "false"));

        var ex = Assert.Throws<InvalidOperationException>(() =>
            ProductionRuntimeConfigurationGuard.ThrowIfUnsafe(
                Env(Environments.Production),
                Config(pairs.ToArray())));
        Assert.Contains(ProductionRuntimeConfigurationGuard.CsrfMustBeEnabled, ex.Message);
    }

    [Fact]
    public void CollectViolations_csrfForceEnabled_skips_csrf()
    {
        var errors = ProductionRuntimeConfigurationGuard.CollectViolations(
            Config(("Security:Csrf:Enabled", "false")),
            csrfForceEnabled: true);
        Assert.DoesNotContain(ProductionRuntimeConfigurationGuard.CsrfMustBeEnabled, errors);
    }

    [Fact]
    public void CollectViolations_allows_Stripe_and_None_gateway()
    {
        foreach (var provider in new[] { "Stripe", "None", "Disabled" })
        {
            var errors = ProductionRuntimeConfigurationGuard.CollectViolations(
                Config(("PaymentGateway:Provider", provider)));
            Assert.DoesNotContain(ProductionRuntimeConfigurationGuard.PaymentGatewayMockNotAllowed, errors);
        }
    }
}

public sealed class ProductionCsrfPostConfigureTests
{
    [Fact]
    public void PostConfigure_enables_CSRF_in_Production_when_disabled()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var sut = new ProductionCsrfPostConfigure(env.Object, NullLogger<ProductionCsrfPostConfigure>.Instance);
        var options = new CsrfOptions { Enabled = false };

        sut.PostConfigure(Options.DefaultName, options);

        Assert.True(options.Enabled);
    }

    [Fact]
    public void PostConfigure_does_not_enable_CSRF_in_Development()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var sut = new ProductionCsrfPostConfigure(env.Object, NullLogger<ProductionCsrfPostConfigure>.Instance);
        var options = new CsrfOptions { Enabled = false };

        sut.PostConfigure(Options.DefaultName, options);

        Assert.False(options.Enabled);
    }
}
