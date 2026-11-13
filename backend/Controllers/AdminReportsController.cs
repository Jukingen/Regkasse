using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using KasseAPI_Final.Auth;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>Admin compliance reports (user activity, exports, schedules).</summary>
[Authorize]
[ApiController]
[Route("api/admin/reports")]
[Produces("application/json")]
public class AdminReportsController : ControllerBase
{
    private readonly IUserActivityReportService _userActivityReports;
    private readonly IAuditReportScheduler _auditScheduler;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminReportsController(
        IUserActivityReportService userActivityReports,
        IAuditReportScheduler auditScheduler,
        ICurrentTenantAccessor tenantAccessor)
    {
        _userActivityReports = userActivityReports;
        _auditScheduler = auditScheduler;
        _tenantAccessor = tenantAccessor;
    }

    private bool IsActorSuperAdmin() =>
        string.Equals(
            RoleCanonicalization.GetCanonicalRole(User.FindFirstValue(ClaimTypes.Role) ?? string.Empty),
            Roles.SuperAdmin,
            StringComparison.Ordinal);

    /// <summary>Compliance user activity report (RKSV audit fields).</summary>
    [HttpGet("user-activity")]
    [HasPermission(AppPermissions.ReportView)]
    [ProducesResponseType(typeof(UserActivityReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserActivityReportDto>> GetUserActivityReport(
        [FromQuery] string userId,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? actionType = null,
        [FromQuery] bool includeTimeline = true,
        [FromQuery] bool includeTopUsers = true,
        [FromQuery] int timelineLimit = UserActivityReportService.DefaultTimelineLimit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { message = "userId is required.", code = "VALIDATION_ERROR" });

        var report = await BuildReportAsync(
            userId, fromDate, toDate, actionType, includeTimeline, includeTopUsers, timelineLimit, cancellationToken)
            .ConfigureAwait(false);

        if (report == null)
            return NotFound(ApiError.NotFound("User not found", $"User '{userId}' was not found or is not accessible."));

        return Ok(report);
    }

    /// <summary>Export user activity report (CSV or PDF) for regulatory submission.</summary>
    [HttpGet("user-activity/export")]
    [HasPermission(AppPermissions.ReportExport)]
    public async Task<IActionResult> ExportUserActivityReport(
        [FromQuery] string format = "csv",
        [FromQuery] string? userId = null,
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] string? actionType = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return BadRequest(new { message = "userId is required.", code = "VALIDATION_ERROR" });

        try
        {
            var (content, contentType, fileName) = await _userActivityReports.ExportAsync(
                new UserActivityReportQuery
                {
                    UserId = userId,
                    FromDate = fromDate,
                    ToDate = toDate,
                    ActionType = actionType,
                    IncludeTimeline = true,
                    IncludeTopUsers = false,
                    TimelineLimit = UserActivityReportService.MaxTimelineLimit,
                },
                format,
                IsActorSuperAdmin(),
                _tenantAccessor.TenantId,
                cancellationToken).ConfigureAwait(false);

            return File(content, contentType, fileName);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message, code = "NOT_FOUND" });
        }
    }

    /// <summary>Schedule weekly or monthly email of user audit export (CSV).</summary>
    [HttpPost("user-activity/schedule")]
    [HasPermission(AppPermissions.ReportExport)]
    [ProducesResponseType(typeof(ScheduleUserActivityReportResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<ScheduleUserActivityReportResponse>> ScheduleUserActivityReport(
        [FromBody] ScheduleUserActivityReportRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var tenantId = _tenantAccessor.TenantId;
        if (!tenantId.HasValue)
            return BadRequest(new { message = "Tenant context is required.", code = "TENANT_REQUIRED" });

        var cron = ResolveScheduleCron(request.Schedule);
        if (cron == null)
            return BadRequest(new { message = "Invalid schedule. Use weekly or monthly.", code = "INVALID_SCHEDULE" });

        var actorId = User.GetActorUserId() ?? "system";
        var filters = new AuditLogQueryFilters
        {
            UserId = request.UserId.Trim(),
            StartDate = request.FromDate,
            EndDate = request.ToDate,
            Action = string.IsNullOrWhiteSpace(request.ActionType) ? null : request.ActionType.Trim(),
        };

        try
        {
            var schedule = await _auditScheduler.CreateScheduleAsync(
                tenantId.Value,
                actorId,
                request.Name.Trim(),
                filters,
                cron,
                request.Recipients,
                request.Format ?? "csv",
                cancellationToken).ConfigureAwait(false);

            return CreatedAtRoute(
                routeName: null,
                routeValues: new { },
                value: new ScheduleUserActivityReportResponse
                {
                    ScheduleId = schedule.Id,
                    Name = schedule.Name,
                    ScheduleCron = schedule.ScheduleCron,
                    NextRunUtc = schedule.NextRunUtc,
                });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message, code = "INVALID_SCHEDULE" });
        }
    }

    private async Task<UserActivityReportDto?> BuildReportAsync(
        string userId,
        DateTime? fromDate,
        DateTime? toDate,
        string? actionType,
        bool includeTimeline,
        bool includeTopUsers,
        int timelineLimit,
        CancellationToken cancellationToken) =>
        await _userActivityReports.BuildReportAsync(
            new UserActivityReportQuery
            {
                UserId = userId,
                FromDate = fromDate,
                ToDate = toDate,
                ActionType = actionType,
                IncludeTimeline = includeTimeline,
                IncludeTopUsers = includeTopUsers,
                TimelineLimit = timelineLimit,
                DefaultRangeDays = 30,
            },
            IsActorSuperAdmin(),
            _tenantAccessor.TenantId,
            cancellationToken).ConfigureAwait(false);

    private static string? ResolveScheduleCron(string schedule) =>
        schedule.Trim().ToLowerInvariant() switch
        {
            "weekly" => "0 8 * * 1",
            "monthly" => "0 8 1 * *",
            _ => null,
        };
}

public sealed class ScheduleUserActivityReportRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string Schedule { get; set; } = "weekly";

    [Required]
    [MinLength(1)]
    public List<string> Recipients { get; set; } = new();

    public string Format { get; set; } = "csv";

    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string? ActionType { get; set; }
}

public sealed class ScheduleUserActivityReportResponse
{
    public Guid ScheduleId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ScheduleCron { get; set; } = string.Empty;
    public DateTime? NextRunUtc { get; set; }
}
