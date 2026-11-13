using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Order;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Deployment-wide marketplace provider credentials. Permission: <c>digital.manage</c>.</summary>
[Authorize]
[ApiController]
[Route("api/admin/online-order-integrations/providers")]
[Produces("application/json")]
public sealed class AdminOnlineOrderIntegrationProvidersController : ControllerBase
{
    private readonly IOnlineOrderIntegrationProviderService _providers;
    private readonly IVerticalProfileGuard _profileGuard;

    public AdminOnlineOrderIntegrationProvidersController(
        IOnlineOrderIntegrationProviderService providers,
        IVerticalProfileGuard profileGuard)
    {
        _providers = providers;
        _profileGuard = profileGuard;
    }

    [HttpGet]
    [HasPermission(AppPermissions.DigitalManage)]
    [ProducesResponseType(typeof(OnlineOrderIntegrationProviderAdminDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get([FromQuery] string? provider, CancellationToken ct)
    {
        var blocked = await MarketplaceProfileGate.RejectAsync(_profileGuard, ct);
        if (blocked is not null)
            return blocked;

        var result = await _providers.GetAdminAsync(provider, ct);
        if (!result.Result.Succeeded)
            return StatusCode(result.Result.StatusCode, new { code = result.Result.Code, message = result.Result.Error });

        return Ok(result.Body);
    }

    [HttpPut]
    [HasPermission(AppPermissions.DigitalManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertOnlineOrderIntegrationProviderRequest? body,
        CancellationToken ct)
    {
        if (body is null)
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Request body is required." });

        var blocked = await MarketplaceProfileGate.RejectAsync(_profileGuard, ct);
        if (blocked is not null)
            return blocked;

        var result = await _providers.UpsertAsync(body, ct);
        if (result.Succeeded)
            return Ok(new { id = result.Id });

        return StatusCode(result.StatusCode, new { code = result.Code, message = result.Error });
    }
}
