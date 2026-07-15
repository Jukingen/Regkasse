using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services.AdminTenants;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>Authenticated tenant list for dev header switcher (membership-scoped unless SuperAdmin).</summary>
[ApiController]
[Route("api/tenants")]
[Authorize]
[Produces("application/json")]
public sealed class TenantsController : ControllerBase
{
    private readonly IAdminTenantService _tenantService;
    private readonly ICurrentTenantAccessor _currentTenantAccessor;
    private readonly AppDbContext _db;

    public TenantsController(
        IAdminTenantService tenantService,
        ICurrentTenantAccessor currentTenantAccessor,
        AppDbContext db)
    {
        _tenantService = tenantService;
        _currentTenantAccessor = currentTenantAccessor;
        _db = db;
    }

    /// <summary>
    /// Tenants visible in the dev header switcher (FA and POS <c>DevTenantSwitcher</c>).
    /// SuperAdmin: all non-deleted tenants; others: active memberships only. Requires authentication.
    /// </summary>
    [HttpGet("switcher")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminTenantListItemDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminTenantListItemDto>>> ListForSwitcher(
        [FromQuery] bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        var actorUserId = User.GetActorUserId();
        var actorIsSuperAdmin = User.IsInRole(Roles.SuperAdmin);
        var effectiveIncludeDeleted = actorIsSuperAdmin && includeDeleted;
        var items = await _tenantService
            .ListForSwitcherAsync(actorUserId, actorIsSuperAdmin, effectiveIncludeDeleted, cancellationToken)
            .ConfigureAwait(false);
        return Ok(items);
    }

    /// <summary>
    /// Current mandant from ambient <see cref="ICurrentTenantAccessor"/> (set by tenant middleware).
    /// Used by FA <c>TenantProvider</c>.
    /// </summary>
    [HttpGet("current")]
    [ProducesResponseType(typeof(CurrentTenantDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentTenant(CancellationToken cancellationToken = default)
    {
        var tenantId = _currentTenantAccessor.TenantId;
        if (!tenantId.HasValue)
        {
            return BadRequest("Tenant context required");
        }

        var tenant = await _db.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (tenant == null)
        {
            return NotFound();
        }

        var nowUtc = DateTime.UtcNow;
        var licenseValid = tenant.LicenseValidUntilUtc.HasValue
            && tenant.LicenseValidUntilUtc.Value > nowUtc;

        return Ok(new CurrentTenantDto
        {
            Id = tenant.Id,
            Slug = tenant.Slug,
            Name = tenant.Name,
            LicenseValidUntilUtc = tenant.LicenseValidUntilUtc,
            LicenseValid = licenseValid,
        });
    }
}
