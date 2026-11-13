using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin TSE statistical anomaly detection (baseline deviation — diagnostic only).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/anomalies")]
[Produces("application/json")]
public sealed class AdminTseAnomalyDetectionController : ControllerBase
{
    private readonly ITseAnomalyDetectionService _anomalies;

    public AdminTseAnomalyDetectionController(ITseAnomalyDetectionService anomalies)
    {
        _anomalies = anomalies;
    }

    [HttpGet("dashboard")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseAnomalyDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseAnomalyDashboardDto>> GetDashboard(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _anomalies.GetDashboardAsync(tenantId, cancellationToken).ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("detect")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseAnomalyResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseAnomalyResultDto>> Detect(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _anomalies
                .DetectAnomaliesAsync(tenantId, User.GetActorUserId(), cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("detect/device")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseAnomalyResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseAnomalyResultDto>> DetectForDevice(
        [FromQuery] Guid deviceId,
        CancellationToken cancellationToken)
    {
        if (deviceId == Guid.Empty)
            return BadRequest(new { error = "deviceId is required." });

        try
        {
            return Ok(await _anomalies
                .DetectAnomaliesForDeviceAsync(deviceId, User.GetActorUserId(), cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("report")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseAnomalyReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseAnomalyReportDto>> Report(
        [FromQuery] Guid tenantId,
        [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        try
        {
            return Ok(await _anomalies
                .GenerateAnomalyReportAsync(tenantId, fromUtc, toUtc, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("check")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(object), StatusCodes.Status200OK)]
    public async Task<ActionResult<object>> CheckValue(
        [FromQuery] Guid tenantId,
        [FromBody] TseAnomalyCheckRequestDto? body,
        CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });
        body ??= new TseAnomalyCheckRequestDto();

        try
        {
            var isAnomaly = await _anomalies
                .IsAnomalyDetectedAsync(tenantId, body.MetricName, body.Value, cancellationToken)
                .ConfigureAwait(false);
            return Ok(new { isAnomaly });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpPost("{anomalyId:guid}/resolve")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseAnomalyDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TseAnomalyDto>> Resolve(
        Guid anomalyId,
        CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _anomalies
                .ResolveAnomalyAsync(anomalyId, User.GetActorUserId(), cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
