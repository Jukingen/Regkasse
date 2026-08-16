using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>POS cashier TSE indicator (Fiskaly SIGN AT SCU + cached health). Not a signing endpoint.</summary>
[Authorize]
[ApiController]
[Route("api/pos/tse")]
public sealed class PosTseController : ControllerBase
{
    private readonly IPosTseStatusService _status;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ILogger<PosTseController> _logger;

    public PosTseController(
        IPosTseStatusService status,
        ICurrentTenantAccessor tenantAccessor,
        ILogger<PosTseController> logger)
    {
        _status = status;
        _tenantAccessor = tenantAccessor;
        _logger = logger;
    }

    /// <summary>Active / Degraded / Inactive snapshot for the POS header chip.</summary>
    [HttpGet("status")]
    [HasPermission(AppPermissions.CashRegisterView)]
    [ProducesResponseType(typeof(PosTseStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PosTseStatusDto>> GetStatus(
        [FromQuery] Guid? cashRegisterId,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (tenantId is null || tenantId == Guid.Empty)
        {
            _logger.LogWarning("POS TSE status: no ambient tenant");
            return NotFound();
        }

        var dto = await _status
            .GetStatusAsync(tenantId.Value, cashRegisterId, cancellationToken)
            .ConfigureAwait(false);
        return Ok(dto);
    }
}
