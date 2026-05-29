using KasseAPI_Final.Configuration;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Tenancy;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace KasseAPI_Final.Services;

public interface ICashRegisterSettingsService
{
    Task<PosCashRegisterFeatureOptions> GetFeatureOptionsAsync(CancellationToken cancellationToken = default);

    Task<CashRegisterSettings> GetOrCreateForEffectiveTenantAsync(CancellationToken cancellationToken = default);

    Task<CashRegisterSettings> UpdateForEffectiveTenantAsync(
        UpdateCashRegisterSettingsRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class UpdateCashRegisterSettingsRequest
{
    public bool EffectiveDefaultOnPosEntry { get; set; } = true;
    public bool AutoOpenSoleClosedRegister { get; set; }
    public bool AutoOpenAssignedClosedRegister { get; set; }
    public decimal DefaultAutoOpenOpeningBalance { get; set; }
}

/// <summary>
/// Tenant-scoped POS cash-register settings from <see cref="CashRegisterSettings"/> table.
/// </summary>
public sealed class CashRegisterSettingsService : ICashRegisterSettingsService
{
    private readonly AppDbContext _db;
    private readonly ICurrentTenantAccessor _tenantAccessor;
    private readonly ISettingsTenantResolver _settingsTenantResolver;
    private PosCashRegisterFeatureOptions? _cachedFeatures;

    public CashRegisterSettingsService(
        AppDbContext db,
        ICurrentTenantAccessor tenantAccessor,
        ISettingsTenantResolver settingsTenantResolver)
    {
        _db = db;
        _tenantAccessor = tenantAccessor;
        _settingsTenantResolver = settingsTenantResolver;
    }

    public async Task<PosCashRegisterFeatureOptions> GetFeatureOptionsAsync(CancellationToken cancellationToken = default)
    {
        if (_cachedFeatures != null)
            return _cachedFeatures;

        if (_tenantAccessor.TenantId == null)
            return PosCashRegisterFeatureOptions.Default;

        var row = await GetOrCreateForEffectiveTenantAsync(cancellationToken).ConfigureAwait(false);
        _cachedFeatures = ToFeatureOptions(row);
        return _cachedFeatures;
    }

    public async Task<CashRegisterSettings> GetOrCreateForEffectiveTenantAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = await _settingsTenantResolver.ResolveEffectiveTenantIdAsync(cancellationToken)
            .ConfigureAwait(false);

        // 1. Try to get existing settings (IgnoreQueryFilters: effective tenant may differ from ambient accessor).
        var existing = await _db.CashRegisterSettings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken)
            .ConfigureAwait(false);

        if (existing != null)
            return existing;

        // 2. If not exists, create new
        var settings = CreateDefaultRow(tenantId);
        _db.CashRegisterSettings.Add(settings);

        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex) when (IsCashRegisterSettingsDuplicateKey(ex))
        {
            // Race condition: another request created it between our check and save
            _db.ChangeTracker.Clear();
            var retry = await _db.CashRegisterSettings
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId, cancellationToken)
                .ConfigureAwait(false);

            if (retry != null)
                return retry;

            throw;
        }

        return settings;
    }

    public async Task<CashRegisterSettings> UpdateForEffectiveTenantAsync(
        UpdateCashRegisterSettingsRequest request,
        CancellationToken cancellationToken = default)
    {
        var row = await GetOrCreateForEffectiveTenantAsync(cancellationToken).ConfigureAwait(false);
        row.EffectiveDefaultOnPosEntry = request.EffectiveDefaultOnPosEntry;
        row.AutoOpenSoleClosedRegister = request.AutoOpenSoleClosedRegister;
        row.AutoOpenAssignedClosedRegister = request.AutoOpenAssignedClosedRegister;
        row.DefaultAutoOpenOpeningBalance = request.DefaultAutoOpenOpeningBalance;
        row.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _cachedFeatures = null;
        return row;
    }

    private static CashRegisterSettings CreateDefaultRow(Guid tenantId) => new()
    {
        TenantId = tenantId,
        EffectiveDefaultOnPosEntry = true,
        AutoOpenSoleClosedRegister = false,
        AutoOpenAssignedClosedRegister = false,
        DefaultAutoOpenOpeningBalance = 0,
        UpdatedAtUtc = DateTime.UtcNow,
    };

    private static PosCashRegisterFeatureOptions ToFeatureOptions(CashRegisterSettings row) => new()
    {
        EffectiveDefaultOnPosEntry = row.EffectiveDefaultOnPosEntry,
        AutoOpenSoleClosedRegister = row.AutoOpenSoleClosedRegister,
        AutoOpenAssignedClosedRegister = row.AutoOpenAssignedClosedRegister,
        DefaultAutoOpenOpeningBalance = row.DefaultAutoOpenOpeningBalance,
    };

    private static bool IsCashRegisterSettingsDuplicateKey(DbUpdateException ex)
    {
        for (var inner = ex.InnerException; inner != null; inner = inner.InnerException)
        {
            if (inner is PostgresException pgEx && pgEx.SqlState == PostgresErrorCodes.UniqueViolation)
                return true;
        }

        return false;
    }
}
