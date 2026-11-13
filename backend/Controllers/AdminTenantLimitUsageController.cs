using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Limits;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Current ambient-tenant limit usage for FA warnings (not SuperAdmin-only write API).</summary>
[ApiController]
[Route("api/admin/limits")]
[Authorize]
[Produces("application/json")]
public sealed class AdminTenantLimitUsageController : ControllerBase
{
    private readonly ITenantLimitGuard _tenantLimitGuard;
    private readonly ITenantLimitDashboardService _dashboard;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminTenantLimitUsageController(
        ITenantLimitGuard tenantLimitGuard,
        ITenantLimitDashboardService dashboard,
        ICurrentTenantAccessor tenantAccessor)
    {
        _tenantLimitGuard = tenantLimitGuard;
        _dashboard = dashboard;
        _tenantAccessor = tenantAccessor;
    }

    [HttpGet]
    [ProducesResponseType(typeof(TenantLimitUsageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TenantLimitUsageDto>> GetUsage(CancellationToken cancellationToken = default)
    {
        if (_tenantAccessor.TenantId is not Guid tenantId || tenantId == Guid.Empty)
            return NotFound(new { message = "Tenant context is required." });

        try
        {
            var usage = await _tenantLimitGuard.GetUsageAsync(tenantId, cancellationToken).ConfigureAwait(false);
            return Ok(usage);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("dashboard")]
    [HasPermission(AppPermissions.LicenseManage)]
    [ProducesResponseType(typeof(LimitDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LimitDashboardDto>> GetDashboard(
        [FromQuery] bool allTenants = false,
        [FromQuery] Guid? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var readerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var isSuperAdmin = User.IsInRole(Roles.SuperAdmin);
        var hasAmbient = _tenantAccessor.TenantId is Guid ambient && ambient != Guid.Empty;
        var hasExplicit = tenantId is Guid explicitId && explicitId != Guid.Empty;

        try
        {
            if (isSuperAdmin && allTenants)
            {
                var all = await _dashboard
                    .GetDashboardForAllTenantsAsync(readerUserId, cancellationToken)
                    .ConfigureAwait(false);
                return Ok(all);
            }

            if (isSuperAdmin && hasExplicit)
            {
                var targeted = await _dashboard
                    .GetDashboardAsync(tenantId!.Value, readerUserId, cancellationToken)
                    .ConfigureAwait(false);
                return Ok(targeted);
            }

            if (isSuperAdmin && !hasAmbient)
            {
                var all = await _dashboard
                    .GetDashboardForAllTenantsAsync(readerUserId, cancellationToken)
                    .ConfigureAwait(false);
                return Ok(all);
            }

            if (!hasAmbient)
                return NotFound(new { message = "Tenant context is required." });

            var dto = await _dashboard
                .GetDashboardAsync(_tenantAccessor.TenantId!.Value, readerUserId, cancellationToken)
                .ConfigureAwait(false);
            return Ok(dto);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(new { message = ex.Message });
        }
    }
}
