using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Tse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin TSE fleet health for external monitoring (JSON status + Prometheus text).
/// Auth: <see cref="AppPermissions.SystemCritical"/> (same as other Admin TSE ops; SuperAdmin passes).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/tse/health")]
[Produces("application/json")]
public sealed class AdminTseHealthController : ControllerBase
{
    public const string PrometheusContentType = "text/plain; version=0.0.4; charset=utf-8";

    private readonly ITseHealthCheckService _healthCheckService;
    private readonly ITseMetricsService _metricsService;

    public AdminTseHealthController(
        ITseHealthCheckService healthCheckService,
        ITseMetricsService metricsService)
    {
        _healthCheckService = healthCheckService;
        _metricsService = metricsService;
    }

    /// <summary>
    /// Overall fleet status. Default <paramref name="liveProbe"/>=true runs device probes;
    /// set <c>liveProbe=false</c> for scrape-safe last-known status.
    /// </summary>
    [HttpGet("status")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseFleetHealthStatusDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseFleetHealthStatusDto>> GetOverallStatus(
        [FromQuery] bool liveProbe = true,
        CancellationToken cancellationToken = default)
    {
        var status = await _healthCheckService
            .GetOverallStatusAsync(liveProbe, cancellationToken)
            .ConfigureAwait(false);
        return Ok(status);
    }

    /// <summary>JSON summary gauges (no live vendor probes).</summary>
    [HttpGet("metrics")]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(TseHealthMetricsSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TseHealthMetricsSummaryDto>> GetSummaryMetrics(
        CancellationToken cancellationToken = default)
    {
        var metrics = await _metricsService.GetSummaryMetricsAsync(cancellationToken)
            .ConfigureAwait(false);
        return Ok(metrics);
    }

    /// <summary>
    /// Prometheus exposition for job <c>regkasse-tse</c>.
    /// Requires Bearer JWT with <c>system.critical</c> (configure scrape auth / network ACL).
    /// </summary>
    [HttpGet("metrics/prometheus")]
    [HasPermission(AppPermissions.SystemCritical)]
    [Produces(PrometheusContentType)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPrometheusMetrics(CancellationToken cancellationToken = default)
    {
        var text = await _metricsService.GetPrometheusMetricsAsync(cancellationToken)
            .ConfigureAwait(false);
        return Content(text, PrometheusContentType);
    }
}
