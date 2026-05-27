using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Tenant-scoped cron schedule for emailing filtered audit log exports.</summary>
[Table("audit_report_schedules")]
public class AuditReportSchedule
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [MaxLength(450)]
    [Column("created_by_user_id")]
    public string CreatedByUserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [Column("name")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Column("filters_json", TypeName = "jsonb")]
    public string FiltersJson { get; set; } = "{}";

    [Required]
    [MaxLength(100)]
    [Column("schedule_cron")]
    public string ScheduleCron { get; set; } = string.Empty;

    [Required]
    [Column("recipients_json", TypeName = "jsonb")]
    public string RecipientsJson { get; set; } = "[]";

    [Required]
    [MaxLength(20)]
    [Column("format")]
    public string Format { get; set; } = "csv";

    [Required]
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Required]
    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("last_run_utc")]
    public DateTime? LastRunUtc { get; set; }

    [Column("next_run_utc")]
    public DateTime? NextRunUtc { get; set; }
}
