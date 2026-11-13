using System.ComponentModel.DataAnnotations;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Billing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Temporary Super Admin workaround: manually clear per-tenant billing license status cache
/// (<c>license_status_{tenantId}</c>) when FA shows stale "License not found" after a new sale.
/// Prefer automatic invalidation on create/activate/extend; remove this endpoint once that path is proven in production.
/// </summary>
[Authorize(Roles = Roles.SuperAdmin)]
[ApiController]
[Route("api/admin/license/cache")]
[Produces("application/json")]
[HasPermission(AppPermissions.SystemCritical)]
public sealed class AdminLicenseCacheController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ILicenseStatusCache _licenseStatusCache;
    private readonly IAuditLogService _auditLogService;
    private readonly ILogger<AdminLicenseCacheController> _logger;

    public AdminLicenseCacheController(
        AppDbContext db,
        ILicenseStatusCache licenseStatusCache,
        IAuditLogService auditLogService,
        ILogger<AdminLicenseCacheController> logger)
    {
        _db = db;
        _licenseStatusCache = licenseStatusCache;
        _auditLogService = auditLogService;
        _logger = logger;
    }

    /// <summary>
    /// Clear billing license status cache for one tenant (by <c>tenantId</c> or <c>tenantSlug</c>).
    /// </summary>
    [HttpPost("clear")]
    [ProducesResponseType(typeof(ClearLicenseCacheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClearLicenseCacheResponse>> Clear(
        [FromBody] ClearLicenseCacheRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
            return BadRequest(new { code = "BODY_REQUIRED", message = "Request body is required." });

        var hasId = request.TenantId is Guid id && id != Guid.Empty;
        var hasSlug = !string.IsNullOrWhiteSpace(request.TenantSlug);
        if (!hasId && !hasSlug)
        {
            return BadRequest(new
            {
                code = "TENANT_TARGET_REQUIRED",
                message = "Specify tenantId or tenantSlug.",
            });
        }

        var tenantQuery = _db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.DeletedAtUtc == null);

        Tenant? tenant;
        if (hasId)
        {
            tenant = await tenantQuery
                .FirstOrDefaultAsync(t => t.Id == request.TenantId!.Value, cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            var slug = request.TenantSlug!.Trim();
            tenant = await tenantQuery
                .FirstOrDefaultAsync(t => t.Slug == slug, cancellationToken)
                .ConfigureAwait(false);
        }

        if (tenant is null)
            return NotFound(new { code = "TENANT_NOT_FOUND", message = "Tenant not found." });

        var cacheKey = LicenseStatusCache.BuildKey(tenant.Id);
        await _licenseStatusCache
            .InvalidateLicenseCacheAsync(tenant.Id, cancellationToken)
            .ConfigureAwait(false);

        var actorUserId = User.GetActorUserId() ?? "unknown";
        var actorRole = User.GetActorRole() ?? Roles.SuperAdmin;
        var response = new ClearLicenseCacheResponse(
            Success: true,
            TenantId: tenant.Id,
            TenantSlug: tenant.Slug,
            CacheKey: cacheKey,
            Message: "License status cache cleared for tenant.");

        await _auditLogService.LogSystemOperationAsync(
            AuditLogActions.SYSTEM_CACHE_CLEARED,
            entityType: "LicenseStatusCache",
            userId: actorUserId,
            userRole: actorRole,
            description: $"License status cache cleared for tenant {tenant.Slug}",
            notes: $"Temporary workaround endpoint POST /api/admin/license/cache/clear; key={cacheKey}",
            requestData: request,
            responseData: response,
            actionType: AuditEventType.SystemCacheCleared,
            tenantId: tenant.Id).ConfigureAwait(false);

        _logger.LogWarning(
            "Super Admin {UserId} cleared license status cache for tenant {TenantId} ({TenantSlug}) key={CacheKey}",
            actorUserId,
            tenant.Id,
            tenant.Slug,
            cacheKey);

        return Ok(response);
    }
}

public sealed class ClearLicenseCacheRequest
{
    public Guid? TenantId { get; set; }

    [MaxLength(100)]
    public string? TenantSlug { get; set; }
}

public sealed record ClearLicenseCacheResponse(
    bool Success,
    Guid TenantId,
    string TenantSlug,
    string CacheKey,
    string Message);
