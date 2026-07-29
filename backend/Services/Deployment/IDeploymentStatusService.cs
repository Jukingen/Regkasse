using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Deployment;

public interface IDeploymentStatusService
{
    Task<DeploymentRunDto> ReportAsync(DeploymentCiReportRequest request, CancellationToken cancellationToken = default);

    Task<DeploymentRunListResponseDto> ListAsync(
        string? stage = null,
        int take = 50,
        CancellationToken cancellationToken = default);
}
