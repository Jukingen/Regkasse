using KasseAPI_Final.Authorization;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Services.Kitchen;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/admin/tenants/{tenantId:guid}/kitchen")]
[Produces("application/json")]
public sealed class AdminTenantKitchenController : ControllerBase
{
    private readonly IAdminKitchenService _kitchen;
    private readonly IAdminTenantService _tenants;

    public AdminTenantKitchenController(IAdminKitchenService kitchen, IAdminTenantService tenants)
    {
        _kitchen = kitchen;
        _tenants = tenants;
    }

    [HttpGet("settings")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(KitchenSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KitchenSettingsDto>> GetSettings(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (await MissingTenantAsync(tenantId, cancellationToken).ConfigureAwait(false))
            return NotFound();

        var settings = await _kitchen.GetSettingsAsync(tenantId, ignoreFilters: true, cancellationToken)
            .ConfigureAwait(false);
        return settings is null ? NotFound() : Ok(settings);
    }

    [HttpPatch("settings")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(typeof(KitchenSettingsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KitchenSettingsDto>> UpdateSettings(
        Guid tenantId,
        [FromBody] UpdateKitchenSettingsRequest request,
        CancellationToken cancellationToken)
    {
        if (await MissingTenantAsync(tenantId, cancellationToken).ConfigureAwait(false))
            return NotFound();
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var settings = await _kitchen
            .UpdateSettingsAsync(tenantId, request, ignoreFilters: true, cancellationToken)
            .ConfigureAwait(false);
        return settings is null ? NotFound() : Ok(settings);
    }

    [HttpGet("orders")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(IReadOnlyList<KitchenAdminOrderDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<KitchenAdminOrderDto>>> ListOrders(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (await MissingTenantAsync(tenantId, cancellationToken).ConfigureAwait(false))
            return NotFound();

        var orders = await _kitchen.ListOrdersAsync(tenantId, ignoreFilters: true, cancellationToken)
            .ConfigureAwait(false);
        return Ok(orders);
    }

    [HttpGet("analytics")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(KitchenAnalyticsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<KitchenAnalyticsDto>> GetAnalytics(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (await MissingTenantAsync(tenantId, cancellationToken).ConfigureAwait(false))
            return NotFound();

        var analytics = await _kitchen.GetAnalyticsAsync(tenantId, ignoreFilters: true, cancellationToken)
            .ConfigureAwait(false);
        return Ok(analytics);
    }

    private async Task<bool> MissingTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var tenant = await _tenants.GetByIdAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return tenant is null;
    }
}
