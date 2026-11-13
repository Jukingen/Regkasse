using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// FA activity feed + Super Admin email alerts for TSE failover lifecycle events.
/// </summary>
public interface ITseFailoverNotificationService
{
    Task NotifyFailoverStartedAsync(
        TseDevice primary,
        TseDevice backup,
        CancellationToken cancellationToken = default);

    Task NotifyFailoverCompletedAsync(
        TseDevice primary,
        TseDevice backup,
        string failoverType,
        CancellationToken cancellationToken = default);

    Task NotifyFailoverFailedAsync(
        TseDevice primary,
        TseDevice? backup,
        Exception? ex = null,
        string? errorMessage = null,
        CancellationToken cancellationToken = default);

    Task NotifyNoBackupAvailableAsync(
        TseDevice primary,
        string? healthMessage = null,
        CancellationToken cancellationToken = default);

    Task NotifyBackupLowHealthAsync(
        TseDevice backup,
        CancellationToken cancellationToken = default);

    Task NotifyFailoverRevertedAsync(
        TseDevice primary,
        CancellationToken cancellationToken = default);
}
