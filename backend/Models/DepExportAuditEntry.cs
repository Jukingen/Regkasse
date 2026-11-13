using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Lifecycle actions recorded in the DEP export audit trail.</summary>
public static class DepExportAuditActions
{
    public const string Created = "Created";
    public const string Downloaded = "Downloaded";
    public const string Archived = "Archived";
    public const string Deleted = "Deleted";
    public const string Validated = "Validated";
    public const string Failed = "Failed";
}

/// <summary>
/// Append-only DEP export lifecycle audit row (distinct from operational <see cref="DepExportHistory"/>).
/// Also mirrored into <see cref="AuditLog"/> for fiscal retention.
/// </summary>
[Table("dep_export_audit_entries")]
public class DepExportAuditEntry : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(32)]
    [Column("action")]
    public string Action { get; set; } = string.Empty;

    [Required]
    [MaxLength(260)]
    [Column("export_name")]
    public string ExportName { get; set; } = string.Empty;

    [Column("export_history_id")]
    public Guid? ExportHistoryId { get; set; }

    [MaxLength(256)]
    [Column("user_email")]
    public string? UserEmail { get; set; }

    [MaxLength(450)]
    [Column("user_id")]
    public string? UserId { get; set; }

    [MaxLength(50)]
    [Column("user_role")]
    public string? UserRole { get; set; }

    [Required]
    [Column("action_at")]
    public DateTime ActionAt { get; set; } = DateTime.UtcNow;

    [MaxLength(45)]
    [Column("ip_address")]
    public string? IpAddress { get; set; }

    [MaxLength(500)]
    [Column("user_agent")]
    public string? UserAgent { get; set; }

    [Column("details", TypeName = "text")]
    public string? Details { get; set; }
}
