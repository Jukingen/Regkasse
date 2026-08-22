using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Limits;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Super Admin per-mandant operational caps (<c>tenant_limits</c>).</summary>
[ApiController]
[Route("api/admin/tenants/{tenantId:guid}/limits")]
[Authorize(Roles = Roles.SuperAdmin)]
[Produces("application/json")]
public sealed class AdminTenantLimitsController : ControllerBase
{
    private readonly ITenantLimitService _tenantLimitService;
    private readonly ILogger<AdminTenantLimitsController> _logger;

    public AdminTenantLimitsController(
        ITenantLimitService tenantLimitService,
        ILogger<AdminTenantLimitsController> logger)
    {
        _tenantLimitService = tenantLimitService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(TenantLimitsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantLimitsDto>> GetLimits(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var limits = await _tenantLimitService.GetLimitsAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(TenantLimitsDto.FromEntity(limits));
        }
        catch (InvalidOperationException ex) when (IsTenantNotFound(ex))
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut]
    [ProducesResponseType(typeof(TenantLimitsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantLimitsDto>> UpdateLimits(
        Guid tenantId,
        [FromBody] UpdateTenantLimitsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
            return BadRequest(new { message = "Request body is required." });

        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var limits = await _tenantLimitService
                .UpdateLimitsAsync(tenantId, request, cancellationToken)
                .ConfigureAwait(false);
            return Ok(TenantLimitsDto.FromEntity(limits));
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

    [HttpPost("reset")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ResetLimits(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _tenantLimitService.ResetLimitsAsync(tenantId, cancellationToken).ConfigureAwait(false);
            var limits = await _tenantLimitService.GetLimitsAsync(tenantId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(TenantLimitsDto.FromEntity(limits));
        }
        catch (InvalidOperationException ex) when (IsTenantNotFound(ex))
        {
            return NotFound(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tenant limits reset failed TenantId={TenantId}", tenantId);
            throw;
        }
    }

    private static bool IsTenantNotFound(InvalidOperationException ex) =>
        ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase);
}
