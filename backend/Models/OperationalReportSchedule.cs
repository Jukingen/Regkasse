using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

[Table("operational_report_schedules")]
public class OperationalReportSchedule
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
    [MaxLength(80)]
    [Column("report_type")]
    public string ReportType { get; set; } = string.Empty;

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
    public string Format { get; set; } = "pdf";

    [Required]
    [Column("filters_json", TypeName = "jsonb")]
    public string FiltersJson { get; set; } = "{}";

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
