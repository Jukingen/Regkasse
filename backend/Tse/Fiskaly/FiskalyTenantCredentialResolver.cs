using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace KasseAPI_Final.Tse.Fiskaly;

public interface IFiskalyTenantCredentialResolver
{
    /// <summary>Ambient tenant, else the Development host keys. Null when signing must fail closed.</summary>
    Task<FiskalyResolvedCredentials?> ResolveAsync(CancellationToken cancellationToken = default);

    /// <summary>Explicit tenant. The ambient accessor must already be that tenant (query filter).</summary>
    Task<FiskalyResolvedCredentials?> ResolveAsync(Guid? tenantId, CancellationToken cancellationToken = default);

    Task<bool> HasActiveCredentialsAsync(bool? runtimeOverride, CancellationToken cancellationToken = default);

    void Invalidate(Guid tenantId);
}

public sealed class FiskalyTenantCredentialResolver : IFiskalyTenantCredentialResolver
{
    public const string CacheKeyPrefix = "fiskaly.tenant.credentials.";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ICurrentTenantAccessor _tenant;
    private readonly FiskalyCredentialProtector _protector;
    private readonly IOptionsMonitor<FiskalyOptions> _options;
    private readonly IHostEnvironment _environment;
    private readonly IMemoryCache _cache;
    private readonly ILogger<FiskalyTenantCredentialResolver> _logger;

    public FiskalyTenantCredentialResolver(
        IDbContextFactory<AppDbContext> dbFactory,
        ICurrentTenantAccessor tenant,
        FiskalyCredentialProtector protector,
        IOptionsMonitor<FiskalyOptions> options,
        IHostEnvironment environment,
        IMemoryCache cache,
        ILogger<FiskalyTenantCredentialResolver> logger)
    {
        _dbFactory = dbFactory;
        _tenant = tenant;
        _protector = protector;
        _options = options;
        _environment = environment;
        _cache = cache;
        _logger = logger;
    }

    public Task<FiskalyResolvedCredentials?> ResolveAsync(CancellationToken cancellationToken = default) =>
        ResolveAsync(null, cancellationToken);

    public async Task<FiskalyResolvedCredentials?> ResolveAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default)
    {
        var effective = tenantId ?? _tenant.TenantId;
        if (effective is not Guid tid || tid == Guid.Empty)
            return DevelopmentHost();

        if (_tenant.TenantId is Guid ambient && ambient != tid)
        {
            _logger.LogWarning(
                "Fiskaly credential lookup refused because ambient tenant {Ambient} does not match {TenantId}.",
                ambient,
                tid);
            return null;
        }

        if (_cache.TryGetValue(CacheKey(tid), out TenantCredentialCacheEntry? cached) && cached is not null)
            return FromCache(cached);

        var loaded = await LoadTenantAsync(tid, cancellationToken).ConfigureAwait(false);
        _cache.Set(CacheKey(tid), loaded, CacheTtl);
        return FromCache(loaded);
    }

    public async Task<bool> HasActiveCredentialsAsync(
        bool? runtimeOverride,
        CancellationToken cancellationToken = default)
    {
        if (!_options.CurrentValue.IsEffectivelyEnabled(runtimeOverride))
            return false;

        var resolved = await ResolveAsync(cancellationToken).ConfigureAwait(false);
        return resolved is not null
            && !string.IsNullOrWhiteSpace(resolved.ApiKey)
            && !string.IsNullOrWhiteSpace(resolved.ApiSecret);
    }

    public void Invalidate(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
            return;
        _cache.Remove(CacheKey(tenantId));
    }

    private FiskalyResolvedCredentials? FromCache(TenantCredentialCacheEntry entry) =>
        entry.Kind switch
        {
            TenantCredentialCacheKind.Active => entry.Credentials,
            TenantCredentialCacheKind.Inactive => null,
            _ => DevelopmentHost()
        };

    private async Task<TenantCredentialCacheEntry> LoadTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var row = await db.TenantFiskalyCredentials
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
            return TenantCredentialCacheEntry.Missing();

        if (!row.IsActive)
            return TenantCredentialCacheEntry.Inactive();

        try
        {
            var apiKey = _protector.Unprotect(row.ApiKeyProtected);
            var apiSecret = _protector.Unprotect(row.ApiSecretProtected);
            var resolved = new FiskalyResolvedCredentials(
                tenantId,
                apiKey,
                apiSecret,
                row.ScuId?.Trim() ?? string.Empty,
                string.IsNullOrWhiteSpace(row.Environment) ? FiskalyOptions.TestEnvironment : row.Environment.Trim(),
                row.SigningCertificateDerBase64,
                row.IssuerCertificatesDerBase64 ?? new List<string>(),
                FiskalyCredentialSources.Tenant);
            return TenantCredentialCacheEntry.Active(resolved);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to unprotect Fiskaly credentials for tenant {TenantId}.", tenantId);
            return TenantCredentialCacheEntry.Inactive();
        }
    }

    /// <summary>
    /// Host <c>Fiskaly:ApiKey</c> only in Development, and only when the tenant has no credential row.
    /// Production never uses the process-wide keys.
    /// </summary>
    private FiskalyResolvedCredentials? DevelopmentHost()
    {
        if (!_environment.IsDevelopment())
            return null;

        var options = _options.CurrentValue;
        if (!options.HasApiCredentials)
            return null;

        return new FiskalyResolvedCredentials(
            null,
            options.ApiKey.Trim(),
            options.ApiSecret.Trim(),
            options.SignatureCreationUnitId?.Trim() ?? string.Empty,
            options.ResolveEnvironment(),
            options.SigningCertificateDerBase64,
            options.IssuerCertificatesDerBase64 ?? new List<string>(),
            FiskalyCredentialSources.Host);
    }

    private static string CacheKey(Guid tenantId) => CacheKeyPrefix + tenantId.ToString("D");

    private enum TenantCredentialCacheKind
    {
        Missing = 0,
        Inactive = 1,
        Active = 2
    }

    private sealed record TenantCredentialCacheEntry(TenantCredentialCacheKind Kind, FiskalyResolvedCredentials? Credentials)
    {
        public static TenantCredentialCacheEntry Missing() => new(TenantCredentialCacheKind.Missing, null);

        public static TenantCredentialCacheEntry Inactive() => new(TenantCredentialCacheKind.Inactive, null);

        public static TenantCredentialCacheEntry Active(FiskalyResolvedCredentials credentials) =>
            new(TenantCredentialCacheKind.Active, credentials);
    }
}
