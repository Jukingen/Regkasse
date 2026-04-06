using Microsoft.Extensions.Hosting;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// Optional FinanzOnline <b>simulated transport</b> behavior profiles. Applied only through simulated clients;
/// never used on real SOAP. By default only <see cref="Environments.Development"/>; optionally non-production when
/// <see cref="EnableBehaviorProfilesOutsideDevelopment"/> is true.
/// </summary>
public sealed class FinanzOnlineSimulationDeveloperOptions
{
    public const string SectionName = "FinanzOnline:Simulation:Developer";

    /// <summary>
    /// None | AlwaysSuccess | ImmediateProtocolSuccess | RetryableSubmitThenSuccess | RetryableUntilDeadLetter |
    /// PermanentSubmitFailure | ProtocolPendingThenSuccess
    /// </summary>
    public string BehaviorProfile { get; set; } = "None";

    /// <summary>
    /// When true and environment is not Production, <see cref="BehaviorProfile"/> is honored outside Development (e.g. Staging + simulation).
    /// </summary>
    public bool EnableBehaviorProfilesOutsideDevelopment { get; set; }

    /// <summary>
    /// For <see cref="BehaviorProfile"/> = RetryableSubmitThenSuccess: number of transient submit failures before success.
    /// </summary>
    public int RetryableSubmitFailuresBeforeSuccess { get; set; } = 2;

    /// <summary>
    /// For ProtocolPendingThenSuccess: protocol queries returning pending before final success.
    /// </summary>
    public int ProtocolPendingQueriesBeforeSuccess { get; set; } = 2;

    /// <summary>
    /// Artificial delay (ms) applied per simulated submit/query when profile is not None.
    /// </summary>
    public int ArtificialLatencyMs { get; set; } = 0;
}

/// <summary>Expose active config scenario or legacy developer profile on admin list responses when safe.</summary>
public static class FinanzOnlineSimulationDeveloperUi
{
    public static string? ActiveProfileForAdminList(
        IHostEnvironment hostEnvironment,
        FinanzOnlineSimulationOptions simulationOptions,
        FinanzOnlineSimulationDeveloperOptions developerOptions)
    {
        if (hostEnvironment.IsProduction())
            return null;

        var scenarioCanon = FinanzOnlineDeveloperSimulationEngine.GetEffectiveCanonicalScenarioName(
            hostEnvironment,
            simulationOptions);
        if (!string.IsNullOrEmpty(scenarioCanon))
            return $"Scenario:{scenarioCanon}";

        if (!hostEnvironment.IsDevelopment() && !developerOptions.EnableBehaviorProfilesOutsideDevelopment)
            return null;
        var p = (developerOptions.BehaviorProfile ?? "None").Trim();
        if (p.Length == 0 || string.Equals(p, "None", StringComparison.OrdinalIgnoreCase))
            return null;
        return $"DeveloperProfile:{p}";
    }
}
