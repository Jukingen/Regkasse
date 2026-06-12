using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services;

public sealed class PaymentMethodDefinitionBootstrapService : IPaymentMethodDefinitionBootstrapService
{
    private readonly AppDbContext _context;

    public PaymentMethodDefinitionBootstrapService(AppDbContext context)
    {
        _context = context;
    }

    public async Task EnsureDefaultsForCashRegisterAsync(
        Guid tenantId,
        Guid cashRegisterId,
        CancellationToken cancellationToken = default)
    {
        var exists = await _context.PaymentMethodDefinitions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(x => x.CashRegisterId == cashRegisterId, cancellationToken);
        if (exists)
            return;

        var sourceRegisterId = await _context.PaymentMethodDefinitions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.CashRegisterId != cashRegisterId)
            .Select(x => x.CashRegisterId)
            .FirstOrDefaultAsync(cancellationToken);

        var utcNow = DateTime.UtcNow;
        List<PaymentMethodDefinition> rows;
        if (sourceRegisterId != Guid.Empty)
        {
            var templates = await _context.PaymentMethodDefinitions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(x => x.CashRegisterId == sourceRegisterId)
                .OrderBy(x => x.DisplayOrder)
                .ThenBy(x => x.Code)
                .ToListAsync(cancellationToken);

            rows = templates.Select(t => new PaymentMethodDefinition
            {
                Id = Guid.NewGuid(),
                TenantId = tenantId,
                CashRegisterId = cashRegisterId,
                Code = t.Code,
                Name = t.Name,
                IsActive = t.IsActive,
                IsDefault = t.IsDefault,
                DisplayOrder = t.DisplayOrder,
                LegacyPaymentMethodValue = t.LegacyPaymentMethodValue,
                FiscalCategory = t.FiscalCategory,
                RequiresTerminal = t.RequiresTerminal,
                TerminalType = t.TerminalType,
                AllowRefund = t.AllowRefund,
                Icon = t.Icon,
                MetadataJson = t.MetadataJson,
                CreatedAtUtc = utcNow,
                UpdatedAtUtc = utcNow,
            }).ToList();
        }
        else
        {
            rows = PaymentMethodDefinitionDefaults.CreateRows(tenantId, cashRegisterId, utcNow).ToList();
        }

        if (rows.Count == 0)
            return;

        _context.PaymentMethodDefinitions.AddRange(rows);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
