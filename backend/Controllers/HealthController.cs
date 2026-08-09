using KasseAPI_Final.Configuration;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.HealthChecks;
using KasseAPI_Final.Services.Deployment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Process and dependency health probes (anonymous). Prefer in-memory TSE/NTP snapshots; DB probe is timeout-bounded.
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthChecks;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly DeploymentOptions _deploymentOptions;

    public HealthController(
        HealthCheckService healthChecks,
        IHostEnvironment hostEnvironment,
        IOptions<DeploymentOptions> deploymentOptions)
    {
        _healthChecks = healthChecks;
        _hostEnvironment = hostEnvironment;
        _deploymentOptions = deploymentOptions.Value;
    }

    /// <summary>Liveness: process is up. No dependency I/O.</summary>
    [HttpGet("live")]
    [Produces("text/plain")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public ContentResult Live() => Content("OK", "text/plain");

    /// <summary>
    /// Readiness: database + fiscal config posture (TSE production lock + FinanzOnline simulation gate) + cache/Redis.
    /// Device TSE/NTP probes are excluded (see <c>/api/health</c>). In Development, fiscal checks stay Healthy
    /// when Soft TSE / FON simulation is intentional; in Production, misconfiguration → Unhealthy (HTTP 503).
    /// Cache/Redis is probed via <see cref="RedisCacheHealthCheck"/> (GetAsync <c>health_check_ping</c>, ≤1s);
    /// failures are <c>Degraded</c> (not Unhealthy) and surfaced as <see cref="HealthProbeResponseDto.RedisStatus"/>.
    /// Response includes <see cref="HealthProbeResponseDto.ReleaseStage"/> for Staging (Demo &amp; QA) / smoke verification.
    /// </summary>
    [HttpGet("ready")]
    [ProducesResponseType(typeof(HealthProbeResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthProbeResponseDto), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        // ready tag includes database, fiscal posture, and RedisCacheHealthCheck (cache ping → RedisStatus).
        var report = await _healthChecks
            .CheckHealthAsync(r => r.Tags.Contains(DatabaseHealthCheck.ReadyTag), cancellationToken)
            .ConfigureAwait(false);
        // releaseStage: primarily for debugging and staging verification (also returned in Production for smoke checks).
        var releaseStage = ReleaseStageResolver.Resolve(_hostEnvironment, _deploymentOptions);
        return ToActionResult(report, releaseStage);
    }

    /// <summary>
    /// Dependency snapshot: database + cached TSE + cached NTP.
    /// HTTP 200 for Healthy/Degraded; 503 only when a critical check (database) is Unhealthy.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var report = await _healthChecks
            .CheckHealthAsync(r => r.Tags.Contains(DatabaseHealthCheck.DepsTag), cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(report);
    }

    /// <summary>
    /// EF Core migration posture: applied vs pending relative to the running binary.
    /// HTTP 200 for Healthy/Degraded; 503 when the check cannot query the database.
    /// </summary>
    [HttpGet("migrations")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Migrations(CancellationToken cancellationToken)
    {
        var report = await _healthChecks
            .CheckHealthAsync(r => r.Tags.Contains(EfMigrationsHealthCheck.MigrationsTag), cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(report);
    }

    private IActionResult ToActionResult(HealthReport report, string? releaseStage = null)
    {
        var statusCode = report.Status switch
        {
            HealthStatus.Healthy => StatusCodes.Status200OK,
            HealthStatus.Degraded => StatusCodes.Status200OK,
            _ => StatusCodes.Status503ServiceUnavailable,
        };

        return StatusCode(statusCode, HealthProbeResponseFactory.FromReport(report, releaseStage));
    }
}
