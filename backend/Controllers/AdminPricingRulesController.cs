using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Yönetici: fiyat kuralları (Happy Hour, gün/kategori/ürün/kasa kapsamı).
/// </summary>
[ApiController]
[Route("api/admin/pricing-rules")]
public class AdminPricingRulesController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<AdminPricingRulesController> _logger;
    private readonly ISettingsTenantResolver _settingsTenantResolver;

    public AdminPricingRulesController(
        AppDbContext context,
        ILogger<AdminPricingRulesController> logger,
        ISettingsTenantResolver settingsTenantResolver)
    {
        _context = context;
        _logger = logger;
        _settingsTenantResolver = settingsTenantResolver;
    }

    [HttpGet]
    [HasPermission(AppPermissions.ProductView)]
    public async Task<ActionResult<IEnumerable<PricingRuleAdminDto>>> GetAll(CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            var list = await _context.PricingRules
                .AsNoTracking()
                .Where(r =>
                    (r.TargetScope == PricingRuleTargetScope.Product &&
                     _context.Products.Any(p => p.Id == r.TargetId && p.TenantId == tenantId))
                    || (r.TargetScope == PricingRuleTargetScope.Category &&
                        _context.Categories.Any(c => c.Id == r.TargetId && c.TenantId == tenantId)))
                .OrderByDescending(x => x.Priority)
                .ThenBy(x => x.Name)
                .ToListAsync(cancellationToken);
            return Ok(list.Select(ToDto));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing pricing rules");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpGet("{id:guid}")]
    [HasPermission(AppPermissions.ProductView)]
    public async Task<ActionResult<PricingRuleAdminDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            var item = await _context.PricingRules.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (item == null || !await RuleOwnedByTenantAsync(item, tenantId, cancellationToken))
                return NotFound(new { message = "Pricing rule not found" });
            return Ok(ToDto(item));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting pricing rule {Id}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPost]
    [HasPermission(AppPermissions.ProductManage)]
    public async Task<ActionResult<PricingRuleAdminDto>> Create([FromBody] CreatePricingRuleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var err = ValidateRequest(request);
            if (err != null)
                return BadRequest(new { message = err });

            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            var tenantErr = await ValidateTenantTargetsAsync(request, tenantId, cancellationToken);
            if (tenantErr != null)
                return BadRequest(new { message = tenantErr });

            var now = DateTime.UtcNow;
            var entity = new PricingRule
            {
                Id = Guid.NewGuid(),
                Name = request.Name.Trim(),
                Priority = request.Priority,
                IsActive = request.IsActive,
                ValidFromDate = request.ValidFromDate,
                ValidToDate = request.ValidToDate,
                DaysOfWeekMask = request.DaysOfWeekMask,
                TimeWindowEnabled = request.TimeWindowEnabled,
                TimeStartMinutes = request.TimeStartMinutes,
                TimeEndMinutes = request.TimeEndMinutes,
                TargetScope = request.TargetScope,
                TargetId = request.TargetId,
                ActionType = request.ActionType,
                ActionValue = request.ActionValue,
                CashRegisterId = request.CashRegisterId,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            _context.PricingRules.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToDto(entity));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating pricing rule");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPut("{id:guid}")]
    [HasPermission(AppPermissions.ProductManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePricingRuleRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var err = ValidateRequest(request);
            if (err != null)
                return BadRequest(new { message = err });

            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            var entity = await _context.PricingRules.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity == null || !await RuleOwnedByTenantAsync(entity, tenantId, cancellationToken))
                return NotFound(new { message = "Pricing rule not found" });

            var tenantErr = await ValidateTenantTargetsAsync(request, tenantId, cancellationToken);
            if (tenantErr != null)
                return BadRequest(new { message = tenantErr });

            entity.Name = request.Name.Trim();
            entity.Priority = request.Priority;
            entity.IsActive = request.IsActive;
            entity.ValidFromDate = request.ValidFromDate;
            entity.ValidToDate = request.ValidToDate;
            entity.DaysOfWeekMask = request.DaysOfWeekMask;
            entity.TimeWindowEnabled = request.TimeWindowEnabled;
            entity.TimeStartMinutes = request.TimeStartMinutes;
            entity.TimeEndMinutes = request.TimeEndMinutes;
            entity.TargetScope = request.TargetScope;
            entity.TargetId = request.TargetId;
            entity.ActionType = request.ActionType;
            entity.ActionValue = request.ActionValue;
            entity.CashRegisterId = request.CashRegisterId;
            entity.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return Ok(ToDto(entity));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating pricing rule {Id}", id);
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
            var entity = await _context.PricingRules.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            if (entity == null || !await RuleOwnedByTenantAsync(entity, tenantId, cancellationToken))
                return NotFound(new { message = "Pricing rule not found" });

            entity.IsActive = false;
            entity.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(new { id = entity.Id, message = "Pricing rule deactivated." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating pricing rule {Id}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    private static string? ValidateRequest(CreatePricingRuleRequest request)
    {
        if (request.ValidFromDate > request.ValidToDate)
            return "ValidFromDate must be on or before ValidToDate.";
        if (request.CashRegisterId.HasValue && request.CashRegisterId.Value == Guid.Empty)
            return "CashRegisterId must be null or a non-empty GUID.";
        return null;
    }

    private async Task<bool> RuleOwnedByTenantAsync(PricingRule r, Guid tenantId, CancellationToken cancellationToken) =>
        r.TargetScope switch
        {
            PricingRuleTargetScope.Product => await _context.Products.AsNoTracking()
                .AnyAsync(p => p.Id == r.TargetId && p.TenantId == tenantId, cancellationToken),
            PricingRuleTargetScope.Category => await _context.Categories.AsNoTracking()
                .AnyAsync(c => c.Id == r.TargetId && c.TenantId == tenantId, cancellationToken),
            _ => false
        };

    private async Task<string?> ValidateTenantTargetsAsync(CreatePricingRuleRequest request, Guid tenantId, CancellationToken cancellationToken)
    {
        var targetOk = request.TargetScope switch
        {
            PricingRuleTargetScope.Product => await _context.Products.AsNoTracking()
                .AnyAsync(p => p.Id == request.TargetId && p.TenantId == tenantId, cancellationToken),
            PricingRuleTargetScope.Category => await _context.Categories.AsNoTracking()
                .AnyAsync(c => c.Id == request.TargetId && c.TenantId == tenantId, cancellationToken),
            _ => false
        };
        if (!targetOk)
            return "TargetId must reference a product or category in the current tenant.";

        if (request.CashRegisterId.HasValue)
        {
            var regOk = await _context.CashRegisters.AsNoTracking()
                .AnyAsync(cr => cr.Id == request.CashRegisterId && cr.TenantId == tenantId, cancellationToken);
            if (!regOk)
                return "CashRegisterId must belong to the current tenant.";
        }

        return null;
    }

    private static PricingRuleAdminDto ToDto(PricingRule x) => new()
    {
        Id = x.Id,
        Name = x.Name,
        Priority = x.Priority,
        IsActive = x.IsActive,
        ValidFromDate = x.ValidFromDate,
        ValidToDate = x.ValidToDate,
        DaysOfWeekMask = x.DaysOfWeekMask,
        TimeWindowEnabled = x.TimeWindowEnabled,
        TimeStartMinutes = x.TimeStartMinutes,
        TimeEndMinutes = x.TimeEndMinutes,
        TargetScope = x.TargetScope,
        TargetId = x.TargetId,
        ActionType = x.ActionType,
        ActionValue = x.ActionValue,
        CashRegisterId = x.CashRegisterId,
        CreatedAtUtc = x.CreatedAtUtc,
        UpdatedAtUtc = x.UpdatedAtUtc,
    };
}
