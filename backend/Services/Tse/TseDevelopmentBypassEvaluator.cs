using KasseAPI_Final.Configuration;
using KasseAPI_Final.Services.Rksv;
using Microsoft.Extensions.Hosting;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Effective TSE health/status bypass in Development. Never bypasses Production/Staging.
/// Overlay <c>TseMode=Real</c> always wins (probes and signing run).
/// </summary>
public static class TseDevelopmentBypassEvaluator
{
    /// <summary>
    /// True when TSE health probes and "device ready" shortcuts should skip hardware.
    /// Does not switch the signing provider (<c>Tse:Mode</c> / <c>Tse:TseMode</c>).
    /// </summary>
    public static bool ShouldBypassTseHealth(
        IHostEnvironment? environment,
        DevelopmentOptions? developmentOptions,
        bool developmentModeEnabled,
        bool developmentModeBypassTseCheck,
        RksvRuntimeSnapshot? overlay)
    {
        if (environment is null || !environment.IsDevelopment())
            return false;

        // FA /admin/rksv/config: Real TSE overlay forces real probes even if bypass flags are on.
        if (overlay is { IsTseSimulation: false })
            return false;

        if (overlay?.BypassTseInDevelopment == true)
            return true;

        if (developmentOptions?.BypassTseInDevelopment == true)
            return true;

        return developmentModeEnabled && developmentModeBypassTseCheck;
    }
}
