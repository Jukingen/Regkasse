using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Tenancy;

/// <inheritdoc />
public sealed class LicenseSyncService : ILicenseSyncService
{
    private readonly AppDbContext _db;
    private readonly ILogger<LicenseSyncService> _logger;

    public LicenseSyncService(AppDbContext db, ILogger<LicenseSyncService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task SyncTenantLicenseExpiryAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null)
            return;

        var latest = await ResolveLatestActiveIssuedForTenantAsync(tenant, cancellationToken)
            .ConfigureAwait(false);
        if (latest == null)
            return;

        await ApplyIssuedExpiryToTenantAsync(tenant, latest, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task SyncTenantsForLicenseKeyAsync(string licenseKey, CancellationToken cancellationToken = default)
    {
        var key = licenseKey?.Trim();
        if (string.IsNullOrEmpty(key))
            return;

        var latest = await _db.IssuedLicenses
            .AsNoTracking()
            .Where(il =>
                il.LicenseKey == key
                && !il.IsDeleted
                && !il.IsRevoked
                && !il.IsCancelled
                && il.SupersededByLicenseId == null)
            .OrderByDescending(il => il.IssuedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (latest == null)
            return;

        var tenants = await _db.Tenants
            .IgnoreQueryFilters()
            .Where(t =>
                t.LicenseKey == key
                || t.Name == latest.CustomerName)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (tenants.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var tenant in tenants)
        {
            if (!ShouldSyncTenantFromIssued(tenant))
                continue;

            tenant.LicenseValidUntilUtc = DateTime.SpecifyKind(latest.ExpiryAtUtc, DateTimeKind.Utc);
            if (string.IsNullOrWhiteSpace(tenant.LicenseKey))
                tenant.LicenseKey = key;
            tenant.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Synced mandant license expiry from issued_licenses for key prefix {Prefix} on {TenantCount} tenant(s). ExpiryUtc={Expiry:o}",
            SafePrefix(key),
            tenants.Count,
            latest.ExpiryAtUtc);
    }

    /// <inheritdoc />
    public async Task SyncTenantsForLicenseKeyReplacementAsync(
        string? previousLicenseKey,
        string newLicenseKey,
        CancellationToken cancellationToken = default)
    {
        await SyncTenantsForLicenseKeyAsync(newLicenseKey, cancellationToken).ConfigureAwait(false);

        var prev = previousLicenseKey?.Trim();
        var next = newLicenseKey.Trim();
        if (string.IsNullOrEmpty(prev) || string.Equals(prev, next, StringComparison.OrdinalIgnoreCase))
            return;

        var latest = await _db.IssuedLicenses
            .AsNoTracking()
            .Where(il =>
                il.LicenseKey == next
                && !il.IsDeleted
                && !il.IsRevoked
                && !il.IsCancelled
                && il.SupersededByLicenseId == null)
            .OrderByDescending(il => il.IssuedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (latest == null)
            return;

        var staleKeyTenants = await _db.Tenants
            .IgnoreQueryFilters()
            .Where(t => t.LicenseKey == prev)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (staleKeyTenants.Count == 0)
            return;

        var expiry = DateTime.SpecifyKind(latest.ExpiryAtUtc, DateTimeKind.Utc);
        var now = DateTime.UtcNow;
        foreach (var tenant in staleKeyTenants)
        {
            if (!ShouldSyncTenantFromIssued(tenant))
                continue;

            tenant.LicenseKey = next;
            tenant.LicenseValidUntilUtc = expiry;
            tenant.UpdatedAt = now;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Replaced mandant license key {OldPrefix} -> {NewPrefix} on {TenantCount} tenant(s).",
            SafePrefix(prev),
            SafePrefix(next),
            staleKeyTenants.Count);
    }

    private async Task<IssuedLicense?> ResolveLatestActiveIssuedForTenantAsync(
        Tenant tenant,
        CancellationToken cancellationToken)
    {
        var key = tenant.LicenseKey?.Trim();
        if (string.IsNullOrEmpty(key) || IsSyntheticTierKey(key))
            return null;

        return await _db.IssuedLicenses
            .AsNoTracking()
            .Where(il =>
                il.LicenseKey == key
                && !il.IsDeleted
                && !il.IsRevoked
                && !il.IsCancelled
                && il.SupersededByLicenseId == null)
            .OrderByDescending(il => il.IssuedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task ApplyIssuedExpiryToTenantAsync(
        Tenant tenant,
        IssuedLicense latest,
        CancellationToken cancellationToken)
    {
        if (!ShouldSyncTenantFromIssued(tenant))
            return;

        var expiry = DateTime.SpecifyKind(latest.ExpiryAtUtc, DateTimeKind.Utc);
        if (tenant.LicenseValidUntilUtc == expiry
            && string.Equals(tenant.LicenseKey?.Trim(), latest.LicenseKey, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        tenant.LicenseValidUntilUtc = expiry;
        if (string.IsNullOrWhiteSpace(tenant.LicenseKey))
            tenant.LicenseKey = latest.LicenseKey;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Synced tenant {TenantId} ({Slug}) license_valid_until_utc to {Expiry:o} from issued license {IssuedId}",
            tenant.Id,
            tenant.Slug,
            expiry,
            latest.Id);
    }

    private static bool ShouldSyncTenantFromIssued(Tenant tenant)
    {
        if (string.Equals(tenant.Status, TenantStatuses.Deleted, StringComparison.OrdinalIgnoreCase))
            return false;

        var key = tenant.LicenseKey?.Trim();
        if (!string.IsNullOrEmpty(key) && IsSyntheticTierKey(key))
            return false;

        return true;
    }

    internal static bool IsActiveIssuedLicense(IssuedLicense il) =>
        !il.IsDeleted
        && !il.IsRevoked
        && !il.IsCancelled
        && il.SupersededByLicenseId == null;

    private static bool IsSyntheticTierKey(string key) =>
        key.StartsWith("TIER:", StringComparison.OrdinalIgnoreCase)
        || key.StartsWith("TEST-", StringComparison.OrdinalIgnoreCase);

    private static string SafePrefix(string key) =>
        key.Length <= 12 ? key : key[..12];
}
