using KasseAPI_Final.Authorization;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Controllers;

/// <summary>
/// Yönetici: yapılandırılabilir ödeme yöntemleri CRUD (POS listesi pro Kasse + payment_details için RKSV legacy eşlemesi).
/// </summary>
[ApiController]
[Route("api/admin/payment-method-definitions")]
public class PaymentMethodDefinitionsController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<PaymentMethodDefinitionsController> _logger;
    private readonly ISettingsTenantResolver _settingsTenantResolver;

    public PaymentMethodDefinitionsController(
        AppDbContext context,
        ILogger<PaymentMethodDefinitionsController> logger,
        ISettingsTenantResolver settingsTenantResolver)
    {
        _context = context;
        _logger = logger;
        _settingsTenantResolver = settingsTenantResolver;
    }

    [HttpGet]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<IEnumerable<PaymentMethodDefinitionAdminDto>>> GetAll(
        [FromQuery] Guid cashRegisterId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (cashRegisterId == Guid.Empty)
                return BadRequest(new { message = "cashRegisterId is required." });

            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            var register = await _context.CashRegisters.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == cashRegisterId && x.TenantId == tenantId, cancellationToken);
            if (register == null)
                return NotFound(new { message = "Cash register not found." });

            var list = await _context.PaymentMethodDefinitions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.CashRegisterId == cashRegisterId)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Code)
                .ToListAsync(cancellationToken);
            return Ok(list.Select(x => ToAdminDto(x, register.RegisterNumber)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error listing payment method definitions");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpGet("{id:guid}")]
    [HasPermission(AppPermissions.SettingsView)]
    public async Task<ActionResult<PaymentMethodDefinitionAdminDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            var item = await _context.PaymentMethodDefinitions.IgnoreQueryFilters().AsNoTracking()
                .Include(x => x.CashRegister)
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken);
            if (item == null)
                return NotFound(new { message = "Payment method definition not found" });
            return Ok(ToAdminDto(item, item.CashRegister?.RegisterNumber));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting payment method definition {Id}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPost]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<ActionResult<PaymentMethodDefinitionAdminDto>> Create([FromBody] CreatePaymentMethodDefinitionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            if (request.CashRegisterId == Guid.Empty)
                return BadRequest(new { message = "CashRegisterId is required." });

            var register = await _context.CashRegisters.IgnoreQueryFilters().AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.CashRegisterId && x.TenantId == tenantId, cancellationToken);
            if (register == null)
                return NotFound(new { message = "Cash register not found." });

            var code = NormalizeCode(request.Code);
            if (string.IsNullOrEmpty(code))
                return BadRequest(new { message = "Code is required." });

            var exists = await _context.PaymentMethodDefinitions.IgnoreQueryFilters().AnyAsync(
                x => x.CashRegisterId == request.CashRegisterId && x.Code == code, cancellationToken);
            if (exists)
                return BadRequest(new { message = "A payment method with this code already exists for this cash register." });

            var now = DateTime.UtcNow;
            var entity = new PaymentMethodDefinition
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CashRegisterId = request.CashRegisterId,
                Code = code,
                Name = request.Name.Trim(),
                LegacyPaymentMethodValue = request.LegacyPaymentMethodValue,
                FiscalCategory = string.IsNullOrWhiteSpace(request.FiscalCategory) ? null : request.FiscalCategory.Trim(),
                IsActive = request.IsActive,
                IsDefault = request.IsDefault,
                DisplayOrder = request.DisplayOrder,
                RequiresTerminal = request.RequiresTerminal,
                TerminalType = string.IsNullOrWhiteSpace(request.TerminalType) ? null : request.TerminalType.Trim(),
                AllowRefund = request.AllowRefund,
                Icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim(),
                MetadataJson = string.IsNullOrWhiteSpace(request.MetadataJson) ? null : request.MetadataJson,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            if (entity.IsDefault)
                await ClearDefaultsExceptAsync(request.CashRegisterId, null, cancellationToken);

            _context.PaymentMethodDefinitions.Add(entity);
            await _context.SaveChangesAsync(cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = entity.Id }, ToAdminDto(entity, register.RegisterNumber));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating payment method definition");
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpPut("{id:guid}")]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePaymentMethodDefinitionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            var entity = await _context.PaymentMethodDefinitions
                .IgnoreQueryFilters()
                .Include(x => x.CashRegister)
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken);
            if (entity == null)
                return NotFound(new { message = "Payment method definition not found" });

            if (request.CashRegisterId != Guid.Empty && request.CashRegisterId != entity.CashRegisterId)
                return BadRequest(new { message = "CashRegisterId cannot be changed." });

            var code = NormalizeCode(request.Code);
            if (string.IsNullOrEmpty(code))
                return BadRequest(new { message = "Code is required." });

            var codeTaken = await _context.PaymentMethodDefinitions
                .IgnoreQueryFilters()
                .AnyAsync(
                    x => x.CashRegisterId == entity.CashRegisterId && x.Code == code && x.Id != id,
                    cancellationToken);
            if (codeTaken)
                return BadRequest(new { message = "A payment method with this code already exists for this cash register." });

            if (request.IsDefault)
                await ClearDefaultsExceptAsync(entity.CashRegisterId, id, cancellationToken);

            entity.Code = code;
            entity.Name = request.Name.Trim();
            entity.LegacyPaymentMethodValue = request.LegacyPaymentMethodValue;
            entity.FiscalCategory = string.IsNullOrWhiteSpace(request.FiscalCategory) ? null : request.FiscalCategory.Trim();
            entity.IsActive = request.IsActive;
            entity.IsDefault = request.IsDefault;
            entity.DisplayOrder = request.DisplayOrder;
            entity.RequiresTerminal = request.RequiresTerminal;
            entity.TerminalType = string.IsNullOrWhiteSpace(request.TerminalType) ? null : request.TerminalType.Trim();
            entity.AllowRefund = request.AllowRefund;
            entity.Icon = string.IsNullOrWhiteSpace(request.Icon) ? null : request.Icon.Trim();
            entity.MetadataJson = string.IsNullOrWhiteSpace(request.MetadataJson) ? null : request.MetadataJson;
            entity.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync(cancellationToken);
            return Ok(ToAdminDto(entity, entity.CashRegister?.RegisterNumber));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating payment method definition {Id}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    [HttpDelete("{id:guid}")]
    [HasPermission(AppPermissions.SettingsManage)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
            var entity = await _context.PaymentMethodDefinitions
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(x => x.Id == id && x.TenantId == tenantId, cancellationToken);
            if (entity == null)
                return NotFound(new { message = "Payment method definition not found" });

            entity.IsActive = false;
            entity.UpdatedAtUtc = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
            return Ok(new { id = entity.Id, message = "Payment method deactivated." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deactivating payment method definition {Id}", id);
            return StatusCode(500, new { message = "Internal server error" });
        }
    }

    private async Task ClearDefaultsExceptAsync(Guid cashRegisterId, Guid? exceptId, CancellationToken cancellationToken)
    {
        var q = _context.PaymentMethodDefinitions.Where(x => x.CashRegisterId == cashRegisterId && x.IsDefault);
        if (exceptId.HasValue)
            q = q.Where(x => x.Id != exceptId.Value);
        await q.ExecuteUpdateAsync(s => s.SetProperty(x => x.IsDefault, false), cancellationToken);
    }

    private static string NormalizeCode(string code) => code.Trim().ToLowerInvariant();

    private static PaymentMethodDefinitionAdminDto ToAdminDto(PaymentMethodDefinition x, string? registerNumber) => new()
    {
        Id = x.Id,
        CashRegisterId = x.CashRegisterId,
        CashRegisterNumber = registerNumber,
        Code = x.Code,
        Name = x.Name,
        IsActive = x.IsActive,
        IsDefault = x.IsDefault,
        DisplayOrder = x.DisplayOrder,
        LegacyPaymentMethodValue = x.LegacyPaymentMethodValue,
        FiscalCategory = x.FiscalCategory,
        RequiresTerminal = x.RequiresTerminal,
        TerminalType = x.TerminalType,
        AllowRefund = x.AllowRefund,
        Icon = x.Icon,
        MetadataJson = x.MetadataJson,
        CreatedAtUtc = x.CreatedAtUtc,
        UpdatedAtUtc = x.UpdatedAtUtc,
    };
}
