using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Self-service request for a temporary permission grant (Super Admin approval).</summary>
[Table("permission_requests")]
public class PermissionRequest
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid? TenantId { get; set; }

    [Required]
    [MaxLength(450)]
    [Column("requester_user_id")]
    public string RequesterUserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(128)]
    [Column("permission")]
    public string Permission { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    [Column("reason")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>1d | 7d | 30d | custom</summary>
    [Required]
    [MaxLength(16)]
    [Column("requested_duration")]
    public string RequestedDuration { get; set; } = "7d";

    [Column("requested_expires_at")]
    public DateTime? RequestedExpiresAt { get; set; }

    [Required]
    [MaxLength(16)]
    [Column("status")]
    public string Status { get; set; } = PermissionRequestStatuses.Pending;

    [Column("requested_at")]
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    [Column("resolved_by_user_id")]
    public string? ResolvedByUserId { get; set; }

    [Column("resolved_at")]
    public DateTime? ResolvedAt { get; set; }

    [MaxLength(500)]
    [Column("resolution_note")]
    public string? ResolutionNote { get; set; }

    [Column("resulting_override_id")]
    public Guid? ResultingOverrideId { get; set; }
}

public static class PermissionRequestStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        Pending, Approved, Rejected, Cancelled,
    };

    public static bool IsValid(string? status) =>
        !string.IsNullOrWhiteSpace(status) && All.Contains(status.Trim());
}

public static class PermissionRequestDurations
{
    public const string OneDay = "1d";
    public const string SevenDays = "7d";
    public const string ThirtyDays = "30d";
    public const string Custom = "custom";

    public static DateTime ResolveExpiresAt(string duration, DateTime utcNow, DateTime? customExpiresAt)
    {
        return (duration ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            OneDay => utcNow.AddDays(1),
            ThirtyDays => utcNow.AddDays(30),
            Custom when customExpiresAt.HasValue => customExpiresAt.Value,
            _ => utcNow.AddDays(7),
        };
    }
}
