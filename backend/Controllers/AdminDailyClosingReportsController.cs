using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using KasseAPI_Final.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Admin read-only reporting: daily closing snapshot from payment rows.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/reports")]
[Produces("application/json")]
public class AdminDailyClosingReportsController : ControllerBase
{
    private readonly IDailyClosingService _dailyClosingService;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly AppDbContext _context;

    public AdminDailyClosingReportsController(
        IDailyClosingService dailyClosingService,
        ISettingsTenantResolver settingsTenantResolver,
        AppDbContext context)
    {
        _dailyClosingService = dailyClosingService;
        _settingsTenantResolver = settingsTenantResolver;
        _context = context;
    }

    /// <summary>
    /// GET: payment-row snapshot for one Austria business day (optional cash register filter).
    /// </summary>
    [HttpGet("daily-closing")]
    [HasPermission(AppPermissions.DailyClosingView)]
    [ProducesResponseType(typeof(DailyClosingSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DailyClosingSummaryDto>> GetDailyClosing(
        [FromQuery] DateTime? date,
        [FromQuery] Guid? cashRegisterId,
        CancellationToken cancellationToken)
    {
        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var businessDate = date ?? PostgreSqlUtcDateTime.GetViennaTodayCalendarMidnightUnspecified();

        if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
        {
            var ok = await _context.CashRegisters.AsNoTracking()
                .AnyAsync(cr => cr.Id == cashRegisterId.Value && cr.TenantId == tenantId, cancellationToken);
            if (!ok)
            {
                return BadRequest(new { message = "Cash register is not in the current tenant", code = "ADMIN_DAILY_CLOSING_INVALID_REGISTER" });
            }
        }

        var dto = await _dailyClosingService.GenerateClosingSummaryAsync(
            tenantId,
            cashRegisterId,
            businessDate,
            cancellationToken);
        return Ok(dto);
    }
}
