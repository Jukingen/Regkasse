using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace KasseAPI_Final.Models;

/// <summary>
/// Per-user, per-tenant admin dashboard widget layout (FA).
/// </summary>
[Table("dashboard_preferences")]
public class DashboardPreferences
{
    [Key]
    public Guid Id { get; set; }

    [Required]
    [StringLength(450)]
    [Column("user_id")]
    public string UserId { get; set; } = string.Empty;

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [Column("widgets", TypeName = "jsonb")]
    public List<DashboardWidget> Widgets { get; set; } = new();

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Single widget placement and visibility in the admin dashboard grid.</summary>
public class DashboardWidget
{
    public string WidgetId { get; set; } = string.Empty;

    public int Order { get; set; }

    public bool IsVisible { get; set; } = true;

    /// <summary>Widget-specific options (e.g. top-products period).</summary>
    public Dictionary<string, JsonElement>? Settings { get; set; }
}
