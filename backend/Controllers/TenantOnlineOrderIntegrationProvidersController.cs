using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Order;
using KasseAPI_Final.Services.VerticalProfiles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Tenant read-only marketplace provider status. Permission: <c>digital.orders.manage</c>.</summary>
[Authorize]
[ApiController]
[Route("api/admin/online-order-integrations/providers/status")]
[Produces("application/json")]
public sealed class TenantOnlineOrderIntegrationProvidersController : ControllerBase
{
    private readonly IOnlineOrderIntegrationProviderService _providers;
    private readonly IVerticalProfileGuard _profileGuard;

    public TenantOnlineOrderIntegrationProvidersController(
        IOnlineOrderIntegrationProviderService providers,
        IVerticalProfileGuard profileGuard)
    {
        _providers = providers;
        _profileGuard = profileGuard;
    }

    [HttpGet]
    [HasPermission(AppPermissions.DigitalOrdersManage)]
    [ProducesResponseType(typeof(OnlineOrderIntegrationProviderTenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get([FromQuery] string? provider, CancellationToken ct)
    {
        var blocked = await MarketplaceProfileGate.RejectAsync(_profileGuard, ct);
        if (blocked is not null)
            return blocked;

        var result = await _providers.GetTenantAsync(provider, ct);
        if (!result.Result.Succeeded)
            return StatusCode(result.Result.StatusCode, new { code = result.Result.Code, message = result.Result.Error });

        return Ok(result.Body);
    }
}
