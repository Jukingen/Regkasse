using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services;

/// <summary>Default POS payment method rows seeded per cash register (matches original global seed).</summary>
public static class PaymentMethodDefinitionDefaults
{
    public static IReadOnlyList<PaymentMethodDefinition> CreateRows(Guid tenantId, Guid cashRegisterId, DateTime utcNow)
    {
        return
        [
            Row(tenantId, cashRegisterId, "cash", "Bar", isActive: true, isDefault: true, displayOrder: 10, legacy: 0, fiscal: "Cash", requiresTerminal: false, terminal: null, icon: "cash-outline", utcNow),
            Row(tenantId, cashRegisterId, "card", "Karte", isActive: true, isDefault: false, displayOrder: 20, legacy: 1, fiscal: "Card", requiresTerminal: true, terminal: "card", icon: "card-outline", utcNow),
            Row(tenantId, cashRegisterId, "transfer", "Überweisung", isActive: true, isDefault: false, displayOrder: 30, legacy: 2, fiscal: "BankTransfer", requiresTerminal: false, terminal: null, icon: "swap-horizontal-outline", utcNow),
            Row(tenantId, cashRegisterId, "voucher", "Gutschein", isActive: true, isDefault: false, displayOrder: 40, legacy: 4, fiscal: "Voucher", requiresTerminal: false, terminal: null, icon: "ticket-outline", utcNow),
            Row(tenantId, cashRegisterId, "check", "Scheck", isActive: false, isDefault: false, displayOrder: 50, legacy: 3, fiscal: "Check", requiresTerminal: false, terminal: null, icon: null, utcNow),
            Row(tenantId, cashRegisterId, "mobile", "Mobil", isActive: false, isDefault: false, displayOrder: 60, legacy: 5, fiscal: "Mobile", requiresTerminal: true, terminal: "softpos", icon: null, utcNow),
        ];
    }

    private static PaymentMethodDefinition Row(
        Guid tenantId,
        Guid cashRegisterId,
        string code,
        string name,
        bool isActive,
        bool isDefault,
        int displayOrder,
        int legacy,
        string fiscal,
        bool requiresTerminal,
        string? terminal,
        string? icon,
        DateTime utcNow) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            CashRegisterId = cashRegisterId,
            Code = code,
            Name = name,
            IsActive = isActive,
            IsDefault = isDefault,
            DisplayOrder = displayOrder,
            LegacyPaymentMethodValue = legacy,
            FiscalCategory = fiscal,
            RequiresTerminal = requiresTerminal,
            TerminalType = terminal,
            AllowRefund = true,
            Icon = icon,
            CreatedAtUtc = utcNow,
            UpdatedAtUtc = utcNow,
        };
}
