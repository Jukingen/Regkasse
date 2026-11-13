using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.ActivityReports;

public interface IActivityReportService
{
    /// <summary>Build a 7-day operation-log activity report for a tenant (Super Admin).</summary>
    Task<ActivityReportDto?> GenerateWeeklyReportAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
