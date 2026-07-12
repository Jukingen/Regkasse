using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs.Rksv;
using KasseAPI_Final.Services.Rksv;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>RKSV Phase 2 Monatsbeleg snapshot API (daily-closing aggregation + TSE chain).</summary>
[Authorize]
[ApiController]
[Route("api/rksv/monatsbeleg")]
[Produces("application/json")]
public sealed class RksvMonatsbelegController : ControllerBase
{
    private readonly IMonatsbelegService _monatsbelegService;
    private readonly IRksvEnvironmentService _rksvEnv;
    private readonly ILogger<RksvMonatsbelegController> _logger;

    public RksvMonatsbelegController(
        IMonatsbelegService monatsbelegService,
        IRksvEnvironmentService rksvEnv,
        ILogger<RksvMonatsbelegController> logger)
    {
        _monatsbelegService = monatsbelegService;
        _rksvEnv = rksvEnv;
        _logger = logger;
    }

    [HttpPost("create")]
    [HasPermission(AppPermissions.RksvMonatsbelegCreate)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateMonatsbeleg(
        [FromBody] CreateRksvMonatsbelegRequest request,
        CancellationToken cancellationToken)
    {
        if (request.CashRegisterId == Guid.Empty)
        {
            return BadRequest(new { success = false, message = "CashRegisterId is required." });
        }

        try
        {
            var result = await _monatsbelegService.CreateMonatsbelegAsync(
                request.CashRegisterId,
                request.Year,
                request.Month,
                cancellationToken);

            return Ok(new
            {
                success = true,
                data = result,
                environment = _rksvEnv.GetEnvironmentDisplayName(),
                isSimulated = _rksvEnv.IsTseSimulated(),
                message = _rksvEnv.IsDemoMode()
                    ? "Monatsbeleg im Demo-Modus erstellt (TSE simuliert)"
                    : "Monatsbeleg erfolgreich erstellt",
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Monatsbeleg create blocked for register {RegisterId} {Year}-{Month:00}",
                request.CashRegisterId, request.Year, request.Month);
            return BadRequest(new { success = false, message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, message = ex.Message });
        }
    }

    [HttpGet("{cashRegisterId:guid}/{year:int}/{month:int}")]
    [HasPermission(AppPermissions.RksvMonatsbelegView)]
    [ProducesResponseType(typeof(MonatsbelegResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMonatsbeleg(
        Guid cashRegisterId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _monatsbelegService.GetMonatsbelegAsync(
                cashRegisterId,
                year,
                month,
                cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Monatsbeleg nicht gefunden" });
        }
    }

    [HttpGet("history/{cashRegisterId:guid}")]
    [HasPermission(AppPermissions.RksvMonatsbelegView)]
    [ProducesResponseType(typeof(IReadOnlyList<MonatsbelegSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMonatsbelegHistory(
        Guid cashRegisterId,
        [FromQuery] int? year,
        CancellationToken cancellationToken)
    {
        var history = await _monatsbelegService.GetMonatsbelegHistoryAsync(
            cashRegisterId,
            year,
            cancellationToken);
        return Ok(history);
    }

    [HttpGet("exists/{cashRegisterId:guid}/{year:int}/{month:int}")]
    [HasPermission(AppPermissions.RksvMonatsbelegView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> MonatsbelegExists(
        Guid cashRegisterId,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var exists = await _monatsbelegService.MonatsbelegExistsAsync(
            cashRegisterId,
            year,
            month,
            cancellationToken);
        return Ok(new { exists });
    }
}
