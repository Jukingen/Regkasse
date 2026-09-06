using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

public interface IFiskalyErrorService
{
    Task<PagedResult<FiskalyErrorListItemDto>> ListAsync(
        FiskalyErrorQuery query,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);

    Task<FiskalyErrorStatsDto> GetStatsAsync(
        FiskalyErrorQuery query,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);

    Task<FiskalyErrorDetailDto?> GetByIdAsync(
        Guid id,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);

    Task<(byte[] Bytes, string ContentType, string FileName)> ExportAsync(
        FiskalyErrorQuery query,
        string format,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);

    Task<FiskalyErrorListItemDto> SetReviewStatusAsync(
        Guid id,
        string reviewStatus,
        string actorUserId,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);
}
