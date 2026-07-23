namespace KasseAPI_Final.Configuration;

/// <summary>Export-as-email delivery: attachment size cap, link TTL, public download base URL.</summary>
public sealed class ExportEmailOptions
{
    public const string SectionName = "ExportEmail";

    /// <summary>Files larger than this are emailed as a download link (default 10 MiB).</summary>
    public long MaxAttachmentBytes { get; set; } = 10L * 1024 * 1024;

    /// <summary>Opaque download link lifetime (default 24 hours).</summary>
    public int DownloadLinkTtlHours { get; set; } = 24;

    /// <summary>Public API base for emailed links (default production API).</summary>
    public string PublicApiBaseUrl { get; set; } = "https://api.regkasse.at";

    /// <summary>Path template; <c>{token}</c> is replaced with the raw token.</summary>
    public string DownloadPathTemplate { get; set; } = "/data/export-email/{token}";

    /// <summary>Relative directory under content root for staged artifacts.</summary>
    public string StorageRelativePath { get; set; } = "App_Data/export-email";

    /// <summary>How long to keep delivery history rows (default 90 days).</summary>
    public int HistoryRetentionDays { get; set; } = 90;
}
