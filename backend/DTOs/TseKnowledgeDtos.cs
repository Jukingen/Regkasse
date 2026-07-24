namespace KasseAPI_Final.DTOs;

public sealed class TseKnowledgeArticleDto
{
    public Guid Id { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsFaq { get; set; }
    public int ViewCount { get; set; }
    public double Rating { get; set; }
    public int RatingCount { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool DiagnosticOnly { get; set; } = true;
}

public sealed class TseKnowledgeArticleFeedbackDto
{
    public Guid ArticleId { get; set; }
    public Guid FeedbackId { get; set; }
    public int Rating { get; set; }
    public double ArticleAverageRating { get; set; }
    public int ArticleRatingCount { get; set; }
    public DateTime SubmittedAt { get; set; }
}

public sealed class SubmitTseKnowledgeFeedbackRequestDto
{
    public int Rating { get; set; }
}
