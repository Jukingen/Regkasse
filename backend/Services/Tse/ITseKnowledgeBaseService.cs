using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Platform TSE knowledge base / FAQ (operational documentation only; not fiscal advice).
/// </summary>
public interface ITseKnowledgeBaseService
{
    Task<IReadOnlyList<TseKnowledgeArticleDto>> SearchArticlesAsync(
        string? query,
        CancellationToken cancellationToken = default);

    Task<TseKnowledgeArticleDto> GetArticleAsync(
        Guid articleId,
        CancellationToken cancellationToken = default);

    Task<TseKnowledgeArticleFeedbackDto> SubmitFeedbackAsync(
        Guid articleId,
        int rating,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TseKnowledgeArticleDto>> GetPopularArticlesAsync(
        int limit = 10,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TseKnowledgeArticleDto>> GetFaqArticlesAsync(
        CancellationToken cancellationToken = default);
}
