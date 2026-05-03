using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services.Vouchers;
using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Controllers;

/// <summary>POS Gutschein validation (no balance mutation).</summary>
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

    /// <summary>Validate a voucher code for the current tenant (masked output only).</summary>
    [HttpPost("validate")]
    [ProducesResponseType(typeof(VoucherValidateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ValidateVoucher([FromBody] ValidateVoucherRequest request, CancellationToken cancellationToken)
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
            if (string.Equals(result.ErrorCode, VoucherValidateErrorCodes.NotFound, StringComparison.Ordinal))
                return NotFound(result);
            return BadRequest(result);
        }

        return Ok(result);
    }
}
