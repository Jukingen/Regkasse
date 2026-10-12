using System.Text.Json;
using System.Text.RegularExpressions;
using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Caching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Services.Extensions;

public sealed record TenantExtensionStatusDto(
    string Name,
    bool Enabled,
    IReadOnlyList<string> Hooks);

public interface ITenantExtensionAdminService
{
    Task<IReadOnlyList<TenantExtensionStatusDto>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<TenantExtensionStatusDto> SetEnabledAsync(
        Guid tenantId,
        string name,
        bool enabled,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken);
}

public sealed class TenantExtensionRegistry : ITenantExtensionRegistry, ITenantExtensionAdminService
{
    private static readonly Regex NamePattern = new("^[a-z0-9_]{1,64}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IReadOnlyList<ITenantFeatureExtension> _extensions;
    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly IMemoryCache _memoryCache;
    private readonly ICacheService _cache;
    private readonly TimeSpan _ttl;
    private readonly IAuditLogService _audit;
    private readonly ILogger<TenantExtensionRegistry> _logger;

    public TenantExtensionRegistry(
        IEnumerable<ITenantFeatureExtension> extensions,
        IDbContextFactory<AppDbContext> dbFactory,
        IMemoryCache memoryCache,
        ICacheService cache,
        IOptions<CacheSettings> cacheSettings,
        IAuditLogService audit,
        ILogger<TenantExtensionRegistry> logger)
    {
        _extensions = extensions.ToList();
        _dbFactory = dbFactory;
        _memoryCache = memoryCache;
        _cache = cache;
        _ttl = cacheSettings.Value.TenantSettingsCacheTtl;
        _audit = audit;
        _logger = logger;
    }

    public IReadOnlyList<ITenantFeatureExtension> GetForHook(Guid tenantId, string hookName)
    {
        if (tenantId == Guid.Empty || string.IsNullOrWhiteSpace(hookName))
            return [];

        var enabled = LoadEnabled(tenantId).ToHashSet(StringComparer.Ordinal);
        if (enabled.Count == 0)
            return [];

        return _extensions
            .Where(ext =>
                enabled.Contains(ext.FeatureName)
                && HooksOf(ext).Contains(hookName, StringComparer.Ordinal)
                && ext.AppliesTo(tenantId))
            .ToList();
    }

    public Task<IReadOnlyList<TenantExtensionStatusDto>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var enabled = LoadEnabled(tenantId).ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<TenantExtensionStatusDto> list = _extensions
            .Select(ext => new TenantExtensionStatusDto(
                ext.FeatureName,
                enabled.Contains(ext.FeatureName),
                HooksOf(ext)))
            .OrderBy(row => row.Name, StringComparer.Ordinal)
            .ToList();
        return Task.FromResult(list);
    }

    public async Task<TenantExtensionStatusDto> SetEnabledAsync(
        Guid tenantId,
        string name,
        bool enabled,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken)
    {
        var featureName = (name ?? string.Empty).Trim();
        if (!NamePattern.IsMatch(featureName))
            throw new ArgumentException("Extension name is invalid.", nameof(name));

        var extension = _extensions.FirstOrDefault(ext =>
            string.Equals(ext.FeatureName, featureName, StringComparison.Ordinal));
        if (extension is null)
            throw new ArgumentException($"Unknown extension '{featureName}'.", nameof(name));

        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.TenantSettings
            .FirstOrDefaultAsync(
                setting => setting.TenantId == tenantId && setting.Key == TenantExtensionSettings.EnabledKey,
                cancellationToken)
            .ConfigureAwait(false);

        var names = ParseNames(row?.Value);
        var wasEnabled = names.Contains(featureName);
        if (enabled)
            names.Add(featureName);
        else
            names.Remove(featureName);

        if (wasEnabled != enabled)
        {
            var stored = JsonSerializer.Serialize(
                names.OrderBy(item => item, StringComparer.Ordinal).ToArray(),
                JsonOptions);
            if (stored.Length > 4000)
                throw new ArgumentException("Enabled extension list is too long.", nameof(name));

            if (row is null)
            {
                row = new TenantSetting
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Key = TenantExtensionSettings.EnabledKey,
                    Value = stored,
                    UpdatedAtUtc = DateTime.UtcNow,
                    UpdatedByUserId = actorUserId,
                };
                db.TenantSettings.Add(row);
            }
            else
            {
                row.Value = stored;
                row.UpdatedAtUtc = DateTime.UtcNow;
                row.UpdatedByUserId = actorUserId;
            }

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await InvalidateAsync(tenantId, cancellationToken).ConfigureAwait(false);

            await _audit.LogSystemOperationAsync(
                    action: "TENANT_EXTENSION_TOGGLED",
                    entityType: "TenantSetting",
                    userId: actorUserId,
                    userRole: actorRole,
                    description: $"Extension {featureName} enabled={enabled}",
                    actionType: AuditEventType.TenantExtensionToggled,
                    entityId: row.Id,
                    tenantId: tenantId,
                    oldValues: new { Name = featureName, Enabled = wasEnabled },
                    newValues: new { Name = featureName, Enabled = enabled })
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Tenant extension {Feature} set to {Enabled} for tenant {TenantId}",
                featureName,
                enabled,
                tenantId);
        }

        return new TenantExtensionStatusDto(featureName, enabled, HooksOf(extension));
    }

    internal static IReadOnlyList<string> HooksOf(ITenantFeatureExtension extension) =>
        extension.GetType()
            .GetCustomAttributes(typeof(TenantExtensionHookAttribute), inherit: false)
            .Cast<TenantExtensionHookAttribute>()
            .Select(attribute => attribute.HookName)
            .Where(hook => !string.IsNullOrWhiteSpace(hook))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

    private string[] LoadEnabled(Guid tenantId)
    {
        var key = CacheKey(tenantId);
        if (_memoryCache.TryGetValue(key, out string[]? local) && local is not null)
            return local;

        var distributed = _cache.GetAsync<string[]>(key).GetAwaiter().GetResult();
        if (distributed is not null)
        {
            _memoryCache.Set(key, distributed, _ttl);
            return distributed;
        }

        var loaded = ReadFromDatabase(tenantId);
        _memoryCache.Set(key, loaded, _ttl);
        _cache.SetAsync(key, loaded, _ttl).GetAwaiter().GetResult();
        return loaded;
    }

    private string[] ReadFromDatabase(Guid tenantId)
    {
        using var db = _dbFactory.CreateDbContext();
        var value = db.TenantSettings.AsNoTracking()
            .Where(setting => setting.TenantId == tenantId && setting.Key == TenantExtensionSettings.EnabledKey)
            .Select(setting => setting.Value)
            .FirstOrDefault();
        return ParseNames(value).OrderBy(item => item, StringComparer.Ordinal).ToArray();
    }

    private async Task InvalidateAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var key = CacheKey(tenantId);
        _memoryCache.Remove(key);
        await _cache.RemoveAsync(key, cancellationToken).ConfigureAwait(false);
    }

    private static string CacheKey(Guid tenantId) =>
        CacheKeys.Format(CacheKeys.TenantSettings, tenantId);

    private static HashSet<string> ParseNames(string? json)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(json))
            return names;

        try
        {
            var parsed = JsonSerializer.Deserialize<string[]>(json, JsonOptions);
            if (parsed is null)
                return names;

            foreach (var item in parsed)
            {
                if (!string.IsNullOrWhiteSpace(item))
                    names.Add(item.Trim());
            }
        }
        catch (JsonException)
        {
            return new HashSet<string>(StringComparer.Ordinal);
        }

        return names;
    }
}
