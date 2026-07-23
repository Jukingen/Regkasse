namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Per-device TSE health evaluation (updates <see cref="Models.TseDevice"/> health columns).
/// Distinct from the process-wide hosted probe <c>Services.TseHealthCheckService</c> / <see cref="ITseHealthMonitor"/>.
/// </summary>
public interface ITseDeviceHealthCheckService
{
    Task<TseHealthResult> CheckHealthAsync(Guid deviceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TseHealthResult>> CheckAllDevicesAsync(CancellationToken cancellationToken = default);

    Task<bool> IsDeviceOperationalAsync(Guid deviceId, CancellationToken cancellationToken = default);
}
