using KasseAPI_Final.Rksv;

namespace KasseAPI_Final.Services;

/// <summary>
/// RKSV Monatsbeleg session gate: applies when TSE is in production-style mode (same as <see cref="IRksvStartbelegPolicy"/>).
/// Sales blocking is a tenant product policy (<see cref="EvaluateSalesGateAsync"/>), not an RKSV legal requirement.
/// </summary>
public interface IRksvMonatsbelegPolicy
{
    /// <summary>
    /// True when Monatsbeleg status is evaluated for shift open and sales
    /// (RKSV: create within 7 days of month end). Whether sales are blocked depends on
    /// <see cref="EvaluateSalesGateAsync"/>.
    /// </summary>
    bool SessionGateApplies { get; }

    Task<bool> HasMonatsbelegForRegisterMonthAsync(
        Guid cashRegisterId,
        int year,
        int month,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tenant blocking mode + Vienna calendar grace for the previous completed month.
    /// </summary>
    Task<MonatsbelegSalesGateDecision> EvaluateSalesGateAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default);
}
