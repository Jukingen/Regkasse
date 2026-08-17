using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Deployment;

public interface IGoLiveCheckService
{
    /// <summary>Runs all production-readiness gates and stores the latest result.</summary>
    Task<GoLiveStatusDto> CheckAllConditionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Returns the last stored result, or runs a fresh check when none exists.</summary>
    Task<GoLiveStatusDto> GetLatestStatusAsync(CancellationToken cancellationToken = default);
}
