using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.License;

public interface ILicenseAuditQueryService
{
    Task<LicenseAuditLogListResponse> ListAsync(
        LicenseAuditLogQuery query,
        CancellationToken cancellationToken = default);

    Task<byte[]> ExportCsvAsync(
        LicenseAuditLogQuery query,
        CancellationToken cancellationToken = default);
}
