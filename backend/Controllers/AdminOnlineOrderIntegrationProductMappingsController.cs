using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Order;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Super Admin product map for marketplace intake. Permission: <c>digital.manage</c>.</summary>
[Authorize]
[ApiController]
[Route("api/admin/online-order-integrations/product-mappings")]
[Produces("application/json")]
public sealed class AdminOnlineOrderIntegrationProductMappingsController : ControllerBase
{
    private readonly IOnlineOrderIntegrationProductMappingService _mappings;
    private readonly IVerticalProfileGuard _profileGuard;

    public AdminOnlineOrderIntegrationProductMappingsController(
        IOnlineOrderIntegrationProductMappingService mappings,
        IVerticalProfileGuard profileGuard)
    {
        _mappings = mappings;
        _profileGuard = profileGuard;
    }

    [HttpGet]
    [HasPermission(AppPermissions.DigitalManage)]
    [ProducesResponseType(typeof(OnlineOrderProductMappingListDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List([FromQuery] string? provider, CancellationToken ct)
    {
        var blocked = await MarketplaceProfileGate.RejectAsync(_profileGuard, ct);
        if (blocked is not null)
            return blocked;

        var result = await _mappings.ListAsync(provider, ct);
        if (result.Succeeded)
            return Ok(result.Body);

        return StatusCode(result.StatusCode, new { code = result.Code, message = result.Error });
    }

    [HttpPut]
    [HasPermission(AppPermissions.DigitalManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Upsert(
        [FromBody] UpsertOnlineOrderProductMappingRequest? body,
        CancellationToken ct)
    {
        if (body is null)
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Request body is required." });

        var blocked = await MarketplaceProfileGate.RejectAsync(_profileGuard, ct);
        if (blocked is not null)
            return blocked;

        var result = await _mappings.UpsertAsync(body, ct);
        if (result.Succeeded)
            return Ok(new { id = result.Id });

        return StatusCode(result.StatusCode, new { code = result.Code, message = result.Error });
    }
}
