using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Controllers;

/// <summary>RKSV auxiliary endpoints (status/reminders).</summary>
[Authorize]
[ApiController]
[Route("api/rksv")]
public sealed class RksvController : ControllerBase
{
    private readonly IMonatsbelegReminderService _monatsbelegReminder;
    private readonly IRksvReminderService _rksvReminder;

    public RksvController(
        IMonatsbelegReminderService monatsbelegReminder,
        IRksvReminderService rksvReminder)
    {
        _monatsbelegReminder = monatsbelegReminder;
        _rksvReminder = rksvReminder;
    }

    /// <summary>Monatsbeleg reminder status for the given cash register (Vienna calendar month).</summary>
    [HttpGet("monatsbeleg/status/{cashRegisterId:guid}")]
    [HasPermission(AppPermissions.CashRegisterView)]
    [ProducesResponseType(typeof(MonatsbelegStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MonatsbelegStatusDto>> GetMonatsbelegStatus(
        Guid cashRegisterId,
        CancellationToken cancellationToken)
    {
        var status = await _monatsbelegReminder.GetMonatsbelegStatusAsync(cashRegisterId, cancellationToken)
            .ConfigureAwait(false);
        if (status == null)
            return NotFound();
        return Ok(status);
    }

    /// <summary>Unified RKSV reminder status (Startbeleg / Monatsbeleg / Jahresbeleg) for the cash register.</summary>
    [HttpGet("reminder/status/{cashRegisterId:guid}")]
    [HasPermission(AppPermissions.CashRegisterView)]
    [ProducesResponseType(typeof(RksvReminderStatusDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RksvReminderStatusDto>> GetRksvReminderStatus(
        Guid cashRegisterId,
        CancellationToken cancellationToken)
    {
        var status = await _rksvReminder.GetRksvStatusAsync(cashRegisterId, cancellationToken)
            .ConfigureAwait(false);
        if (status == null)
            return NotFound();
        return Ok(status);
    }
}
