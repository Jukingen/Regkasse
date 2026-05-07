using KasseAPI_Final.Configuration;

namespace KasseAPI_Final.Services;

/// <summary>
/// Runs one NTP sampling cycle (SNTP queries, DB log, in-memory status, cash-register drift mirrors).
/// </summary>
public interface INtpSynchronizationCoordinator
{
    /// <param name="ignoreDisabled">
    /// When true, runs sampling even if <see cref="NtpSettings.Enabled"/> is false (manual admin sync).
    /// </param>
    Task<NtpSyncCycleResult> RunSynchronizationCycleAsync(
        NtpSettings settings,
        bool ignoreDisabled,
        CancellationToken cancellationToken);
}
