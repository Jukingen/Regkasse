using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Append-only status transition for an <see cref="OnlineOrder"/>.</summary>
[Table("online_order_status_changes")]
public class OnlineOrderStatusChange : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("online_order_id")]
    public Guid OnlineOrderId { get; set; }

    [ForeignKey(nameof(OnlineOrderId))]
    public virtual OnlineOrder? OnlineOrder { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("from_status")]
    public string FromStatus { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    [Column("to_status")]
    public string ToStatus { get; set; } = string.Empty;

    [Column("changed_at")]
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    [Column("actor_user_id")]
    public string? ActorUserId { get; set; }

    [MaxLength(200)]
    [Column("reason")]
    public string? Reason { get; set; }
}
