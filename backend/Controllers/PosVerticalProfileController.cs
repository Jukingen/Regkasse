using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Tenant-scoped effective POS vertical profile.</summary>
[Authorize]
[ApiController]
[Route("api/pos/vertical-profile")]
[Produces("application/json")]
[HasPermission(AppPermissions.CartView)]
public sealed class PosVerticalProfileController : ControllerBase
{
    private readonly IVerticalProfileService _profiles;

    public PosVerticalProfileController(IVerticalProfileService profiles)
    {
        _profiles = profiles;
    }

    [HttpGet]
    [ProducesResponseType(typeof(EffectiveVerticalProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EffectiveVerticalProfileDto>> Get(
        CancellationToken cancellationToken)
    {
        var profile = await _profiles
            .GetForCurrentTenantAsync(cancellationToken)
            .ConfigureAwait(false);
        return profile is null ? NotFound() : Ok(profile);
    }
}
