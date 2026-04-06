namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// Top-level FinanzOnline simulation scenario (config-driven). Binds to <c>FinanzOnline:Simulation</c> (sibling of nested <c>Developer</c> section).
/// Effective only when the host is not Production and (Development or <see cref="EnableScenarioOutsideDevelopment"/>).
/// </summary>
public sealed class FinanzOnlineSimulationOptions
{
    public const string SectionName = "FinanzOnline:Simulation";

    /// <summary>
    /// None | ImmediateSuccess | RetryThenSuccess | PermanentFailure | AwaitingProtocolThenSuccess | DeadLetter
    /// </summary>
    public string Scenario { get; set; } = "None";

    /// <summary>For <see cref="Scenario"/> RetryThenSuccess: transient submit failures before success.</summary>
    public int RetryCountBeforeSuccess { get; set; } = 2;

    /// <summary>For AwaitingProtocolThenSuccess: status_kasse queries returning pending before final success.</summary>
    public int ProtocolPendingQueriesBeforeSuccess { get; set; } = 2;

    /// <summary>Delay applied per simulated submit/query when an effective scenario is active.</summary>
    public int FixedDelayMs { get; set; }

    /// <summary>
    /// Non-zero: mixed into in-memory attempt keys so parallel test runs can isolate counters without colliding on correlation ids.
    /// </summary>
    public long Seed { get; set; }

    /// <summary>When true, <see cref="Scenario"/> may apply outside Development (still never in Production).</summary>
    public bool EnableScenarioOutsideDevelopment { get; set; }
}

/// <summary>Normalized scenario ids for <see cref="FinanzOnlineSimulationOptions.Scenario"/>.</summary>
public static class FinanzOnlineSimulationScenarios
{
    public const string None = "None";
    public const string ImmediateSuccess = "ImmediateSuccess";
    public const string RetryThenSuccess = "RetryThenSuccess";
    public const string PermanentFailure = "PermanentFailure";
    public const string AwaitingProtocolThenSuccess = "AwaitingProtocolThenSuccess";
    public const string DeadLetter = "DeadLetter";
}
