using KasseAPI_Final.HealthChecks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

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

    public HealthController(HealthCheckService healthChecks)
    {
        _healthChecks = healthChecks;
    }

    /// <summary>Liveness: process is up. No dependency I/O.</summary>
    [HttpGet("live")]
    [Produces("text/plain")]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public ContentResult Live() => Content("OK", "text/plain");

    /// <summary>
    /// Readiness: database + fiscal config posture (TSE production lock + FinanzOnline simulation gate).
    /// Device TSE/NTP probes are excluded (see <c>/api/health</c>). In Development, fiscal checks stay Healthy
    /// when Soft TSE / FON simulation is intentional; in Production, misconfiguration → Unhealthy (HTTP 503).
    /// </summary>
    [HttpGet("ready")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ready(CancellationToken cancellationToken)
    {
        var report = await _healthChecks
            .CheckHealthAsync(r => r.Tags.Contains(DatabaseHealthCheck.ReadyTag), cancellationToken)
            .ConfigureAwait(false);
        return ToActionResult(report);
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

    private IActionResult ToActionResult(HealthReport report)
    {
        var statusCode = report.Status switch
        {
            HealthStatus.Healthy => StatusCodes.Status200OK,
            HealthStatus.Degraded => StatusCodes.Status200OK,
            _ => StatusCodes.Status503ServiceUnavailable,
        };

        return StatusCode(statusCode, new
        {
            status = report.Status.ToString(),
            totalDurationMs = report.TotalDuration.TotalMilliseconds,
            checkedAtUtc = DateTime.UtcNow,
            entries = report.Entries.ToDictionary(
                e => e.Key,
                e => new
                {
                    status = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    durationMs = e.Value.Duration.TotalMilliseconds,
                    data = e.Value.Data,
                }),
        });
    }
}
