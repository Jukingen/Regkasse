using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

public interface IFiskalyStatisticsService
{
    Task<FiskalyStatisticsDto> GetAsync(
        FiskalyStatisticsQuery query,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);

    Task<(byte[] Bytes, string ContentType, string FileName)> ExportAsync(
        FiskalyStatisticsQuery query,
        string format,
        bool actorIsSuperAdmin,
        CancellationToken cancellationToken = default);
}
