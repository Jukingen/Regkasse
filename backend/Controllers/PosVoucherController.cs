using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Vouchers;
using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Controllers;

/// <summary>POS Gutschein validation (no balance mutation). Codes are hashed at rest; lookup via <see cref="IVoucherService"/> only.</summary>
[ApiController]
[Route("api/pos/vouchers")]
[Authorize]
[HasPermission(AppPermissions.PaymentTake)]
public class PosVoucherController : BaseController
{
    private readonly IVoucherService _voucherService;
    private readonly ISettingsTenantResolver _tenantResolver;

    public PosVoucherController(
        IVoucherService voucherService,
        ISettingsTenantResolver tenantResolver,
        ILogger<PosVoucherController> logger)
        : base(logger)
    {
        _voucherService = voucherService;
        _tenantResolver = tenantResolver;
    }

    /// <summary>
    /// Validate a voucher code for the current tenant.
    /// Accepts <c>voucherCode</c> or <c>code</c> in the body. Success returns masked code only (never plaintext).
    /// </summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(VoucherValidateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VoucherValidateResponse>> ValidateVoucher(
        [FromBody] ValidateVoucherRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var result = await _voucherService.ValidateVoucherByCodeAsync(
            tenantId,
            request.VoucherCode,
            request.Amount,
            cancellationToken);

        if (!result.Ok)
        {
            var error = MapErrorMessage(result);
            if (string.Equals(result.ErrorCode, VoucherValidateErrorCodes.NotFound, StringComparison.Ordinal))
                return NotFound(new { error, ok = false, errorCode = result.ErrorCode, message = result.Message });
            return BadRequest(new { error, ok = false, errorCode = result.ErrorCode, message = result.Message });
        }

        return Ok(result);
    }

    private static string MapErrorMessage(VoucherValidateResponse result)
    {
        if (!string.IsNullOrWhiteSpace(result.Message))
            return result.Message!;

        return result.ErrorCode switch
        {
            VoucherValidateErrorCodes.Expired => "Voucher expired",
            VoucherValidateErrorCodes.NoBalance or VoucherValidateErrorCodes.Redeemed => "Voucher already used",
            VoucherValidateErrorCodes.NotFound => "Voucher not found",
            _ => "Voucher validation failed",
        };
    }
}
