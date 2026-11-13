using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Limits;

/// <summary>Loads, upserts, and evaluates <see cref="TenantLimits"/> rows (Super Admin + enforcement callers).</summary>
public sealed class TenantLimitService : ITenantLimitService
{
    private readonly AppDbContext _db;
    private readonly ITenantLimitCacheService _cache;
    private readonly ILogger<TenantLimitService> _logger;

    public TenantLimitService(
        AppDbContext db,
        ITenantLimitCacheService cache,
        ILogger<TenantLimitService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public async Task<TenantLimits> GetLimitsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await EnsureTenantExistsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return await _cache.GetOrCreateAsync(
                tenantId,
                ct => LoadOrCreateAsync(tenantId, ct),
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> GetLimitValueAsync(
        Guid tenantId,
        string limitKey,
        CancellationToken cancellationToken = default)
    {
        var limits = await GetLimitsAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return limits.GetIntLimit(limitKey);
    }

    public async Task<bool> CheckLimitAsync(
        Guid tenantId,
        string limitKey,
        int currentValue,
        CancellationToken cancellationToken = default)
    {
        var limit = await GetLimitValueAsync(tenantId, limitKey, cancellationToken).ConfigureAwait(false);
        return currentValue < limit;
    }

    public async Task<TenantLimits> UpdateLimitsAsync(
        Guid tenantId,
        UpdateTenantLimitsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        await EnsureTenantExistsAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var row = await GetTrackedRowAsync(tenantId, cancellationToken).ConfigureAwait(false);
        var created = false;
        if (row == null)
        {
            row = TenantLimits.CreateDefault(tenantId);
            _db.TenantLimits.Add(row);
            created = true;
        }

        ApplyRequest(row, request);
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _cache.InvalidateAsync(tenantId, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Tenant limits {Action} TenantId={TenantId}",
            created ? "created" : "updated",
            tenantId);

        return await LoadOrCreateAsync(tenantId, cancellationToken).ConfigureAwait(false);
    }

    public Task<TenantLimits> SetLimitValueAsync(
        Guid tenantId,
        string limitKey,
        decimal value,
        CancellationToken cancellationToken = default)
    {
        var request = ToSingleFieldRequest(limitKey, value);
        return UpdateLimitsAsync(tenantId, request, cancellationToken);
    }

    public async Task ResetLimitsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        await EnsureTenantExistsAsync(tenantId, cancellationToken).ConfigureAwait(false);

        var row = await GetTrackedRowAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (row == null)
        {
            row = TenantLimits.CreateDefault(tenantId);
            _db.TenantLimits.Add(row);
        }
        else
        {
            row.ApplyDefaults();
        }

        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await _cache.InvalidateAsync(tenantId, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Tenant limits reset to defaults TenantId={TenantId}", tenantId);
    }

    private async Task EnsureTenantExistsAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        if (tenantId == Guid.Empty)
            throw new InvalidOperationException("Tenant not found.");

        var exists = await _db.Tenants
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(t => t.Id == tenantId && t.DeletedAtUtc == null, cancellationToken)
            .ConfigureAwait(false);
        if (!exists)
            throw new InvalidOperationException("Tenant not found.");
    }

    /// <summary>Returns the persisted caps row, creating a defaults row on first read.</summary>
    private async Task<TenantLimits> LoadOrCreateAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var existing = await _db.TenantLimits
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(l => l.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (existing != null)
            return existing;

        var created = TenantLimits.CreateDefault(tenantId);
        _db.TenantLimits.Add(created);
        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            _db.Entry(created).State = EntityState.Detached;
            var raced = await _db.TenantLimits
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.TenantId == tenantId, cancellationToken)
                .ConfigureAwait(false);
            if (raced != null)
                return raced;
            throw;
        }

        _logger.LogInformation("Tenant limits created with defaults TenantId={TenantId}", tenantId);

        return await _db.TenantLimits
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(l => l.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);
    }

    private Task<TenantLimits?> GetTrackedRowAsync(Guid tenantId, CancellationToken cancellationToken) =>
        _db.TenantLimits
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.TenantId == tenantId, cancellationToken);

    private static void ApplyRequest(TenantLimits row, UpdateTenantLimitsRequest request)
    {
        if (request.MaxActiveRegistersPerUser is int maxActiveRegistersPerUser)
            row.MaxActiveRegistersPerUser = maxActiveRegistersPerUser;
        if (request.MaxProductsPerTenant is int maxProductsPerTenant)
            row.MaxProductsPerTenant = maxProductsPerTenant;
        if (request.MaxUsersPerTenant is int maxUsersPerTenant)
            row.MaxUsersPerTenant = maxUsersPerTenant;
        if (request.DailyMaxTransactions is int dailyMaxTransactions)
            row.DailyMaxTransactions = dailyMaxTransactions;
        if (request.MaxTransactionAmount is decimal maxTransactionAmount)
            row.MaxTransactionAmount = maxTransactionAmount;
        if (request.DailyMaxRevenue is decimal dailyMaxRevenue)
            row.DailyMaxRevenue = dailyMaxRevenue;
        if (request.MaxBackupsPerTenant is int maxBackupsPerTenant)
            row.MaxBackupsPerTenant = maxBackupsPerTenant;
        if (request.MaxBackupSizeMb is int maxBackupSizeMb)
            row.MaxBackupSizeMb = maxBackupSizeMb;
        if (request.MaxOfflineTransactions is int maxOfflineTransactions)
            row.MaxOfflineTransactions = maxOfflineTransactions;
    }

    private static UpdateTenantLimitsRequest ToSingleFieldRequest(string limitKey, decimal value)
    {
        var key = TenantLimits.NormalizeLimitKey(limitKey);
        return key switch
        {
            TenantLimitKeys.MaxActiveRegistersPerUser => new() { MaxActiveRegistersPerUser = ToPositiveInt(value) },
            TenantLimitKeys.MaxProductsPerTenant => new() { MaxProductsPerTenant = ToPositiveInt(value) },
            TenantLimitKeys.MaxUsersPerTenant => new() { MaxUsersPerTenant = ToPositiveInt(value) },
            TenantLimitKeys.DailyMaxTransactions => new() { DailyMaxTransactions = ToPositiveInt(value) },
            TenantLimitKeys.MaxTransactionAmount => new() { MaxTransactionAmount = ToPositiveMoney(value) },
            TenantLimitKeys.DailyMaxRevenue => new() { DailyMaxRevenue = ToPositiveMoney(value) },
            TenantLimitKeys.MaxBackupsPerTenant => new() { MaxBackupsPerTenant = ToPositiveInt(value) },
            TenantLimitKeys.MaxBackupSizeMb => new() { MaxBackupSizeMb = ToPositiveInt(value) },
            TenantLimitKeys.MaxOfflineTransactions => new() { MaxOfflineTransactions = ToPositiveInt(value) },
            _ => throw new ArgumentOutOfRangeException(nameof(limitKey), limitKey, "Unknown tenant limit key."),
        };
    }

    private static int ToPositiveInt(decimal value)
    {
        var truncated = decimal.ToInt32(decimal.Truncate(value));
        if (truncated < 1 || truncated > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Integer limit must be between 1 and 1000000.");
        return truncated;
    }

    private static decimal ToPositiveMoney(decimal value)
    {
        if (value < 0.01m || value > 1_000_000_000m)
            throw new ArgumentOutOfRangeException(nameof(value), value, "Money limit must be between 0.01 and 1000000000.");
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
