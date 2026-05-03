namespace KasseAPI_Final.Services;

/// <summary>
/// RKSV Monatsbeleg session gate: applies when TSE is in production-style mode (same as <see cref="IRksvStartbelegPolicy"/>).
/// </summary>
public interface IRksvMonatsbelegPolicy
{
    /// <summary>True when Monatsbeleg must exist for the Vienna calendar month before shift open and sales.</summary>
    bool SessionGateApplies { get; }

    Task<bool> HasMonatsbelegForRegisterMonthAsync(
        Guid cashRegisterId,
        int year,
        int month,
        CancellationToken cancellationToken = default);
}
