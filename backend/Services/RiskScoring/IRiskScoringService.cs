using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.RiskScoring;

public interface IRiskScoringService
{
    /// <summary>Pure rule evaluation (no persistence).</summary>
    RiskScore CalculateRisk(UserAction action);

    /// <summary>
    /// Evaluate and optionally persist when score reaches Medium+ (or always when forced).
    /// </summary>
    Task<EvaluateUserActionResponseDto> EvaluateAsync(
        UserAction action,
        bool persistIfElevated = true,
        CancellationToken cancellationToken = default);

    Task<RiskScoreListResponseDto> ListAsync(
        bool unresolvedOnly,
        string? riskLevel,
        int limit,
        int offset,
        CancellationToken cancellationToken = default);

    Task<RiskScoreDto?> ResolveAsync(
        Guid id,
        string resolvedByUserId,
        string resolution,
        CancellationToken cancellationToken = default);
}
