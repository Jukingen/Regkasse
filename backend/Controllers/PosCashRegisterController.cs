using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// POS-only cash-register readiness (effective register + optional controlled auto-open).
/// </summary>
[Authorize]
[ApiController]
[Route("api/pos/cash-register")]
public sealed class PosCashRegisterController : ControllerBase
{
    private readonly IPosCashRegisterReadinessService _readiness;
    private readonly ILogger<PosCashRegisterController> _logger;

    public PosCashRegisterController(
        IPosCashRegisterReadinessService readiness,
        ILogger<PosCashRegisterController> logger)
    {
        _readiness = readiness;
        _logger = logger;
    }

    /// <summary>
    /// Resolves effective register for the session and may auto-open the sole (or assigned) closed register when flags allow.
    /// </summary>
    [HttpPost("ensure-ready")]
    [HasPermission(AppPermissions.CartView)]
    [ProducesResponseType(typeof(PosCashRegisterContextDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<PosCashRegisterContextDto>> EnsureReady(CancellationToken cancellationToken)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                     ?? User.FindFirst("user_id")?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            _logger.LogWarning("EnsureReady: no user id in claims");
            return Unauthorized(new { message = "User not authenticated" });
        }

        var dto = await _readiness.EnsureReadyForPosAsync(userId, User, cancellationToken);
        return Ok(dto);
    }
}
