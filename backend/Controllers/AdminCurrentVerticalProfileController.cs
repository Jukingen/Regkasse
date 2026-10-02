using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Ambient tenant effective POS vertical profile for FA catalog editors.</summary>
[Authorize]
[ApiController]
[Route("api/admin/vertical-profile")]
[Produces("application/json")]
[HasPermission(AppPermissions.ProductView)]
public sealed class AdminCurrentVerticalProfileController : ControllerBase
{
    private readonly IVerticalProfileService _profiles;

    public AdminCurrentVerticalProfileController(IVerticalProfileService profiles)
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
