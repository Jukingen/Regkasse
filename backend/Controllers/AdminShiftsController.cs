using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>Admin read-only overview of POS cashier shifts and linked daily closings.</summary>
[Authorize]
[ApiController]
[Route("api/admin/shifts")]
[Produces("application/json")]
public sealed class AdminShiftsController : ControllerBase
{
    private readonly IAdminShiftOverviewService _overview;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly AppDbContext _context;

    public AdminShiftsController(
        IAdminShiftOverviewService overview,
        ISettingsTenantResolver tenantResolver,
        AppDbContext context)
    {
        _overview = overview;
        _tenantResolver = tenantResolver;
        _context = context;
    }

    /// <summary>Active shifts, recent history, and daily closings for the effective tenant.</summary>
    [HttpGet("overview")]
    [HasPermission(AppPermissions.ShiftView)]
    [ProducesResponseType(typeof(AdminShiftOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminShiftOverviewDto>> GetOverview(
        [FromQuery] Guid? cashRegisterId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);

        if (cashRegisterId.HasValue && cashRegisterId.Value != Guid.Empty)
        {
            var ok = await _context.CashRegisters.AsNoTracking()
                .AnyAsync(cr => cr.Id == cashRegisterId.Value && cr.TenantId == tenantId, cancellationToken);
            if (!ok)
            {
                return BadRequest(new
                {
                    message = "Cash register is not in the current tenant",
                    code = "ADMIN_SHIFT_INVALID_REGISTER",
                });
            }
        }

        if (fromUtc.HasValue && toUtc.HasValue && fromUtc.Value >= toUtc.Value)
        {
            return BadRequest(new { message = "fromUtc must be before toUtc", code = "ADMIN_SHIFT_INVALID_RANGE" });
        }

        var dto = await _overview.GetOverviewAsync(
            tenantId,
            cashRegisterId,
            fromUtc,
            toUtc,
            limit ?? 200,
            cancellationToken);

        return Ok(dto);
    }
}
