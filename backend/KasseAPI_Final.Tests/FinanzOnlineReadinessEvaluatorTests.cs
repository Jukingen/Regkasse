using KasseAPI_Final.Services.FinanzOnlineIntegration;
using Microsoft.Extensions.Hosting;
using Moq;
using Xunit;

namespace KasseAPI_Final.Tests;

public sealed class FinanzOnlineReadinessEvaluatorTests
{
    private static FinanzOnlineSessionOptions RealSessionWithFullConfigCredentials(string sessionBaseUrl) =>
        new()
        {
            UseSimulation = false,
            BaseUrl = sessionBaseUrl,
            DefaultCredential = new FinanzOnlineScopedCredential
            {
                Username = "fon_user",
                Password = "fon_pass",
                TelematikId = "123456789012",
                HerstellerId = "H1234567890123456789012",
            },
        };

    [Fact]
    public void All_simulated_is_degraded_with_simulation_finding()
    {
        var r = FinanzOnlineReadinessEvaluator.Evaluate(
            new FinanzOnlineSessionOptions { UseSimulation = true, BaseUrl = "" },
            new FinanzOnlineRegistrierkassenOptions { UseSimulation = true, EnableRealTestSubmission = false, BaseUrl = "" },
            new FinanzOnlineTransmissionQueryOptions { UseSimulation = true, EnableRealTestQuery = false },
            new FinanzOnlineOutboxOptions { Enabled = true },
            new FinanzOnlineConnectivityOptions { UseCompanySettings = false },
            new FinanzOnlineDevTestOptions(),
            simulationOptions: null,
            hostEnvironment: null,
            tenantCompanyProbe: null);

        Assert.Equal("Degraded", r.OverallStatus);
        Assert.Equal("AllSimulated", r.TransportMode);
        Assert.False(r.RealTestSubmissionPossible);
        Assert.Contains(r.Findings, f => f.Code == "FO_READINESS_SIMULATION_ACTIVE");
        Assert.NotNull(r.Diagnostics);
        Assert.True(r.Diagnostics!.AnyLayerSimulated);
    }

    [Fact]
    public void Mixed_layers_is_unhealthy()
    {
        var r = FinanzOnlineReadinessEvaluator.Evaluate(
            RealSessionWithFullConfigCredentials("https://example.test/session"),
            new FinanzOnlineRegistrierkassenOptions { UseSimulation = true, EnableRealTestSubmission = true, BaseUrl = "https://example.test/rkdb" },
            new FinanzOnlineTransmissionQueryOptions { UseSimulation = false, EnableRealTestQuery = true },
            new FinanzOnlineOutboxOptions { Enabled = true },
            new FinanzOnlineConnectivityOptions { UseCompanySettings = false },
            new FinanzOnlineDevTestOptions(),
            simulationOptions: null,
            hostEnvironment: null,
            tenantCompanyProbe: null);

        Assert.Equal("Unhealthy", r.OverallStatus);
        Assert.Equal("Mixed", r.TransportMode);
        Assert.Contains(r.Findings, f => f.Code == "FO_READINESS_MIXED_TRANSPORT_LAYERS");
        Assert.True(r.Diagnostics!.MixedTransportLayers);
    }

    [Fact]
    public void Real_transport_with_flags_and_urls_healthy()
    {
        var r = FinanzOnlineReadinessEvaluator.Evaluate(
            RealSessionWithFullConfigCredentials("https://finanzonline.bmf.gv.at/fonws/ws/session"),
            new FinanzOnlineRegistrierkassenOptions
            {
                UseSimulation = false,
                EnableRealTestSubmission = true,
                BaseUrl = "https://finanzonline.bmf.gv.at/fonws/ws/rkdb",
            },
            new FinanzOnlineTransmissionQueryOptions { UseSimulation = false, EnableRealTestQuery = true },
            new FinanzOnlineOutboxOptions { Enabled = true },
            new FinanzOnlineConnectivityOptions { UseCompanySettings = false },
            new FinanzOnlineDevTestOptions(),
            simulationOptions: null,
            hostEnvironment: null,
            tenantCompanyProbe: null);

        Assert.Equal("Healthy", r.OverallStatus);
        Assert.True(r.RealTestSubmissionPossible);
        Assert.True(r.ProtocolReconciliationPossible);
        Assert.True(r.Diagnostics!.SessionEndpointResolvable);
        Assert.True(r.Diagnostics.RkdbEndpointResolvable);
    }

    [Fact]
    public void EnableRealTestSubmission_with_reg_simulation_is_blocking_error()
    {
        var r = FinanzOnlineReadinessEvaluator.Evaluate(
            new FinanzOnlineSessionOptions { UseSimulation = true },
            new FinanzOnlineRegistrierkassenOptions { UseSimulation = true, EnableRealTestSubmission = true },
            new FinanzOnlineTransmissionQueryOptions { UseSimulation = true },
            new FinanzOnlineOutboxOptions { Enabled = true },
            new FinanzOnlineConnectivityOptions(),
            new FinanzOnlineDevTestOptions(),
            simulationOptions: null,
            hostEnvironment: null,
            tenantCompanyProbe: null);

        Assert.Contains(r.Findings, f => f.Code == "FO_READINESS_CONFLICT_ENABLE_REAL_TEST_WITH_REG_SIMULATION");
    }

    [Fact]
    public void Outbox_disabled_is_unhealthy()
    {
        var r = FinanzOnlineReadinessEvaluator.Evaluate(
            RealSessionWithFullConfigCredentials("https://x/s"),
            new FinanzOnlineRegistrierkassenOptions { UseSimulation = false, EnableRealTestSubmission = true, BaseUrl = "https://x/r" },
            new FinanzOnlineTransmissionQueryOptions { UseSimulation = false, EnableRealTestQuery = true },
            new FinanzOnlineOutboxOptions { Enabled = false },
            new FinanzOnlineConnectivityOptions { UseCompanySettings = false },
            new FinanzOnlineDevTestOptions(),
            simulationOptions: null,
            hostEnvironment: null,
            tenantCompanyProbe: null);

        Assert.Equal("Unhealthy", r.OverallStatus);
        Assert.Contains(r.Findings, f => f.Code == "FO_READINESS_OUTBOX_DISABLED");
        Assert.False(r.RealTestSubmissionPossible);
        Assert.False(r.Diagnostics!.OutboxPipelineEnabled);
    }

    [Fact]
    public void Simulation_scenario_active_finding_when_development()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var r = FinanzOnlineReadinessEvaluator.Evaluate(
            new FinanzOnlineSessionOptions { UseSimulation = true },
            new FinanzOnlineRegistrierkassenOptions { UseSimulation = true },
            new FinanzOnlineTransmissionQueryOptions { UseSimulation = true },
            new FinanzOnlineOutboxOptions { Enabled = true },
            new FinanzOnlineConnectivityOptions(),
            new FinanzOnlineDevTestOptions(),
            new FinanzOnlineSimulationOptions
            {
                Scenario = FinanzOnlineSimulationScenarios.ImmediateSuccess,
                FixedDelayMs = 5,
                Seed = 42,
            },
            env.Object,
            tenantCompanyProbe: null);

        Assert.Contains(r.Findings, f => f.Code == "FO_READINESS_SIMULATION_SCENARIO_ACTIVE");
        Assert.Equal(FinanzOnlineSimulationScenarios.ImmediateSuccess, r.ConfiguredSimulationScenario);
        Assert.Equal(FinanzOnlineSimulationScenarios.ImmediateSuccess, r.EffectiveSimulationScenario);
        Assert.Equal(5, r.SimulationFixedDelayMs);
        Assert.Equal(42, r.SimulationSeed);
    }

    [Fact]
    public void Simulation_scenario_in_production_is_ignored_with_error_finding()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Production);
        var r = FinanzOnlineReadinessEvaluator.Evaluate(
            RealSessionWithFullConfigCredentials("https://x/s"),
            new FinanzOnlineRegistrierkassenOptions
            {
                UseSimulation = false,
                EnableRealTestSubmission = true,
                BaseUrl = "https://x/r",
            },
            new FinanzOnlineTransmissionQueryOptions { UseSimulation = false, EnableRealTestQuery = true },
            new FinanzOnlineOutboxOptions { Enabled = true },
            new FinanzOnlineConnectivityOptions { UseCompanySettings = false },
            new FinanzOnlineDevTestOptions(),
            new FinanzOnlineSimulationOptions { Scenario = FinanzOnlineSimulationScenarios.RetryThenSuccess },
            env.Object,
            tenantCompanyProbe: null);

        Assert.Contains(r.Findings, f => f.Code == "FO_READINESS_SIMULATION_SCENARIO_IGNORED_IN_PRODUCTION");
        Assert.Null(r.EffectiveSimulationScenario);
    }

    [Fact]
    public void Unknown_simulation_scenario_emits_warning()
    {
        var env = new Mock<IHostEnvironment>();
        env.Setup(e => e.EnvironmentName).Returns(Environments.Development);
        var r = FinanzOnlineReadinessEvaluator.Evaluate(
            new FinanzOnlineSessionOptions { UseSimulation = true },
            new FinanzOnlineRegistrierkassenOptions { UseSimulation = true },
            new FinanzOnlineTransmissionQueryOptions { UseSimulation = true },
            new FinanzOnlineOutboxOptions { Enabled = true },
            new FinanzOnlineConnectivityOptions(),
            new FinanzOnlineDevTestOptions(),
            new FinanzOnlineSimulationOptions { Scenario = "NotARealScenario" },
            env.Object,
            tenantCompanyProbe: null);

        Assert.Contains(r.Findings, f => f.Code == "FO_READINESS_SIMULATION_SCENARIO_UNKNOWN");
        Assert.Null(r.EffectiveSimulationScenario);
    }

    [Fact]
    public void UseCompanySettings_without_tenant_probe_marks_unverified_and_blocks_real_submit()
    {
        var r = FinanzOnlineReadinessEvaluator.Evaluate(
            new FinanzOnlineSessionOptions { UseSimulation = false, BaseUrl = "" },
            new FinanzOnlineRegistrierkassenOptions { UseSimulation = false, EnableRealTestSubmission = true, BaseUrl = "" },
            new FinanzOnlineTransmissionQueryOptions { UseSimulation = false, EnableRealTestQuery = true },
            new FinanzOnlineOutboxOptions { Enabled = true },
            new FinanzOnlineConnectivityOptions { UseCompanySettings = true },
            new FinanzOnlineDevTestOptions(),
            simulationOptions: null,
            hostEnvironment: null,
            tenantCompanyProbe: null);

        Assert.Contains(r.Findings, f => f.Code == "FO_READINESS_COMPANY_SETTINGS_UNVERIFIED_IN_THIS_CONTEXT");
        Assert.False(r.RealTestSubmissionPossible);
        Assert.False(r.Diagnostics!.CompanySettingsProbeEvaluated ?? true);
    }

    [Fact]
    public void Company_probe_complete_allows_real_submit_without_config_base_urls()
    {
        var probe = new FinanzOnlineReadinessTenantCompanyProbe
        {
            WasEvaluated = true,
            CompanySettingsRowExists = true,
            HasFinanzOnlineApiUrl = true,
            HasFinanzOnlineUsername = true,
            HasFinanzOnlinePassword = true,
            HasFinanzOnlineTelematikId = true,
            HasFinanzOnlineHerstellerId = true,
        };

        var r = FinanzOnlineReadinessEvaluator.Evaluate(
            new FinanzOnlineSessionOptions { UseSimulation = false, BaseUrl = "" },
            new FinanzOnlineRegistrierkassenOptions { UseSimulation = false, EnableRealTestSubmission = true, BaseUrl = "" },
            new FinanzOnlineTransmissionQueryOptions { UseSimulation = false, EnableRealTestQuery = true },
            new FinanzOnlineOutboxOptions { Enabled = true },
            new FinanzOnlineConnectivityOptions { UseCompanySettings = true },
            new FinanzOnlineDevTestOptions(),
            simulationOptions: null,
            hostEnvironment: null,
            tenantCompanyProbe: probe);

        Assert.DoesNotContain(r.Findings, f => f.Code == "FO_READINESS_COMPANY_SETTINGS_UNVERIFIED_IN_THIS_CONTEXT");
        Assert.True(r.RealTestSubmissionPossible);
        Assert.True(r.Diagnostics!.CompanySettingsFinanzOnlineComplete);
    }

    [Fact]
    public void Config_credentials_missing_username_is_blocking()
    {
        var r = FinanzOnlineReadinessEvaluator.Evaluate(
            new FinanzOnlineSessionOptions
            {
                UseSimulation = false,
                BaseUrl = "https://x/s",
                DefaultCredential = new FinanzOnlineScopedCredential
                {
                    Username = "",
                    Password = "p",
                    TelematikId = "123456789012",
                    HerstellerId = "H1234567890123456789012",
                },
            },
            new FinanzOnlineRegistrierkassenOptions { UseSimulation = false, EnableRealTestSubmission = true, BaseUrl = "https://x/r" },
            new FinanzOnlineTransmissionQueryOptions { UseSimulation = false, EnableRealTestQuery = true },
            new FinanzOnlineOutboxOptions { Enabled = true },
            new FinanzOnlineConnectivityOptions { UseCompanySettings = false },
            new FinanzOnlineDevTestOptions(),
            simulationOptions: null,
            hostEnvironment: null,
            tenantCompanyProbe: null);

        Assert.Contains(r.Findings, f => f.Code == "FO_READINESS_CONFIG_SESSION_CREDENTIALS_MISSING");
        Assert.False(r.RealTestSubmissionPossible);
    }
}
