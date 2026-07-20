using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Per-tenant digital service row (website / app): enablement gate + provision lifecycle.
/// Distinct from <see cref="Subscription"/> (billing). Sketch name <c>TenantDigitalService</c>
/// maps to this entity (<c>tenant_service_statuses</c>).
/// </summary>
[Table("tenant_service_statuses")]
public class TenantServiceStatus : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    /// <summary><see cref="TenantServiceTypes"/> — website or app.</summary>
    [Required]
    [MaxLength(16)]
    [Column("service_type")]
    public string ServiceType { get; set; } = string.Empty;

    /// <summary>
    /// Mandanten-Admin preference: when false, tenant opts out of using the service.
    /// Defaults to <c>true</c>.
    /// </summary>
    [Column("is_enabled")]
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// Super Admin platform gate: activate / deactivate for the tenant.
    /// Defaults to <c>true</c> until Super Admin deactivates.
    /// </summary>
    [Column("is_active")]
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Provision lifecycle: <see cref="TenantDigitalServiceStatuses"/> —
    /// <c>none</c>, <c>pending</c>, <c>created</c>, <c>published</c>, <c>rejected</c>.
    /// </summary>
    [Required]
    [MaxLength(16)]
    [Column("status")]
    public string Status { get; set; } = TenantDigitalServiceStatuses.None;

    /// <summary>Published or generated public URL (relative or absolute).</summary>
    [MaxLength(2048)]
    [Column("url")]
    public string? Url { get; set; }

    /// <summary>Last template used for website generation (e.g. modern).</summary>
    [MaxLength(64)]
    [Column("template_id")]
    public string? TemplateId { get; set; }

    /// <summary>Optional customization snapshot JSON (colors, pages, etc.).</summary>
    [Column("customization")]
    public string? Customization { get; set; }

    /// <summary>When the Mandanten last requested creation (pending / rejected).</summary>
    [Column("requested_at")]
    public DateTime? RequestedAt { get; set; }

    /// <summary>When Super Admin last generated the artifact.</summary>
    [Column("artifact_created_at")]
    public DateTime? ArtifactCreatedAt { get; set; }

    /// <summary>When the service was last published live.</summary>
    [Column("published_at")]
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// Optional Super Admin monthly price override (EUR). Null = use catalog list price.
    /// </summary>
    [Column("custom_price", TypeName = "decimal(10,2)")]
    public decimal? CustomPrice { get; set; }

    [Column("activated_at")]
    public DateTime? ActivatedAt { get; set; }

    [Column("deactivated_at")]
    public DateTime? DeactivatedAt { get; set; }

    /// <summary>AspNetUsers id of the Super Admin who deactivated (string Identity key).</summary>
    [MaxLength(450)]
    [Column("deactivated_by_user_id")]
    public string? DeactivatedByUserId { get; set; }

    [MaxLength(500)]
    [Column("deactivation_reason")]
    public string? DeactivationReason { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    /// <summary>True when both Mandanten preference and Super Admin gate allow the service.</summary>
    [NotMapped]
    public bool IsAvailable => IsEnabled && IsActive;

    /// <summary>True when a creation request is awaiting Super Admin action.</summary>
    [NotMapped]
    public bool HasRequest =>
        string.Equals(Status, TenantDigitalServiceStatuses.Pending, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Provision lifecycle values for <see cref="TenantServiceStatus.Status"/>.</summary>
public static class TenantDigitalServiceStatuses
{
    public const string None = "none";
    public const string Pending = "pending";
    public const string Created = "created";
    public const string Published = "published";
    public const string Rejected = "rejected";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        None,
        Pending,
        Created,
        Published,
        Rejected,
    };

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status) && All.Contains(status.Trim());
}

/// <summary>Allowed <see cref="TenantServiceStatus.ServiceType"/> values (aligned with <see cref="ServicePricingTypes"/>).</summary>
public static class TenantServiceTypes
{
    public const string Website = ServicePricingTypes.Website;
    public const string App = ServicePricingTypes.App;

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Website,
        App,
    };

    public static bool IsValid(string? serviceType) =>
        !string.IsNullOrWhiteSpace(serviceType) && All.Contains(serviceType.Trim());
}
