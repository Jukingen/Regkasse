namespace KasseAPI_Final.DTOs;

/// <summary>GET /api/system/time/status — RKSV clock health for operators.</summary>
public sealed class SystemTimeStatusDto
{
    public DateTime SystemTimeUtc { get; set; }

    public DateTime? NtpTimeUtc { get; set; }

    public double? OffsetSeconds { get; set; }

    public bool IsSynchronized { get; set; }

    public DateTime? LastSyncAt { get; set; }

    /// <summary>ok | warning | critical</summary>
    public string WarningLevel { get; set; } = "ok";
}
