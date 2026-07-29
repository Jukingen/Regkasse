using System.Collections.Concurrent;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.FeatureFlags;

public sealed class FeatureFlagService : IFeatureFlagService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IOptionsMonitor<FeatureFlagsOptions> _options;
    private readonly IMemoryCache _cache;
    private readonly IAuditLogService _auditLog;
    private readonly ILogger<FeatureFlagService> _logger;
    private readonly ConcurrentDictionary<string, byte> _cacheKeys = new(StringComparer.Ordinal);

    public FeatureFlagService(
        IDbContextFactory<AppDbContext> dbFactory,
        IOptionsMonitor<FeatureFlagsOptions> options,
        IMemoryCache cache,
        IAuditLogService auditLog,
        ILogger<FeatureFlagService> logger)
    {
        _dbFactory = dbFactory;
        _options = options;
        _cache = cache;
        _auditLog = auditLog;
        _logger = logger;
    }

    public bool IsEnabled(string featureName, string? tenantId = null)
    {
        var name = FeatureFlagNames.Normalize(featureName);
        if (string.IsNullOrEmpty(name))
            return false;

        var tenantGuid = ParseTenantId(tenantId);
        var cacheKey = CacheKey(name, tenantGuid);
        if (_cache.TryGetValue(cacheKey, out bool cached))
            return cached;

        var effective = ResolveEffective(name, tenantGuid);
        _cache.Set(cacheKey, effective, CacheDuration);
        _cacheKeys.TryAdd(cacheKey, 0);
        return effective;
    }

    public async Task SetEnabledAsync(
        string featureName,
        bool enabled,
        string? tenantId = null,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var name = FeatureFlagNames.Normalize(featureName);
        if (string.IsNullOrEmpty(name) || !FeatureFlagNames.All.Contains(name, StringComparer.Ordinal))
            throw new ArgumentException($"Unknown feature flag '{featureName}'.", nameof(featureName));

        var tenantGuid = ParseTenantId(tenantId);
        var key = FeatureFlagNames.SettingsKey(name);
        var value = enabled ? "true" : "false";

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.TenantSettings
            .FirstOrDefaultAsync(
                s => s.Key == key && s.TenantId == tenantGuid,
                cancellationToken)
            .ConfigureAwait(false);

        var oldValue = row?.Value;
        if (row is null)
        {
            row = new TenantSetting
            {
                Id = Guid.NewGuid(),
                TenantId = tenantGuid,
                Key = key,
                Value = value,
                UpdatedAtUtc = DateTime.UtcNow,
                UpdatedByUserId = actorUserId,
            };
            db.TenantSettings.Add(row);
        }
        else
        {
            row.Value = value;
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.UpdatedByUserId = actorUserId;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Invalidate(name, tenantGuid);

        await _auditLog.LogSystemOperationAsync(
                action: "FEATURE_FLAG_SET",
                entityType: "TenantSetting",
                userId: actorUserId ?? "system",
                userRole: "SuperAdmin",
                description: $"Feature flag {name} set to {value} (tenant={tenantGuid?.ToString("D") ?? "global"})",
                status: AuditLogStatus.Success,
                actionType: AuditEventType.FeatureFlagChanged,
                entityId: row.Id,
                tenantId: tenantGuid,
                oldValues: oldValue is null ? null : new { Value = oldValue },
                newValues: new { Name = name, Value = value, TenantId = tenantGuid })
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Feature flag {Feature} set to {Enabled} for tenant {TenantId}",
            name,
            enabled,
            tenantGuid?.ToString("D") ?? "global");
    }

    public async Task ClearOverrideAsync(
        string featureName,
        string? tenantId = null,
        string? actorUserId = null,
        CancellationToken cancellationToken = default)
    {
        var name = FeatureFlagNames.Normalize(featureName);
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Feature name is required.", nameof(featureName));

        var tenantGuid = ParseTenantId(tenantId);
        var key = FeatureFlagNames.SettingsKey(name);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.TenantSettings
            .FirstOrDefaultAsync(
                s => s.Key == key && s.TenantId == tenantGuid,
                cancellationToken)
            .ConfigureAwait(false);
        if (row is null)
            return;

        var oldValue = row.Value;
        db.TenantSettings.Remove(row);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        Invalidate(name, tenantGuid);

        await _auditLog.LogSystemOperationAsync(
                action: "FEATURE_FLAG_CLEAR",
                entityType: "TenantSetting",
                userId: actorUserId ?? "system",
                userRole: "SuperAdmin",
                description: $"Feature flag {name} override cleared (tenant={tenantGuid?.ToString("D") ?? "global"})",
                status: AuditLogStatus.Success,
                actionType: AuditEventType.FeatureFlagChanged,
                entityId: row.Id,
                tenantId: tenantGuid,
                oldValues: new { Value = oldValue },
                newValues: new { Name = name, Cleared = true, TenantId = tenantGuid })
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<FeatureFlagStatusDto>> GetStatusesAsync(
        string? tenantId = null,
        CancellationToken cancellationToken = default)
    {
        var tenantGuid = ParseTenantId(tenantId);
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var keys = FeatureFlagNames.All.Select(FeatureFlagNames.SettingsKey).ToList();
        var rows = await db.TenantSettings.AsNoTracking()
            .Where(s => keys.Contains(s.Key) && (s.TenantId == null || s.TenantId == tenantGuid))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var list = new List<FeatureFlagStatusDto>(FeatureFlagNames.All.Count);
        foreach (var name in FeatureFlagNames.All)
        {
            var key = FeatureFlagNames.SettingsKey(name);
            var configDefault = GetConfigDefault(name);
            var tenantOverride = tenantGuid is Guid tid
                ? rows.FirstOrDefault(r => r.TenantId == tid && r.Key == key)
                : null;
            var globalOverride = rows.FirstOrDefault(r => r.TenantId == null && r.Key == key);

            bool? overrideValue = null;
            string source = "config";
            bool enabled = configDefault;

            if (tenantOverride is not null && TryParseBool(tenantOverride.Value, out var tVal))
            {
                overrideValue = tVal;
                enabled = tVal;
                source = "tenant_override";
            }
            else if (globalOverride is not null && TryParseBool(globalOverride.Value, out var gVal))
            {
                overrideValue = gVal;
                enabled = gVal;
                source = "global_override";
            }

            list.Add(new FeatureFlagStatusDto
            {
                Name = name,
                Enabled = enabled,
                ConfigDefault = configDefault,
                OverrideValue = overrideValue,
                Source = source,
                TenantId = tenantGuid?.ToString("D"),
            });
        }

        return list;
    }

    private bool ResolveEffective(string canonicalName, Guid? tenantId)
    {
        var configDefault = GetConfigDefault(canonicalName);
        var key = FeatureFlagNames.SettingsKey(canonicalName);

        try
        {
            using var db = _dbFactory.CreateDbContext();
            if (tenantId is Guid tid)
            {
                var tenantRow = db.TenantSettings.AsNoTracking()
                    .FirstOrDefault(s => s.TenantId == tid && s.Key == key);
                if (tenantRow is not null && TryParseBool(tenantRow.Value, out var tVal))
                    return tVal;
            }

            var globalRow = db.TenantSettings.AsNoTracking()
                .FirstOrDefault(s => s.TenantId == null && s.Key == key);
            if (globalRow is not null && TryParseBool(globalRow.Value, out var gVal))
                return gVal;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Feature flag DB lookup failed for {Feature}; using config default", canonicalName);
        }

        return configDefault;
    }

    private bool GetConfigDefault(string canonicalName)
    {
        var opts = _options.CurrentValue;
        return canonicalName switch
        {
            FeatureFlagNames.EnableNewPaymentFlow => opts.EnableNewPaymentFlow,
            FeatureFlagNames.EnableDepExportV2 => opts.EnableDepExportV2,
            FeatureFlagNames.EnableOnlineOrdersV2 => opts.EnableOnlineOrdersV2,
            FeatureFlagNames.EnableAutoAusfall => opts.EnableAutoAusfall,
            _ => false,
        };
    }

    private void Invalidate(string canonicalName, Guid? tenantId)
    {
        _cache.Remove(CacheKey(canonicalName, tenantId));
        _cache.Remove(CacheKey(canonicalName, null));
        // Drop related tenant keys for this flag
        foreach (var key in _cacheKeys.Keys.Where(k => k.StartsWith(canonicalName + "|", StringComparison.Ordinal)))
        {
            _cache.Remove(key);
            _cacheKeys.TryRemove(key, out _);
        }
    }

    private static string CacheKey(string name, Guid? tenantId) =>
        $"{name}|{tenantId?.ToString("N") ?? "global"}";

    private static Guid? ParseTenantId(string? tenantId)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return null;
        return Guid.TryParse(tenantId.Trim(), out var g) && g != Guid.Empty ? g : null;
    }

    private static bool TryParseBool(string? raw, out bool value)
    {
        value = false;
        if (string.IsNullOrWhiteSpace(raw))
            return false;
        if (bool.TryParse(raw.Trim(), out value))
            return true;
        if (raw.Trim() is "1" or "yes" or "on")
        {
            value = true;
            return true;
        }

        if (raw.Trim() is "0" or "no" or "off")
        {
            value = false;
            return true;
        }

        return false;
    }
}
