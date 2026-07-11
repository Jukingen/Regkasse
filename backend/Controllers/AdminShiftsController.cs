using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>Admin overview and recovery actions for POS cashier shifts.</summary>
[Authorize]
[ApiController]
[Route("api/admin/shifts")]
[Produces("application/json")]
public sealed class AdminShiftsController : ControllerBase
{
    private readonly IAdminShiftOverviewService _overview;
    private readonly IAdminShiftManagementService _management;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly AppDbContext _context;

    public AdminShiftsController(
        IAdminShiftOverviewService overview,
        IAdminShiftManagementService management,
        ISettingsTenantResolver tenantResolver,
        AppDbContext context)
    {
        _overview = overview;
        _management = management;
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

    /// <summary>Force-closes an open register and any active shift rows (Manager recovery).</summary>
    [HttpPost("registers/{cashRegisterId:guid}/force-close")]
    [HasPermission(AppPermissions.ShiftManage)]
    [ProducesResponseType(typeof(AdminForceCloseShiftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AdminForceCloseShiftResponse>> ForceCloseRegister(
        Guid cashRegisterId,
        [FromBody] AdminForceCloseShiftRequest? request,
        CancellationToken cancellationToken)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var registerExists = await _context.CashRegisters.AsNoTracking()
            .AnyAsync(r => r.Id == cashRegisterId && r.TenantId == tenantId, cancellationToken);
        if (!registerExists)
            return NotFound(new { message = "Cash register not found", code = "ADMIN_SHIFT_REGISTER_NOT_FOUND" });

        var actorUserId = User.GetActorUserId();
        if (string.IsNullOrEmpty(actorUserId))
            return Unauthorized(new { message = "User not authenticated" });

        var actorRole = User.GetActorRole() ?? Roles.FallbackUnknown;
        var result = await _management.ForceCloseRegisterAsync(
            cashRegisterId,
            actorUserId,
            actorRole,
            request?.ClosingBalance,
            request?.Reason,
            cancellationToken);

        return result.Kind switch
        {
            AdminShiftForceCloseKind.Success => Ok(new AdminForceCloseShiftResponse
            {
                CashRegisterId = result.CashRegisterId,
                ClosedShiftCount = result.ClosedShiftCount,
            }),
            AdminShiftForceCloseKind.NotFound => NotFound(new
            {
                message = "Cash register not found",
                code = "ADMIN_SHIFT_REGISTER_NOT_FOUND",
            }),
            AdminShiftForceCloseKind.AlreadyClosed => BadRequest(new
            {
                message = "Cash register is already closed",
                code = "ADMIN_SHIFT_REGISTER_ALREADY_CLOSED",
            }),
            _ => StatusCode(StatusCodes.Status500InternalServerError),
        };
    }
}
