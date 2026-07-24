using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

/// <summary>Categories for Super Admin TSE smart recommendations.</summary>
public static class TseRecommendationCategories
{
    public const string Performance = "Performance";
    public const string Cost = "Cost";
    public const string Security = "Security";
    public const string Reliability = "Reliability";
}

/// <summary>Impact levels for TSE smart recommendations.</summary>
public static class TseRecommendationImpacts
{
    public const string High = "High";
    public const string Medium = "Medium";
    public const string Low = "Low";
}

/// <summary>
/// Persisted Super Admin TSE operational recommendation (diagnostic advisory — not fiscal).
/// Apply/dismiss/rate are workflow markers; applying does not mutate fiscal signing state.
/// </summary>
[Table("tse_recommendations")]
public sealed class TseRecommendation
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("tenant_id")]
    public Guid TenantId { get; set; }

    /// <summary>Stable machine code for upsert/dedup (e.g. reduce_idle_backups).</summary>
    [Required]
    [MaxLength(64)]
    [Column("code")]
    public string Code { get; set; } = string.Empty;

    [Required]
    [MaxLength(32)]
    [Column("category")]
    public string Category { get; set; } = TseRecommendationCategories.Reliability;

    [Required]
    [MaxLength(200)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(16)]
    [Column("impact")]
    public string Impact { get; set; } = TseRecommendationImpacts.Medium;

    /// <summary>Indicative monthly EUR savings (not an invoice).</summary>
    [Column("estimated_savings")]
    public int EstimatedSavings { get; set; }

    /// <summary>1–10 effort estimate for operators.</summary>
    [Column("effort_score")]
    public int EffortScore { get; set; } = 5;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("is_applied")]
    public bool IsApplied { get; set; }

    [Column("applied_at")]
    public DateTime? AppliedAt { get; set; }

    [MaxLength(450)]
    [Column("applied_by")]
    public string? AppliedBy { get; set; }

    [Column("is_dismissed")]
    public bool IsDismissed { get; set; }

    [Column("dismissed_at")]
    public DateTime? DismissedAt { get; set; }

    [MaxLength(450)]
    [Column("dismissed_by")]
    public string? DismissedBy { get; set; }

    /// <summary>1–5 star rating; 0 means unrated.</summary>
    [Column("rating")]
    public int Rating { get; set; }

    [Column("rated_at")]
    public DateTime? RatedAt { get; set; }

    [MaxLength(450)]
    [Column("rated_by")]
    public string? RatedBy { get; set; }

    [ForeignKey(nameof(TenantId))]
    public Tenant? Tenant { get; set; }
}
