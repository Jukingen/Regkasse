using KasseAPI_Final.Models;
using Microsoft.Extensions.Hosting;

namespace KasseAPI_Final.Services.Tse;

/// <summary>
/// Development-only Soft TSE fallback when Fiskaly is down.
/// Production/Staging never honor these flags (startup lock rejects them).
/// </summary>
public static class TseSoftFallbackPolicy
{
    public static bool IsAllowed(TseOptions options, IHostEnvironment? environment) =>
        options.FallbackEnabled
        && options.SoftTseEnabled
        && environment?.IsDevelopment() == true;
}
