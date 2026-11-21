using System.Security.Claims;
using KasseAPI_Final.Auth;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>Admin audit log export. List search stays on <c>api/AuditLog</c> and uses the same filters.</summary>
[Authorize]
[ApiController]
[Route("api/admin/audit-logs")]
public sealed class AdminAuditLogsController : ControllerBase
{
    private readonly IAuditExportService _exportService;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly IFileNamingService _fileNaming;
    private readonly AppDbContext _db;
    private readonly ILogger<AdminAuditLogsController> _logger;

    public AdminAuditLogsController(
        IAuditExportService exportService,
        ICurrentTenantAccessor tenantAccessor,
        IFileNamingService fileNaming,
        AppDbContext db,
        ILogger<AdminAuditLogsController> logger)
    {
        _exportService = exportService;
        _tenantAccessor = tenantAccessor;
        _fileNaming = fileNaming;
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Streams the filtered audit log as a CSV or XLSX attachment.
    /// Filters match the activity list. Requires permission <c>audit.view</c>.
    /// </summary>
    /// <param name="format"><c>csv</c> (default) or <c>xlsx</c>.</param>
    /// <param name="startDate">Inclusive lower bound on the audit timestamp.</param>
    /// <param name="endDate">Inclusive calendar end date. The query uses a half-open upper bound.</param>
    /// <param name="userId">Actor user id.</param>
    /// <param name="targetUserId">User id stored as the entity name or in metadata.</param>
    /// <param name="userRole">Actor role at the time of the event.</param>
    /// <param name="action">Exact action code.</param>
    /// <param name="entityType">Exact entity type.</param>
    /// <param name="entityId">Entity id.</param>
    /// <param name="ipAddress">Client IP address.</param>
    /// <param name="status">Exact audit status.</param>
    /// <param name="statusOutcome"><c>success</c> or <c>failure</c>. Ignored when <paramref name="status"/> is set.</param>
    /// <param name="hasChanges">When true, only rows that stored old, new, or diff values.</param>
    /// <param name="search">Full-text search over action, entity type, description, and actor email.</param>
    /// <param name="page">1-based page. Omit with <paramref name="pageSize"/> to export the filtered set.</param>
    /// <param name="pageSize">Page size. Omit with <paramref name="page"/> to export the filtered set.</param>
    /// <response code="200">CSV (<c>text/csv</c>) or XLSX attachment.</response>
    /// <response code="400">Format is not csv or xlsx, or the export exceeds the row cap.</response>
    /// <response code="500">Export failed before the response stream started.</response>
    [HttpGet("export")]
    [HasPermission(AppPermissions.AuditView)]
    [Produces("text/csv", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task Export(
        [FromQuery] string format = "csv",
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? userId = null,
        [FromQuery] string? targetUserId = null,
        [FromQuery] string? userRole = null,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        [FromQuery] Guid? entityId = null,
        [FromQuery] string? ipAddress = null,
        [FromQuery] AuditLogStatus? status = null,
        [FromQuery] string? statusOutcome = null,
        [FromQuery] bool? hasChanges = null,
        [FromQuery] string? search = null,
        [FromQuery] int? page = null,
        [FromQuery] int? pageSize = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = (format ?? "csv").Trim().ToLowerInvariant();
        if (normalized is not ("csv" or "xlsx"))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(
                new { message = "format must be csv or xlsx.", code = "AUDIT_EXPORT_FORMAT" },
                cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            var filters = AuditLogQueryExtensions.ToFilters(
                startDate, endDate, userId, userRole, targetUserId, action, entityType, entityId,
                ipAddress, status, statusOutcome, hasChanges);
            filters.Search = search;
            filters.Page = page is > 0 ? page : null;
            filters.PageSize = pageSize is > 0 ? pageSize : null;
            if (!IsActorSuperAdmin())
                filters.ExcludePlatformOperatorActors = true;

            string? tenantSlug = null;
            if (_tenantAccessor.TenantId is { } tenantId)
            {
                tenantSlug = await _db.Tenants.AsNoTracking()
                    .Where(t => t.Id == tenantId)
                    .Select(t => t.Slug)
                    .FirstOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            var fileName = _fileNaming.GenerateFileName(
                AuditExportFileNames.Prefix,
                normalized,
                registerId: ExportFileNameSegments.DateOnly(filters.StartDate),
                additional: ExportFileNameSegments.DateOnly(filters.EndDate),
                tenantSlug: tenantSlug);

            Response.ContentType = normalized == "xlsx"
                ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
                : "text/csv; charset=utf-8";
            Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";
            await _exportService.StreamTabularExportAsync(filters, normalized, Response.Body, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            if (Response.HasStarted)
                throw;
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(new { message = ex.Message, code = "EXPORT_TOO_LARGE" }, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting admin audit logs");
            if (Response.HasStarted)
                return;
            Response.StatusCode = StatusCodes.Status500InternalServerError;
            await Response.WriteAsJsonAsync(
                new { message = "Internal server error while exporting audit logs.", code = "AUDIT_LOG_EXPORT_ERROR" },
                cancellationToken).ConfigureAwait(false);
        }
    }

    private bool IsActorSuperAdmin() =>
        RoleHierarchy.IsSuperAdminActor(User.GetActorRole())
        || string.Equals(
            RoleCanonicalization.GetCanonicalRole(User.FindFirstValue(ClaimTypes.Role) ?? string.Empty),
            Roles.SuperAdmin,
            StringComparison.Ordinal);
}
