namespace KasseAPI_Final.Configuration;

/// <summary>Rate limits, size caps, link TTL, and sensitive-export gates for admin downloads.</summary>
public sealed class DownloadSecurityOptions
{
    public const string SectionName = "DownloadSecurity";

    /// <summary>Max recorded downloads per user per UTC day (default 50).</summary>
    public int MaxDownloadsPerUserPerDay { get; set; } = 50;

    /// <summary>Max allowed download payload size in bytes (default 2 GiB).</summary>
    public long MaxFileSizeBytes { get; set; } = 2L * 1024 * 1024 * 1024;

    /// <summary>Time-limited download ticket lifetime (default 24 hours).</summary>
    public int DownloadLinkTtlHours { get; set; } = 24;

    /// <summary>Require Super Admin approval for GDPR / system backup / audit exports.</summary>
    public bool RequireApprovalForSensitiveExports { get; set; } = true;

    /// <summary>Require step-up TOTP for system backup and audit log export downloads.</summary>
    public bool RequireTwoFactorForCriticalExports { get; set; } = true;

    /// <summary>When true, Super Admin may self-approve without a second actor.</summary>
    public bool SuperAdminMaySelfApprove { get; set; } = true;
}
