using KasseAPI_Final.Authorization;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Order;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Super Admin Wolt OAuth. Permission: <c>digital.manage</c>. No new permission.</summary>
[Authorize]
[ApiController]
[Route("api/admin/online-order-integrations/wolt")]
[Produces("application/json")]
public sealed class AdminWoltIntegrationController : ControllerBase
{
    private readonly IWoltIntegrationConnectService _connect;
    private readonly IVerticalProfileGuard _profileGuard;

    public AdminWoltIntegrationController(
        IWoltIntegrationConnectService connect,
        IVerticalProfileGuard profileGuard)
    {
        _connect = connect;
        _profileGuard = profileGuard;
    }

    [HttpPost("connect")]
    [HasPermission(AppPermissions.DigitalManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Connect(CancellationToken ct)
    {
        var blocked = await MarketplaceProfileGate.RejectAsync(_profileGuard, ct);
        if (blocked is not null)
            return blocked;

        var result = await _connect.BeginAsync(User.GetActorUserId(), ct);
        if (!result.Succeeded)
            return StatusCode(result.StatusCode, new { code = result.Code, message = result.Error });

        return Ok(new { authorizationUrl = result.AuthorizationUrl });
    }

    [HttpGet("callback")]
    [HasPermission(AppPermissions.DigitalManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status502BadGateway)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Callback([FromQuery] string? code, [FromQuery] string? state, CancellationToken ct)
    {
        var blocked = await MarketplaceProfileGate.RejectAsync(_profileGuard, ct);
        if (blocked is not null)
            return blocked;

        var result = await _connect.CompleteAsync(User.GetActorUserId(), code, state, ct);
        if (result.Succeeded)
            return Ok(new { id = result.Id });

        return StatusCode(result.StatusCode, new { code = result.Code, message = result.Error });
    }
}
