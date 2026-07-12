namespace KasseAPI_Final.Models;

/// <summary>
/// Top-level RKSV environment policy (demo vs production presentation and integrations).
/// Binds to the <c>RKSV</c> configuration section.
/// </summary>
public sealed class RksvOptions
{
    public const string SectionName = "RKSV";

    /// <summary>Demo | Production</summary>
    public string Mode { get; set; } = "Production";

    /// <summary>Simulation | Real — mirrors intended TSE integration mode.</summary>
    public string TseMode { get; set; } = "Real";

    /// <summary>Simulation | Real — mirrors intended FinanzOnline integration mode.</summary>
    public string FinanzOnlineMode { get; set; } = "Real";

    /// <summary>When true, receipts/closings show the DEMO / NICHT FISKAL disclaimer.</summary>
    public bool ShowDemoLabel { get; set; }

    public bool IsDemoMode =>
        string.Equals(Mode, "Demo", StringComparison.OrdinalIgnoreCase);

    public bool IsProductionMode =>
        string.Equals(Mode, "Production", StringComparison.OrdinalIgnoreCase);

    public bool IsTseSimulation =>
        string.Equals(TseMode, "Simulation", StringComparison.OrdinalIgnoreCase);

    public bool IsFinanzOnlineSimulation =>
        string.Equals(FinanzOnlineMode, "Simulation", StringComparison.OrdinalIgnoreCase);
}
