using System.Globalization;
using Microsoft.EntityFrameworkCore;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;

namespace KasseAPI_Final.Services;

/// <summary>
/// Zahlungsarten aus payment_method_definitions; Fallback entspricht der früheren GetPaymentMethodEnum-Switch-Logik.
/// </summary>
public sealed class PaymentMethodCatalogService : IPaymentMethodCatalogService
{
    private readonly AppDbContext _context;
    private readonly ISettingsTenantResolver _settingsTenantResolver;

    public PaymentMethodCatalogService(AppDbContext context, ISettingsTenantResolver settingsTenantResolver)
    {
        _context = context;
        _settingsTenantResolver = settingsTenantResolver;
    }

    public async Task<IReadOnlyList<PosPaymentMethodDto>> GetActivePosMethodsAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var rows = await _context.PaymentMethodDefinitions
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.IsActive)
            .OrderBy(x => x.DisplayOrder)
            .ThenBy(x => x.Code)
            .ToListAsync(cancellationToken);

        return rows.Select(ToPosDto).ToList();
    }

    public async Task<PaymentMethodResolutionResult> ResolveForPaymentAsync(string? methodCode, CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(methodCode);
        if (string.IsNullOrEmpty(code))
            return new PaymentMethodResolutionResult(true, "0", null);

        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var row = await _context.PaymentMethodDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == code, cancellationToken);

        if (row != null)
        {
            if (!row.IsActive)
                return new PaymentMethodResolutionResult(false, "0", "This payment method is disabled.");
            return new PaymentMethodResolutionResult(true, row.LegacyPaymentMethodValue.ToString(CultureInfo.InvariantCulture), null);
        }

        return new PaymentMethodResolutionResult(true, LegacyFallbackRaw(code), null);
    }

    public async Task<string> ResolveRawForFilterAsync(string? methodCode, CancellationToken cancellationToken = default)
    {
        var code = NormalizeCode(methodCode);
        if (string.IsNullOrEmpty(code))
            return "0";

        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var row = await _context.PaymentMethodDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.TenantId == tenantId && x.Code == code, cancellationToken);

        if (row != null)
            return row.LegacyPaymentMethodValue.ToString(CultureInfo.InvariantCulture);

        return LegacyFallbackRaw(code);
    }

    private static PosPaymentMethodDto ToPosDto(PaymentMethodDefinition x)
    {
        var code = x.Code;
        // 0 = Invoice.PaymentMethod.Cash (nested enum name collides with Invoice.PaymentMethod property).
        var requiresCashAmount = x.LegacyPaymentMethodValue == 0;
        return new PosPaymentMethodDto(
            Id: code,
            Name: x.Name,
            Type: code,
            Icon: string.IsNullOrWhiteSpace(x.Icon) ? "ellipse-outline" : x.Icon!,
            IsDefault: x.IsDefault,
            RequiresReceivedAmount: requiresCashAmount,
            RequiresTerminal: x.RequiresTerminal,
            TerminalType: x.TerminalType,
            AllowRefund: x.AllowRefund);
    }

    private static string? NormalizeCode(string? methodCode)
    {
        if (string.IsNullOrWhiteSpace(methodCode)) return null;
        return methodCode.Trim().ToLowerInvariant();
    }

    /// <summary>Identisch zur früheren PaymentService.GetPaymentMethodEnum-Logik.</summary>
    private static string LegacyFallbackRaw(string paymentMethodLower)
    {
        return paymentMethodLower switch
        {
            "cash" => "0",
            "card" => "1",
            "banktransfer" => "2",
            "transfer" => "2",
            "check" => "3",
            "voucher" => "4",
            "mobile" => "5",
            _ => "0"
        };
    }
}
