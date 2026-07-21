using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>
/// Structured FA operator feedback (ease of use, performance, feature request, bug).
/// Tenant-scoped; submitters see their own rows; Super Admin reviews all.
/// </summary>
[Table("admin_user_feedback")]
public class AdminUserFeedback : ITenantEntity
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    [ForeignKey(nameof(TenantId))]
    public virtual Tenant? Tenant { get; set; }

    /// <summary><see cref="AdminFeedbackCategories"/>.</summary>
    [Required]
    [MaxLength(32)]
    [Column("category")]
    public string Category { get; set; } = AdminFeedbackCategories.FeatureRequest;

    /// <summary><see cref="AdminFeedbackStatuses"/>.</summary>
    [Required]
    [MaxLength(32)]
    [Column("status")]
    public string Status { get; set; } = AdminFeedbackStatuses.UnderReview;

    [Required]
    [MaxLength(200)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(4000)]
    [Column("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>Optional 1–5 rating (primarily for EaseOfUse / Performance).</summary>
    [Column("rating")]
    public int? Rating { get; set; }

    [MaxLength(500)]
    [Column("page_path")]
    public string? PagePath { get; set; }

    [Required]
    [MaxLength(450)]
    [Column("submitted_by_user_id")]
    public string SubmittedByUserId { get; set; } = string.Empty;

    [MaxLength(200)]
    [Column("submitted_by_display_name")]
    public string? SubmittedByDisplayName { get; set; }

    [Column("created_at_utc")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    [Column("updated_at_utc")]
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [MaxLength(450)]
    [Column("reviewed_by_user_id")]
    public string? ReviewedByUserId { get; set; }

    [Column("reviewed_at_utc")]
    public DateTime? ReviewedAtUtc { get; set; }

    [MaxLength(1000)]
    [Column("reviewer_note")]
    public string? ReviewerNote { get; set; }
}

public static class AdminFeedbackCategories
{
    public const string EaseOfUse = "EaseOfUse";
    public const string Performance = "Performance";
    public const string FeatureRequest = "FeatureRequest";
    public const string Bug = "Bug";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        EaseOfUse,
        Performance,
        FeatureRequest,
        Bug,
    };

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim());
}

public static class AdminFeedbackStatuses
{
    public const string UnderReview = "UnderReview";
    public const string InProgress = "InProgress";
    public const string Implemented = "Implemented";
    public const string Declined = "Declined";
    public const string Duplicate = "Duplicate";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        UnderReview,
        InProgress,
        Implemented,
        Declined,
        Duplicate,
    };

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && All.Contains(value.Trim());
}
