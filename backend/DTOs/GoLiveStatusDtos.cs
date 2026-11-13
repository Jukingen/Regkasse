namespace KasseAPI_Final.DTOs;

/// <summary>Aggregated production-readiness verdict (GO / NO-GO).</summary>
public sealed class GoLiveStatusDto
{
    public const string StatusGo = "GO";
    public const string StatusNoGo = "NO-GO";

    /// <summary><see cref="StatusGo"/> or <see cref="StatusNoGo"/>.</summary>
    public string Status { get; set; } = StatusNoGo;

    public List<GoLiveCheckDto> Checks { get; set; } = new();

    public DateTime CheckedAtUtc { get; set; }

    public string Summary { get; set; } = string.Empty;
}

/// <summary>One automated go-live gate (Fiskaly, config, FON, backup, monitoring, sign-off).</summary>
public sealed class GoLiveCheckDto
{
    public const string CategoryFiskaly = "Fiskaly";
    public const string CategoryConfig = "Config";
    public const string CategoryFon = "FON";
    public const string CategoryBackup = "Backup";
    public const string CategoryMonitoring = "Monitoring";
    public const string CategorySignOff = "Sign-off";

    public const string NameFiskaly = "Fiskaly";
    public const string NameConfiguration = "Configuration";
    public const string NameFon = "FON";
    public const string NameBackup = "Backup";
    public const string NameMonitoring = "Monitoring";
    public const string NameSignOff = "Sign-off";

    public string Name { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public bool Passed { get; set; }

    public string Details { get; set; } = string.Empty;

    /// <summary>What to do when <see cref="Passed"/> is false. Empty when the check passed.</summary>
    public string Remediation { get; set; } = string.Empty;
}
