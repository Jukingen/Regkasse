using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Super Admin TSE operational incident (not a fiscal/RKSV record).
/// </summary>
[Table("tse_incidents")]
public class TseIncident
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("device_id")]
    public Guid? DeviceId { get; set; }

    [Required]
    [MaxLength(200)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    [Column("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary><see cref="TseIncidentSeverities"/>.</summary>
    [Required]
    [MaxLength(16)]
    [Column("severity")]
    public string Severity { get; set; } = TseIncidentSeverities.Medium;

    /// <summary><see cref="TseIncidentStatuses"/>.</summary>
    [Required]
    [MaxLength(24)]
    [Column("status")]
    public string Status { get; set; } = TseIncidentStatuses.Open;

    [Column("detected_at")]
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    [MaxLength(4000)]
    [Column("resolution")]
    public string? Resolution { get; set; }

    [MaxLength(450)]
    [Column("created_by")]
    public string? CreatedBy { get; set; }

    [MaxLength(450)]
    [Column("updated_by")]
    public string? UpdatedBy { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    [ForeignKey(nameof(DeviceId))]
    public virtual TseDevice? Device { get; set; }

    public virtual ICollection<TseIncidentLog> Logs { get; set; } = new List<TseIncidentLog>();
    public virtual ICollection<TseIncidentAction> Actions { get; set; } = new List<TseIncidentAction>();
}

[Table("tse_incident_logs")]
public class TseIncidentLog
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("incident_id")]
    public Guid IncidentId { get; set; }

    [Required]
    [MaxLength(64)]
    [Column("event_type")]
    public string EventType { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    [Column("message")]
    public string Message { get; set; } = string.Empty;

    [MaxLength(450)]
    [Column("actor_user_id")]
    public string? ActorUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(IncidentId))]
    public virtual TseIncident? Incident { get; set; }
}

[Table("tse_incident_actions")]
public class TseIncidentAction
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("incident_id")]
    public Guid IncidentId { get; set; }

    [Required]
    [MaxLength(64)]
    [Column("action_type")]
    public string ActionType { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [MaxLength(450)]
    [Column("performed_by")]
    public string? PerformedBy { get; set; }

    [Column("performed_at")]
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;

    [Column("is_completed")]
    public bool IsCompleted { get; set; }

    [ForeignKey(nameof(IncidentId))]
    public virtual TseIncident? Incident { get; set; }
}

public static class TseIncidentSeverities
{
    public const string Critical = "Critical";
    public const string High = "High";
    public const string Medium = "Medium";
    public const string Low = "Low";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Critical, High, Medium, Low,
    };

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim());
}

public static class TseIncidentStatuses
{
    public const string Open = "Open";
    public const string Investigating = "Investigating";
    public const string Resolved = "Resolved";
    public const string Closed = "Closed";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Open, Investigating, Resolved, Closed,
    };

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim());
}

public static class TseIncidentLogEventTypes
{
    public const string Created = "Created";
    public const string StatusChanged = "StatusChanged";
    public const string Note = "Note";
    public const string ActionAdded = "ActionAdded";
    public const string ReportGenerated = "ReportGenerated";
}

public static class TseIncidentActionTypes
{
    public const string Investigate = "Investigate";
    public const string Failover = "Failover";
    public const string RenewCertificate = "RenewCertificate";
    public const string ContactVendor = "ContactVendor";
    public const string Other = "Other";
}
