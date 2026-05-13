namespace KasseAPI_Final.Configuration;

/// <summary>
/// RKSV: periodic NTP drift checks against FinanzOnline DEP time expectations.
/// </summary>
public sealed class NtpSettings
{
    public const string SectionName = "NtpSettings";

    public bool Enabled { get; set; } = true;

    /// <summary>
    /// When <see cref="Microsoft.Extensions.Hosting.IHostEnvironment.EnvironmentName"/> is Development,
    /// skips fiscal NTP blocking and returns a healthy time status DTO (never enable in production configs).
    /// </summary>
    public bool DevelopmentBypass { get; set; }

    /// <summary>Background sync cadence (default 60 minutes).</summary>
    public int SyncIntervalMinutes { get; set; } = 60;

    /// <summary>Fiscal payments require absolute offset within this bound (seconds).</summary>
    public int MaxAllowedOffsetSeconds { get; set; } = 5;

    /// <summary>Offsets beyond this emit critical telemetry and always fail fiscal guard when NTP is enabled.</summary>
    public int CriticalOffsetSeconds { get; set; } = 60;

    public string[] NtpServers { get; set; } =
    [
        "pool.ntp.org",
        "at.pool.ntp.org",
        "time.google.com"
    ];
}
