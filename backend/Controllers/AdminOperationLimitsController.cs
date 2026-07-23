using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Operations;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Tenant operation quota status for FA warnings (bulk delete remaining, etc.).</summary>
[Authorize]
[ApiController]
[Route("api/admin/operation-limits")]
[Produces("application/json")]
[HasPermission(AppPermissions.ProductView)]
public sealed class AdminOperationLimitsController : ControllerBase
{
    private readonly IOperationLimitService _limits;
    private readonly ISettingsTenantResolver _tenantResolver;

    public AdminOperationLimitsController(
        IOperationLimitService limits,
        ISettingsTenantResolver tenantResolver)
    {
        _limits = limits;
        _tenantResolver = tenantResolver;
    }

    [HttpGet("status")]
    [ProducesResponseType(typeof(OperationLimitStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OperationLimitStatusDto>> GetStatus(
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
            .ConfigureAwait(false);
        if (tenantId == Guid.Empty)
            return NotFound();

        var status = await _limits.GetStatusAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return Ok(status);
    }
}
