using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

public static class DepExportScheduleTypes
{
    public const string Daily = "Daily";
    public const string Weekly = "Weekly";
    public const string Monthly = "Monthly";
    public const string Yearly = "Yearly";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("scheduleType is required.", nameof(value));

        return value.Trim() switch
        {
            "Daily" or "daily" => Daily,
            "Weekly" or "weekly" => Weekly,
            "Monthly" or "monthly" => Monthly,
            "Yearly" or "yearly" => Yearly,
            _ => throw new ArgumentException($"Unsupported scheduleType '{value}'. Allowed: Daily, Weekly, Monthly, Yearly.", nameof(value)),
        };
    }
}

/// <summary>Tenant-scoped schedule for automated RKSV DEP §7 exports.</summary>
[Table("dep_export_schedules")]
public class DepExportSchedule
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Required]
    [Column("cash_register_id")]
    public Guid CashRegisterId { get; set; }

    [Required]
    [MaxLength(16)]
    [Column("schedule_type")]
    public string ScheduleType { get; set; } = DepExportScheduleTypes.Monthly;

    [Required]
    [Column("day_of_month")]
    public int DayOfMonth { get; set; } = 1;

    [Required]
    [MaxLength(5)]
    [Column("time_of_day")]
    public string TimeOfDay { get; set; } = "00:00";

    [Required]
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    [Column("recipient_emails", TypeName = "text")]
    public string? RecipientEmails { get; set; }

    [Column("last_run_at")]
    public DateTime LastRunAt { get; set; }

    [Column("next_run_at")]
    public DateTime? NextRunAt { get; set; }

    [Required]
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
