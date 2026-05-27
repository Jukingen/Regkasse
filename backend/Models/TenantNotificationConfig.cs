using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

[Table("tenant_notification_configs")]
public class TenantNotificationConfig
{
    [Key]
    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("config", TypeName = "jsonb")]
    public NotificationConfig Config { get; set; } = NotificationConfig.CreateDefault();

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; }
}
