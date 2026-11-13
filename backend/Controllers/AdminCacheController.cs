using KasseAPI_Final.Authorization;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Caching;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Super Admin cache troubleshooting: clear tenant, prefix, or all application cache entries.
/// </summary>
[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/admin/cache")]
[Produces("application/json")]
[HasPermission(AppPermissions.SystemCritical)]
public sealed class AdminCacheController : ControllerBase
{
    private readonly ICacheManagementService _cacheManagement;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AdminCacheController> _logger;

    public AdminCacheController(
        ICacheManagementService cacheManagement,
        IAuditLogService auditLogService,
        ILogger<AdminCacheController> logger)
    {
        _cacheManagement = cacheManagement;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <summary>Clear application cache entries (tenant, prefix, or all).</summary>
    [HttpPost("clear")]
    [ProducesResponseType(typeof(ClearCacheResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ClearCacheResult>> Clear(
        [FromBody] ClearCacheRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { code = "BODY_REQUIRED", message = "Request body is required." });

        if (!request.ClearAll
            && string.IsNullOrWhiteSpace(request.Prefix)
            && request.TenantId is null)
        {
            return BadRequest(new
            {
                code = "CACHE_CLEAR_TARGET_REQUIRED",
                message = "Specify tenantId, prefix, or clearAll=true.",
            });
        }

        var result = await _cacheManagement.ClearAsync(request, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
            return BadRequest(new { code = "CACHE_CLEAR_FAILED", message = result.Detail });

        var actorUserId = User.GetActorUserId() ?? "unknown";
        var actorRole = User.GetActorRole() ?? Roles.SuperAdmin;

        await _auditLogService.LogSystemOperationAsync(
            AuditLogActions.SYSTEM_CACHE_CLEARED,
            entityType: "Cache",
            userId: actorUserId,
            userRole: actorRole,
            description: $"Cache cleared ({result.Mode})",
            notes: result.Detail,
            requestData: request,
            responseData: result,
            actionType: AuditEventType.SystemCacheCleared,
            tenantId: request.TenantId).ConfigureAwait(false);

        _logger.LogWarning(
            "Super Admin {UserId} cleared cache mode={Mode} tenant={TenantId} prefix={Prefix} clearAll={ClearAll}",
            actorUserId,
            result.Mode,
            request.TenantId,
            request.Prefix,
            request.ClearAll);

        return Ok(result);
    }
}
