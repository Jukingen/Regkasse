using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Scheduled Super Admin billing reminder for a Mandanten license sale.</summary>
[Table("license_reminders")]
public class LicenseReminder
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant Tenant { get; set; } = null!;

    [Column("license_sale_id")]
    public Guid LicenseSaleId { get; set; }

    [ForeignKey(nameof(LicenseSaleId))]
    public virtual LicenseSale LicenseSale { get; set; } = null!;

    [Column("reminder_date_utc")]
    public DateTime ReminderDateUtc { get; set; }

    [Column("reminder_sent_at_utc")]
    public DateTime? ReminderSentAtUtc { get; set; }

    [Column("reminder_type")]
    [MaxLength(20)]
    public string ReminderType { get; set; } = LicenseReminderTypes.Expiry;

    [Column("status")]
    [MaxLength(20)]
    public string Status { get; set; } = LicenseReminderStatuses.Pending;
}
