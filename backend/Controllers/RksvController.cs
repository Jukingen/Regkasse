using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Rksv;

namespace KasseAPI_Final.Controllers;

/// <summary>RKSV auxiliary endpoints (status/reminders).</summary>
[Authorize]
[ApiController]
[Route("api/rksv")]
public sealed class RksvController : ControllerBase
{
    private readonly IMonatsbelegReminderService _monatsbelegReminder;
    private readonly IRksvReminderService _rksvReminder;
    private readonly IRksvEnvironmentService _rksvEnvironment;

    public RksvController(
        IMonatsbelegReminderService monatsbelegReminder,
        IRksvReminderService rksvReminder,
        IRksvEnvironmentService rksvEnvironment)
    {
        _monatsbelegReminder = monatsbelegReminder;
        _rksvReminder = rksvReminder;
        _rksvEnvironment = rksvEnvironment;
    }

    /// <summary>RKSV deployment environment (Demo/Production) for POS and Admin badges.</summary>
    [HttpGet("environment")]
    [ProducesResponseType(typeof(RksvEnvironmentStatusDto), StatusCodes.Status200OK)]
    public ActionResult<RksvEnvironmentStatusDto> GetEnvironment() =>
        Ok(RksvEnvironmentStatusDto.FromService(_rksvEnvironment));

    /// <summary>RKSV environment status (simulation flag, labels, TSE display) for POS and Admin UI.</summary>
    [HttpGet("status")]
    [ProducesResponseType(typeof(RksvStatusDto), StatusCodes.Status200OK)]
    public ActionResult<RksvStatusDto> GetStatus() =>
        Ok(RksvStatusDto.FromService(_rksvEnvironment));

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

    /// <summary>Monatsbeleg status for all tenant cash registers (admin dashboard overview).</summary>
    [HttpGet("monatsbeleg/status-overview")]
    [HasPermission(AppPermissions.CashRegisterView)]
    [ProducesResponseType(typeof(IReadOnlyList<MonatsbelegRegisterStatusItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<MonatsbelegRegisterStatusItemDto>>> GetMonatsbelegStatusOverview(
        CancellationToken cancellationToken)
    {
        var items = await _monatsbelegReminder.GetMonatsbelegStatusOverviewAsync(cancellationToken)
            .ConfigureAwait(false);
        return Ok(items);
    }

    /// <summary>Unified RKSV reminder status for all tenant cash registers (admin dashboard overview).</summary>
    [HttpGet("reminder/status-overview")]
    [HasPermission(AppPermissions.CashRegisterView)]
    [ProducesResponseType(typeof(IReadOnlyList<RksvReminderRegisterStatusItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<RksvReminderRegisterStatusItemDto>>> GetRksvReminderStatusOverview(
        CancellationToken cancellationToken)
    {
        var items = await _rksvReminder.GetRksvStatusOverviewAsync(cancellationToken)
            .ConfigureAwait(false);
        return Ok(items);
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
