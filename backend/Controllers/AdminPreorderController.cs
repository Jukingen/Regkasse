using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Preorder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>FA Vorbestellung stats and list. Not website online-orders.</summary>
[Authorize]
[ApiController]
[Route("api/admin/orders")]
[Produces("application/json")]
public sealed class AdminPreorderController : ControllerBase
{
    private readonly IPreorderService _preorders;

    public AdminPreorderController(IPreorderService preorders)
    {
        _preorders = preorders;
    }

    [HttpGet("preorder-stats")]
    [HasPermission(AppPermissions.OrderView)]
    [ProducesResponseType(typeof(PreorderStatsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PreorderStatsDto>> GetStats(CancellationToken cancellationToken)
    {
        return Ok(await _preorders.GetStatsAsync(cancellationToken));
    }

    [HttpGet("preorders")]
    [HasPermission(AppPermissions.OrderView)]
    [ProducesResponseType(typeof(PreorderListResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PreorderListResponseDto>> List(
        [FromQuery] string? status = null,
        [FromQuery] string? receiptNumber = null,
        [FromQuery] int take = 100,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _preorders.ListAsync(status, receiptNumber, take, cancellationToken));
    }

    [HttpGet("preorder-settings")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(PreorderSettingsDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PreorderSettingsDto>> GetSettings(CancellationToken cancellationToken)
    {
        return Ok(await _preorders.GetSettingsAsync(cancellationToken));
    }

    [HttpPut("preorder-settings")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(PreorderSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PreorderSettingsDto>> UpdateSettings(
        [FromBody] PreorderSettingsDto request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        try
        {
            return Ok(await _preorders.UpdateSettingsAsync(request, cancellationToken));
        }
        catch (InvalidOperationException ex) when (ex.Message is "PREORDER_TENANT_REQUIRED" or "PREORDER_SETTINGS_NOT_FOUND")
        {
            return NotFound();
        }
    }
}
