using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin error log viewer backed by Elmah PostgreSQL storage.
/// </summary>
[Authorize]
[ApiController]
[Route("api/admin/errors")]
[Produces("application/json")]
[HasPermission(AppPermissions.SystemCritical)]
public sealed class AdminElmahErrorsController : ControllerBase
{
    private readonly IElmahErrorQueryService _elmahErrorQueryService;
    private readonly ILogExportService _logExport;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly AppDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AdminElmahErrorsController> _logger;

    public AdminElmahErrorsController(
        IElmahErrorQueryService elmahErrorQueryService,
        ILogExportService logExport,
        ISettingsTenantResolver settingsTenantResolver,
        AppDbContext db,
        IConfiguration configuration,
        IAuditLogService auditLogService,
        ILogger<AdminElmahErrorsController> logger)
    {
        _elmahErrorQueryService = elmahErrorQueryService;
        _logExport = logExport;
        _settingsTenantResolver = settingsTenantResolver;
        _db = db;
        _configuration = configuration;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ElmahErrorListResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ElmahErrorListResponseDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var applicationName = ResolveApplicationName();
            var result = await _elmahErrorQueryService
                .ListAsync(applicationName, page, pageSize, cancellationToken)
                .ConfigureAwait(false);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list Elmah errors.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Elmah error log is unavailable." });
        }
    }

    /// <summary>
    /// Export Elmah error logs. Filename: <c>log_{tenantSlug}_{stamp}.{txt|csv|json}</c>.
    /// </summary>
    [HttpGet("export")]
    [Produces("text/plain", "text/csv", "application/json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Export(
        [FromQuery] string format = "txt",
        [FromQuery] int? maxRows = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var applicationName = ResolveApplicationName();
            var tenantSlug = await ResolveTenantSlugAsync(cancellationToken).ConfigureAwait(false);
            var result = await _logExport
                .ExportAsync(applicationName, tenantSlug, format, maxRows, cancellationToken)
                .ConfigureAwait(false);
            return File(result.Content, result.ContentType, result.FileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export Elmah errors.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Elmah error log export is unavailable." });
        }
    }

    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken = default)
    {
        if (!User.HasPermissionClaim(AppPermissions.SystemCritical))
            return Forbid();

        try
        {
            var applicationName = ResolveApplicationName();
            var deleted = await _elmahErrorQueryService
                .ClearAsync(applicationName, cancellationToken)
                .ConfigureAwait(false);

            await _auditLogService.LogSystemOperationAsync(
                action: "ElmahErrorsCleared",
                entityType: "ElmahError",
                userId: User.GetActorUserId() ?? "unknown",
                userRole: User.GetActorRole() ?? "Unknown",
                description: $"Elmah error log cleared ({deleted} rows).",
                actionType: AuditEventType.Other,
                correlationIdOverride: HttpContext.Items[Middleware.CorrelationIdMiddleware.CorrelationIdItemKey] as string)
                .ConfigureAwait(false);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clear Elmah errors.");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "Failed to clear Elmah error log." });
        }
    }

    private async Task<string?> ResolveTenantSlugAsync(CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
            if (tenantId == Guid.Empty)
                return null;
            return await _db.Tenants.AsNoTracking()
                .Where(t => t.Id == tenantId)
                .Select(t => t.Slug)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }

    private string ResolveApplicationName()
        => _configuration["Elmah:ApplicationName"]?.Trim() is { Length: > 0 } name
            ? name
            : "Regkasse";
}
