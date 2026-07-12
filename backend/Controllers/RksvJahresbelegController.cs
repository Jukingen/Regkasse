using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs.Rksv;
using KasseAPI_Final.Services.Rksv;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>RKSV Phase 3 Jahresbeleg snapshot API (Monatsbeleg aggregation + TSE chain).</summary>
[Authorize]
[ApiController]
[Route("api/rksv/jahresbeleg")]
[Produces("application/json")]
public sealed class RksvJahresbelegController : ControllerBase
{
    private readonly IJahresbelegService _jahresbelegService;
    private readonly IRksvEnvironmentService _rksvEnv;
    private readonly ILogger<RksvJahresbelegController> _logger;

    public RksvJahresbelegController(
        IJahresbelegService jahresbelegService,
        IRksvEnvironmentService rksvEnv,
        ILogger<RksvJahresbelegController> logger)
    {
        _jahresbelegService = jahresbelegService;
        _rksvEnv = rksvEnv;
        _logger = logger;
    }

    [HttpPost("create")]
    [HasPermission(AppPermissions.RksvJahresbelegCreate)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateJahresbeleg(
        [FromBody] CreateRksvJahresbelegRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CashRegisterId == Guid.Empty)
        {
            return BadRequest(new { success = false, message = "CashRegisterId is required." });
        }

        try
        {
            var result = await _jahresbelegService.CreateJahresbelegAsync(
                request.CashRegisterId,
                request.Year,
                request.UseDecemberMonatsbeleg,
                cancellationToken);

            return Ok(new
            {
                success = true,
                data = result,
                environment = _rksvEnv.GetEnvironmentDisplayName(),
                isSimulated = _rksvEnv.IsTseSimulated(),
                message = _rksvEnv.IsDemoMode()
                    ? "Jahresbeleg im Demo-Modus erstellt (TSE simuliert)"
                    : "Jahresbeleg erfolgreich erstellt",
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Jahresbeleg create blocked for register {RegisterId} year {Year}",
                request.CashRegisterId, request.Year);
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("{cashRegisterId:guid}/{year:int}")]
    [HasPermission(AppPermissions.RksvJahresbelegView)]
    [ProducesResponseType(typeof(JahresbelegResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetJahresbeleg(
        Guid cashRegisterId,
        int year,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _jahresbelegService.GetJahresbelegAsync(
                cashRegisterId,
                year,
                cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Jahresbeleg nicht gefunden" });
        }
    }

    [HttpGet("history/{cashRegisterId:guid}")]
    [HasPermission(AppPermissions.RksvJahresbelegView)]
    [ProducesResponseType(typeof(IReadOnlyList<JahresbelegSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJahresbelegHistory(
        Guid cashRegisterId,
        CancellationToken cancellationToken)
    {
        var history = await _jahresbelegService.GetJahresbelegHistoryAsync(
            cashRegisterId,
            cancellationToken);
        return Ok(history);
    }

    [HttpGet("exists/{cashRegisterId:guid}/{year:int}")]
    [HasPermission(AppPermissions.RksvJahresbelegView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> JahresbelegExists(
        Guid cashRegisterId,
        int year,
        CancellationToken cancellationToken)
    {
        var exists = await _jahresbelegService.JahresbelegExistsAsync(
            cashRegisterId,
            year,
            cancellationToken);
        return Ok(new { exists });
    }
}
