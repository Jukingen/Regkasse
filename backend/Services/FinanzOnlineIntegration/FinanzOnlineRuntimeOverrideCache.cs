using System.Text.Json;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FinanzOnlineIntegration;

/// <summary>
/// Process-wide overlay for FinanzOnline runtime options. Registered as Singleton
/// (hosted services + <see cref="FinanzOnlineRuntimeOptionsAccessor"/>).
/// <see cref="IDbContextFactory{TContext}"/> is scoped — open a scope before use.
/// </summary>
public sealed class FinanzOnlineRuntimeOverrideCache
{
    private const string MemoryKey = "finanzonline.runtime.overlay.global";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FinanzOnlineRuntimeOverrideCache> _logger;
    private readonly object _gate = new();

    public FinanzOnlineRuntimeOverrideCache(
        IServiceScopeFactory scopeFactory,
        IMemoryCache cache,
        ILogger<FinanzOnlineRuntimeOverrideCache> logger)
    {
        _scopeFactory = scopeFactory;
        _cache = cache;
        _logger = logger;
    }

    public FinanzOnlineRuntimeOverlay? GetOverlay()
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

    public void SetOverlay(FinanzOnlineRuntimeOverlay? overlay)
    {
        var stored = overlay is null || !overlay.HasAny ? null : overlay;
        _cache.Set(MemoryKey, new CacheEntry(stored), TimeSpan.FromMinutes(5));
    }

    public void ClearOverride() =>
        _cache.Set(MemoryKey, new CacheEntry(null), TimeSpan.FromMinutes(5));

    internal static string Serialize(FinanzOnlineRuntimeOverlay overlay) =>
        JsonSerializer.Serialize(overlay, JsonOptions);

    internal static FinanzOnlineRuntimeOverlay? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        try
        {
            var parsed = JsonSerializer.Deserialize<FinanzOnlineRuntimeOverlay>(raw, JsonOptions);
            return parsed is null || !parsed.HasAny ? null : parsed;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private FinanzOnlineRuntimeOverlay? LoadFromDatabase()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
            using var db = dbFactory.CreateDbContext();
            var row = db.TenantSettings.AsNoTracking()
                .FirstOrDefault(s => s.TenantId == null && s.Key == FinanzOnlineRuntimeOverlay.SettingsKey);
            return row is null ? null : Parse(row.Value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load FinanzOnline runtime overlay; using configuration.");
            return null;
        }
    }

    private sealed record CacheEntry(FinanzOnlineRuntimeOverlay? Overlay);
}

public sealed class FinanzOnlineRuntimeOptionsAccessor
{
    private readonly IOptionsMonitor<FinanzOnlineSessionOptions> _session;
    private readonly IOptionsMonitor<FinanzOnlineRegistrierkassenOptions> _registrierkassen;
    private readonly IOptionsMonitor<FinanzOnlineTransmissionQueryOptions> _transmission;
    private readonly IOptionsMonitor<FinanzOnlineRetryJobOptions> _retry;
    private readonly FinanzOnlineRuntimeOverrideCache _cache;
    private readonly IHostEnvironment _hostEnvironment;

    public FinanzOnlineRuntimeOptionsAccessor(
        IOptionsMonitor<FinanzOnlineSessionOptions> session,
        IOptionsMonitor<FinanzOnlineRegistrierkassenOptions> registrierkassen,
        IOptionsMonitor<FinanzOnlineTransmissionQueryOptions> transmission,
        IOptionsMonitor<FinanzOnlineRetryJobOptions> retry,
        FinanzOnlineRuntimeOverrideCache cache,
        IHostEnvironment hostEnvironment)
    {
        _session = session;
        _registrierkassen = registrierkassen;
        _transmission = transmission;
        _retry = retry;
        _cache = cache;
        _hostEnvironment = hostEnvironment;
    }

    private bool IsProduction => _hostEnvironment.IsProduction();
    private FinanzOnlineRuntimeOverlay? Overlay => _cache.GetOverlay();

    public FinanzOnlineSessionOptions Session =>
        _session.CurrentValue.WithRuntime(Overlay, IsProduction);

    public FinanzOnlineRegistrierkassenOptions Registrierkassen =>
        _registrierkassen.CurrentValue.WithRuntime(Overlay, IsProduction);

    public FinanzOnlineTransmissionQueryOptions TransmissionQuery =>
        _transmission.CurrentValue.WithRuntime(Overlay, IsProduction);

    public FinanzOnlineRetryJobOptions RetryJob =>
        _retry.CurrentValue.WithRuntime(Overlay);
}
