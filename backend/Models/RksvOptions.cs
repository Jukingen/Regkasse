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

    /// <summary>
    /// Simulation | Real — RKSV presentation and production-lock overlay
    /// (<c>IRksvEnvironmentService</c>, DEMO labels, TSE health-bypass gate).
    /// This is not hardware TSE policy and does not drive
    /// <c>PaymentService</c> signing. Signing follows
    /// <see cref="TseOptions.TseMode"/> (<c>Off</c> | <c>Demo</c> | <c>Device</c>)
    /// via <see cref="TseOptions.RequiresFiscalSignature"/>.
    /// Vocabulary is intentionally different from <see cref="TseOptions.TseMode"/>.
    /// </summary>
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
