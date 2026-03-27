using KasseAPI_Final.Models.Reports;

namespace KasseAPI_Final.Services;

public interface IJahresberichtService
{
    Task<JahresberichtDto> GenerateOrRefreshProvisionalAsync(
        JahresberichtGenerationRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<JahresberichtDto> FinalizeAsync(JahresberichtFinalizeRequest request, string actorUserId, CancellationToken cancellationToken = default);

    Task<JahresberichtDto> CreateCorrectionAsync(JahresberichtCorrectionRequest request, string actorUserId, CancellationToken cancellationToken = default);

    Task<JahresberichtDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JahresberichtListItemDto>> ListAsync(
        DateTime? fromYear,
        DateTime? toYear,
        string? scopeKind,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default);

    Task<JahresberichtDto> SubmitToFinanzOnlineAsync(Guid reportId, string actorUserId, CancellationToken cancellationToken = default);
}
