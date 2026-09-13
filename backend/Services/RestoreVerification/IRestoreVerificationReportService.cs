using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.RestoreVerification;

public interface IRestoreVerificationReportService
{
    Task<RestoreVerificationReportDto?> GetReportAsync(
        Guid runId,
        RestoreVerificationAccessScope access,
        CancellationToken cancellationToken = default);

    byte[] ToCsv(RestoreVerificationReportDto report);

    byte[] ToPdf(RestoreVerificationReportDto report);
}
