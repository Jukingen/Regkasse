using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services;

public sealed class ApprovalWorkflowService : IApprovalWorkflowService
{
    private readonly AppDbContext _context;
    private readonly ISettingsTenantResolver _tenantResolver;
    private readonly IOptionsMonitor<PaymentReversalApprovalOptions> _options;

    public ApprovalWorkflowService(
        AppDbContext context,
        ISettingsTenantResolver tenantResolver,
        IOptionsMonitor<PaymentReversalApprovalOptions> options)
    {
        _context = context;
        _tenantResolver = tenantResolver;
        _options = options;
    }

    public async Task<ApprovalRequirement> CheckApprovalRequirementAsync(
        PaymentDetails payment,
        PaymentReversalOperation operation,
        decimal amount,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var opts = _options.CurrentValue;
        if (!opts.Enabled)
            return new ApprovalRequirement { RequiresApproval = false };

        var factors = new List<string>();
        var reasons = new List<string>();

        if (amount >= opts.HighRiskAmountThresholdEur)
        {
            factors.Add("HIGH_AMOUNT");
            reasons.Add($"Betrag über {opts.HighRiskAmountThresholdEur:N0}€");
        }

        if (operation == PaymentReversalOperation.Refund
            && payment.TotalAmount > 0m
            && amount / payment.TotalAmount >= opts.HighRiskRefundShareThreshold)
        {
            factors.Add("HIGH_REFUND_SHARE");
            reasons.Add(
                $"Erstattungsanteil über {opts.HighRiskRefundShareThreshold:P0} des Gesamtbetrags");
        }

        if (opts.OfflineOriginAlwaysRequiresApproval && payment.OfflineTransactionId != null)
        {
            factors.Add("OFFLINE_ORIGIN");
            reasons.Add("Offline-Ursprungszahlung");
        }

        if (opts.PaymentOlderThanThresholdRequiresApproval
            && payment.CreatedAt < DateTime.UtcNow.AddHours(-opts.HighRiskPaymentAgeHours))
        {
            factors.Add("PAYMENT_AGE");
            reasons.Add($"Zahlung ist älter als {opts.HighRiskPaymentAgeHours} Stunden");
        }

        if (opts.StornoFrequencyRequiresApproval && !string.IsNullOrWhiteSpace(userId))
        {
            var recentStornos = await CountRecentStornosForUserAsync(userId, cancellationToken);
            if (recentStornos >= opts.HighRiskStornoCountThreshold)
            {
                factors.Add("HIGH_STORNO_FREQUENCY");
                reasons.Add(
                    $"Zu viele Stornos in kurzer Zeit ({recentStornos} in der letzten {opts.HighRiskStornoWindowHours} Stunde(n))");
            }
        }

        return new ApprovalRequirement
        {
            RequiresApproval = factors.Count > 0,
            Reason = reasons.Count > 0 ? reasons[0] : null,
            RiskFactors = factors,
            Reasons = reasons,
        };
    }

    private async Task<int> CountRecentStornosForUserAsync(string userId, CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue;
        var tenantId = await _tenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken);
        var cutoff = DateTime.UtcNow.AddHours(-opts.HighRiskStornoWindowHours);

        return await _context.PaymentDetails.AsNoTracking()
            .Where(p => p.IsStorno && p.CreatedAt > cutoff)
            .Where(p => p.CreatedBy == userId || p.CashierId == userId)
            .Where(p => _context.CashRegisters.Any(cr => cr.Id == p.CashRegisterId && cr.TenantId == tenantId))
            .CountAsync(cancellationToken);
    }
}
