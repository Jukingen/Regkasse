using KasseAPI_Final.Models.Reports;

namespace KasseAPI_Final.Services;

public interface IMonatsberichtService
{
    Task<MonatsberichtDto> GenerateOrRefreshProvisionalAsync(
        MonatsberichtGenerationRequest request,
        string actorUserId,
        CancellationToken cancellationToken = default);

    Task<MonatsberichtDto> FinalizeAsync(MonatsberichtFinalizeRequest request, string actorUserId, CancellationToken cancellationToken = default);

    Task<MonatsberichtDto> CreateCorrectionAsync(MonatsberichtCorrectionRequest request, string actorUserId, CancellationToken cancellationToken = default);

    Task<MonatsberichtDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MonatsberichtListItemDto>> ListAsync(
        DateTime? fromMonth,
        DateTime? toMonth,
        string? scopeKind,
        Guid? cashRegisterId,
        CancellationToken cancellationToken = default);

    Task<MonatsberichtDto> SubmitToFinanzOnlineAsync(Guid reportId, string actorUserId, CancellationToken cancellationToken = default);
}
