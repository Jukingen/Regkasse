namespace KasseAPI_Final.Configuration;

/// <summary>
/// Configuration-driven metadata about the latest published POS Android APK.
/// Consumed by <c>GET /api/app/version</c> and surfaced to the in-app update checker.
/// Section name: <see cref="SectionName"/>.
/// </summary>
/// <remarks>
/// Ops mutate this section in <c>appsettings.json</c> (or env-var overrides) when a new APK
/// is published. The endpoint is anonymous and config-driven only — no DB access — so a
/// misconfigured section never blocks the POS from booting.
/// </remarks>
public sealed class AppUpdateOptions
{
    public const string SectionName = "AppUpdate";

    /// <summary>
    /// Latest published POS APK version code (monotonic integer).
    /// Bump by 1 every release; the client compares this to its embedded <c>APP_VERSION_CODE</c>.
    /// </summary>
    public int LatestVersionCode { get; set; }

    /// <summary>Display version, e.g. <c>"1.0.1"</c>. Mirrors <c>app.json</c> &gt; <c>expo.version</c>.</summary>
    public string LatestVersionName { get; set; } = string.Empty;

    /// <summary>
    /// Direct download URL to the signed APK. HTTPS is strongly recommended; the client
    /// streams the response into its private cache directory and never stores credentials in the URL.
    /// </summary>
    public string? DownloadUrl { get; set; }

    /// <summary>Optional HTML/Markdown release notes URL surfaced as a "Was ist neu?" link.</summary>
    public string? ReleaseNotesUrl { get; set; }

    /// <summary>
    /// When <c>true</c>, the client SHOULD treat the update as required. The POS UI may still
    /// allow a single snooze cycle so a cashier is not locked out mid-shift.
    /// </summary>
    public bool Mandatory { get; set; }

    /// <summary>
    /// Hard-block threshold: clients with <c>versionCode &lt; MinimumSupportedVersionCode</c> MUST
    /// refuse to operate. Independent of <see cref="LatestVersionCode"/>: ops can ship optional
    /// updates while still supporting older devices, then bump this floor when an old version is retired.
    /// </summary>
    public int MinimumSupportedVersionCode { get; set; }

    /// <summary>SHA-256 (lowercase hex) of the APK; the client may verify after download. Optional.</summary>
    public string? Sha256 { get; set; }

    /// <summary>APK size in bytes for client-side progress UX. Optional.</summary>
    public long? SizeBytes { get; set; }
}
