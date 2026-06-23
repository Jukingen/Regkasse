using KasseAPI_Final.Authorization;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Billing;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

public sealed partial class AdminLicenseController
{
    /// <summary>
    /// Extend the current tenant mandant license via billing <c>license_sales</c> key (Manager self-service).
    /// </summary>
    [HttpPost("extend")]
    [HasPermission(AppPermissions.SettingsManage)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExtendLicense(
        [FromBody] ExtendLicenseRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var tenantId = _currentTenantAccessor.TenantId;
        if (!tenantId.HasValue || tenantId.Value == Guid.Empty)
            return BadRequest(new { message = "Tenant context required." });

        var (accessibleTenantId, accessError) = await ResolveAccessibleMandantTenantIdAsync(
                tenantId.Value,
                ct)
            .ConfigureAwait(false);
        if (accessError != null)
            return accessError;

        if (accessibleTenantId is not Guid effectiveTenantId)
            return BadRequest(new { message = "Tenant context required." });

        var actorUserIdText = User.GetActorUserId();
        if (!Guid.TryParse(actorUserIdText, out var actorUserId) || actorUserId == Guid.Empty)
            return BadRequest(new { message = "User context required." });

        var result = await _billingTenantLicenseService
            .ExtendLicenseAsync(
                effectiveTenantId,
                request.LicenseKey.Trim(),
                actorUserId,
                ct)
            .ConfigureAwait(false);

        if (!result.Success)
            return BadRequest(new { message = result.Message });

        _logger.LogInformation(
            "Billing mandant license extended for tenant {TenantId} by user {ActorUserId}",
            effectiveTenantId,
            actorUserIdText);

        return Ok(new
        {
            success = true,
            message = result.Message,
            licenseKey = result.LicenseKey,
            validUntil = result.ValidUntilUtc,
            plan = result.LicensePlan,
        });
    }
}
