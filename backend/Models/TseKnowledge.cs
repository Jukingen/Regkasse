using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace KasseAPI_Final.Models;

public static class TseKnowledgeCategories
{
    public const string GettingStarted = "GettingStarted";
    public const string Health = "Health";
    public const string Failover = "Failover";
    public const string Compliance = "Compliance";
    public const string Operations = "Operations";
    public const string Faq = "FAQ";
}

/// <summary>
/// Platform TSE knowledge-base article / FAQ entry (operational docs only; not fiscal content).
/// </summary>
[Table("tse_knowledge_articles")]
public sealed class TseKnowledgeArticle
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(128)]
    [Column("slug")]
    public string Slug { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    [Column("description")]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(8000)]
    [Column("body")]
    public string Body { get; set; } = string.Empty;

    [Required]
    [MaxLength(64)]
    [Column("category")]
    public string Category { get; set; } = TseKnowledgeCategories.Operations;

    [Column("is_faq")]
    public bool IsFaq { get; set; }

    [Column("view_count")]
    public int ViewCount { get; set; }

    [Column("rating_sum")]
    public int RatingSum { get; set; }

    [Column("rating_count")]
    public int RatingCount { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [Column("is_published")]
    public bool IsPublished { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    public ICollection<TseKnowledgeFeedback> Feedback { get; set; } = new List<TseKnowledgeFeedback>();

    [NotMapped]
    public double AverageRating =>
        RatingCount <= 0 ? 0 : Math.Round((double)RatingSum / RatingCount, 2);
}

[Table("tse_knowledge_feedback")]
public sealed class TseKnowledgeFeedback
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column("article_id")]
    public Guid ArticleId { get; set; }

    [Column("rating")]
    public int Rating { get; set; }

    [MaxLength(450)]
    [Column("actor_user_id")]
    public string? ActorUserId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(ArticleId))]
    public TseKnowledgeArticle? Article { get; set; }
}
