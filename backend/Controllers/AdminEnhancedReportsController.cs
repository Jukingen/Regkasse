using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Reports;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Reports;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Admin operational/compliance reports (reconciliation, TSE, offline, users, peak hours, product movement).
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/reports")]
[Produces("application/json")]
public class AdminEnhancedReportsController : ControllerBase
{
    private readonly IComplianceOperationalReportingService _compliance;
    private readonly IOperationalReportingService _operational;
    private readonly IPeakHoursAnalysisService _peakHours;
    private readonly IProductMovementAnalysisService _productMovement;
    private readonly IAdminOperationalReportExportService _export;
    private readonly IOperationalReportScheduler _scheduler;
    private readonly ISettingsTenantResolver _tenantResolver;

    public AdminEnhancedReportsController(
        IComplianceOperationalReportingService compliance,
        IOperationalReportingService operational,
        IPeakHoursAnalysisService peakHours,
        IProductMovementAnalysisService productMovement,
        IAdminOperationalReportExportService export,
        IOperationalReportScheduler scheduler,
        ISettingsTenantResolver tenantResolver)
    {
        _compliance = compliance;
        _operational = operational;
        _peakHours = peakHours;
        _productMovement = productMovement;
        _export = export;
        _scheduler = scheduler;
        _tenantResolver = tenantResolver;
    }

    [HttpGet("daily-reconciliation")]
    [HasPermission(AppPermissions.ReportView)]
    public async Task<ActionResult<DailyReconciliationReportDto>> DailyReconciliation(
        [FromQuery] DateTime? businessDate,
        [FromQuery] Guid? cashRegisterId,
        CancellationToken cancellationToken)
    {
        var data = await _compliance.GetDailyReconciliationAsync(businessDate, cashRegisterId, cancellationToken);
        return Ok(data);
    }

    [HttpGet("tse-continuity")]
    [HasPermission(AppPermissions.ReportView)]
    public async Task<ActionResult<TseChainContinuityReportDto>> TseContinuity(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] Guid? cashRegisterId,
        CancellationToken cancellationToken)
    {
        var data = await _compliance.GetTseChainContinuityAsync(startDate, endDate, cashRegisterId, cancellationToken);
        return Ok(data);
    }

    [HttpGet("offline-recovery")]
    [HasPermission(AppPermissions.ReportView)]
    public async Task<ActionResult<OfflineRecoveryReportDto>> OfflineRecovery(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] Guid? cashRegisterId,
        [FromQuery] int recentLimit = 50,
        CancellationToken cancellationToken = default)
    {
        var data = await _compliance.GetOfflineRecoveryAsync(startDate, endDate, cashRegisterId, recentLimit, cancellationToken);
        return Ok(data);
    }

    [HttpGet("user-performance")]
    [HasPermission(AppPermissions.ReportView)]
    public async Task<ActionResult<UserPerformanceReportDto>> UserPerformance(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] Guid? cashRegisterId,
        [FromQuery] string? cashierId,
        [FromQuery] int? paymentMethod,
        [FromQuery] bool activeOnly = true,
        [FromQuery] decimal highStornoRateThreshold = UserPerformanceReportDto.DefaultHighStornoRateThreshold,
        CancellationToken cancellationToken = default)
    {
        var data = await _operational.GetUserPerformanceAsync(
            startDate, endDate, cashRegisterId, cashierId, paymentMethod, activeOnly, highStornoRateThreshold, cancellationToken);
        return Ok(data);
    }

    [HttpGet("peak-hours")]
    [HasPermission(AppPermissions.ReportView)]
    public async Task<ActionResult<PeakHoursReportDto>> PeakHours(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] Guid? cashRegisterId,
        CancellationToken cancellationToken)
    {
        var data = await _peakHours.GetPeakHoursAsync(startDate, endDate, cashRegisterId, cancellationToken);
        return Ok(data);
    }

    [HttpGet("product-movement")]
    [HasPermission(AppPermissions.ReportView)]
    public async Task<ActionResult<ProductMovementReportDto>> ProductMovement(
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        CancellationToken cancellationToken)
    {
        var data = await _productMovement.GetProductMovementAsync(startDate, endDate, cancellationToken);
        return Ok(data);
    }

    [HttpGet("{reportType}/export")]
    [HasPermission(AppPermissions.ReportExport)]
    public async Task<IActionResult> Export(
        [FromRoute] string reportType,
        [FromQuery] string format = "csv",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] DateTime? businessDate = null,
        [FromQuery] Guid? cashRegisterId = null,
        CancellationToken cancellationToken = default)
    {
        if (!TryParseReportType(reportType, out var type))
            return BadRequest(new { message = "Unknown report type." });

        var (content, contentType, fileName) = await _export.ExportAsync(
            type, format, startDate, endDate, businessDate, cashRegisterId, cancellationToken);
        return File(content, contentType, fileName);
    }

    [HttpPost("schedule")]
    [HasPermission(AppPermissions.ReportExport)]
    public async Task<ActionResult<OperationalReportScheduleResponse>> Schedule(
        [FromBody] ScheduleOperationalReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryParseReportType(request.ReportType, out var type))
            return BadRequest(new { message = "Unknown report type." });

        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var filters = request.Filters ?? new OperationalReportFilters();
        var schedule = await _scheduler.CreateScheduleAsync(
            tenantId,
            userId,
            type,
            request.Schedule,
            request.Recipients,
            request.Format,
            filters,
            cancellationToken);

        return Ok(MapSchedule(schedule));
    }

    private static bool TryParseReportType(string value, out AdminOperationalReportType type)
    {
        var key = value.Trim().Replace("-", "", StringComparison.OrdinalIgnoreCase);
        switch (key)
        {
            case "dailyreconciliation":
                type = AdminOperationalReportType.DailyReconciliation;
                return true;
            case "tsecontinuity":
                type = AdminOperationalReportType.TseContinuity;
                return true;
            case "offlinerecovery":
                type = AdminOperationalReportType.OfflineRecovery;
                return true;
            case "userperformance":
                type = AdminOperationalReportType.UserPerformance;
                return true;
            case "peakhours":
                type = AdminOperationalReportType.PeakHours;
                return true;
            case "productmovement":
                type = AdminOperationalReportType.ProductMovement;
                return true;
            default:
                return Enum.TryParse(value, ignoreCase: true, out type);
        }
    }

    private static OperationalReportScheduleResponse MapSchedule(OperationalReportSchedule s) => new()
    {
        Id = s.Id,
        ReportType = s.ReportType,
        ScheduleCron = s.ScheduleCron,
        Format = s.Format,
        IsActive = s.IsActive,
        Recipients = JsonSerializer.Deserialize<List<string>>(s.RecipientsJson) ?? new List<string>(),
        Filters = JsonSerializer.Deserialize<OperationalReportFilters>(s.FiltersJson),
        LastRunUtc = s.LastRunUtc,
        NextRunUtc = s.NextRunUtc,
        CreatedAtUtc = s.CreatedAtUtc,
    };
}

public sealed class ScheduleOperationalReportRequest
{
    [Required]
    public string ReportType { get; set; } = string.Empty;

    [Required]
    public string Schedule { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<string> Recipients { get; set; } = new();

    [Required]
    public string Format { get; set; } = "pdf";

    public OperationalReportFilters? Filters { get; set; }
}

public sealed class OperationalReportScheduleResponse
{
    public Guid Id { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public string ScheduleCron { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<string> Recipients { get; set; } = new();
    public OperationalReportFilters? Filters { get; set; }
    public DateTime? LastRunUtc { get; set; }
    public DateTime? NextRunUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
