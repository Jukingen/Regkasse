using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Mandanten-Admin list of TSE-signed Monatsbelege (and December Jahresbelege) plus failed auto-create rows.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/rksv/monatsbelege")]
[Produces("application/json")]
public sealed class AdminRksvMonatsbelegController : ControllerBase
{
    private readonly IRksvMonatsbelegService _list;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly AppDbContext _db;

    public AdminRksvMonatsbelegController(
        IRksvMonatsbelegService list,
        ISettingsTenantResolver tenantResolver,
        AppDbContext db)
    {
        _list = list;
        _tenantResolver = tenantResolver;
        _db = db;
    }

    /// <summary>Live sales rollup for one register and Vienna month or year. Does not sign anything.</summary>
    [HttpGet("sales-summary")]
    [HasPermission(AppPermissions.RksvMonatsbelegView)]
    [ProducesResponseType(typeof(PeriodSalesSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PeriodSalesSummaryDto>> GetSalesSummary(
        [FromQuery] Guid cashRegisterId,
        [FromQuery] int year,
        [FromQuery] int? month,
        CancellationToken cancellationToken = default)
    {
        if (cashRegisterId == Guid.Empty)
            return BadRequest(new { message = "Cash register id is required" });
        if (year is < 2000 or > 2100)
            return BadRequest(new { message = "Year is out of range" });
        if (month is int reportMonth && (reportMonth < 1 || reportMonth > 12))
            return BadRequest(new { message = "Month is out of range" });

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId == Guid.Empty)
            return NotFound();

        var registerVisible = await _db.CashRegisters.AsNoTracking()
            .AnyAsync(r => r.Id == cashRegisterId, cancellationToken)
            .ConfigureAwait(false);
        if (!registerVisible)
            return NotFound();

        DateTime fromUtc;
        DateTime toUtc;
        string periodLabel;
        if (month is int selectedMonth)
        {
            (fromUtc, toUtc) = RksvClosingPeriodHelper.MonthUtcRange(year, selectedMonth);
            periodLabel = PeriodSalesSummaryAggregator.MonthLabel(year, selectedMonth);
        }
        else
        {
            (fromUtc, toUtc) = RksvClosingPeriodHelper.YearUtcRange(year);
            periodLabel = PeriodSalesSummaryAggregator.YearLabel(year);
        }

        var summary = await PeriodSalesSummaryAggregator.AggregateAsync(
                _db,
                cashRegisterId,
                fromUtc,
                toUtc,
                periodLabel,
                cancellationToken)
            .ConfigureAwait(false);

        var paymentId = await FindSpecialReceiptPaymentIdAsync(
                cashRegisterId,
                year,
                month,
                cancellationToken)
            .ConfigureAwait(false);

        return Ok(new PeriodSalesSummaryDto
        {
            PeriodLabel = summary.PeriodLabel,
            ReceiptCount = summary.ReceiptCount,
            TotalRevenue = summary.TotalRevenue,
            NetAmount = summary.NetAmount,
            Vat10 = summary.Vat10,
            Vat20 = summary.Vat20,
            GrossAmount = summary.GrossAmount,
            ZeroSales = summary.ZeroSales,
            PaymentId = paymentId,
        });
    }

    [HttpGet]
    [HasPermission(AppPermissions.RksvMonatsbelegView)]
    [ProducesResponseType(typeof(MonatsbelegListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MonatsbelegListResponse>> Get(
        [FromQuery] int? year,
        [FromQuery] Guid? cashRegisterId,
        [FromQuery] string? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken).ConfigureAwait(false);
        if (tenantId == Guid.Empty)
            return NotFound();

        var result = await _list.ListAsync(
                new MonatsbelegListQuery
                {
                    Year = year,
                    CashRegisterId = cashRegisterId,
                    Status = status,
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                },
                cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    private async Task<Guid?> FindSpecialReceiptPaymentIdAsync(
        Guid cashRegisterId,
        int year,
        int? month,
        CancellationToken cancellationToken)
    {
        var query = _db.PaymentDetails.AsNoTracking()
            .Where(p => p.IsActive && p.CashRegisterId == cashRegisterId && p.RksvSpecialReceiptYear == year);

        if (month is int reportMonth)
        {
            query = query.Where(p =>
                p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Monatsbeleg
                && p.RksvSpecialReceiptMonth == reportMonth);
        }
        else
        {
            query = query.Where(p =>
                p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Jahresbeleg
                || (p.RksvSpecialReceiptKind == RksvSpecialReceiptKinds.Monatsbeleg && p.RksvSpecialReceiptMonth == 12));
        }

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }
}
