namespace KasseAPI_Final.Models;

/// <summary>
/// Cached TSE availability classification used for POS routing (distinct from <see cref="KasseAPI_Final.Services.TseStatus"/> device DTO).
/// </summary>
public enum TseOperationalHealth
{
    Online = 0,
    Degraded = 1,
    Offline = 2
}
