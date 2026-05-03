namespace KasseAPI_Final.Services;

/// <summary>
/// RKSV Startbeleg session gate: applies when TSE is in production-style mode (not Off/Demo soft signing).
/// </summary>
public interface IRksvStartbelegPolicy
{
    /// <summary>True when Startbeleg must exist before shift open and sales (Device TSE mode).</summary>
    bool SessionGateApplies { get; }

    Task<bool> HasStartbelegForRegisterAsync(Guid cashRegisterId, CancellationToken cancellationToken = default);
}
