namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Detects unhealthy primary TSE devices and activates healthy backups (automatic or manual).
/// </summary>
public interface ITseFailoverService
{
    Task<FailoverResult> CheckAndFailoverAsync(Guid primaryDeviceId, CancellationToken cancellationToken = default);

    Task<FailoverResult> ManualFailoverAsync(
        Guid primaryDeviceId,
        Guid backupDeviceId,
        string performedByUserId,
        CancellationToken cancellationToken = default);

    Task<FailoverResult> RevertToPrimaryAsync(
        Guid primaryDeviceId,
        string performedByUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the device currently expected to sign for a cash register
    /// (failover-active backup if any, otherwise primary).
    /// </summary>
    Task<Models.TseDevice?> GetActiveDeviceForRegisterAsync(
        Guid cashRegisterId,
        CancellationToken cancellationToken = default);
}
