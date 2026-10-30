using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Order;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Tenant Foodora credentials. Permission: <c>digital.orders.manage</c>.</summary>
[Authorize]
[ApiController]
[Route("api/admin/online-order-integrations/foodora/credentials")]
[Produces("application/json")]
public sealed class AdminFoodoraCredentialsController : ControllerBase
{
    private readonly IFoodoraTenantCredentialService _credentials;
    private readonly IVerticalProfileGuard _profileGuard;

    public AdminFoodoraCredentialsController(
        IFoodoraTenantCredentialService credentials,
        IVerticalProfileGuard profileGuard)
    {
        _credentials = credentials;
        _profileGuard = profileGuard;
    }

    [HttpGet]
    [HasPermission(AppPermissions.DigitalOrdersManage)]
    [ProducesResponseType(typeof(FoodoraTenantCredentialStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var blocked = await MarketplaceProfileGate.RejectAsync(_profileGuard, ct);
        if (blocked is not null)
            return blocked;

        var result = await _credentials.GetAsync(ct);
        if (!result.Result.Succeeded)
            return StatusCode(result.Result.StatusCode, new { code = result.Result.Code, message = result.Result.Error });

        return Ok(result.Body);
    }

    [HttpPut]
    [HasPermission(AppPermissions.DigitalOrdersManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Upsert([FromBody] UpsertFoodoraTenantCredentialRequest? body, CancellationToken ct)
    {
        if (body is null)
            return BadRequest(new { code = "VALIDATION_ERROR", message = "Request body is required." });

        var blocked = await MarketplaceProfileGate.RejectAsync(_profileGuard, ct);
        if (blocked is not null)
            return blocked;

        var result = await _credentials.UpsertAsync(body, ct);
        if (result.Succeeded)
            return Ok(new { id = result.Id });

        return StatusCode(result.StatusCode, new { code = result.Code, message = result.Error });
    }
}
