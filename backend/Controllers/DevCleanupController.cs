using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models.DTOs;
using KasseAPI_Final.Services.Dev;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Development-only maintenance endpoints (no-op / hidden outside Development).
/// </summary>
[Authorize]
[ApiController]
[Route("api/dev/cleanup")]
[Produces("application/json")]
public sealed class DevCleanupController : ControllerBase
{
    private readonly IHostEnvironment _environment;
    private readonly AppDbContext _db;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly ILogger<DevCleanupController> _logger;

    public DevCleanupController(
        IHostEnvironment environment,
        AppDbContext db,
        ISettingsTenantResolver settingsTenantResolver,
        ILogger<DevCleanupController> logger)
    {
        _environment = environment;
        _db = db;
        _settingsTenantResolver = settingsTenantResolver;
        _logger = logger;
    }

    /// <summary>
    /// Removes test users (bar/cafe/test email patterns) and their tenant memberships.
    /// </summary>
    /// <summary>
    /// Hard-deletes all products (and optionally categories) for one tenant — Development only.
    /// Use before a fresh demo catalog import. Requires products.manage or SuperAdmin.
    /// </summary>
    [HttpPost("tenant-catalog")]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(typeof(DevTenantCatalogCleanupResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DevTenantCatalogCleanupResult>> PurgeTenantCatalog(
        [FromBody] DevTenantCatalogCleanupRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = "Dev catalog purge requires ASPNETCORE_ENVIRONMENT=Development. Prefer POST /api/admin/products/dev/purge-catalog." });
        }

        if (request.ConfirmPhrase != DevTenantCatalogCleanup.ConfirmPhrase
            && request.ConfirmPhrase != DevTenantCatalogCleanup.FiscalOverridePhrase)
        {
            return BadRequest(new
            {
                message = $"Invalid confirm phrase. Use {DevTenantCatalogCleanup.ConfirmPhrase}.",
            });
        }

        Guid tenantId;
        try
        {
            tenantId = await ResolveCleanupTenantIdAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        if (tenantId == Guid.Empty)
            return BadRequest(new { message = "Tenant could not be resolved." });

        try
        {
            var result = await DevTenantCatalogCleanup.ExecuteAsync(
                _db,
                tenantId,
                request.IncludeCategories,
                allowFiscalOverride: request.ConfirmPhrase == DevTenantCatalogCleanup.FiscalOverridePhrase,
                cancellationToken).ConfigureAwait(false);

            _logger.LogWarning(
                "Dev tenant catalog purge: tenant={TenantId} products={Products} categories={Categories} fiscalPayments={HasFiscal}",
                result.TenantId,
                result.ProductsDeleted,
                result.CategoriesDeleted,
                result.HasFiscalPayments);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("orphaned-users")]
    [Authorize(Roles = Roles.SuperAdmin)]
    [ProducesResponseType(typeof(DevOrphanedUserCleanupResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DevOrphanedUserCleanupResponse>> CleanupOrphanedUsers(
        CancellationToken cancellationToken = default)
    {
        if (!_environment.IsDevelopment())
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = "Dev catalog purge requires ASPNETCORE_ENVIRONMENT=Development. Prefer POST /api/admin/products/dev/purge-catalog." });
        }

        var users = await DevOrphanedUserCleanup
            .WhereOrphanedTestUserEmail(_db.Users)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (users.Count == 0)
        {
            return Ok(new DevOrphanedUserCleanupResponse(
                "Cleanup completed",
                DeletedMemberships: 0,
                DeletedUsers: 0));
        }

        var userIds = users.Select(u => u.Id).ToList();

        var memberships = await _db.UserTenantMemberships
            .IgnoreQueryFilters()
            .Where(m => userIds.Contains(m.UserId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (memberships.Count > 0)
            _db.UserTenantMemberships.RemoveRange(memberships);

        _db.Users.RemoveRange(users);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Dev orphaned-user cleanup removed {MembershipCount} memberships and {UserCount} users",
            memberships.Count,
            users.Count);

        return Ok(new DevOrphanedUserCleanupResponse(
            "Cleanup completed",
            memberships.Count,
            users.Count));
    }

    private async Task<Guid> ResolveCleanupTenantIdAsync(
        DevTenantCatalogCleanupRequest request,
        CancellationToken cancellationToken)
    {
        if (User.IsInRole(Roles.SuperAdmin)
            || User.Claims.Any(c =>
                (string.Equals(c.Type, "role", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(c.Type, System.Security.Claims.ClaimTypes.Role, StringComparison.OrdinalIgnoreCase))
                && string.Equals(c.Value, Roles.SuperAdmin, StringComparison.OrdinalIgnoreCase)))
        {
            if (request.TenantId.HasValue && request.TenantId.Value != Guid.Empty)
                return request.TenantId.Value;

            if (!string.IsNullOrWhiteSpace(request.TenantSlug))
            {
                var slug = request.TenantSlug.Trim();
                var tenant = await _db.Tenants.AsNoTracking()
                    .FirstOrDefaultAsync(
                        t => t.Slug.ToLower() == slug.ToLower(),
                        cancellationToken)
                    .ConfigureAwait(false);
                if (tenant != null)
                    return tenant.Id;

                throw new InvalidOperationException($"Tenant slug '{slug}' was not found.");
            }
        }

        return await _settingsTenantResolver.ResolveEffectiveTenantIdAsync().ConfigureAwait(false);
    }
}

public sealed record DevOrphanedUserCleanupResponse(
    string Message,
    int DeletedMemberships,
    int DeletedUsers);
