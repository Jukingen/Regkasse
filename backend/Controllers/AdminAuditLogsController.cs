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

    /// <summary>GET api/admin/audit-logs/export?format=csv|xlsx. Streams the filtered rows.</summary>
    [HttpGet("export")]
    [HasPermission(AppPermissions.AuditView)]
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
