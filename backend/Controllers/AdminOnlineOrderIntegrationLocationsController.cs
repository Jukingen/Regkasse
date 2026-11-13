using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Order;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Super Admin location map for marketplace intake. Permission: <c>digital.manage</c>.</summary>
[Authorize]
[ApiController]
[Route("api/admin/online-order-integrations/locations")]
[Produces("application/json")]
public sealed class AdminOnlineOrderIntegrationLocationsController : ControllerBase
{
    private readonly IOnlineOrderIntegrationLocationService _locations;
    private readonly IVerticalProfileGuard _profileGuard;

    public AdminOnlineOrderIntegrationLocationsController(
        IOnlineOrderIntegrationLocationService locations,
        IVerticalProfileGuard profileGuard)
    {
        _locations = locations;
        _profileGuard = profileGuard;
    }

    [HttpPut]
    [HasPermission(AppPermissions.DigitalManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertOnlineOrderIntegrationLocationRequest? body,
        CancellationToken ct)
    {
        if (body is null)
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Request body is required." });

        var blocked = await MarketplaceProfileGate.RejectAsync(_profileGuard, ct);
        if (blocked is not null)
            return blocked;

        var result = await _locations.UpsertAsync(body, ct);
        if (result.Succeeded)
            return Ok(new { id = result.Id });

        return StatusCode(result.StatusCode, new { code = result.Code, message = result.Error });
    }
}
