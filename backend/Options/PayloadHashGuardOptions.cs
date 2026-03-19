namespace KasseAPI_Final.Configuration;

/// <summary>
/// Guards for legacy payload_hash mismatch: threshold-based warning and optional startup check.
/// </summary>
public sealed class PayloadHashGuardOptions
{
    public const string SectionName = "PayloadHashGuard";

    /// <summary>When mismatch ratio (RuntimeMismatchCount/Scanned * 100) exceeds this percent, LegacyDataQualityRiskHigh is set and ops are logged. Default 10.</summary>
    public double MismatchWarningThresholdPercent { get; set; } = 10.0;

    /// <summary>Sample size used when checking risk for fiscal export or risk endpoint. Default 500.</summary>
    public int SampleSizeForExportCheck { get; set; } = 500;

    /// <summary>When true, at startup a one-time check runs and logs a warning if mismatch ratio is high (ops mode). Default false.</summary>
    public bool RunStartupCheck { get; set; } = false;
}
