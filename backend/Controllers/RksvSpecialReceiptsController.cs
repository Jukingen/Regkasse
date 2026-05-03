using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// RKSV Sonderbelege (admin API). Not exposed on POS payment routes.
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
}
