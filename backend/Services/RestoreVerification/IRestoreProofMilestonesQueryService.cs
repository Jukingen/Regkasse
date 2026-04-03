using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.RestoreVerification;

/// <summary>
/// Yedek / artifact / restore drill için &quot;son deneme&quot; ve &quot;son bilinen iyi kanıt&quot; seçimi.
/// </summary>
public interface IRestoreProofMilestonesQueryService
{
    Task<RestoreProofMilestonesResponseDto> GetMilestonesAsync(CancellationToken cancellationToken = default);
}
