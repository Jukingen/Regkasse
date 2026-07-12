using KasseAPI_Final.Authorization;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
    private readonly IConfiguration _configuration;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AdminElmahErrorsController> _logger;

    public AdminElmahErrorsController(
        IElmahErrorQueryService elmahErrorQueryService,
        IConfiguration configuration,
        IAuditLogService auditLogService,
        ILogger<AdminElmahErrorsController> logger)
    {
        _elmahErrorQueryService = elmahErrorQueryService;
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

    private string ResolveApplicationName()
        => _configuration["Elmah:ApplicationName"]?.Trim() is { Length: > 0 } name
            ? name
            : "Regkasse";
}
