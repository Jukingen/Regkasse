using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Admin calendar for daily-closing status (Vienna business days). Mandant-scoped; not a Super Admin SaaS prefix.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/daily-closing")]
[Produces("application/json")]
[Tags("Admin")]
public sealed class AdminDailyClosingCalendarController : ControllerBase
{
    private readonly IDailyClosingService _dailyClosingService;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly AppDbContext _context;

    public AdminDailyClosingCalendarController(
        IDailyClosingService dailyClosingService,
        ISettingsTenantResolver settingsTenantResolver,
        AppDbContext context)
    {
        _dailyClosingService = dailyClosingService;
        _settingsTenantResolver = settingsTenantResolver;
        _context = context;
    }

    /// <summary>
    /// GET: month grid of closed / open / empty / future Vienna days for the ambient tenant.
    /// </summary>
    [HttpGet("calendar")]
    [HasPermission(AppPermissions.DailyClosingView)]
    [ProducesResponseType(typeof(DailyClosingCalendarDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DailyClosingCalendarDto>> GetCalendar(
        [FromQuery] int year,
        [FromQuery] int month,
        [FromQuery] Guid? cashRegisterId,
        CancellationToken cancellationToken)
    {
        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);

        if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
        {
            var ok = await _context.CashRegisters.AsNoTracking()
                .AnyAsync(cr => cr.Id == cashRegisterId.Value && cr.TenantId == tenantId, cancellationToken);
            if (!ok)
            {
                return NotFound(new
                {
                    message = "Cash register is not in the current tenant",
                    code = "ADMIN_DAILY_CLOSING_REGISTER_NOT_FOUND",
                });
            }
        }

        try
        {
            var result = await _dailyClosingService.GetCalendarAsync(
                tenantId,
                year,
                month,
                cashRegisterId,
                cancellationToken);
            return Ok(result);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(new
            {
                message = ex.Message,
                code = "ADMIN_DAILY_CLOSING_INVALID_PERIOD",
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new
            {
                message = "Cash register is not in the current tenant",
                code = "ADMIN_DAILY_CLOSING_REGISTER_NOT_FOUND",
            });
        }
    }

    /// <summary>
    /// GET: Vienna-today status plus ISO week (Mon–Sun) counts for the dashboard widget.
    /// </summary>
    [HttpGet("dashboard-summary")]
    [HasPermission(AppPermissions.DailyClosingView)]
    [ProducesResponseType(typeof(DailyClosingDashboardSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DailyClosingDashboardSummaryDto>> GetDashboardSummary(
        [FromQuery] Guid? cashRegisterId,
        CancellationToken cancellationToken)
    {
        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);

        if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
        {
            var ok = await _context.CashRegisters.AsNoTracking()
                .AnyAsync(cr => cr.Id == cashRegisterId.Value && cr.TenantId == tenantId, cancellationToken);
            if (!ok)
            {
                return NotFound(new
                {
                    message = "Cash register is not in the current tenant",
                    code = "ADMIN_DAILY_CLOSING_REGISTER_NOT_FOUND",
                });
            }
        }

        try
        {
            var result = await _dailyClosingService.GetDashboardSummaryAsync(
                tenantId,
                cashRegisterId,
                cancellationToken);
            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new
            {
                message = "Cash register is not in the current tenant",
                code = "ADMIN_DAILY_CLOSING_REGISTER_NOT_FOUND",
            });
        }
    }
}
