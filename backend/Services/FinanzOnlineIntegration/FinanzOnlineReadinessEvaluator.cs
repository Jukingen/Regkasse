using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// Configuration readiness (no SOAP). Optionally augments with tenant <c>company_settings</c> columns when a probe is supplied (admin API).
/// </summary>
public static class FinanzOnlineReadinessEvaluator
{
    public static FinanzOnlineReadinessResponse Evaluate(
        FinanzOnlineSessionOptions session,
        FinanzOnlineRegistrierkassenOptions registrierkassen,
        FinanzOnlineTransmissionQueryOptions transmissionQuery,
        FinanzOnlineOutboxOptions outbox,
        FinanzOnlineConnectivityOptions connectivity,
        FinanzOnlineDevTestOptions devTest,
        FinanzOnlineSimulationOptions? simulationOptions = null,
        IHostEnvironment? hostEnvironment = null,
        FinanzOnlineReadinessTenantCompanyProbe? tenantCompanyProbe = null)
    {
        simulationOptions ??= new FinanzOnlineSimulationOptions();
        var findings = new List<FinanzOnlineReadinessFindingDto>();
        var sSim = session.UseSimulation;
        var rSim = registrierkassen.UseSimulation;
        var tSim = transmissionQuery.UseSimulation;
        var simCount = (sSim ? 1 : 0) + (rSim ? 1 : 0) + (tSim ? 1 : 0);

        string transportMode;
        if (simCount == 3)
            transportMode = "AllSimulated";
        else if (simCount == 0)
            transportMode = "AllReal";
        else
        {
            transportMode = "Mixed";
            findings.Add(new FinanzOnlineReadinessFindingDto
            {
                Severity = "Error",
                Code = "FO_READINESS_MIXED_TRANSPORT_LAYERS",
                Category = "Transport",
                Message =
                    "FinanzOnline transport layers are inconsistent: Session, Registrierkassen, and TransmissionQuery must all use the same simulation setting (all simulated or all real). Mixed mode is unsupported and misleading. Action: align FinanzOnline:Session:UseSimulation, FinanzOnline:Registrierkassen:UseSimulation, and FinanzOnline:TransmissionQuery:UseSimulation.",
            });
        }

        if (simCount > 0)
        {
            findings.Add(new FinanzOnlineReadinessFindingDto
            {
                Severity = "Warning",
                Code = "FO_READINESS_SIMULATION_ACTIVE",
                Category = "Simulation",
                Message =
                    "At least one FinanzOnline SOAP layer uses simulation — outbound traffic to BMF is not authoritative. Action: do not treat protocol success as real FinanzOnline acceptance; disable all three UseSimulation flags for real TEST/PROD SOAP.",
            });
        }

        if (registrierkassen.EnableRealTestSubmission && rSim)
        {
            findings.Add(new FinanzOnlineReadinessFindingDto
            {
                Severity = "Error",
                Code = "FO_READINESS_CONFLICT_ENABLE_REAL_TEST_WITH_REG_SIMULATION",
                Category = "Transport",
                Message =
                    "Registrierkassen.EnableRealTestSubmission is true but Registrierkassen.UseSimulation is true — real TEST submission cannot run. Action: set Registrierkassen.UseSimulation=false and align all layers to real transport.",
            });
        }

        if (transmissionQuery.EnableRealTestQuery && tSim)
        {
            findings.Add(new FinanzOnlineReadinessFindingDto
            {
                Severity = "Error",
                Code = "FO_READINESS_CONFLICT_ENABLE_REAL_QUERY_WITH_TX_SIMULATION",
                Category = "Transport",
                Message =
                    "TransmissionQuery.EnableRealTestQuery is true but TransmissionQuery.UseSimulation is true — real protocol query cannot run. Action: set TransmissionQuery.UseSimulation=false.",
            });
        }

        var companyProbeEvaluated = connectivity.UseCompanySettings && tenantCompanyProbe?.WasEvaluated == true;

        if (connectivity.UseCompanySettings && simCount == 0)
        {
            if (!companyProbeEvaluated)
            {
                findings.Add(new FinanzOnlineReadinessFindingDto
                {
                    Severity = "Warning",
                    Code = "FO_READINESS_COMPANY_SETTINGS_UNVERIFIED_IN_THIS_CONTEXT",
                    Category = "CompanySettings",
                    Message =
                        "FinanzOnline:Connectivity:UseCompanySettings is true, but tenant company_settings were not evaluated in this call (typical for anonymous GET /health/finanzonline). Action: call GET api/admin/finanzonline-readiness with an authenticated tenant context to verify FinanzOnlineApiUrl, credentials, and participant IDs.",
                });
            }
        }

        if (simCount == 0 && !connectivity.UseCompanySettings)
        {
            if (string.IsNullOrWhiteSpace(session.BaseUrl))
            {
                findings.Add(new FinanzOnlineReadinessFindingDto
                {
                    Severity = "Error",
                    Code = "FO_READINESS_SESSION_BASEURL_MISSING",
                    Category = "Transport",
                    Message =
                        "FinanzOnline:Session:BaseUrl is empty — real session SOAP cannot run. Action: set Session:BaseUrl or enable FinanzOnline:Connectivity:UseCompanySettings and maintain FinanzOnlineApiUrl in company_settings.",
                });
            }

            if (string.IsNullOrWhiteSpace(registrierkassen.BaseUrl))
            {
                findings.Add(new FinanzOnlineReadinessFindingDto
                {
                    Severity = "Error",
                    Code = "FO_READINESS_RKDB_BASEURL_MISSING",
                    Category = "Transport",
                    Message =
                        "FinanzOnline:Registrierkassen:BaseUrl is empty — real rkdb SOAP cannot run. Action: set Registrierkassen:BaseUrl or use company_settings FinanzOnlineApiUrl with UseCompanySettings.",
                });
            }
        }

        if (simCount == 0 && connectivity.UseCompanySettings && companyProbeEvaluated && tenantCompanyProbe != null)
        {
            var p = tenantCompanyProbe;
            if (!p.CompanySettingsRowExists)
            {
                findings.Add(new FinanzOnlineReadinessFindingDto
                {
                    Severity = "Error",
                    Code = "FO_READINESS_COMPANY_SETTINGS_ROW_MISSING",
                    Category = "CompanySettings",
                    Message =
                        "No company_settings row exists for the effective tenant — FinanzOnline cannot resolve URLs or credentials from company settings. Action: create company settings for the tenant or disable UseCompanySettings and use configuration file endpoints and credentials.",
                });
            }
            else
            {
                if (!p.HasFinanzOnlineApiUrl)
                {
                    findings.Add(new FinanzOnlineReadinessFindingDto
                    {
                        Severity = "Error",
                        Code = "FO_READINESS_COMPANY_SETTINGS_API_URL_MISSING",
                        Category = "CompanySettings",
                        Message =
                            "CompanySettings.FinanzOnlineApiUrl is empty — session and rkdb SOAP hosts cannot be derived. Action: set FinanzOnlineApiUrl for the tenant.",
                    });
                }

                if (!p.HasFinanzOnlineUsername || !p.HasFinanzOnlinePassword)
                {
                    findings.Add(new FinanzOnlineReadinessFindingDto
                    {
                        Severity = "Error",
                        Code = "FO_READINESS_COMPANY_SETTINGS_CREDENTIALS_INCOMPLETE",
                        Category = "Credentials",
                        Message =
                            "CompanySettings FinanzOnline username and/or password is missing — real session login cannot succeed. Action: set FinanzOnlineUsername and FinanzOnlinePassword (or disable UseCompanySettings and use configuration credentials).",
                    });
                }

                if (!p.HasFinanzOnlineTelematikId || !p.HasFinanzOnlineHerstellerId)
                {
                    findings.Add(new FinanzOnlineReadinessFindingDto
                    {
                        Severity = "Error",
                        Code = "FO_READINESS_COMPANY_SETTINGS_PARTICIPANT_IDS_INCOMPLETE",
                        Category = "Credentials",
                        Message =
                            "CompanySettings FinanzOnlineTelematikId and/or FinanzOnlineHerstellerId is missing — SOAP session requires tid and herstellerid. Action: set both fields in company_settings.",
                    });
                }
            }
        }

        if (simCount == 0 && !connectivity.UseCompanySettings)
        {
            if (!SessionConfigHasCredentialPair(session))
            {
                findings.Add(new FinanzOnlineReadinessFindingDto
                {
                    Severity = "Error",
                    Code = "FO_READINESS_CONFIG_SESSION_CREDENTIALS_MISSING",
                    Category = "Credentials",
                    Message =
                        "No FinanzOnline session username/password is configured in appsettings (DefaultCredential or ScopedCredentials). Action: set credentials or enable UseCompanySettings.",
                });
            }
            else if (!SessionConfigHasFullyUsableCredential(session))
            {
                findings.Add(new FinanzOnlineReadinessFindingDto
                {
                    Severity = "Error",
                    Code = "FO_READINESS_CONFIG_SESSION_PARTICIPANT_IDS_MISSING",
                    Category = "Credentials",
                    Message =
                        "Session credentials exist but TelematikId and/or HerstellerId is missing on every configured credential row. Action: set TelematikId and HerstellerId on DefaultCredential or a matching ScopedCredential.",
                });
            }
        }

        if (simCount == 0 && !registrierkassen.EnableRealTestSubmission)
        {
            findings.Add(new FinanzOnlineReadinessFindingDto
            {
                Severity = "Warning",
                Code = "FO_READINESS_REAL_TEST_SUBMISSION_DISABLED",
                Category = "Transport",
                Message =
                    "Real SOAP transports are enabled but FinanzOnline:Registrierkassen:EnableRealTestSubmission is false — TEST rkdb submissions will be rejected with TEST_REAL_SUBMISSION_DISABLED. Action: set EnableRealTestSubmission=true when you intend real TEST rkdb traffic.",
            });
        }

        if (simCount == 0 && !transmissionQuery.EnableRealTestQuery)
        {
            findings.Add(new FinanzOnlineReadinessFindingDto
            {
                Severity = "Warning",
                Code = "FO_READINESS_REAL_TEST_PROTOCOL_QUERY_DISABLED",
                Category = "Transport",
                Message =
                    "FinanzOnline:TransmissionQuery:EnableRealTestQuery is false — outbox rows awaiting protocol cannot complete via real status_kasse (TEST_REAL_QUERY_DISABLED). Action: set EnableRealTestQuery=true for protocol reconciliation.",
            });
        }

        if (!outbox.Enabled)
        {
            findings.Add(new FinanzOnlineReadinessFindingDto
            {
                Severity = "Error",
                Code = "FO_READINESS_OUTBOX_DISABLED",
                Category = "Outbox",
                Message =
                    "FinanzOnlineOutbox:Enabled is false — the outbox background processor is not running and queued FinanzOnline messages will not advance. Action: set FinanzOnlineOutbox:Enabled=true in production environments that rely on the SOAP pipeline.",
            });
        }

        if (devTest.AllowEnqueueSmokeTest)
        {
            findings.Add(new FinanzOnlineReadinessFindingDto
            {
                Severity = "Warning",
                Code = "FO_READINESS_DEVTEST_SMOKE_ENQUEUE_ENABLED",
                Category = "DevTest",
                Message =
                    "FinanzOnline:DevTest:AllowEnqueueSmokeTest is true — development-only outbox enqueue is allowed. Action: set to false outside trusted development environments.",
            });
        }

        var configuredScenarioRaw = FinanzOnlineDeveloperSimulationEngine.GetConfiguredScenarioRawOrNull(simulationOptions);
        if (configuredScenarioRaw != null && hostEnvironment != null)
        {
            if (hostEnvironment.IsProduction())
            {
                findings.Add(new FinanzOnlineReadinessFindingDto
                {
                    Severity = "Error",
                    Code = "FO_READINESS_SIMULATION_SCENARIO_IGNORED_IN_PRODUCTION",
                    Category = "Simulation",
                    Message =
                        $"FinanzOnline:Simulation:Scenario is set to \"{configuredScenarioRaw}\" but Production never applies simulation scenarios — value is ignored. Action: remove Scenario from production configuration.",
                });
            }
            else
            {
                var normalized = FinanzOnlineDeveloperSimulationEngine.GetEffectiveCanonicalScenarioName(
                    hostEnvironment,
                    simulationOptions);
                if (normalized == null)
                {
                    findings.Add(new FinanzOnlineReadinessFindingDto
                    {
                        Severity = "Warning",
                        Code = "FO_READINESS_SIMULATION_SCENARIO_UNKNOWN",
                        Category = "Simulation",
                        Message =
                            $"FinanzOnline:Simulation:Scenario value \"{configuredScenarioRaw}\" is not recognized — expected None, ImmediateSuccess, RetryThenSuccess, PermanentFailure, AwaitingProtocolThenSuccess, or DeadLetter. Action: fix Scenario spelling or use a supported value.",
                    });
                }
                else if (!FinanzOnlineDeveloperSimulationEngine.ShouldApplySimulationScenario(hostEnvironment, simulationOptions))
                {
                    findings.Add(new FinanzOnlineReadinessFindingDto
                    {
                        Severity = "Warning",
                        Code = "FO_READINESS_SIMULATION_SCENARIO_NOT_APPLIED",
                        Category = "Simulation",
                        Message =
                            $"FinanzOnline:Simulation:Scenario is \"{normalized}\" but the host is not Development and FinanzOnline:Simulation:EnableScenarioOutsideDevelopment is false — scenario is not applied. Action: enable EnableScenarioOutsideDevelopment only if intentional, or run in Development.",
                    });
                }
                else
                {
                    var delayPart = simulationOptions.FixedDelayMs > 0
                        ? $" FixedDelayMs={simulationOptions.FixedDelayMs}."
                        : "";
                    var seedPart = simulationOptions.Seed != 0
                        ? $" Seed={simulationOptions.Seed}."
                        : "";
                    findings.Add(new FinanzOnlineReadinessFindingDto
                    {
                        Severity = "Warning",
                        Code = "FO_READINESS_SIMULATION_SCENARIO_ACTIVE",
                        Category = "Simulation",
                        Message =
                            $"Config-driven FinanzOnline simulation scenario is active: {normalized}.{delayPart}{seedPart} Outcomes are not BMF-authoritative. Action: disable Scenario for real BMF validation.",
                    });
                }
            }
        }

        var hasError = findings.Any(f => string.Equals(f.Severity, "Error", StringComparison.OrdinalIgnoreCase));
        var hasWarning = findings.Any(f => string.Equals(f.Severity, "Warning", StringComparison.OrdinalIgnoreCase));

        var sessionEndpointResolvable = !connectivity.UseCompanySettings
            ? !string.IsNullOrWhiteSpace(session.BaseUrl)
            : companyProbeEvaluated && tenantCompanyProbe!.HasFinanzOnlineApiUrl;

        var rkdbEndpointResolvable = !connectivity.UseCompanySettings
            ? !string.IsNullOrWhiteSpace(registrierkassen.BaseUrl)
            : companyProbeEvaluated && tenantCompanyProbe!.HasFinanzOnlineApiUrl;

        var companyFinanzOnlineComplete = companyProbeEvaluated &&
            tenantCompanyProbe != null &&
            tenantCompanyProbe.CompanySettingsRowExists &&
            tenantCompanyProbe.HasFinanzOnlineApiUrl &&
            tenantCompanyProbe.HasFinanzOnlineUsername &&
            tenantCompanyProbe.HasFinanzOnlinePassword &&
            tenantCompanyProbe.HasFinanzOnlineTelematikId &&
            tenantCompanyProbe.HasFinanzOnlineHerstellerId;

        var configSessionHasUserPass = SessionConfigHasCredentialPair(session);
        var configSessionHasParticipants = SessionConfigHasFullyUsableCredential(session);

        var credentialsReadyForReal = simCount > 0
            ? true
            : !connectivity.UseCompanySettings
                ? configSessionHasUserPass && configSessionHasParticipants
                : companyProbeEvaluated && companyFinanzOnlineComplete;

        var realSubmitPossible = simCount == 0
            && outbox.Enabled
            && registrierkassen.EnableRealTestSubmission
            && sessionEndpointResolvable
            && rkdbEndpointResolvable
            && credentialsReadyForReal
            && !hasError;

        var protocolPossible = simCount == 0
            && outbox.Enabled
            && transmissionQuery.EnableRealTestQuery
            && sessionEndpointResolvable
            && rkdbEndpointResolvable
            && credentialsReadyForReal
            && !hasError;

        string overall;
        if (hasError)
            overall = "Unhealthy";
        else if (simCount > 0 || hasWarning)
            overall = "Degraded";
        else
            overall = "Healthy";

        var summary = overall switch
        {
            "Healthy" =>
                "FinanzOnline configuration matches the gates checked here for real SOAP TEST traffic (no live SOAP call was made).",
            "Unhealthy" =>
                "Blocking FinanzOnline configuration issues — use diagnostics flags and findings (category + code) to fix before expecting real TEST traffic.",
            _ =>
                "FinanzOnline is simulated or degraded — review Diagnostics and Findings; do not assume BMF-authoritative results until resolved.",
        };

        var effectiveForResponse = hostEnvironment != null
            ? FinanzOnlineDeveloperSimulationEngine.GetEffectiveCanonicalScenarioName(hostEnvironment, simulationOptions)
            : null;

        var diagnostics = new FinanzOnlineReadinessDiagnosticsDto
        {
            SessionLayerSimulated = sSim,
            RegistrierkassenLayerSimulated = rSim,
            TransmissionQueryLayerSimulated = tSim,
            MixedTransportLayers = string.Equals(transportMode, "Mixed", StringComparison.Ordinal),
            AnyLayerSimulated = simCount > 0,
            AllLayersReal = simCount == 0 && !string.Equals(transportMode, "Mixed", StringComparison.Ordinal),
            OutboxPipelineEnabled = outbox.Enabled,
            RegistrierkassenEnableRealTestSubmission = registrierkassen.EnableRealTestSubmission,
            TransmissionQueryEnableRealTestQuery = transmissionQuery.EnableRealTestQuery,
            ConnectivityUsesCompanySettings = connectivity.UseCompanySettings,
            CompanySettingsProbeEvaluated = connectivity.UseCompanySettings ? companyProbeEvaluated : null,
            CompanySettingsFinanzOnlineComplete = connectivity.UseCompanySettings && companyProbeEvaluated
                ? companyFinanzOnlineComplete
                : null,
            ConfigSessionBaseUrlConfigured = !string.IsNullOrWhiteSpace(session.BaseUrl),
            ConfigRkdbBaseUrlConfigured = !string.IsNullOrWhiteSpace(registrierkassen.BaseUrl),
            ConfigSessionHasUsernamePassword = configSessionHasUserPass,
            ConfigSessionHasParticipantIds = configSessionHasParticipants,
            SessionEndpointResolvable = sessionEndpointResolvable,
            RkdbEndpointResolvable = rkdbEndpointResolvable,
        };

        return new FinanzOnlineReadinessResponse
        {
            OverallStatus = overall,
            TransportMode = transportMode,
            RealTestSubmissionPossible = realSubmitPossible,
            ProtocolReconciliationPossible = protocolPossible,
            OutboxWorkerEnabled = outbox.Enabled,
            Summary = summary,
            Findings = findings.OrderByDescending(f => f.Severity, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Code).ToList(),
            ConfiguredSimulationScenario = configuredScenarioRaw,
            EffectiveSimulationScenario = effectiveForResponse,
            SimulationFixedDelayMs = simulationOptions.FixedDelayMs > 0 ? simulationOptions.FixedDelayMs : null,
            SimulationSeed = simulationOptions.Seed != 0 ? simulationOptions.Seed : null,
            Diagnostics = diagnostics,
        };
    }

    /// <summary>Resolves options from a root <see cref="IServiceProvider"/> (creates a scope for scoped options).</summary>
    public static FinanzOnlineReadinessResponse EvaluateFromRootServices(IServiceProvider rootServices)
    {
        using var scope = rootServices.CreateScope();
        var sp = scope.ServiceProvider;
        return Evaluate(
            sp.GetRequiredService<IOptionsMonitor<FinanzOnlineSessionOptions>>().CurrentValue,
            sp.GetRequiredService<IOptionsMonitor<FinanzOnlineRegistrierkassenOptions>>().CurrentValue,
            sp.GetRequiredService<IOptionsMonitor<FinanzOnlineTransmissionQueryOptions>>().CurrentValue,
            sp.GetRequiredService<IOptionsMonitor<FinanzOnlineOutboxOptions>>().CurrentValue,
            sp.GetRequiredService<IOptionsMonitor<FinanzOnlineConnectivityOptions>>().CurrentValue,
            sp.GetRequiredService<IOptionsMonitor<FinanzOnlineDevTestOptions>>().CurrentValue,
            sp.GetRequiredService<IOptionsMonitor<FinanzOnlineSimulationOptions>>().CurrentValue,
            sp.GetRequiredService<IHostEnvironment>(),
            tenantCompanyProbe: null);
    }

    private static bool SessionConfigHasCredentialPair(FinanzOnlineSessionOptions session)
    {
        if (CredentialPairOk(session.DefaultCredential))
            return true;
        return session.ScopedCredentials.Any(CredentialPairOk);
    }

    private static bool SessionConfigHasFullyUsableCredential(FinanzOnlineSessionOptions session)
    {
        if (UsableCredential(session.DefaultCredential))
            return true;
        return session.ScopedCredentials.Any(UsableCredential);
    }

    private static bool CredentialPairOk(FinanzOnlineScopedCredential c) =>
        !string.IsNullOrWhiteSpace(c.Username) && !string.IsNullOrWhiteSpace(c.Password);

    private static bool ParticipantPairOk(FinanzOnlineScopedCredential c) =>
        !string.IsNullOrWhiteSpace(c.TelematikId) && !string.IsNullOrWhiteSpace(c.HerstellerId);

    private static bool UsableCredential(FinanzOnlineScopedCredential c) =>
        CredentialPairOk(c) && ParticipantPairOk(c);
}
