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
    IReadOnlyList<string> Hooks,
    object? Options = null);

public interface ITenantExtensionAdminService
{
    Task<IReadOnlyList<TenantExtensionStatusDto>> ListAsync(Guid tenantId, CancellationToken cancellationToken);

    Task<TenantExtensionStatusDto> SetEnabledAsync(
        Guid tenantId,
        string name,
        bool enabled,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken,
        JsonElement? options = null);
}

public sealed class ExtensionOptionsUnsupportedException : Exception
{
    public const string ErrorCode = "EXTENSION_OPTIONS_UNSUPPORTED";

    public ExtensionOptionsUnsupportedException()
        : base("This extension does not accept options.")
    {
    }
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

    public async Task<IReadOnlyList<TenantExtensionStatusDto>> ListAsync(
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        var enabled = LoadEnabled(tenantId).ToHashSet(StringComparer.Ordinal);
        var optionKeys = new List<string>();
        if (_extensions.Any(ext => ext.FeatureName == TenantExtensionNames.MinibarAutoCharge))
            optionKeys.Add(MinibarAutoChargePolicy.SettingsKey);
        if (_extensions.Any(ext => ext.FeatureName == TenantExtensionNames.BreakfastIncluded))
            optionKeys.Add(BreakfastIncludedPolicy.SettingsKey);
        if (_extensions.Any(ext => ext.FeatureName == TenantExtensionNames.LateCheckoutFee))
            optionKeys.Add(LateCheckoutFeePolicy.SettingsKey);

        var storedOptions = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (optionKeys.Count > 0)
        {
            await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
            var rows = await db.TenantSettings.AsNoTracking()
                .Where(row => row.TenantId == tenantId && optionKeys.Contains(row.Key))
                .Select(row => new { row.Key, row.Value })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var row in rows)
                storedOptions[row.Key] = row.Value;
        }

        return _extensions
            .Select(ext => new TenantExtensionStatusDto(
                ext.FeatureName,
                enabled.Contains(ext.FeatureName),
                HooksOf(ext),
                OptionsOf(ext.FeatureName, storedOptions)))
            .OrderBy(row => row.Name, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<TenantExtensionStatusDto> SetEnabledAsync(
        Guid tenantId,
        string name,
        bool enabled,
        string actorUserId,
        string actorRole,
        CancellationToken cancellationToken,
        JsonElement? options = null)
    {
        var featureName = (name ?? string.Empty).Trim();
        var extension = _extensions.FirstOrDefault(ext =>
            string.Equals(ext.FeatureName, featureName, StringComparison.Ordinal));
        var hasOptions = options is JsonElement element
            && element.ValueKind is not JsonValueKind.Undefined and not JsonValueKind.Null;
        var isMinibar = string.Equals(featureName, TenantExtensionNames.MinibarAutoCharge, StringComparison.Ordinal);
        var isBreakfast = string.Equals(featureName, TenantExtensionNames.BreakfastIncluded, StringComparison.Ordinal);
        var isLateCheckout = string.Equals(featureName, TenantExtensionNames.LateCheckoutFee, StringComparison.Ordinal);
        var declaresOptions = extension is not null && (isMinibar || isBreakfast || isLateCheckout);
        if (hasOptions && !declaresOptions)
            throw new ExtensionOptionsUnsupportedException();
        if (!NamePattern.IsMatch(featureName))
            throw new ArgumentException("Extension name is invalid.", nameof(name));
        if (extension is null)
            throw new ArgumentException($"Unknown extension '{featureName}'.", nameof(name));

        bool? requestedFlag = null;
        decimal? requestedPrice = null;
        LateCheckoutFeeOptionsDto? requestedLate = null;
        if (hasOptions && isMinibar)
        {
            if (!TryReadRequireSettlement(options!.Value, out var flag))
                throw new ArgumentException("requireSettlementBeforeClose must be a boolean.", "options");
            requestedFlag = flag;
        }
        else if (hasOptions && isBreakfast)
        {
            if (!BreakfastIncludedPolicy.TryReadPrice(options!.Value, out var price))
                throw new ArgumentException("pricePerPersonPerNight must be a non-negative number.", "options");
            requestedPrice = price;
        }
        else if (hasOptions && isLateCheckout)
        {
            if (!LateCheckoutFeePolicy.TryRead(options!.Value, out var late))
                throw new ArgumentException(
                    "freeUntilLocalTime, fee, and timeZone are required.",
                    "options");
            requestedLate = late;
        }

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

        var enabledChanged = wasEnabled != enabled;
        TenantSetting? policyRow = null;
        var optionsChanged = false;
        var previousFlag = true;
        var previousPrice = 0m;
        var previousLate = LateCheckoutFeePolicy.Defaults;
        string? policyKey = isMinibar
            ? MinibarAutoChargePolicy.SettingsKey
            : isBreakfast
                ? BreakfastIncludedPolicy.SettingsKey
                : isLateCheckout
                    ? LateCheckoutFeePolicy.SettingsKey
                    : null;
        if (policyKey is not null)
        {
            policyRow = await db.TenantSettings
                .FirstOrDefaultAsync(
                    setting => setting.TenantId == tenantId && setting.Key == policyKey,
                    cancellationToken)
                .ConfigureAwait(false);
            if (isMinibar)
                previousFlag = MinibarAutoChargePolicy.RequireSettlementBeforeClose(policyRow?.Value);
            else if (isBreakfast && BreakfastIncludedPolicy.TryReadPrice(policyRow?.Value, out var storedPrice))
                previousPrice = storedPrice;
            else if (isLateCheckout && LateCheckoutFeePolicy.TryRead(policyRow?.Value, out var storedLate))
                previousLate = storedLate;
        }

        if (requestedFlag is bool nextFlag && nextFlag != previousFlag)
            optionsChanged = true;
        if (requestedPrice is decimal nextPrice && nextPrice != previousPrice)
            optionsChanged = true;
        if (requestedLate is LateCheckoutFeeOptionsDto nextLate
            && !LateCheckoutFeePolicy.Same(nextLate, previousLate))
            optionsChanged = true;

        if (optionsChanged && policyKey is not null)
        {
            var storedPolicy = isBreakfast
                ? JsonSerializer.Serialize(new BreakfastIncludedOptionsDto(requestedPrice!.Value), JsonOptions)
                : isLateCheckout
                    ? JsonSerializer.Serialize(requestedLate, JsonOptions)
                    : JsonSerializer.Serialize(new MinibarAutoChargeOptionsDto(requestedFlag!.Value), JsonOptions);
            if (policyRow is null)
            {
                policyRow = new TenantSetting
                {
                    Id = Guid.NewGuid(),
                    TenantId = tenantId,
                    Key = policyKey,
                    Value = storedPolicy,
                    UpdatedAtUtc = DateTime.UtcNow,
                    UpdatedByUserId = actorUserId,
                };
                db.TenantSettings.Add(policyRow);
            }
            else
            {
                policyRow.Value = storedPolicy;
                policyRow.UpdatedAtUtc = DateTime.UtcNow;
                policyRow.UpdatedByUserId = actorUserId;
            }
        }

        if (enabledChanged)
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
        }

        if (enabledChanged || optionsChanged)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            if (enabledChanged)
                await InvalidateAsync(tenantId, cancellationToken).ConfigureAwait(false);

            var auditEntityId = enabledChanged ? row!.Id : policyRow!.Id;
            object? oldOptions = isBreakfast
                ? new { PricePerPersonPerNight = previousPrice }
                : isLateCheckout
                    ? previousLate
                    : new { RequireSettlementBeforeClose = previousFlag };
            object? newOptions = isBreakfast
                ? new { PricePerPersonPerNight = requestedPrice }
                : isLateCheckout
                    ? requestedLate
                    : new { RequireSettlementBeforeClose = requestedFlag };
            object oldValues = optionsChanged
                ? new { Name = featureName, Enabled = wasEnabled, Options = oldOptions }
                : new { Name = featureName, Enabled = wasEnabled };
            object newValues = optionsChanged
                ? new { Name = featureName, Enabled = enabled, Options = newOptions }
                : new { Name = featureName, Enabled = enabled };
            var description = !optionsChanged
                ? $"Extension {featureName} enabled={enabled}"
                : isBreakfast
                    ? $"Extension {featureName} enabled={enabled} pricePerPersonPerNight={requestedPrice}"
                    : isLateCheckout
                        ? $"Extension {featureName} enabled={enabled} fee={requestedLate?.Fee}"
                        : $"Extension {featureName} enabled={enabled} requireSettlementBeforeClose={requestedFlag}";

            await _audit.LogSystemOperationAsync(
                    action: "TENANT_EXTENSION_TOGGLED",
                    entityType: "TenantSetting",
                    userId: actorUserId,
                    userRole: actorRole,
                    description: description,
                    actionType: AuditEventType.TenantExtensionToggled,
                    entityId: auditEntityId,
                    tenantId: tenantId,
                    oldValues: oldValues,
                    newValues: newValues)
                .ConfigureAwait(false);

            _logger.LogInformation(
                "Tenant extension {Feature} set to {Enabled} for tenant {TenantId}",
                featureName,
                enabled,
                tenantId);
        }

        object? optionsDto = isMinibar
            ? new MinibarAutoChargeOptionsDto(requestedFlag ?? previousFlag)
            : isBreakfast
                ? new BreakfastIncludedOptionsDto(requestedPrice ?? previousPrice)
                : isLateCheckout
                    ? requestedLate ?? previousLate
                    : null;
        return new TenantExtensionStatusDto(featureName, enabled, HooksOf(extension), optionsDto);
    }

    private static object? OptionsOf(string featureName, IReadOnlyDictionary<string, string?> stored)
    {
        if (string.Equals(featureName, TenantExtensionNames.MinibarAutoCharge, StringComparison.Ordinal))
        {
            stored.TryGetValue(MinibarAutoChargePolicy.SettingsKey, out var json);
            return new MinibarAutoChargeOptionsDto(MinibarAutoChargePolicy.RequireSettlementBeforeClose(json));
        }

        if (string.Equals(featureName, TenantExtensionNames.BreakfastIncluded, StringComparison.Ordinal))
        {
            stored.TryGetValue(BreakfastIncludedPolicy.SettingsKey, out var json);
            var price = BreakfastIncludedPolicy.TryReadPrice(json, out var parsed) ? parsed : 0m;
            return new BreakfastIncludedOptionsDto(price);
        }

        if (string.Equals(featureName, TenantExtensionNames.LateCheckoutFee, StringComparison.Ordinal))
        {
            stored.TryGetValue(LateCheckoutFeePolicy.SettingsKey, out var json);
            return LateCheckoutFeePolicy.TryRead(json, out var parsed)
                ? parsed
                : LateCheckoutFeePolicy.Defaults;
        }

        return null;
    }

    private static bool TryReadRequireSettlement(JsonElement options, out bool requireSettlementBeforeClose)
    {
        requireSettlementBeforeClose = true;
        if (options.ValueKind != JsonValueKind.Object
            || !options.TryGetProperty("requireSettlementBeforeClose", out var flag)
            || flag.ValueKind is not JsonValueKind.True and not JsonValueKind.False)
            return false;

        requireSettlementBeforeClose = flag.GetBoolean();
        return true;
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
