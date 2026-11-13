namespace KasseAPI_Final.Models;

/// <summary>
/// Per-device health classification for TSE failover (distinct from runtime
/// <see cref="TseOperationalHealth"/> probe status used for POS routing).
/// </summary>
public enum TseHealthStatus
{
    Healthy = 1,
    Degraded = 2,
    Unhealthy = 3,
    Offline = 4,
    Expired = 5,
    Revoked = 6
}
