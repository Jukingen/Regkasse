namespace KasseAPI_Final.DTOs;

/// <summary>
/// Response body for <c>GET /api/app/version</c>. Pure metadata — no PII, no auth-protected fields.
/// Mirrors <see cref="KasseAPI_Final.Configuration.AppUpdateOptions"/>.
/// </summary>
public sealed class AppVersionResponseDto
{
    /// <summary>Latest published POS APK version code (monotonic integer).</summary>
    public int LatestVersionCode { get; set; }

    /// <summary>Display version (e.g. "1.0.1"); for UI only, never used for comparison.</summary>
    public string LatestVersionName { get; set; } = string.Empty;

    /// <summary>HTTPS URL to the signed APK file. Null when no published artifact is configured.</summary>
    public string? DownloadUrl { get; set; }

    /// <summary>Optional release-notes URL.</summary>
    public string? ReleaseNotesUrl { get; set; }

    /// <summary>Whether clients should treat this update as required.</summary>
    public bool Mandatory { get; set; }

    /// <summary>Hard-block floor: clients below this versionCode must refuse to operate.</summary>
    public int MinimumSupportedVersionCode { get; set; }

    /// <summary>SHA-256 of the APK (lowercase hex). Optional integrity check for clients.</summary>
    public string? Sha256 { get; set; }

    /// <summary>APK size in bytes for progress UX. Optional.</summary>
    public long? SizeBytes { get; set; }

    /// <summary>UTC instant the server emitted this snapshot (helps detect cache problems).</summary>
    public DateTime ServerTimeUtc { get; set; }
}
