using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace KasseAPI_Final.Tse.Fiskaly;

/// <summary>
/// Overlay for <c>Fiskaly:Enabled</c>. Config remains the default.
/// Resolution: tenant row → global row (<c>TenantId</c> null) → config.
/// Mandanten-Admin writes the ambient tenant; Super Admin without tenant writes global.
/// </summary>
public sealed class FiskalyEnabledOverrideCache
{
    public const string SettingsKey = "Fiskaly:Enabled";
    private const string MemoryKeyPrefix = "fiskaly.enabled.override.";

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FiskalyEnabledOverrideCache> _logger;
    private readonly ICurrentTenantAccessor? _tenantAccessor;
    private readonly object _gate = new();

    public FiskalyEnabledOverrideCache(
        IDbContextFactory<AppDbContext> dbFactory,
        IMemoryCache cache,
        ILogger<FiskalyEnabledOverrideCache> logger,
        ICurrentTenantAccessor? tenantAccessor = null)
    {
        _dbFactory = dbFactory;
        _cache = cache;
        _logger = logger;
        _tenantAccessor = tenantAccessor;
    }

    /// <summary>
    /// Effective overlay for the ambient tenant (or global when no tenant).
    /// Null = use <c>Fiskaly:Enabled</c> from configuration.
    /// </summary>
    public bool? OverrideEnabled => Resolve(_tenantAccessor?.TenantId).Overlay;

    public FiskalyEnabledResolution Resolve(Guid? tenantId)
    {
        if (tenantId is Guid tid)
        {
            var tenant = GetScope(tid);
            if (tenant is not null)
                return new FiskalyEnabledResolution(tenant, "tenant_override");
        }

        var global = GetScope(null);
        if (global is not null)
            return new FiskalyEnabledResolution(global, "global_override");

        return new FiskalyEnabledResolution(null, "config");
    }

    public bool IsEnabled(bool configEnabled) => OverrideEnabled ?? configEnabled;

    public void SetOverride(bool enabled) => SetOverride(enabled, _tenantAccessor?.TenantId);

    public void SetOverride(bool enabled, Guid? tenantId)
    {
        _cache.Set(MemoryKey(tenantId), (bool?)enabled, TimeSpan.FromMinutes(5));
    }

    public void ClearOverride() => ClearOverride(_tenantAccessor?.TenantId);

    public void ClearOverride(Guid? tenantId)
    {
        _cache.Set(MemoryKey(tenantId), (bool?)null, TimeSpan.FromMinutes(5));
    }

    private bool? GetScope(Guid? tenantId)
    {
        var key = MemoryKey(tenantId);
        if (_cache.TryGetValue(key, out bool? cached))
            return cached;

        lock (_gate)
        {
            if (_cache.TryGetValue(key, out cached))
                return cached;

            var loaded = LoadFromDatabase(tenantId);
            _cache.Set(key, loaded, TimeSpan.FromMinutes(5));
            return loaded;
        }
    }

    private bool? LoadFromDatabase(Guid? tenantId)
    {
        try
        {
            using var db = _dbFactory.CreateDbContext();
            var row = db.TenantSettings.AsNoTracking()
                .FirstOrDefault(s => s.TenantId == tenantId && s.Key == SettingsKey);
            if (row is null)
                return null;
            return TryParseBool(row.Value, out var value) ? value : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to load Fiskaly:Enabled override for tenant {TenantId}; using next fallback.",
                tenantId?.ToString("D") ?? "global");
            return null;
        }
    }

    private static string MemoryKey(Guid? tenantId) =>
        MemoryKeyPrefix + (tenantId?.ToString("D") ?? "global");

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
}

public readonly record struct FiskalyEnabledResolution(bool? Overlay, string Source);
