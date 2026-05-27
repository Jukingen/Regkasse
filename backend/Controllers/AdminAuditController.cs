using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Controllers;

[Authorize]
[ApiController]
[Route("api/admin/audit")]
[Produces("application/json")]
public class AdminAuditController : ControllerBase
{
    private readonly IAuditExportService _exportService;
    private readonly IAuditExportJobManager _exportJobs;
    private readonly IAuditReportScheduler _reportScheduler;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly AuditRetentionOptions _retentionOptions;
    private readonly ILogger<AdminAuditController> _logger;

    public AdminAuditController(
        IAuditExportService exportService,
        IAuditExportJobManager exportJobs,
        IAuditReportScheduler reportScheduler,
        ICurrentTenantAccessor tenantAccessor,
        IOptions<AuditRetentionOptions> retentionOptions,
        ILogger<AdminAuditController> logger)
    {
        _exportService = exportService;
        _exportJobs = exportJobs;
        _reportScheduler = reportScheduler;
        _tenantAccessor = tenantAccessor;
        _retentionOptions = retentionOptions.Value;
        _logger = logger;
    }

    [HttpGet("retention")]
    [HasPermission(AppPermissions.AuditView)]
    public ActionResult<AuditRetentionInfoResponse> GetRetention()
    {
        var years = _retentionOptions.RetentionYears > 0 ? _retentionOptions.RetentionYears : 7;
        var minCutoff = DateTime.UtcNow.Date.AddYears(-years);
        return Ok(new AuditRetentionInfoResponse
        {
            RetentionYears = years,
            MinCutoffDate = minCutoff,
            Message = $"Audit logs must be retained for at least {years} years (RKSV compliance).",
        });
    }

    [HttpPost("export")]
    [HasPermission(AppPermissions.AuditExport)]
    public async Task<ActionResult> StartExport([FromBody] AuditExportRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var tenantId = _tenantAccessor.TenantId;
        if (!tenantId.HasValue)
            return BadRequest(new { message = "Tenant context is required.", code = "TENANT_REQUIRED" });

        var filters = request.ToFilters();
        var count = await _exportService.CountForExportAsync(filters, cancellationToken).ConfigureAwait(false);
        if (count > IAuditExportService.MaxExportRows)
            return BadRequest(new { message = $"Export exceeds {IAuditExportService.MaxExportRows} rows ({count} matched). Narrow filters.", code = "EXPORT_TOO_LARGE" });

        if (count >= IAuditExportService.BackgroundExportThreshold)
        {
            var jobId = await _exportJobs.StartJobAsync(tenantId.Value, filters, request.Format, cancellationToken).ConfigureAwait(false);
            return Accepted(new { jobId, matchedRows = count, message = "Background export started. Poll GET export/jobs/{jobId}." });
        }

        var format = request.Format.Trim().ToLowerInvariant();
        var ext = format == "json" ? "json" : "csv";
        var contentType = format == "json" ? "application/json" : "text/csv";
        var fileName = $"audit_logs_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{ext}";
        await using var ms = new MemoryStream();
        await _exportService.StreamExportAsync(filters, request.Format, ms, cancellationToken).ConfigureAwait(false);
        return File(ms.ToArray(), contentType, fileName);
    }

    [HttpGet("export/jobs/{jobId}")]
    [HasPermission(AppPermissions.AuditExport)]
    public ActionResult<AuditExportJobStatusDto> GetExportJob(string jobId)
    {
        var status = _exportJobs.GetStatus(jobId);
        if (status == null)
            return NotFound();
        return Ok(status);
    }

    [HttpGet("export/jobs/{jobId}/download")]
    [HasPermission(AppPermissions.AuditExport)]
    public ActionResult DownloadExportJob(string jobId)
    {
        if (!_exportJobs.TryOpenDownload(jobId, out var stream, out var fileName, out var contentType) || stream == null)
            return NotFound(new { message = "Export not ready or expired." });

        return File(stream, contentType ?? "text/csv", fileName);
    }

    [HttpPost("schedule-report")]
    [HasPermission(AppPermissions.AuditExport)]
    public async Task<ActionResult<AuditReportScheduleResponse>> ScheduleReport(
        [FromBody] ScheduleAuditReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var tenantId = _tenantAccessor.TenantId;
        if (!tenantId.HasValue)
            return BadRequest(new { message = "Tenant context is required.", code = "TENANT_REQUIRED" });

        var actorId = User.GetActorUserId() ?? "system";
        try
        {
            var schedule = await _reportScheduler.CreateScheduleAsync(
                tenantId.Value,
                actorId,
                request.Name,
                request.Filters ?? new AuditLogQueryFilters(),
                request.Schedule,
                request.Recipients,
                request.Format,
                cancellationToken).ConfigureAwait(false);

            return CreatedAtAction(nameof(GetSchedule), new { id = schedule.Id }, ToScheduleDto(schedule));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message, code = "INVALID_SCHEDULE" });
        }
    }

    [HttpGet("schedules")]
    [HasPermission(AppPermissions.AuditView)]
    public async Task<ActionResult<IEnumerable<AuditReportScheduleResponse>>> ListSchedules(CancellationToken cancellationToken)
    {
        var tenantId = _tenantAccessor.TenantId;
        if (!tenantId.HasValue)
            return Ok(Array.Empty<AuditReportScheduleResponse>());

        var list = await _reportScheduler.GetSchedulesAsync(tenantId.Value, cancellationToken).ConfigureAwait(false);
        return Ok(list.Select(ToScheduleDto));
    }

    [HttpGet("schedules/{id:guid}")]
    [HasPermission(AppPermissions.AuditView)]
    public async Task<ActionResult<AuditReportScheduleResponse>> GetSchedule(Guid id, CancellationToken cancellationToken)
    {
        var schedule = await _reportScheduler.GetScheduleByIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (schedule == null)
            return NotFound();
        return Ok(ToScheduleDto(schedule));
    }

    [HttpDelete("schedules/{id:guid}")]
    [HasPermission(AppPermissions.AuditExport)]
    public async Task<ActionResult> DeactivateSchedule(Guid id, CancellationToken cancellationToken)
    {
        var ok = await _reportScheduler.DeactivateScheduleAsync(id, cancellationToken).ConfigureAwait(false);
        if (!ok)
            return NotFound();
        return NoContent();
    }

    private static AuditReportScheduleResponse ToScheduleDto(AuditReportSchedule s) => new()
    {
        Id = s.Id,
        Name = s.Name,
        ScheduleCron = s.ScheduleCron,
        Format = s.Format,
        IsActive = s.IsActive,
        Recipients = JsonSerializer.Deserialize<List<string>>(s.RecipientsJson) ?? new List<string>(),
        Filters = JsonSerializer.Deserialize<AuditLogQueryFilters>(s.FiltersJson),
        LastRunUtc = s.LastRunUtc,
        NextRunUtc = s.NextRunUtc,
        CreatedAtUtc = s.CreatedAtUtc,
    };
}

public sealed class AuditRetentionInfoResponse
{
    public int RetentionYears { get; set; }
    public DateTime MinCutoffDate { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class AuditExportRequest
{
    [Required]
    public string Format { get; set; } = "csv";

    public AuditLogQueryFilters? Filters { get; set; }

    public AuditLogQueryFilters ToFilters() => Filters ?? new AuditLogQueryFilters();
}

public sealed class ScheduleAuditReportRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public AuditLogQueryFilters? Filters { get; set; }

    [Required]
    public string Schedule { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    public List<string> Recipients { get; set; } = new();

    [Required]
    public string Format { get; set; } = "csv";
}

public sealed class AuditReportScheduleResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ScheduleCron { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public List<string> Recipients { get; set; } = new();
    public AuditLogQueryFilters? Filters { get; set; }
    public DateTime? LastRunUtc { get; set; }
    public DateTime? NextRunUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
