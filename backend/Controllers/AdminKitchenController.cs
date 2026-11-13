using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.Kitchen;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/kitchen")]
[Produces("application/json")]
[HasPermission(AppPermissions.SettingsView)]
public sealed class AdminKitchenController : ControllerBase
{
    private readonly IAdminKitchenService _kitchen;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminKitchenController(IAdminKitchenService kitchen, ICurrentTenantAccessor tenantAccessor)
    {
        _kitchen = kitchen;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet("settings")]
    [ProducesResponseType(typeof(KitchenSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KitchenSettingsDto>> GetSettings(CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var settings = await _kitchen.GetSettingsAsync(tenantId, ignoreFilters: false, cancellationToken)
            .ConfigureAwait(false);
        return settings is null ? NotFound() : Ok(settings);
    }

    [HttpPatch("settings")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(KitchenSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KitchenSettingsDto>> UpdateSettings(
        [FromBody] UpdateKitchenSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var settings = await _kitchen
            .UpdateSettingsAsync(tenantId, request, ignoreFilters: false, cancellationToken)
            .ConfigureAwait(false);
        return settings is null ? NotFound() : Ok(settings);
    }

    [HttpGet("orders")]
    [ProducesResponseType(typeof(IReadOnlyList<KitchenAdminOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<KitchenAdminOrderDto>>> ListOrders(
        CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var orders = await _kitchen.ListOrdersAsync(tenantId, ignoreFilters: false, cancellationToken)
            .ConfigureAwait(false);
        return Ok(orders);
    }

    [HttpGet("analytics")]
    [ProducesResponseType(typeof(KitchenAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KitchenAnalyticsDto>> GetAnalytics(CancellationToken cancellationToken)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound();

        var analytics = await _kitchen.GetAnalyticsAsync(tenantId, ignoreFilters: false, cancellationToken)
            .ConfigureAwait(false);
        return Ok(analytics);
    }
}
