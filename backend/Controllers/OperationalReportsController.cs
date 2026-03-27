using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Operatör ve muhasebe için POS ödeme tabanlı raporlar (payment_details). Dashboard kartlarının resmi artefakt karşılığı.
/// X/Z: donanım TSE çıktısı değildir — ara özet (X) ve Tagesabschluss satırları (Z referansı).
/// </summary>
[ApiController]
[Route("api/Reports/operational")]
[HasPermission(AppPermissions.ReportView)]
public class OperationalReportsController : ControllerBase
{
    private readonly IOperationalReportingService _reporting;

    public OperationalReportsController(IOperationalReportingService reporting)
    {
        _reporting = reporting;
    }

    /// <summary>
    /// Kasiyer-Leistung: Zählungen und Beträge aus <c>payment_details</c> (kein Audit-Mix; siehe <see cref="StaffPerformanceReliabilityDto"/>).
    /// </summary>
    [HttpGet("staff-performance")]
    [ProducesResponseType(typeof(StaffPerformanceReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<StaffPerformanceReportDto>> GetStaffPerformance(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] Guid? cashRegisterId,
        [FromQuery] string? cashierId,
        [FromQuery] int? paymentMethod,
        [FromQuery] bool activeOnly = true,
        [FromQuery] bool includePerStaffPerDay = false,
        CancellationToken cancellationToken = default)
    {
        var data = await _reporting.GetStaffPerformanceAsync(
            startDate,
            endDate,
            cashRegisterId,
            cashierId,
            paymentMethod,
            activeOnly,
            includePerStaffPerDay,
            cancellationToken);
        return Ok(data);
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(OperationalSummaryDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<OperationalSummaryDto>> GetSummary(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] Guid? cashRegisterId,
        [FromQuery] string? cashierId,
        [FromQuery] int? paymentMethod,
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var data = await _reporting.GetSummaryAsync(
            startDate, endDate, cashRegisterId, cashierId, paymentMethod, activeOnly, cancellationToken);
        return Ok(data);
    }

    [HttpGet("periodic")]
    [ProducesResponseType(typeof(PeriodicOperationalReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PeriodicOperationalReportDto>> GetPeriodic(
        [FromQuery] string periodPreset = "custom",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] Guid? cashRegisterId = null,
        [FromQuery] string? cashierId = null,
        [FromQuery] int? paymentMethod = null,
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var data = await _reporting.GetPeriodicAsync(
            periodPreset, startDate, endDate, cashRegisterId, cashierId, paymentMethod, activeOnly, cancellationToken);
        return Ok(data);
    }

    [HasPermission(AppPermissions.ReportExport)]
    [HttpPost("periodic/freeze")]
    [ProducesResponseType(typeof(PeriodenberichtRunDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PeriodenberichtRunDto>> FreezePeriodic(
        [FromBody] FreezePeriodenberichtRequest request,
        CancellationToken cancellationToken = default)
    {
        var userId = User.GetActorUserId() ?? "unknown";
        var data = await _reporting.FreezePeriodicAsync(request, userId, cancellationToken);
        return Ok(data);
    }

    [HttpGet("periodic/frozen")]
    [ProducesResponseType(typeof(IReadOnlyList<PeriodenberichtRunListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PeriodenberichtRunListItemDto>>> ListFrozenPeriodic(
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] Guid? cashRegisterId,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var data = await _reporting.ListFrozenPeriodenberichteAsync(fromDate, toDate, cashRegisterId, limit, cancellationToken);
        return Ok(data);
    }

    [HttpGet("periodic/frozen/{id:guid}")]
    [ProducesResponseType(typeof(PeriodenberichtRunDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PeriodenberichtRunDto>> GetFrozenPeriodicById(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var data = await _reporting.GetFrozenPeriodenberichtByIdAsync(id, cancellationToken);
        if (data == null) return NotFound();
        return Ok(data);
    }

    [HttpGet("interim")]
    [ProducesResponseType(typeof(InterimOperationalReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<InterimOperationalReportDto>> GetInterim(
        [FromQuery] Guid? cashRegisterId = null,
        [FromQuery] string? cashierId = null,
        [FromQuery] int? paymentMethod = null,
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var data = await _reporting.GetInterimAsync(
            cashRegisterId, cashierId, paymentMethod, activeOnly, cancellationToken);
        return Ok(data);
    }

    [HttpGet("closings")]
    [ProducesResponseType(typeof(ClosingReferenceReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ClosingReferenceReportDto>> GetClosings(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] Guid? cashRegisterId = null,
        CancellationToken cancellationToken = default)
    {
        var data = await _reporting.GetClosingReferenceAsync(startDate, endDate, cashRegisterId, cancellationToken);
        return Ok(data);
    }

    /// <summary>
    /// X/Z reference bundle: interim (X-like), full-day operational totals, daily closings (Z-like). Read model, not hardware TSE output.
    /// </summary>
    [HttpGet("xz-reference-bundle")]
    [ProducesResponseType(typeof(XzReferenceBundleDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<XzReferenceBundleDto>> GetXzReferenceBundle(
        [FromQuery] DateTime? businessDate = null,
        [FromQuery] Guid? cashRegisterId = null,
        [FromQuery] string? cashierId = null,
        [FromQuery] int? paymentMethod = null,
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var data = await _reporting.GetXzReferenceBundleAsync(
            businessDate, cashRegisterId, cashierId, paymentMethod, activeOnly, cancellationToken);
        return Ok(data);
    }

    /// <summary>CSV export — aynı filtreler; satış satırları (refund/storno hariç toplam sütunları).</summary>
    [HasPermission(AppPermissions.ReportExport)]
    [HttpGet("export/summary.csv")]
    public async Task<IActionResult> ExportSummaryCsv(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] Guid? cashRegisterId,
        [FromQuery] string? cashierId,
        [FromQuery] int? paymentMethod,
        [FromQuery] bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var summary = await _reporting.GetSummaryAsync(
            startDate, endDate, cashRegisterId, cashierId, paymentMethod, activeOnly, cancellationToken);

        var sb = new StringBuilder();
        var inv = CultureInfo.InvariantCulture;
        sb.AppendLine("bucket,type,count,totalAmount");
        foreach (var m in summary.ByPaymentMethod)
            sb.AppendLine(
                $"method,{Escape(m.MethodKey)},{m.Count.ToString(inv)},{m.TotalAmount.ToString("0.##", inv)}");

        foreach (var c in summary.ByCashier)
            sb.AppendLine(
                $"cashier,{Escape(c.CashierId)},{c.Count.ToString(inv)},{c.TotalAmount.ToString("0.##", inv)}");

        sb.AppendLine(
            $"totals,gross,{summary.PaymentRowCount.ToString(inv)},{summary.GrossTotalAmount.ToString("0.##", inv)}");
        sb.AppendLine(
            $"totals,refunds,{summary.RefundRowCount.ToString(inv)},{summary.RefundAmountTotal.ToString("0.##", inv)}");

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        var fn = $"operational-summary-{summary.Meta.PeriodStartLocalDate:yyyyMMdd}-{summary.Meta.PeriodEndLocalDate:yyyyMMdd}.csv";
        return File(bytes, "text/csv; charset=utf-8", fn);
    }

    private static string Escape(string? s)
    {
        if (string.IsNullOrEmpty(s)) return "\"\"";
        return "\"" + s.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
    }
}
