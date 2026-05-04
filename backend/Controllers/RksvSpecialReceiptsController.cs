using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Rksv;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// RKSV Sonderbelege (admin + authorized POS for Startbeleg / Monatsbeleg / Jahresbeleg). Not exposed on POS payment routes.
/// </summary>
[Authorize]
[ApiController]
[Route("api/rksv/special-receipts")]
public sealed class RksvSpecialReceiptsController : ControllerBase
{
    private readonly IRksvSpecialReceiptService _specialReceipts;
    private readonly IPosCriticalActionAuditService _posCriticalAudit;
    private readonly ILogger<RksvSpecialReceiptsController> _logger;

    public RksvSpecialReceiptsController(
        IRksvSpecialReceiptService specialReceipts,
        IPosCriticalActionAuditService posCriticalAudit,
        ILogger<RksvSpecialReceiptsController> logger)
    {
        _specialReceipts = specialReceipts;
        _posCriticalAudit = posCriticalAudit;
        _logger = logger;
    }

    private Task AuditSpecialSuccessAsync(string userId, Guid cashRegisterId, string receiptKind, Guid paymentId, CancellationToken ct) =>
        _posCriticalAudit.LogSpecialReceiptOutcomeAsync(userId, cashRegisterId, receiptKind, "success",
            AuditLogStatus.Success, null, paymentId, ct);

    private Task AuditSpecialBlockedAsync(string userId, Guid cashRegisterId, string receiptKind, string? errorCode, CancellationToken ct) =>
        _posCriticalAudit.LogSpecialReceiptOutcomeAsync(userId, cashRegisterId, receiptKind, "blocked",
            AuditLogStatus.ValidationError, errorCode, null, ct);

    private Task AuditSpecialFailedAsync(string userId, Guid cashRegisterId, string receiptKind, string? errorCode, CancellationToken ct) =>
        _posCriticalAudit.LogSpecialReceiptOutcomeAsync(userId, cashRegisterId, receiptKind, "failed",
            AuditLogStatus.Failed, errorCode ?? "SPECIAL_RECEIPT_FAILED", null, ct);

    private static bool IsRksvGuardConflict(string errorCode) =>
        errorCode is RksvGuardErrorCodes.DuplicateStartbeleg
            or RksvGuardErrorCodes.DuplicateMonatsbeleg
            or RksvGuardErrorCodes.DuplicateJahresbeleg
            or RksvGuardErrorCodes.DuplicateSchlussbeleg
            or RksvGuardErrorCodes.RegisterAlreadyDecommissioned;

    /// <summary>Creates a Monats-Nullbeleg (zero TSE receipt in normal Beleg sequence).</summary>
    [HttpPost("nullbeleg")]
    [HasPermission(AppPermissions.RksvNullbelegCreate)]
    [ProducesResponseType(typeof(CreateNullbelegResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateNullbelegResponse>> CreateNullbeleg(
        [FromBody] CreateNullbelegRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var result = await _specialReceipts.CreateNullbelegAsync(request, userId, cancellationToken);
            await AuditSpecialSuccessAsync(userId, request.CashRegisterId, "nullbeleg", result.PaymentId, cancellationToken);
            return Ok(result);
        }
        catch (RksvOperationGuardException ex)
        {
            await AuditSpecialBlockedAsync(userId, request.CashRegisterId, "nullbeleg", ex.ErrorCode, cancellationToken);
            if (IsRksvGuardConflict(ex.ErrorCode))
                return Conflict(new { message = ex.Message, code = ex.ErrorCode });
            _logger.LogWarning(ex, "Nullbeleg rejected");
            return BadRequest(new { message = ex.Message, code = ex.ErrorCode });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                await AuditSpecialBlockedAsync(userId, request.CashRegisterId, "nullbeleg", "DUPLICATE_RECEIPT", cancellationToken);
                return Conflict(new { message = ex.Message });
            }
            await AuditSpecialFailedAsync(userId, request.CashRegisterId, "nullbeleg", null, cancellationToken);
            _logger.LogWarning(ex, "Nullbeleg rejected");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Creates RKSV Startbeleg (first zero TSE receipt for the register).</summary>
    [HttpPost("startbeleg")]
    [HasPermission(AppPermissions.RksvStartbelegCreate)]
    [ProducesResponseType(typeof(CreateStartbelegResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateStartbelegResponse>> CreateStartbeleg(
        [FromBody] CreateStartbelegRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var result = await _specialReceipts.CreateStartbelegAsync(request, userId, cancellationToken);
            await AuditSpecialSuccessAsync(userId, request.CashRegisterId, "startbeleg", result.PaymentId, cancellationToken);
            return Ok(result);
        }
        catch (RksvOperationGuardException ex)
        {
            await AuditSpecialBlockedAsync(userId, request.CashRegisterId, "startbeleg", ex.ErrorCode, cancellationToken);
            if (IsRksvGuardConflict(ex.ErrorCode))
                return Conflict(new { message = ex.Message, code = ex.ErrorCode });
            _logger.LogWarning(ex, "Startbeleg rejected");
            return BadRequest(new { message = ex.Message, code = ex.ErrorCode });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                await AuditSpecialBlockedAsync(userId, request.CashRegisterId, "startbeleg", "DUPLICATE_RECEIPT", cancellationToken);
                return Conflict(new { message = ex.Message });
            }
            await AuditSpecialFailedAsync(userId, request.CashRegisterId, "startbeleg", null, cancellationToken);
            _logger.LogWarning(ex, "Startbeleg rejected");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Creates RKSV Monatsbeleg (monthly zero TSE receipt for the current Vienna calendar month).</summary>
    [HttpPost("monatsbeleg")]
    [HasPermission(AppPermissions.RksvMonatsbelegCreate)]
    [ProducesResponseType(typeof(CreateMonatsbelegResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateMonatsbelegResponse>> CreateMonatsbeleg(
        [FromBody] CreateMonatsbelegRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var result = await _specialReceipts.CreateMonatsbelegAsync(request, userId, cancellationToken);
            await AuditSpecialSuccessAsync(userId, request.CashRegisterId, "monatsbeleg", result.PaymentId, cancellationToken);
            return Ok(result);
        }
        catch (RksvOperationGuardException ex)
        {
            await AuditSpecialBlockedAsync(userId, request.CashRegisterId, "monatsbeleg", ex.ErrorCode, cancellationToken);
            if (IsRksvGuardConflict(ex.ErrorCode))
                return Conflict(new { message = ex.Message, code = ex.ErrorCode });
            _logger.LogWarning(ex, "Monatsbeleg rejected");
            return BadRequest(new { message = ex.Message, code = ex.ErrorCode });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                await AuditSpecialBlockedAsync(userId, request.CashRegisterId, "monatsbeleg", "DUPLICATE_RECEIPT", cancellationToken);
                return Conflict(new { message = ex.Message });
            }
            await AuditSpecialFailedAsync(userId, request.CashRegisterId, "monatsbeleg", null, cancellationToken);
            _logger.LogWarning(ex, "Monatsbeleg rejected");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Creates RKSV Jahresbeleg (annual zero TSE receipt for a Vienna calendar year).</summary>
    [HttpPost("jahresbeleg")]
    [HasPermission(AppPermissions.RksvJahresbelegCreate)]
    [ProducesResponseType(typeof(CreateJahresbelegResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateJahresbelegResponse>> CreateJahresbeleg(
        [FromBody] CreateJahresbelegRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var result = await _specialReceipts.CreateJahresbelegAsync(request, userId, cancellationToken);
            await AuditSpecialSuccessAsync(userId, request.CashRegisterId, "jahresbeleg", result.PaymentId, cancellationToken);
            return Ok(result);
        }
        catch (RksvOperationGuardException ex)
        {
            await AuditSpecialBlockedAsync(userId, request.CashRegisterId, "jahresbeleg", ex.ErrorCode, cancellationToken);
            if (IsRksvGuardConflict(ex.ErrorCode))
                return Conflict(new { message = ex.Message, code = ex.ErrorCode });
            _logger.LogWarning(ex, "Jahresbeleg rejected");
            return BadRequest(new { message = ex.Message, code = ex.ErrorCode });
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
            {
                await AuditSpecialBlockedAsync(userId, request.CashRegisterId, "jahresbeleg", "DUPLICATE_RECEIPT", cancellationToken);
                return Conflict(new { message = ex.Message });
            }
            await AuditSpecialFailedAsync(userId, request.CashRegisterId, "jahresbeleg", null, cancellationToken);
            _logger.LogWarning(ex, "Jahresbeleg rejected");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Creates RKSV Schlussbeleg (Endbeleg) and permanently decommissions the cash register.</summary>
    [HttpPost("schlussbeleg")]
    [HasPermission(AppPermissions.RksvSchlussbelegCreate)]
    [ProducesResponseType(typeof(CreateSchlussbelegResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateSchlussbelegResponse>> CreateSchlussbeleg(
        [FromBody] CreateSchlussbelegRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var userId = User.GetActorUserId();
        if (string.IsNullOrEmpty(userId))
            return Unauthorized();

        try
        {
            var result = await _specialReceipts.CreateSchlussbelegAsync(request, userId, cancellationToken);
            await AuditSpecialSuccessAsync(userId, request.CashRegisterId, "schlussbeleg", result.PaymentId, cancellationToken);
            return Ok(result);
        }
        catch (RksvOperationGuardException ex)
        {
            await AuditSpecialBlockedAsync(userId, request.CashRegisterId, "schlussbeleg", ex.ErrorCode, cancellationToken);
            if (IsRksvGuardConflict(ex.ErrorCode))
                return Conflict(new { message = ex.Message, code = ex.ErrorCode });
            _logger.LogWarning(ex, "Schlussbeleg rejected");
            return BadRequest(new { message = ex.Message, code = ex.ErrorCode });
        }
        catch (InvalidOperationException ex)
        {
            // Duplicate Schlussbeleg row, or register already decommissioned — deterministic conflicts (prefer 409).
            if (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("already permanently decommissioned", StringComparison.OrdinalIgnoreCase))
            {
                await AuditSpecialBlockedAsync(userId, request.CashRegisterId, "schlussbeleg", "DUPLICATE_OR_DECOMMISSIONED", cancellationToken);
                return Conflict(new { message = ex.Message });
            }
            await AuditSpecialFailedAsync(userId, request.CashRegisterId, "schlussbeleg", null, cancellationToken);
            _logger.LogWarning(ex, "Schlussbeleg rejected");
            return BadRequest(new { message = ex.Message });
        }
    }
}
