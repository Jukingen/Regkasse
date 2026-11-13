using System.Text.Json;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// Process-wide overlay for <c>FinanzOnlineOutbox</c> worker options. Config remains the default.
/// Super Admin writes a global <c>tenant_settings</c> row (<c>TenantId</c> null).
/// Legacy boolean values for <see cref="SettingsKey"/> are still accepted.
/// Registered as Singleton (hosted services + readiness). Opens a scope for the scoped
/// <see cref="IDbContextFactory{TContext}"/>.
/// </summary>
public sealed class FinanzOnlineOutboxEnabledOverrideCache
{
    public const string SettingsKey = "FinanzOnlineOutbox:Enabled";
    private const string MemoryKey = "finanzonline.outbox.overlay.global";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FinanzOnlineOutboxEnabledOverrideCache> _logger;
    private readonly object _gate = new();

    public FinanzOnlineOutboxEnabledOverrideCache(
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache,
        ILogger<FinanzOnlineOutboxEnabledOverrideCache> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _logger = logger;
    }

    public FinanzOnlineOutboxEnabledResolution Resolve()
    {
        var overlay = GetOverlay();
        return overlay?.Enabled is null
            ? new FinanzOnlineOutboxEnabledResolution(null, overlay?.HasAny == true ? "global_override" : "config")
            : new FinanzOnlineOutboxEnabledResolution(overlay.Enabled, "global_override");
    }

    public bool IsEnabled(bool configEnabled) => GetOverlay()?.Enabled ?? configEnabled;

    public FinanzOnlineOutboxOverlay? GetOverlay()
    {
        if (_cache.TryGetValue(MemoryKey, out CacheEntry? cached) && cached is not null)
            return cached.Overlay;

        lock (_gate)
        {
            if (_cache.TryGetValue(MemoryKey, out cached) && cached is not null)
                return cached.Overlay;

            var loaded = LoadFromDatabase();
            _cache.Set(MemoryKey, new CacheEntry(loaded), TimeSpan.FromMinutes(5));
            return loaded;
        }
    }

    public void SetOverride(bool enabled) =>
        SetOverlay(new FinanzOnlineOutboxOverlay { Enabled = enabled });

    public void SetOverlay(FinanzOnlineOutboxOverlay? overlay)
    {
        var stored = overlay is null || !overlay.HasAny ? null : overlay;
        _cache.Set(MemoryKey, new CacheEntry(stored), TimeSpan.FromMinutes(5));
    }

    public void ClearOverride() =>
        _cache.Set(MemoryKey, new CacheEntry(null), TimeSpan.FromMinutes(5));

    internal static string Serialize(FinanzOnlineOutboxOverlay overlay) =>
        JsonSerializer.Serialize(overlay, JsonOptions);

    internal static FinanzOnlineOutboxOverlay? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        var trimmed = raw.Trim();
        if (TryParseBool(trimmed, out var enabled))
            return new FinanzOnlineOutboxOverlay { Enabled = enabled };

        try
        {
            var parsed = JsonSerializer.Deserialize<FinanzOnlineOutboxOverlay>(trimmed, JsonOptions);
            return parsed is null || !parsed.HasAny ? null : parsed;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private FinanzOnlineOutboxOverlay? LoadFromDatabase()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using var db = dbFactory.CreateDbContext();
            var row = db.TenantSettings.AsNoTracking()
                .FirstOrDefault(s => s.TenantId == null && s.Key == SettingsKey);
            if (row is null)
                return null;
            return Parse(row.Value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load FinanzOnlineOutbox overlay; using configuration.");
            return null;
        }
    }

    internal static bool TryParseBool(string? raw, out bool value)
    {
        value = false;
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        if (bool.TryParse(raw.Trim(), out value))
            return true;
        var normalized = raw.Trim().ToLowerInvariant();
        if (normalized is "1" or "yes" or "on")
        {
            value = true;
            return true;
        }

        if (normalized is "0" or "no" or "off")
        {
            value = false;
            return true;
        }

        return false;
    }

    private sealed record CacheEntry(FinanzOnlineOutboxOverlay? Overlay);
}

public readonly record struct FinanzOnlineOutboxEnabledResolution(bool? Overlay, string Source);

