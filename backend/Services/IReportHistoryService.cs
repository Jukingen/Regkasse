using KasseAPI_Final.Models.Reports;

namespace KasseAPI_Final.Services;

public interface IReportHistoryService
{
    Task<ReportHistoryTimelineDto?> GetHistoryAsync(
        string reportType,
        Guid reportId,
        CancellationToken cancellationToken = default);
}
