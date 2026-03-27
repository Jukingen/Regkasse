using KasseAPI_Final.Models.Reports;

namespace KasseAPI_Final.Services;

public interface IReportSubmissionCompatibilityService
{
    Task<ReportSubmissionEnvelopeDto> BuildEnvelopeAsync(
        BuildReportSubmissionEnvelopeRequest request,
        CancellationToken cancellationToken = default);

    TagesberichtSubmissionStateDto ToLegacySubmissionState(ReportSubmissionEnvelopeDto envelope);
}
