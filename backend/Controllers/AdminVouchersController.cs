using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.Vouchers;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Admin Gutscheine: issuance, listing, cancellation, and ledger audit.</summary>
[Authorize]
[ApiController]
[Route("api/admin/vouchers")]
[Produces("application/json")]
public class AdminVouchersController : ControllerBase
{
    private readonly IAdminVoucherService _adminVouchers;
    private readonly ISettingsTenantResolver _tenantResolver;

    public AdminVouchersController(IAdminVoucherService adminVouchers, ISettingsTenantResolver tenantResolver)
    {
        _adminVouchers = adminVouchers;
        _tenantResolver = tenantResolver;
    }

    /// <summary>Paged voucher list (masked code only).</summary>
    [HttpGet]
    [HasPermission(AppPermissions.VoucherRead)]
    public async Task<ActionResult<AdminVoucherListResponse>> GetList(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? q = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var result = await _adminVouchers.ListAsync(tenantId, page, pageSize, q, cancellationToken).ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>Voucher metadata (no plaintext code).</summary>
    [HttpGet("{id:guid}")]
    [HasPermission(AppPermissions.VoucherRead)]
    public async Task<ActionResult<AdminVoucherDetailDto>> GetById(Guid id, CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var row = await _adminVouchers.GetDetailAsync(tenantId, id, cancellationToken).ConfigureAwait(false);
        if (row == null)
            return NotFound();
        return Ok(row);
    }

    /// <summary>Append-only ledger lines (audit).</summary>
    [HttpGet("{id:guid}/ledger")]
    [HasPermission(AppPermissions.VoucherAuditView)]
    public async Task<ActionResult<IReadOnlyList<AdminVoucherLedgerLineDto>>> GetLedger(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var exists = await _adminVouchers.GetDetailAsync(tenantId, id, cancellationToken).ConfigureAwait(false);
        if (exists == null)
            return NotFound();
        var lines = await _adminVouchers.GetLedgerAsync(tenantId, id, cancellationToken).ConfigureAwait(false);
        return Ok(lines);
    }

    /// <summary>Issue a new voucher. Plaintext code is returned once in the response body.</summary>
    [HttpPost]
    [HasPermission(AppPermissions.VoucherCreate)]
    public async Task<ActionResult<CreateAdminVoucherResponse>> Create(
        [FromBody] CreateAdminVoucherRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var (response, error) = await _adminVouchers.CreateAsync(tenantId, userId, request, cancellationToken).ConfigureAwait(false);
        if (error != null)
        {
            return error switch
            {
                "UNSUPPORTED_CURRENCY" => BadRequest(new { error = error, message = "Only EUR is supported for vouchers." }),
                "EXPIRY_REQUIRED" or "EXPIRY_INVALID" or "EXPIRY_MODE_INVALID" => BadRequest(new { error = error }),
                "CODE_GENERATION_FAILED" => StatusCode(503, new { error = error }),
                _ => BadRequest(new { error = error }),
            };
        }

        return CreatedAtAction(nameof(GetById), new { id = response!.Id }, response);
    }

    /// <summary>Cancel a voucher with a mandatory reason; writes a Cancel ledger line.</summary>
    [HttpPost("{id:guid}/cancel")]
    [HasPermission(AppPermissions.VoucherCancel)]
    public async Task<IActionResult> Cancel(
        Guid id,
        [FromBody] CancelAdminVoucherRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        var (ok, error) = await _adminVouchers.CancelAsync(tenantId, userId, id, request.Reason, cancellationToken)
            .ConfigureAwait(false);
        if (!ok)
        {
            return error switch
            {
                "NOT_FOUND" => NotFound(),
                "ALREADY_CANCELLED" => Conflict(new { error = error }),
                "NOT_CANCELLABLE" => Conflict(new { error = error }),
                "REASON_TOO_SHORT" => BadRequest(new { error = error }),
                _ => BadRequest(new { error = error ?? "CANCEL_FAILED" }),
            };
        }

        return NoContent();
    }
}
