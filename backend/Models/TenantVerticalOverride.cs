using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace KasseAPI_Final.Models;

/// <summary>One tenant-scoped JSON overlay for its assigned <see cref="VerticalProfile"/>.</summary>
[Table("tenant_vertical_overrides")]
public sealed class TenantVerticalOverride : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [JsonIgnore]
    public Tenant? Tenant { get; set; }

    [Required]
    [Column("overrides_json", TypeName = "jsonb")]
    public string OverridesJson { get; set; } = "{}";

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
