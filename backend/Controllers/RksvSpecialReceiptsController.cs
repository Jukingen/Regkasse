using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
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
    private readonly ILogger<RksvSpecialReceiptsController> _logger;

    public RksvSpecialReceiptsController(
        IRksvSpecialReceiptService specialReceipts,
        ILogger<RksvSpecialReceiptsController> logger)
    {
        _specialReceipts = specialReceipts;
        _logger = logger;
    }

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
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = ex.Message });
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
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = ex.Message });
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
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = ex.Message });
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
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            if (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = ex.Message });
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
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            // Duplicate Schlussbeleg row, or register already decommissioned — deterministic conflicts (prefer 409).
            if (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("already permanently decommissioned", StringComparison.OrdinalIgnoreCase))
                return Conflict(new { message = ex.Message });
            _logger.LogWarning(ex, "Schlussbeleg rejected");
            return BadRequest(new { message = ex.Message });
        }
    }
}
