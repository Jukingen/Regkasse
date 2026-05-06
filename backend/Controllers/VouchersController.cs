using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Controllers.Base;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Vouchers;
using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Controllers;

/// <summary>POS voucher issuance — Mehrzweckgutschein sale (RKSV-non-fiscal, no TSE).</summary>
[ApiController]
[Route("api/vouchers")]
[Authorize]
public class VouchersController : BaseController
{
    private readonly IVoucherIssuanceService _issuance;
    private readonly ISettingsTenantResolver _tenantResolver;

    public VouchersController(
        IVoucherIssuanceService issuance,
        ISettingsTenantResolver tenantResolver,
        ILogger<VouchersController> logger)
        : base(logger)
    {
        _issuance = issuance;
        _tenantResolver = tenantResolver;
    }

    /// <summary>
    /// Issue a stored-value voucher. No RKSV/TSE signing (issuance excluded from DEP). Plain code is returned once only — not logged server-side.
    /// </summary>
    [HttpPost("issue")]
    [HasPermission(AppPermissions.VoucherIssue)]
    [ProducesResponseType(typeof(IssueVoucherResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<IssueVoucherResponse>> Issue(
        [FromBody] IssueVoucherRequest request,
        CancellationToken cancellationToken)
    {
        // RKSV-non-fiscal path: issuance must never imply TSE; client-provided falsy flags carry no semantic weight here.
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var userRole = User.GetActorRole();
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);

        var (response, error) = await _issuance
            .IssueAsync(tenantId, userId, userRole ?? "Unknown", request, cancellationToken)
            .ConfigureAwait(false);

        if (error != null)
        {
            return error switch
            {
                "UNSUPPORTED_CURRENCY" => BadRequest(new { error, message = "Only EUR is supported for vouchers." }),
                "INVALID_DATE_RANGE" => BadRequest(new { error, message = "ValidUntil must be after ValidFrom." }),
                "CUSTOMER_NOT_FOUND" => BadRequest(new { error, message = "Customer not found or inactive." }),
                "CODE_GENERATION_FAILED" => StatusCode(503, new { error }),
                _ => BadRequest(new { error }),
            };
        }

        return StatusCode(StatusCodes.Status201Created, response);
    }
}

