using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Authenticated mandant billing license endpoints for Managers (tenant JWT required).
/// Anonymous POS deployment endpoints remain on the main <see cref="LicenseController"/> partial.
/// </summary>
public partial class LicenseController
{
    /// <summary>Current tenant mandant license status from billing <c>license_sales</c>.</summary>
    /// <remarks>
    /// Route is <c>GET /api/license/billing/status</c> (not <c>/status</c>) so the anonymous deployment
    /// snapshot at <c>GET /api/license/status</c> stays available for POS bootstrap.
    /// </remarks>
    [Authorize]
    [HttpGet("billing/status")]
    [ProducesResponseType(typeof(TenantLicenseStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetBillingStatus(CancellationToken ct)
    {
        if (!TryResolveTenantId(out var errorResult, out var tenantId))
            return errorResult!;

        var status = await _tenantLicenseService
            .GetCurrentStatusAsync(tenantId, ct)
            .ConfigureAwait(false);
        return Ok(status);
    }

    /// <summary>Activate a billing-format license key for the current tenant.</summary>
    /// <remarks>
    /// Route is <c>POST /api/license/billing/activate</c> so unified anonymous
    /// <c>POST /api/license/activate</c> (deployment + optional billing branch) stays unchanged for POS/FA.
    /// </remarks>
    [Authorize]
    [HttpPost("billing/activate")]
    [ProducesResponseType(typeof(ActivationResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ActivateBillingLicense(
        [FromBody] MandantLicenseKeyRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (!TryResolveTenantId(out var tenantError, out var tenantId))
            return tenantError!;

        if (!TryResolveActorUserId(out var userError, out var userId))
            return userError!;

        try
        {
            var result = await _tenantLicenseService
                .ActivateLicenseAsync(tenantId, request.LicenseKey.Trim(), userId, ct)
                .ConfigureAwait(false);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>Extend the current tenant mandant license via a new billing sale key.</summary>
    [Authorize]
    [HttpPost("extend")]
    [ProducesResponseType(typeof(ExtendResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ExtendLicense(
        [FromBody] ExtendLicenseRequest request,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        if (!TryResolveTenantId(out var tenantError, out var tenantId))
            return tenantError!;

        if (!TryResolveActorUserId(out var userError, out var userId))
            return userError!;

        try
        {
            var result = await _tenantLicenseService
                .ExtendLicenseAsync(tenantId, request.LicenseKey.Trim(), userId, ct)
                .ConfigureAwait(false);

            if (!result.Success)
                return BadRequest(new { message = result.Message });

            _logger.LogInformation(
                "Mandant billing license extended for tenant {TenantId} by user {ActorUserId}",
                tenantId,
                userId);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    private bool TryResolveTenantId(out IActionResult? errorResult, out Guid tenantId)
    {
        tenantId = Guid.Empty;
        errorResult = null;

        var fromAccessor = _tenantAccessor.TenantId;
        if (!fromAccessor.HasValue || fromAccessor.Value == Guid.Empty)
        {
            errorResult = BadRequest(new { message = "Tenant context required" });
            return false;
        }

        tenantId = fromAccessor.Value;
        return true;
    }

    private bool TryResolveActorUserId(out IActionResult? errorResult, out Guid actorUserId)
    {
        actorUserId = Guid.Empty;
        errorResult = null;

        var userId = User.GetActorUserId();
        if (string.IsNullOrWhiteSpace(userId) || !Guid.TryParse(userId, out actorUserId) || actorUserId == Guid.Empty)
        {
            errorResult = Unauthorized(new { message = "Authenticated user id is required." });
            return false;
        }

        return true;
    }
}
