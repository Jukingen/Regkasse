using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Manager request for Super Admin to create a website or app for the tenant.
/// Approve does not auto-generate — Super Admin still runs create/publish separately.
/// </summary>
[Table("digital_service_requests")]
public class DigitalServiceRequest : ITenantEntity
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

    /// <summary><see cref="DigitalServiceRequestStatuses"/>.</summary>
    [Required]
    [MaxLength(16)]
    [Column("status")]
    public string Status { get; set; } = DigitalServiceRequestStatuses.Pending;

    [MaxLength(450)]
    [Column("requested_by_user_id")]
    public string? RequestedByUserId { get; set; }

    [Column("requested_at")]
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)]
    [Column("note")]
    public string? Note { get; set; }

    [MaxLength(450)]
    [Column("resolved_by_user_id")]
    public string? ResolvedByUserId { get; set; }

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    [MaxLength(500)]
    [Column("resolution_note")]
    public string? ResolutionNote { get; set; }
}

public static class DigitalServiceRequestStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Pending,
        Approved,
        Rejected,
        Cancelled,
    };

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status) && All.Contains(status.Trim());
}
