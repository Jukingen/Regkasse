using KasseAPI_Final.Auth;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Helpers;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace KasseAPI_Final.Controllers;

/// <summary>Legacy route — prefer GET /api/admin/reports/user-activity.</summary>
[Authorize]
[HasPermission(AppPermissions.UserView)]
[ApiController]
[Route("api/admin/users")]
[Produces("application/json")]
public class AdminUserActivityReportController : ControllerBase
{
    private readonly IUserActivityReportService _reportService;
    private readonly ICurrentTenantAccessor _tenantAccessor;

    public AdminUserActivityReportController(
        IUserActivityReportService reportService,
        ICurrentTenantAccessor tenantAccessor)
    {
        _reportService = reportService;
        _tenantAccessor = tenantAccessor;
    }

    private bool IsActorSuperAdmin() =>
        string.Equals(
            RoleCanonicalization.GetCanonicalRole(User.FindFirstValue(ClaimTypes.Role) ?? string.Empty),
            Roles.SuperAdmin,
            StringComparison.Ordinal);

    [HttpGet("{id}/activity-report")]
    [ProducesResponseType(typeof(UserActivityReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserActivityReportDto>> GetActivityReport(
        string id,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int timelineLimit = UserActivityReportService.DefaultTimelineLimit,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(id))
            return NotFound(ApiError.NotFound("User not found", "User id is required."));

        var report = await _reportService.BuildReportAsync(
            new UserActivityReportQuery
            {
                UserId = id,
                FromDate = startDate,
                ToDate = endDate,
                IncludeTimeline = true,
                IncludeTopUsers = true,
                TimelineLimit = timelineLimit,
                DefaultRangeDays = 90,
            },
            IsActorSuperAdmin(),
            _tenantAccessor.TenantId,
            cancellationToken).ConfigureAwait(false);

        if (report == null)
            return NotFound(ApiError.NotFound("User not found", $"User id '{id}' was not found or is not accessible."));

        return Ok(report);
    }
}
