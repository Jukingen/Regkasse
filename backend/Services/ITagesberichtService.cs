using KasseAPI_Final.Models.Reports;

namespace KasseAPI_Final.Services;

public interface ITagesberichtService
{
    Task<TagesberichtDto> GenerateOrRefreshProvisionalAsync(
        TagesberichtGenerationRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<TagesberichtDto> FinalizeAsync(TagesberichtFinalizeRequest request, string actorUserId, CancellationToken cancellationToken = default);

    Task<TagesberichtDto> CreateCorrectionAsync(TagesberichtCorrectionRequest request, string actorUserId, CancellationToken cancellationToken = default);

    Task<TagesberichtDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TagesberichtListItemDto>> ListAsync(
        DateTime? fromDate,
        DateTime? toDate,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default);

    Task<TagesberichtDto> SubmitToFinanzOnlineAsync(Guid reportId, string actorUserId, CancellationToken cancellationToken = default);
}
