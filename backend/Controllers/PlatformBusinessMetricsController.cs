using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Metrics;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Deployment-wide business snapshot. JSON, not Prometheus text (<c>GET /metrics</c>).
/// </summary>
[ApiController]
[Route("api/metrics")]
[HasPermission(AppPermissions.SystemCritical)]
[Produces("application/json")]
public sealed class PlatformBusinessMetricsController : ControllerBase
{
    private readonly IPlatformBusinessMetricsService _metrics;

    public PlatformBusinessMetricsController(IPlatformBusinessMetricsService metrics)
    {
        _metrics = metrics;
    }

    [HttpGet]
    [ProducesResponseType(typeof(PlatformBusinessMetricsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PlatformBusinessMetricsDto>> Get(CancellationToken cancellationToken)
    {
        var snapshot = await _metrics.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);
        return Ok(snapshot);
    }
}
