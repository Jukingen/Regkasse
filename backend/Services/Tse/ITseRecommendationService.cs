using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Rule-based Super Admin TSE smart recommendations (diagnostic advisory workflow).
/// </summary>
public interface ITseRecommendationService
{
    Task<IReadOnlyList<TseRecommendationDto>> GetRecommendationsAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<TseRecommendationResultDto> ApplyRecommendationAsync(
        Guid recommendationId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<TseRecommendationResultDto> DismissRecommendationAsync(
        Guid recommendationId,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task<TseRecommendationFeedbackDto> RateRecommendationAsync(
        Guid recommendationId,
        int rating,
        string? actorUserId = null,
        CancellationToken cancellationToken = default);
}
