using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Tse;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Mandanten-Admin TSE self-service portal (read-only status + health history).
/// Ambient JWT tenant only — cross-tenant → 404.
/// </summary>
[Authorize(Roles = Roles.Manager)]
[ApiController]
[Route("api/tenant/tse")]
[Produces("application/json")]
public sealed class TenantTseController : ControllerBase
{
    private readonly ITenantTsePortalService _portal;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public TenantTseController(
        ITenantTsePortalService portal,
        ICurrentTenantAccessor tenantAccessor)
    {
        _portal = portal;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet("status")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(TenantTseStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantTseStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        try
        {
            return Ok(await _portal.GetStatusAsync(tenantId, cancellationToken).ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpGet("health-history")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(TenantTseHealthHistoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantTseHealthHistoryDto>> GetHealthHistory(
        [FromQuery] int days = 30,
        CancellationToken cancellationToken = default)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        try
        {
            return Ok(await _portal
                .GetHealthHistoryAsync(tenantId, days, cancellationToken)
                .ConfigureAwait(false));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }
}
