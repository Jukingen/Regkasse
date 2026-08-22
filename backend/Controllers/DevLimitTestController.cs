using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Limits;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Development-only Super Admin QA API for tenant operational caps.
/// All routes return <see cref="NotFoundResult"/> outside Development.
/// </summary>
[ApiController]
[Route("api/dev/limits")]
[Authorize(Roles = Roles.SuperAdmin)]
[Produces("application/json")]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class DevLimitTestController : ControllerBase
{
    private readonly IHostEnvironment _environment;
    private readonly ITenantLimitService _tenantLimitService;
    private readonly ITenantLimitGuard _tenantLimitGuard;
    private readonly ITenantLimitCacheService _cache;
    private readonly ILogger<DevLimitTestController> _logger;

    public DevLimitTestController(
        IHostEnvironment environment,
        ITenantLimitService tenantLimitService,
        ITenantLimitGuard tenantLimitGuard,
        ITenantLimitCacheService cache,
        ILogger<DevLimitTestController> logger)
    {
        _environment = environment;
        _tenantLimitService = tenantLimitService;
        _tenantLimitGuard = tenantLimitGuard;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>Live usage vs caps for a mandant (Super Admin Development panel).</summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(TenantLimitUsageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantLimitUsageDto>> GetStatus(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (EnsureNotDevelopment() is { } denied)
            return denied;

        if (tenantId == Guid.Empty)
            return BadRequest(new { message = "tenantId is required." });

        try
        {
            var usage = await _tenantLimitGuard.GetUsageAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(usage);
        }
        catch (InvalidOperationException ex) when (IsTenantNotFound(ex))
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost("set")]
    [ProducesResponseType(typeof(TenantLimitUsageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantLimitUsageDto>> SetLimit(
        [FromBody] SetLimitRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (EnsureNotDevelopment() is { } denied)
            return denied;

        if (request == null || request.TenantId == Guid.Empty)
            return BadRequest(new { message = "tenantId is required." });

        if (string.IsNullOrWhiteSpace(request.LimitKey))
            return BadRequest(new { message = "limitKey is required." });

        try
        {
            await _tenantLimitService
                .SetLimitValueAsync(request.TenantId, request.LimitKey, request.Value, cancellationToken)
                .ConfigureAwait(false);
            var usage = await _tenantLimitGuard.GetUsageAsync(request.TenantId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(usage);
        }
        catch (InvalidOperationException ex) when (IsTenantNotFound(ex))
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("reset-all")]
    [ProducesResponseType(typeof(TenantLimitUsageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantLimitUsageDto>> ResetAllLimits(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (EnsureNotDevelopment() is { } denied)
            return denied;

        if (tenantId == Guid.Empty)
            return BadRequest(new { message = "tenantId is required." });

        try
        {
            await _tenantLimitService.ResetLimitsAsync(tenantId, cancellationToken).ConfigureAwait(false);
            var usage = await _tenantLimitGuard.GetUsageAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(usage);
        }
        catch (InvalidOperationException ex) when (IsTenantNotFound(ex))
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Moves caps relative to live usage (near / at / tiny / reset). Does not create fiscal rows.
    /// </summary>
    [HttpPost("scenario/trigger")]
    [ProducesResponseType(typeof(TenantLimitUsageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantLimitUsageDto>> TriggerLimitScenario(
        [FromBody] TriggerLimitScenarioRequest? request,
        CancellationToken cancellationToken = default)
    {
        if (EnsureNotDevelopment() is { } denied)
            return denied;

        if (request == null || request.TenantId == Guid.Empty)
            return BadRequest(new { message = "tenantId is required." });

        if (string.IsNullOrWhiteSpace(request.Scenario))
            return BadRequest(new { message = "scenario is required." });

        try
        {
            var usage = await _tenantLimitGuard.GetUsageAsync(request.TenantId, cancellationToken)
                .ConfigureAwait(false);
            var patch = DevLimitScenarioPlanner.Build(usage, request.Scenario, request.LimitKey);
            if (patch == null)
            {
                await _tenantLimitService.ResetLimitsAsync(request.TenantId, cancellationToken)
                    .ConfigureAwait(false);
            }
            else
            {
                await _tenantLimitService
                    .UpdateLimitsAsync(request.TenantId, patch, cancellationToken)
                    .ConfigureAwait(false);
            }

            var updated = await _tenantLimitGuard.GetUsageAsync(request.TenantId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(updated);
        }
        catch (InvalidOperationException ex) when (IsTenantNotFound(ex))
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("cache/clear")]
    [ProducesResponseType(typeof(TenantLimitUsageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantLimitUsageDto>> ClearLimitCache(
        [FromQuery] Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (EnsureNotDevelopment() is { } denied)
            return denied;

        if (tenantId == Guid.Empty)
            return BadRequest(new { message = "tenantId is required." });

        try
        {
            await _cache.InvalidateAsync(tenantId, cancellationToken).ConfigureAwait(false);
            var usage = await _tenantLimitGuard.GetUsageAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(usage);
        }
        catch (InvalidOperationException ex) when (IsTenantNotFound(ex))
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Returns <see cref="NotFoundResult"/> when the host is not Development.</summary>
    private ActionResult? EnsureNotDevelopment()
    {
        if (_environment.IsDevelopment())
            return null;

        _logger.LogWarning("Dev limit test API rejected: endpoint is only available in Development.");
        return NotFound();
    }

    private static bool IsTenantNotFound(InvalidOperationException ex) =>
        ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase);
}
