using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

public interface IElmahErrorQueryService
{
    Task<ElmahErrorListResponseDto> ListAsync(
        string applicationName,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<int> ClearAsync(string applicationName, CancellationToken cancellationToken = default);
}
