using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Audit trail for periodic NTP vs system clock comparisons (RKSV observability).
/// </summary>
[Table("system_time_sync_logs")]
public sealed class SystemTimeSyncLog
{
    public Guid Id { get; set; }

    public DateTime SyncTimeUtc { get; set; }

    public DateTime SystemTimeUtc { get; set; }

    public DateTime NtpTimeUtc { get; set; }

    /// <summary>Signed seconds: NTP consensus minus local clock (positive = local slow).</summary>
    [Column(TypeName = "double precision")]
    public double OffsetSeconds { get; set; }

    [MaxLength(512)]
    public string NtpServerUsed { get; set; } = string.Empty;

    public bool IsSuccess { get; set; }

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }
}
