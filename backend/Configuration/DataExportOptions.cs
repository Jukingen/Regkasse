namespace KasseAPI_Final.Configuration;

/// <summary>GDPR / mandant data-export packaging and download-link settings.</summary>
public sealed class DataExportOptions
{
    public const string SectionName = "DataExport";

    /// <summary>Public API origin used in download links (no trailing slash). Default production API host.</summary>
    public string PublicApiBaseUrl { get; set; } = "https://api.regkasse.at";

    /// <summary>How long a download token remains valid after export is ready.</summary>
    public int DownloadLinkValidDays { get; set; } = 7;

    /// <summary>Relative path template; <c>{token}</c> is replaced. Matches production sketch route.</summary>
    public string DownloadPathTemplate { get; set; } = "/data/download/{token}";
}
