using KasseAPI_Final.DTOs;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Fleet-level TSE health checks for Super Admin / external monitoring.
/// Delegates live probes to <see cref="ITseDeviceHealthCheckService"/>.
/// Named separately from the process hosted probe <c>Services.TseHealthCheckService</c>.
/// </summary>
public interface ITseHealthCheckService
{
    Task<IReadOnlyList<TseHealthResult>> CheckAllDevicesAsync(
        CancellationToken cancellationToken = default);

    /// <param name="liveProbe">
    /// When true, runs live device probes (writes health columns).
    /// When false, returns last-known <see cref="Models.TseDevice"/> health (scrape-safe).
    /// </param>
    Task<TseFleetHealthStatusDto> GetOverallStatusAsync(
        bool liveProbe = true,
        CancellationToken cancellationToken = default);
}
