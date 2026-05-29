namespace KasseAPI_Final.Configuration;

/// <summary>
/// Feature flags for POS cash-register readiness and controlled auto-open (single-register first).
/// </summary>
public sealed class PosCashRegisterFeatureOptions
{
    public const string SectionName = "PosCashRegisterFeatures";

    /// <summary>
    /// When true, POST ensure-ready may auto-open a closed register (per other flags), persist assignment after open,
    /// and run <c>ApplySoleOpenRegisterAutoAssignmentIfNeededAsync</c> (settings write when POS operational cardinality is one and that register is already Open).
    /// When false, endpoint returns read-only resolution (no settings mutation, no open).
    /// </summary>
    public bool EffectiveDefaultOnPosEntry { get; set; } = true;

    /// <summary>
    /// Auto-open the sole cash register when it is Closed and policy allows (ShiftOpen required).
    /// Default false in production appsettings; enable per deployment.
    /// </summary>
    public bool AutoOpenSoleClosedRegister { get; set; }

    /// <summary>
    /// When multiple registers exist, auto-open only the user's assigned default if it is Closed and selectable.
    /// </summary>
    public bool AutoOpenAssignedClosedRegister { get; set; }

    /// <summary>
    /// Opening balance recorded on auto-open shift transaction (product default: 0).
    /// </summary>
    public decimal DefaultAutoOpenOpeningBalance { get; set; }

    /// <summary>Safe fallback when no ambient tenant context is available (no DB read or create).</summary>
    public static PosCashRegisterFeatureOptions Default { get; } = new();
}
