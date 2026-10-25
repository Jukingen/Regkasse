namespace KasseAPI_Final.Configuration;

/// <summary>
/// Whether an accepted Backup/DR known gap may change the compliance verdict.
/// Default <see cref="AllowsGateOverride"/> is false: acceptance stays amber and the verdict stays fail.
/// </summary>
public sealed class BackupKnownGapsAcceptanceOptions
{
    public const string SectionName = "Backup:KnownGapsAcceptance";

    /// <summary>
    /// When false, accepting a gap does not turn a failing compliance verdict into pass.
    /// The card still shows the accepted gap as amber.
    /// </summary>
    public bool AllowsGateOverride { get; set; }
}
