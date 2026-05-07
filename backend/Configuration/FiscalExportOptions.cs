namespace KasseAPI_Final.Configuration;

/// <summary>Fiscal / DEP-style export behavior (admin API).</summary>
public sealed class FiscalExportOptions
{
    public const string SectionName = "FiscalExport";

    /// <summary>When true, export actions require <c>X-Disclaimer-Acknowledged: true</c> (recommended for production).</summary>
    public bool RequireDisclaimerAcknowledgment { get; set; } = true;

    /// <summary>Log warning when acknowledgment is missing or invalid (potential bypass attempts).</summary>
    public bool LogFailedAttempts { get; set; } = true;
}
