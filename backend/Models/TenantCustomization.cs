using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Per-tenant branding / feature flags for generated website or customer app.
/// One row per <see cref="CustomizationSurface"/> (website | app).
/// </summary>
[Table("tenant_customizations")]
public class TenantCustomization : ITenantEntity
{
    public const string TypeWebsite = "website";
    public const string TypeApp = "app";

    public static readonly string[] AllowedTypes = [TypeWebsite, TypeApp];

    public static readonly string[] DefaultPages = ["home", "menu", "about", "contact"];

    public static readonly string[] AllowedPages =
    [
        "home", "menu", "about", "contact", "gallery", "reservation"
    ];

    public static readonly string[] AllowedFeatures =
    [
        "online-order", "reservation", "loyalty", "live-menu"
    ];

    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    /// <summary><c>website</c> or <c>app</c>.</summary>
    [Required]
    [MaxLength(16)]
    [Column("surface")]
    public string Surface { get; set; } = TypeWebsite;

    [MaxLength(32)]
    [Column("primary_color")]
    public string? PrimaryColor { get; set; }

    [MaxLength(32)]
    [Column("secondary_color")]
    public string? SecondaryColor { get; set; }

    [MaxLength(32)]
    [Column("background_color")]
    public string? BackgroundColor { get; set; }

    [MaxLength(32)]
    [Column("text_color")]
    public string? TextColor { get; set; }

    [MaxLength(128)]
    [Column("font_family")]
    public string? FontFamily { get; set; }

    [MaxLength(2048)]
    [Column("logo_url")]
    public string? LogoUrl { get; set; }

    [MaxLength(2048)]
    [Column("favicon_url")]
    public string? FaviconUrl { get; set; }

    /// <summary>JSON array of page ids, e.g. <c>["home","menu","contact"]</c>.</summary>
    [Column("pages_json")]
    public string PagesJson { get; set; } = """["home","menu","about","contact"]""";

    /// <summary>JSON array of feature ids, e.g. <c>["live-menu","online-order"]</c>.</summary>
    [Column("features_json")]
    public string FeaturesJson { get; set; } = """["live-menu"]""";

    /// <summary>Optional extra CSS appended after generated styles (max length enforced in service).</summary>
    [Column("custom_css")]
    public string? CustomCss { get; set; }

    /// <summary>Optional extra JS appended after generated scripts (max length enforced in service).</summary>
    [Column("custom_js")]
    public string? CustomJs { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
