using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Security;
using KasseAPI_Final.Services;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Admin: tenant-scoped MwSt tax group catalog (Austrian rates including 4.9% / 13%).
/// </summary>
[ApiController]
[Route("api/admin/tax-groups")]
public class AdminTaxGroupsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<AdminTaxGroupsController> _logger;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private readonly ITaxRegulationService _taxRegulationService;
    private readonly ITaxBulkUpdateService _taxBulkUpdateService;
    private readonly IProductService _productService;
    private readonly ITaxGroupStatsService _taxGroupStatsService;

    public AdminTaxGroupsController(
        AppDbContext context,
        ILogger<AdminTaxGroupsController> logger,
        ISettingsTenantResolver settingsTenantResolver,
        ITaxRegulationService taxRegulationService,
        ITaxBulkUpdateService taxBulkUpdateService,
        IProductService productService,
        ITaxGroupStatsService taxGroupStatsService)
    {
        _context = context;
        _logger = logger;
        _settingsTenantResolver = settingsTenantResolver;
        _taxRegulationService = taxRegulationService;
        _taxBulkUpdateService = taxBulkUpdateService;
        _productService = productService;
        _taxGroupStatsService = taxGroupStatsService;
    }

    [HttpGet]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<IEnumerable<TaxGroupAdminDto>>> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            await TaxGroupSeedData.SeedSystemTaxGroupsAsync(_context, tenantId, cancellationToken)
                .ConfigureAwait(false);

            var list = await _context.TaxGroups
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId)
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.Rate)
                .ThenBy(x => x.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return Ok(list.Select(ToDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing tax groups");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Product distribution + period Umsatz per tax group (FA stats cards).
    /// GET api/admin/tax-groups/stats?fromUtc=&amp;toUtc=
    /// Defaults to the current UTC calendar year when dates are omitted.
    /// </summary>
    [HttpGet("stats")]
    [HasPermission(AppPermissions.SettingsView)]
    [ProducesResponseType(typeof(TaxGroupStatsReport), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaxGroupStatsReport>> GetStats(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
            await TaxGroupSeedData.SeedSystemTaxGroupsAsync(_context, tenantId, cancellationToken)
                .ConfigureAwait(false);

            var now = DateTime.UtcNow;
            var periodStart = fromUtc ?? new DateTime(now.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            var periodEnd = toUtc ?? now.AddDays(1);

            var report = await _taxGroupStatsService
                .GetStatsAsync(tenantId, periodStart, periodEnd, cancellationToken)
                .ConfigureAwait(false);
            return Ok(report);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tax group stats");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpGet("{id:guid}")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<TaxGroupAdminDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            var item = await _context.TaxGroups.AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken)
                .ConfigureAwait(false);
            if (item == null)
                return NotFound(new { message = "Tax group not found" });
            return Ok(ToDto(item));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting tax group {Id}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    private const int ApplyToProductsMaxBatchSize = 500;

    /// <summary>
    /// Reassign all products from one tax group to another (writes tax history per product).
    /// </summary>
    [HttpPost("bulk-update")]
    [HasPermission(AppPermissions.ProductManage)]
    public async Task<ActionResult<TaxBulkUpdateResultDto>> BulkUpdate(
        [FromBody] TaxBulkUpdateRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.OldTaxGroupId == Guid.Empty || request.NewTaxGroupId == Guid.Empty)
                return BadRequest(new { message = "Old and new tax group ids are required." });

            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
            var actorRaw = User.GetActorUserId();
            var changedBy = Guid.TryParse(actorRaw, out var parsed) ? parsed : Guid.Empty;

            var result = await _taxBulkUpdateService
                .UpdateTaxForProductsAsync(
                    tenantId,
                    request.OldTaxGroupId,
                    request.NewTaxGroupId,
                    changedBy,
                    request.Reason,
                    cancellationToken)
                .ConfigureAwait(false);

            await _productService.InvalidateProductsCacheAsync(tenantId).ConfigureAwait(false);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Tax group not found" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk-updating product tax groups");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    /// <summary>
    /// Assign one tax group to an explicit product selection (FA quick tax actions).
    /// POST api/admin/tax-groups/apply-to-products
    /// </summary>
    [HttpPost("apply-to-products")]
    [HasPermission(AppPermissions.ProductManage)]
    [ProducesResponseType(typeof(TaxApplyToProductsResultDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TaxApplyToProductsResultDto>> ApplyToProducts(
        [FromBody] TaxApplyToProductsRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (request.TaxGroupId == Guid.Empty)
                return BadRequest(new { message = "Tax group id is required." });
            if (request.ProductIds == null || request.ProductIds.Count == 0)
                return BadRequest(new { message = "At least one product id is required." });
            if (request.ProductIds.Count > ApplyToProductsMaxBatchSize)
                return BadRequest(new { message = $"Maximum {ApplyToProductsMaxBatchSize} products per request." });

            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
                .ConfigureAwait(false);
            var actorRaw = User.GetActorUserId();
            var changedBy = Guid.TryParse(actorRaw, out var parsed) ? parsed : Guid.Empty;

            var result = await _taxBulkUpdateService
                .ApplyTaxGroupToProductsAsync(
                    tenantId,
                    request.TaxGroupId,
                    request.ProductIds,
                    changedBy,
                    request.Reason,
                    cancellationToken)
                .ConfigureAwait(false);

            if (result.UpdatedProducts > 0)
                await _productService.InvalidateProductsCacheAsync(tenantId).ConfigureAwait(false);

            return Ok(result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { message = "Tax group not found" });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error applying tax group to selected products");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPost]
    [HasPermission(AppPermissions.ProductManage)]
    public async Task<ActionResult<TaxGroupAdminDto>> Create(
        [FromBody] UpsertTaxGroupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            var name = request.Name.Trim();
            if (string.IsNullOrEmpty(name))
                return BadRequest(new { message = "Name is required." });

            var rate = decimal.Round(request.Rate, 2, MidpointRounding.AwayFromZero);
            if (!await _taxRegulationService.IsTaxRateValidAsync(rate, cancellationToken).ConfigureAwait(false))
            {
                return BadRequest(new
                {
                    message = "Tax rate does not match current Austrian MwSt regulations.",
                    code = "TAX_RATE_INVALID",
                });
            }

            var code = NormalizeAustrianCode(request.AustrianCode);
            if (code != null)
            {
                var codeTaken = await _context.TaxGroups.AnyAsync(
                    x => x.TenantId == tenantId && x.AustrianCode == code,
                    cancellationToken).ConfigureAwait(false);
                if (codeTaken)
                    return BadRequest(new { message = "A tax group with this Austrian code already exists." });
            }

            var now = DateTime.UtcNow;
            var entity = new TaxGroup
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                Name = name,
                Description = TrimOrNull(request.Description),
                Rate = rate,
                IsActive = request.IsActive,
                IsDefault = request.IsDefault,
                IsSystem = false,
                Color = TrimOrNull(request.Color),
                Icon = TrimOrNull(request.Icon),
                AustrianCode = code,
                GroupType = InferGroupType(rate),
                ValidFrom = request.ValidFrom,
                ValidTo = request.ValidTo,
                CreatedAt = now,
                UpdatedAt = now,
            };

            if (entity.IsDefault)
                await ClearDefaultsExceptAsync(tenantId, null, cancellationToken).ConfigureAwait(false);

            _context.TaxGroups.Add(entity);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToDto(entity));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating tax group");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPut("{id:guid}")]
    [HasPermission(AppPermissions.ProductManage)]
    public async Task<ActionResult<TaxGroupAdminDto>> Update(
        Guid id,
        [FromBody] UpsertTaxGroupRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            var entity = await _context.TaxGroups
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken)
                .ConfigureAwait(false);
            if (entity == null)
                return NotFound(new { message = "Tax group not found" });

            var name = request.Name.Trim();
            if (string.IsNullOrEmpty(name))
                return BadRequest(new { message = "Name is required." });

            var rate = decimal.Round(request.Rate, 2, MidpointRounding.AwayFromZero);
            if (!entity.IsSystem
                && !await _taxRegulationService.IsTaxRateValidAsync(rate, cancellationToken).ConfigureAwait(false))
            {
                return BadRequest(new
                {
                    message = "Tax rate does not match current Austrian MwSt regulations.",
                    code = "TAX_RATE_INVALID",
                });
            }

            var code = NormalizeAustrianCode(request.AustrianCode);
            if (entity.IsSystem)
            {
                // System presets keep rate + Austrian code; UI metadata may change.
                if (code != null && !string.Equals(code, entity.AustrianCode, StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = "Austrian code cannot be changed for system tax groups." });
                if (rate != entity.Rate)
                    return BadRequest(new { message = "Rate cannot be changed for system tax groups." });
            }
            else if (code != null)
            {
                var codeTaken = await _context.TaxGroups.AnyAsync(
                    x => x.TenantId == tenantId && x.AustrianCode == code && x.Id != id,
                    cancellationToken).ConfigureAwait(false);
                if (codeTaken)
                    return BadRequest(new { message = "A tax group with this Austrian code already exists." });
                entity.AustrianCode = code;
                entity.Rate = rate;
                entity.GroupType = InferGroupType(entity.Rate);
            }
            else
            {
                entity.AustrianCode = null;
                entity.Rate = rate;
                entity.GroupType = InferGroupType(entity.Rate);
            }

            entity.Name = name;
            entity.Description = TrimOrNull(request.Description);
            entity.IsActive = request.IsActive;
            entity.Color = TrimOrNull(request.Color);
            entity.Icon = TrimOrNull(request.Icon);
            entity.ValidFrom = request.ValidFrom;
            entity.ValidTo = request.ValidTo;
            entity.UpdatedAt = DateTime.UtcNow;

            if (request.IsDefault && !entity.IsDefault)
            {
                await ClearDefaultsExceptAsync(tenantId, entity.Id, cancellationToken).ConfigureAwait(false);
                entity.IsDefault = true;
            }
            else if (!request.IsDefault && entity.IsDefault && !entity.IsSystem)
            {
                entity.IsDefault = false;
            }

            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Ok(ToDto(entity));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating tax group {Id}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(AppPermissions.ProductManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            var entity = await _context.TaxGroups
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken)
                .ConfigureAwait(false);
            if (entity == null)
                return NotFound(new { message = "Tax group not found" });
            if (entity.IsSystem)
                return BadRequest(new { message = "System tax groups cannot be deleted." });
            if (entity.IsDefault)
                return BadRequest(new { message = "Default tax group cannot be deleted. Set another default first." });

            _context.TaxGroups.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting tax group {Id}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    private async Task ClearDefaultsExceptAsync(Guid tenantId, Guid? keepId, CancellationToken cancellationToken)
    {
        var defaults = await _context.TaxGroups
            .Where(x => x.TenantId == tenantId && x.IsDefault && (keepId == null || x.Id != keepId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        foreach (var row in defaults)
            row.IsDefault = false;
    }

    private static TaxGroupAdminDto ToDto(TaxGroup x) => new()
    {
        Id = x.Id,
        Name = x.Name,
        Description = x.Description,
        Rate = x.Rate,
        IsActive = x.IsActive,
        IsDefault = x.IsDefault,
        IsSystem = x.IsSystem,
        Color = x.Color,
        Icon = x.Icon,
        GroupType = x.GroupType?.ToString(),
        AustrianCode = x.AustrianCode,
        ValidFrom = x.ValidFrom,
        ValidTo = x.ValidTo,
        ReplacedBy = x.ReplacedBy,
        CreatedAt = x.CreatedAt,
        UpdatedAt = x.UpdatedAt,
    };

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeAustrianCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;
        var normalized = code.Trim().ToUpperInvariant();
        return normalized is "A" or "B" or "C" or "D" or "E" ? normalized : null;
    }

    private static TaxGroupType? InferGroupType(decimal rate) => rate switch
    {
        20m => TaxGroupType.Standard,
        10m => TaxGroupType.Reduced,
        4.9m => TaxGroupType.ReducedNew,
        13m => TaxGroupType.Middle,
        0m => TaxGroupType.Zero,
        _ => null,
    };
}
