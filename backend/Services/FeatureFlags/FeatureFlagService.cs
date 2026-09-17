using System.Collections.Concurrent;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Countries;
using KasseAPI_Final.Services.Countries;
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
    private readonly ICountryProfileRegistry _countries;
    private readonly ConcurrentDictionary<string, byte> _cacheKeys = new(StringComparer.Ordinal);

    public FeatureFlagService(
        IDbContextFactory<AppDbContext> dbFactory,
        IOptionsMonitor<FeatureFlagsOptions> options,
        IMemoryCache cache,
        IAuditLogService auditLog,
        ILogger<FeatureFlagService> logger,
        ICountryProfileRegistry countries)
    {
        _dbFactory = dbFactory;
        _options = options;
        _cache = cache;
        _auditLog = auditLog;
        _logger = logger;
        _countries = countries;
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

        var effective = Resolve(name, tenantGuid, tenantRow: null, globalRow: null, profile: null, loadFromDb: true).Enabled;
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
        if (!enabled && name == FeatureFlagNames.FiscalRksvAt && tenantGuid is Guid lockTenant)
        {
            var profile = await LoadProfileAsync(lockTenant, cancellationToken).ConfigureAwait(false);
            if (CountryFeatureFlagDefaults.IsRksvAtLocked(name, profile))
                throw new FeatureFlagLockedException(name);
        }

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

        CountryProfile? profile = null;
        if (tenantGuid is Guid tid)
            profile = await LoadProfileAsync(tid, cancellationToken, db).ConfigureAwait(false);

        var list = new List<FeatureFlagStatusDto>(FeatureFlagNames.All.Count);
        foreach (var name in FeatureFlagNames.All)
        {
            var key = FeatureFlagNames.SettingsKey(name);
            var tenantOverride = tenantGuid is Guid
                ? rows.FirstOrDefault(r => r.TenantId == tenantGuid && r.Key == key)
                : null;
            var globalOverride = rows.FirstOrDefault(r => r.TenantId == null && r.Key == key);
            var resolved = Resolve(name, tenantGuid, tenantOverride, globalOverride, profile, loadFromDb: false);

            list.Add(new FeatureFlagStatusDto
            {
                Name = name,
                Enabled = resolved.Enabled,
                ConfigDefault = resolved.ConfigDefault,
                OverrideValue = resolved.OverrideValue,
                Source = resolved.Source,
                TenantId = tenantGuid?.ToString("D"),
            });
        }

        return list;
    }

    private FlagResolution Resolve(
        string canonicalName,
        Guid? tenantId,
        TenantSetting? tenantRow,
        TenantSetting? globalRow,
        CountryProfile? profile,
        bool loadFromDb)
    {
        var configDefault = GetConfigDefault(canonicalName);
        var isExperimental = FeatureFlagNames.IsExperimental(canonicalName);
        var key = FeatureFlagNames.SettingsKey(canonicalName);

        try
        {
            AppDbContext? db = null;
            if (loadFromDb)
                db = _dbFactory.CreateDbContext();

            using (db)
            {
                if (loadFromDb && db is not null)
                {
                    if (!isExperimental && tenantId is Guid profileTenant)
                        profile = LoadProfile(db, profileTenant);

                    if (tenantId is Guid tid)
                    {
                        tenantRow = db.TenantSettings.AsNoTracking()
                            .FirstOrDefault(s => s.TenantId == tid && s.Key == key);
                    }

                    globalRow = db.TenantSettings.AsNoTracking()
                        .FirstOrDefault(s => s.TenantId == null && s.Key == key);
                }

                if (!isExperimental && CountryFeatureFlagDefaults.IsRksvAtLocked(canonicalName, profile))
                {
                    return new FlagResolution(true, FeatureFlagSources.Locked, OverrideValue: null, configDefault);
                }

                if (tenantRow is not null && TryParseBool(tenantRow.Value, out var tVal))
                {
                    return new FlagResolution(tVal, FeatureFlagSources.TenantOverride, tVal, configDefault);
                }

                if (!isExperimental
                    && CountryFeatureFlagDefaults.TryGet(canonicalName, profile, out var countryVal))
                {
                    return new FlagResolution(countryVal, FeatureFlagSources.CountryProfile, OverrideValue: null, configDefault);
                }

                if (globalRow is not null && TryParseBool(globalRow.Value, out var gVal))
                {
                    return new FlagResolution(gVal, FeatureFlagSources.GlobalOverride, gVal, configDefault);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Feature flag DB lookup failed for {Feature}; using config default", canonicalName);
        }

        return new FlagResolution(configDefault, FeatureFlagSources.Config, OverrideValue: null, configDefault);
    }

    private async Task<CountryProfile> LoadProfileAsync(
        Guid tenantId,
        CancellationToken cancellationToken,
        AppDbContext? existing = null)
    {
        if (existing is not null)
            return LoadProfile(existing, tenantId);

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        return LoadProfile(db, tenantId);
    }

    private CountryProfile LoadProfile(AppDbContext db, Guid tenantId)
    {
        // Explicit tenant id: Super Admin may inspect another mandant's flags while ambient
        // EF filters still bind to the caller's tenant. Constrain by TenantId immediately.
        var country = db.CompanySettings.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.TenantId == tenantId)
            .Select(s => s.Country)
            .FirstOrDefault();
        return _countries.GetOrDefault(country);
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
            FeatureFlagNames.FiscalKassenSicherheitDe => opts.Fiscal?.KassenSicherheitDe ?? false,
            FeatureFlagNames.FiscalMwstCh => opts.Fiscal?.MwstCh ?? false,
            FeatureFlagNames.EInvoicingZugferd => opts.EInvoicing?.Zugferd ?? false,
            FeatureFlagNames.EInvoicingXRechnung => opts.EInvoicing?.XRechnung ?? false,
            FeatureFlagNames.EInvoicingQrRechnung => opts.EInvoicing?.QrRechnung ?? false,
            FeatureFlagNames.EInvoicingEn16931 => opts.EInvoicing?.En16931 ?? false,
            FeatureFlagNames.ViesCheckEnabled => opts.Vies?.CheckEnabled ?? false,
            _ => false,
        };
    }

    private void Invalidate(string canonicalName, Guid? tenantId)
    {
        _cache.Remove(CacheKey(canonicalName, tenantId));
        _cache.Remove(CacheKey(canonicalName, null));
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

    private readonly record struct FlagResolution(
        bool Enabled,
        string Source,
        bool? OverrideValue,
        bool ConfigDefault);
}
