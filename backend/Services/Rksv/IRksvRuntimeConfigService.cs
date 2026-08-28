using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Rksv;

public interface IRksvRuntimeConfigService
{
    /// <summary>Cached effective overlay (appsettings until the singleton row exists).</summary>
    RksvRuntimeSnapshot GetEffective();

    Task<RksvRuntimeSnapshot> GetEffectiveAsync(CancellationToken cancellationToken = default);

    Task<RksvRuntimeSnapshot> UpdateAsync(
        RksvRuntimeConfig values,
        Guid? updatedByUserId,
        CancellationToken cancellationToken = default);

    Task ReloadCacheAsync(CancellationToken cancellationToken = default);
}
