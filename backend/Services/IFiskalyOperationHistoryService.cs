using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

public sealed class FiskalyOperationHistoryQuery
{
    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }

    public string? OperationType { get; init; }

    public string? Status { get; init; }

    public string? Search { get; init; }

    public Guid? TenantId { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 25;
}

public interface IFiskalyOperationHistoryService
{
    Task<PagedResult<FiskalyOperationHistoryListItemDto>> ListAsync(
        FiskalyOperationHistoryQuery query,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);

    Task<FiskalyOperationHistoryDetailDto?> GetByIdAsync(
        Guid id,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);

    Task<FiskalyOperationHistoryRetryResultDto> RetryAsync(
        Guid id,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);
}
