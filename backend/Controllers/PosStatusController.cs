using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Combined POS status probes (license, register readiness, settings revision) in one round-trip.
/// </summary>
[Authorize]
[ApiController]
[Route("api/pos/status")]
public sealed class PosStatusController : ControllerBase
{
    private readonly IPosStatusService _posStatus;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ILogger<PosStatusController> _logger;

    public PosStatusController(
        IPosStatusService posStatus,
        ICurrentTenantAccessor tenantAccessor,
        ILogger<PosStatusController> logger)
    {
        _posStatus = posStatus;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    /// <summary>
    /// Returns deployment + mandant license, health/license subset, read-only cash-register readiness, and settings revision.
    /// </summary>
    [HttpGet("overview")]
    [HasPermission(AppPermissions.CartView)]
    [ProducesResponseType(typeof(PosStatusOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PosStatusOverviewDto>> GetOverview(CancellationToken cancellationToken)
    {
        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("Pos status overview: no user id in claims");
            return Unauthorized(new { message = "User not authenticated" });
        }

        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
            return NotFound();

        var overview = await _posStatus
            .GetOverviewAsync(userId, User, tenantId.Value, cancellationToken)
            .ConfigureAwait(false);

        return Ok(overview);
    }
}
