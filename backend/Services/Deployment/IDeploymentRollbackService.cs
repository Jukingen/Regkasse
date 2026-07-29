using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Deployment;

public interface IDeploymentRollbackService
{
    Task<DeploymentRollbackResultDto> RollbackAsync(
        DeploymentRollbackRequest request,
        string actor,
        CancellationToken cancellationToken = default);
}
