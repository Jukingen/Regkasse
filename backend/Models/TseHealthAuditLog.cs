using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Persisted audit row when <see cref="TseOperationalHealth"/> transitions (TSE probe policy).
/// </summary>
[Table("tse_health_audit_logs")]
public sealed class TseHealthAuditLog
{
    public Guid Id { get; set; }

    public DateTime TimestampUtc { get; set; }

    public TseOperationalHealth OldStatus { get; set; }

    public TseOperationalHealth NewStatus { get; set; }

    public int ConsecutiveFailures { get; set; }

    [MaxLength(2000)]
    public string? ReasonSafe { get; set; }
}
