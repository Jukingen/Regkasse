using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Tenant-scoped admin activity feed row (in-app, email, webhook).</summary>
[Table("activity_events")]
public class ActivityEvent : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; }

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("type")]
    public ActivityEventType Type { get; set; }

    [Required]
    [MaxLength(20)]
    [Column("severity")]
    public string Severity { get; set; } = ActivitySeverityNames.Info;

    [Required]
    [MaxLength(200)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    [Column("description")]
    public string? Description { get; set; }

    [MaxLength(450)]
    [Column("actor_user_id")]
    public string? ActorUserId { get; set; }

    [MaxLength(200)]
    [Column("actor_name")]
    public string? ActorName { get; set; }

    [MaxLength(100)]
    [Column("entity_type")]
    public string? EntityType { get; set; }

    [MaxLength(100)]
    [Column("entity_id")]
    public string? EntityId { get; set; }

    [Column("metadata_json", TypeName = "jsonb")]
    public string? MetadataJson { get; set; }

    /// <summary>Internal deduplication for monitoring alerts (not exposed on API).</summary>
    [MaxLength(120)]
    [Column("dedup_key")]
    public string? DedupKey { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; }
}
