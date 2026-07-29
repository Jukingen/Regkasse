using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.License;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

public sealed partial class AdminLicenseController
{
    /// <summary>
    /// Super Admin renewal conversion funnel (reminder → page view → renew → activate).
    /// </summary>
    [HttpGet("renewal-funnel")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [HasPermission(AppPermissions.SystemCritical)]
    [ProducesResponseType(typeof(LicenseRenewalFunnelDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LicenseRenewalFunnelDto>> GetRenewalFunnel(
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        [FromServices] ILicenseRenewalFunnelService funnelService = null!,
        CancellationToken cancellationToken = default)
    {
        var result = await funnelService
            .GetFunnelAsync(new LicenseRenewalFunnelQuery(fromUtc, toUtc), cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>
    /// Records that the current tenant opened the license renewal UI (deduped per UTC day).
    /// </summary>
    [HttpPost("renewal-funnel/page-view")]
    [HasPermission(AppPermissions.LicenseManage)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RecordRenewalFunnelPageView(
        [FromServices] ILicenseRenewalFunnelService funnelService = null!,
        [FromServices] ICurrentTenantAccessor tenantAccessor = null!,
        CancellationToken cancellationToken = default)
    {
        var tenantId = tenantAccessor.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            return NotFound();

        var actorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actorUserId))
            return Unauthorized();

        var actorRole = User.FindFirstValue(ClaimTypes.Role)
            ?? User.FindFirstValue("role")
            ?? "Manager";

        await funnelService
            .RecordPageViewAsync(tenantId.Value, actorUserId, actorRole, cancellationToken)
            .ConfigureAwait(false);

        return NoContent();
    }
}
