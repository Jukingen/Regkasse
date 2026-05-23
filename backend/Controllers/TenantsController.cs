using KasseAPI_Final.Authorization;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.AdminTenants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Authenticated tenant list for dev header switcher (membership-scoped unless SuperAdmin).</summary>
[ApiController]
[Route("api/tenants")]
[Authorize]
[Produces("application/json")]
public sealed class TenantsController : ControllerBase
{
    private readonly IAdminTenantService _tenantService;

    public TenantsController(IAdminTenantService tenantService)
    {
        _tenantService = tenantService;
    }

    /// <summary>Tenants visible in the header switcher (all for SuperAdmin; active memberships otherwise).</summary>
    [HttpGet("switcher")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminTenantListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminTenantListItemDto>>> ListForSwitcher(
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var actorUserId = User.GetActorUserId();
        var actorIsSuperAdmin = User.IsInRole(Roles.SuperAdmin);
        var effectiveIncludeDeleted = actorIsSuperAdmin && includeDeleted;
        var items = await _tenantService
            .ListForSwitcherAsync(actorUserId, actorIsSuperAdmin, effectiveIncludeDeleted, cancellationToken)
            .ConfigureAwait(false);
        return Ok(items);
    }
}
