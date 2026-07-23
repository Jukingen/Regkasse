using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

public interface IPermissionAnalyticsService
{
    Task<PermissionAnalyticsSummaryDto> GetSummaryAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionAnalyticsTrendPointDto>> GetTrendAsync(
        int days = 30,
        CancellationToken cancellationToken = default);

    Task SnapshotTodayAsync(CancellationToken cancellationToken = default);

    Task<(byte[] Content, string ContentType, string FileName)> ExportAsync(
        string format,
        CancellationToken cancellationToken = default);
}
