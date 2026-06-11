using System.Security.Claims;
using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services;

/// <summary>Aggregates POS status probes into a single round-trip.</summary>
public interface IPosStatusService
{
    Task<PosStatusOverviewDto> GetOverviewAsync(
        string userId,
        ClaimsPrincipal principal,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
