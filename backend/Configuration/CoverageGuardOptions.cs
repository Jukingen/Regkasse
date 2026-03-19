namespace KasseAPI_Final.Configuration;

/// <summary>
/// Options for device/sequence coverage guard: threshold-based alerting and risk scoring.
/// </summary>
public sealed class CoverageGuardOptions
{
    public const string SectionName = "CoverageGuard";

    /// <summary>Alert when coverage (min of DeviceId and Sequence) is below this percent (0..100). Default 80.</summary>
    public double LowCoverageThresholdPercent { get; set; } = 80.0;

    /// <summary>Minimum number of samples in the window before emitting a low-coverage alert. Default 10.</summary>
    public int MinSamplesForAlert { get; set; } = 10;

    /// <summary>When true, low-coverage condition is also written to audit log. Default true.</summary>
    public bool WriteAlertToAuditLog { get; set; } = true;
}
