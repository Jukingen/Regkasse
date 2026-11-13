using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Permission / role change audit list + revert + reports for Admin FA.</summary>
[Authorize]
[ApiController]
[Route("api/admin/audit")]
[Produces("application/json")]
public sealed class AdminPermissionAuditController : ControllerBase
{
    private readonly IPermissionAuditService _permissionAudit;
    private readonly IPermissionAuditReportService _permissionReports;
    private readonly IAuditReportScheduler _reportScheduler;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminPermissionAuditController(
        IPermissionAuditService permissionAudit,
        IPermissionAuditReportService permissionReports,
        IAuditReportScheduler reportScheduler,
        ICurrentTenantAccessor tenantAccessor)
    {
        _permissionAudit = permissionAudit;
        _permissionReports = permissionReports;
        _reportScheduler = reportScheduler;
        _tenantAccessor = tenantAccessor;
    }

    /// <summary>Paginated permission audit entries (expanded per permission key).</summary>
    [HttpGet("permissions")]
    [HasPermission(AppPermissions.AuditView)]
    [ProducesResponseType(typeof(PermissionAuditLogsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<PermissionAuditLogsResponse>> GetPermissionAuditLogs(
        [FromQuery] string? roleId,
        [FromQuery] string? roleName,
        [FromQuery] string? permissionKey,
        [FromQuery] string? actorUserId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _permissionAudit.GetPermissionAuditLogsAsync(
            roleId, roleName, permissionKey, actorUserId, fromDate, toDate, page, pageSize, cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>Aggregated permission-change report (by date, actors, permissions).</summary>
    [HttpGet("permissions/report")]
    [HasPermission(AppPermissions.AuditView)]
    [ProducesResponseType(typeof(PermissionAuditReportDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PermissionAuditReportDto>> GetPermissionAuditReport(
        [FromQuery] string? roleId,
        [FromQuery] string? roleName,
        [FromQuery] string? permissionKey,
        [FromQuery] string? actorUserId,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken cancellationToken = default)
    {
        var report = await _permissionReports.GetReportAsync(
            new PermissionAuditReportFilters
            {
                RoleId = roleId,
                RoleName = roleName,
                PermissionKey = permissionKey,
                ActorUserId = actorUserId,
                FromDate = fromDate,
                ToDate = toDate,
            },
            cancellationToken).ConfigureAwait(false);
        return Ok(report);
    }

    /// <summary>Export permission audit as CSV, JSON, or PDF.</summary>
    [HttpGet("permissions/export")]
    [HasPermission(AppPermissions.AuditExport)]
    [Produces("text/csv", "application/json", "application/pdf")]
    public async Task<IActionResult> ExportPermissionAudit(
        [FromQuery] string format = "csv",
        [FromQuery] string? roleId = null,
        [FromQuery] string? roleName = null,
        [FromQuery] string? permissionKey = null,
        [FromQuery] string? actorUserId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _permissionReports.ExportAsync(
            new PermissionAuditReportFilters
            {
                RoleId = roleId,
                RoleName = roleName,
                PermissionKey = permissionKey,
                ActorUserId = actorUserId,
                FromDate = fromDate,
                ToDate = toDate,
            },
            format,
            cancellationToken).ConfigureAwait(false);

        return File(result.Content, result.ContentType, result.FileName);
    }

    /// <summary>Compliance snapshot: who has access, last review, expired/stale overrides.</summary>
    [HttpGet("permissions/compliance")]
    [HasPermission(AppPermissions.AuditView)]
    [ProducesResponseType(typeof(PermissionComplianceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<PermissionComplianceDto>> GetPermissionCompliance(
        [FromQuery] int staleDays = 90,
        CancellationToken cancellationToken = default)
    {
        var result = await _permissionReports.GetComplianceAsync(staleDays, cancellationToken)
            .ConfigureAwait(false);
        return Ok(result);
    }

    /// <summary>Schedule weekly/monthly permission audit reports emailed to auditors.</summary>
    [HttpPost("permissions/schedule-report")]
    [HasPermission(AppPermissions.AuditExport)]
    [ProducesResponseType(typeof(AuditReportScheduleResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<AuditReportScheduleResponse>> SchedulePermissionReport(
        [FromBody] SchedulePermissionAuditReportRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var tenantId = _tenantAccessor.TenantId;
        if (!tenantId.HasValue)
            return BadRequest(new { message = "Tenant context is required.", code = "TENANT_REQUIRED" });

        var format = NormalizeScheduleFormat(request.Format);
        var cron = string.IsNullOrWhiteSpace(request.Schedule)
            ? PresetCron(request.Preset)
            : request.Schedule.Trim();

        var filters = new AuditLogQueryFilters
        {
            StartDate = request.FromDate,
            EndDate = request.ToDate,
            UserId = request.ActorUserId,
            Search = request.RoleName,
            EntityType = string.IsNullOrWhiteSpace(request.RoleName) ? null : AuditLogEntityTypes.ROLE,
        };

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
        try
        {
            var schedule = await _reportScheduler.CreateScheduleAsync(
                tenantId.Value,
                actorId,
                request.Name.Trim(),
                filters,
                cron,
                request.Recipients,
                format,
                cancellationToken).ConfigureAwait(false);

            return CreatedAtAction(
                nameof(GetPermissionAuditReport),
                new { },
                new AuditReportScheduleResponse
                {
                    Id = schedule.Id,
                    Name = schedule.Name,
                    ScheduleCron = schedule.ScheduleCron,
                    Format = schedule.Format,
                    IsActive = schedule.IsActive,
                    Recipients = request.Recipients.ToList(),
                    Filters = filters,
                    LastRunUtc = schedule.LastRunUtc,
                    NextRunUtc = schedule.NextRunUtc,
                    CreatedAtUtc = schedule.CreatedAtUtc,
                });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message, code = "INVALID_SCHEDULE" });
        }
    }

    /// <summary>Revert a role-permission audit entry to its oldValues snapshot.</summary>
    [HttpPost("permissions/{auditId:guid}/revert")]
    [HasPermission(AppPermissions.UserManage)]
    [ProducesResponseType(typeof(RevertPermissionAuditResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RevertPermissionAuditResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RevertPermissionAuditResponse>> RevertPermissionAudit(
        Guid auditId,
        [FromBody] RevertPermissionAuditRequest? request,
        CancellationToken cancellationToken = default)
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var actorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (string.IsNullOrEmpty(actorId))
            return Unauthorized();

        request ??= new RevertPermissionAuditRequest();
        var result = await _permissionAudit.RevertAsync(
            auditId, actorId, actorRole, request.Reason, request.Force, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Success && result.WarningNewerChanges)
            return Conflict(result);
        if (!result.Success && string.Equals(result.Message, "Audit entry not found.", StringComparison.Ordinal))
            return NotFound(result);
        if (!result.Success)
            return BadRequest(result);

        return Ok(result);
    }

    /// <summary>Append a note related to a permission audit entry (new audit row; append-only).</summary>
    [HttpPost("permissions/{auditId:guid}/note")]
    [HasPermission(AppPermissions.AuditView)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddPermissionAuditNote(
        Guid auditId,
        [FromBody] AddPermissionAuditNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        var actorRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (string.IsNullOrEmpty(actorId))
            return Unauthorized();

        var (ok, error) = await _permissionAudit.AddNoteAsync(
            auditId, actorId, actorRole, request.Note, cancellationToken).ConfigureAwait(false);
        if (!ok)
            return NotFound(new { success = false, message = error });
        return Ok(new { success = true });
    }

    private static string NormalizeScheduleFormat(string? format)
    {
        var f = (format ?? "permission-pdf").Trim().ToLowerInvariant();
        return f switch
        {
            "csv" or PermissionAuditScheduleFormats.Csv => PermissionAuditScheduleFormats.Csv,
            "json" or PermissionAuditScheduleFormats.Json => PermissionAuditScheduleFormats.Json,
            _ => PermissionAuditScheduleFormats.Pdf,
        };
    }

    private static string PresetCron(string? preset) =>
        (preset ?? "weekly").Trim().ToLowerInvariant() switch
        {
            "monthly" or "compliance" => "0 8 1 * *",
            _ => "0 8 * * 1",
        };
}

public sealed class SchedulePermissionAuditReportRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    /// <summary>weekly | monthly | compliance — used when Schedule is empty.</summary>
    [MaxLength(32)]
    public string? Preset { get; set; } = "weekly";

    /// <summary>Optional cron (UTC). Overrides Preset when set.</summary>
    [MaxLength(64)]
    public string? Schedule { get; set; }

    [Required]
    [MinLength(1)]
    public List<string> Recipients { get; set; } = new();

    /// <summary>permission-csv | permission-json | permission-pdf (or csv/json/pdf).</summary>
    [MaxLength(32)]
    public string Format { get; set; } = "permission-pdf";

    public string? RoleName { get; set; }
    public string? ActorUserId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
