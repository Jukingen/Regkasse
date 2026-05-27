using KasseAPI_Final.Models.DTOs;

namespace KasseAPI_Final.Services;

public interface IUserActivityReportService
{
    Task<UserActivityReportDto?> BuildReportAsync(
        UserActivityReportQuery query,
        bool actorIsSuperAdmin,
        Guid? ambientTenantId,
        CancellationToken cancellationToken = default);

    Task<(byte[] Content, string ContentType, string FileName)> ExportAsync(
        UserActivityReportQuery query,
        string format,
        bool actorIsSuperAdmin,
        Guid? ambientTenantId,
        CancellationToken cancellationToken = default);
}
