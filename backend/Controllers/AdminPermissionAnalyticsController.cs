using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/permission-analytics")]
[Produces("application/json")]
public sealed class AdminPermissionAnalyticsController : ControllerBase
{
    private readonly IPermissionAnalyticsService _analytics;

    public AdminPermissionAnalyticsController(IPermissionAnalyticsService analytics)
    {
        _analytics = analytics;
    }

    [HttpGet("summary")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(PermissionAnalyticsSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PermissionAnalyticsSummaryDto>> Summary(CancellationToken cancellationToken)
    {
        return Ok(await _analytics.GetSummaryAsync(cancellationToken));
    }

    [HttpGet("trend")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(IReadOnlyList<PermissionAnalyticsTrendPointDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PermissionAnalyticsTrendPointDto>>> Trend(
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _analytics.GetTrendAsync(days, cancellationToken));
    }

    [HttpGet("export")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Export(
        [FromQuery] string format = "csv",
        CancellationToken cancellationToken = default)
    {
        var (content, contentType, fileName) = await _analytics.ExportAsync(format, cancellationToken);
        return File(content, contentType, fileName);
    }
}
