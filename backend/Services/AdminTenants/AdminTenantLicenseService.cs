using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services;
using KasseAPI_Final.Services.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.AdminTenants;

public sealed class AdminTenantLicenseService : IAdminTenantLicenseService
{
    public static class Tiers
    {
        public const string Basic = "basic";
        public const string Standard = "standard";
        public const string Premium = "premium";

        public static bool IsKnown(string tier) =>
            string.Equals(tier, Basic, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tier, Standard, StringComparison.OrdinalIgnoreCase)
            || string.Equals(tier, Premium, StringComparison.OrdinalIgnoreCase);

        public static IReadOnlyList<string> FeaturesFor(string tier)
        {
            if (string.Equals(tier, Basic, StringComparison.OrdinalIgnoreCase))
                return [LicenseFeatureIds.PosFiscal, LicenseFeatureIds.AdminBasic];
            if (string.Equals(tier, Standard, StringComparison.OrdinalIgnoreCase))
                return
                [
                    LicenseFeatureIds.PosFiscal,
                    LicenseFeatureIds.PosOffline,
                    LicenseFeatureIds.AdminBasic,
                    LicenseFeatureIds.AdminRksv,
                ];
            if (string.Equals(tier, Premium, StringComparison.OrdinalIgnoreCase))
                return LicenseFeatureIds.All;
            throw new ArgumentException($"Unknown tier: {tier}", nameof(tier));
        }
    }

    private readonly AppDbContext _db;
    private readonly ILicenseSyncService _licenseSync;
    private readonly ILicenseIssuanceService _licenseIssuance;
    private readonly ILogger<AdminTenantLicenseService> _logger;

    public AdminTenantLicenseService(
        AppDbContext db,
        ILicenseSyncService licenseSync,
        ILicenseIssuanceService licenseIssuance,
        ILogger<AdminTenantLicenseService> logger)
    {
        _db = db;
        _licenseSync = licenseSync;
        _licenseIssuance = licenseIssuance;
        _logger = logger;
    }

    public async Task<TenantLicenseOverviewDto?> GetOverviewAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null)
            return null;

        var history = await BuildHistoryAsync(tenant, cancellationToken).ConfigureAwait(false);
        var features = await ResolveFeaturesAsync(tenant, cancellationToken).ConfigureAwait(false);
        return new TenantLicenseOverviewDto(BuildStatus(tenant, history, features), history);
    }

    public async Task<(TenantLicenseOverviewDto? Result, string? Error)> ActivateTrialAsync(
        Guid tenantId,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await LoadMutableTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant == null)
            return (null, "Tenant not found.");
        if (tenant.Status == TenantStatuses.Deleted)
            return (null, "Deleted tenants cannot receive a trial license.");

        var now = DateTime.UtcNow;
        tenant.LicenseValidUntilUtc = now.AddDays(30);
        tenant.LicenseKey = null;
        tenant.UpdatedAt = now;
        tenant.UpdatedBy = actorUserId;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Super-admin activated 30-day trial for tenant {TenantId}", tenantId);
        return (await GetOverviewAsync(tenantId, cancellationToken).ConfigureAwait(false), null);
    }

    public async Task<(TenantLicenseOverviewDto? Result, string? Error)> ExtendAsync(
        Guid tenantId,
        ExtendTenantLicenseRequest request,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await LoadMutableTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant == null)
            return (null, "Tenant not found.");
        if (tenant.Status == TenantStatuses.Deleted)
            return (null, "Deleted tenants cannot be updated.");

        var now = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.LicenseKey))
        {
            var key = request.LicenseKey.Trim();
            var issued = await _db.IssuedLicenses.AsNoTracking()
                .Where(il => il.LicenseKey == key && !il.IsDeleted && !il.IsRevoked && !il.IsCancelled && il.SupersededByLicenseId == null)
                .OrderByDescending(il => il.IssuedAtUtc)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            if (issued == null)
                return (null, "Issued license key was not found.");

            tenant.LicenseKey = key;
            tenant.LicenseValidUntilUtc = DateTime.SpecifyKind(issued.ExpiryAtUtc, DateTimeKind.Utc);
        }
        else if (request.ValidUntilUtc.HasValue)
        {
            tenant.LicenseValidUntilUtc = DateTime.SpecifyKind(request.ValidUntilUtc.Value, DateTimeKind.Utc);
        }
        else
        {
            return (null, "Provide licenseKey or validUntilUtc.");
        }

        tenant.UpdatedAt = now;
        tenant.UpdatedBy = actorUserId;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (!string.IsNullOrWhiteSpace(tenant.LicenseKey))
            await _licenseSync.SyncTenantLicenseExpiryAsync(tenantId, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Super-admin extended license for tenant {TenantId}", tenantId);
        return (await GetOverviewAsync(tenantId, cancellationToken).ConfigureAwait(false), null);
    }

    public async Task<(TenantLicenseOverviewDto? Result, string? Error)> SetTierAsync(
        Guid tenantId,
        SetTenantLicenseTierRequest request,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        if (!Tiers.IsKnown(request.Tier))
            return (null, "Invalid tier. Use basic, standard, or premium.");

        var tenant = await LoadMutableTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant == null)
            return (null, "Tenant not found.");
        if (tenant.Status == TenantStatuses.Deleted)
            return (null, "Deleted tenants cannot be updated.");

        var now = DateTime.UtcNow;
        var currentEnd = tenant.LicenseValidUntilUtc;
        var baseInstant = currentEnd.HasValue && currentEnd.Value > now ? currentEnd.Value : now;
        tenant.LicenseValidUntilUtc = request.ValidUntilUtc.HasValue
            ? DateTime.SpecifyKind(request.ValidUntilUtc.Value, DateTimeKind.Utc)
            : baseInstant.AddDays(365);

        if (string.IsNullOrWhiteSpace(tenant.LicenseKey))
            tenant.LicenseKey = $"TIER:{request.Tier.Trim().ToLowerInvariant()}";

        tenant.UpdatedAt = now;
        tenant.UpdatedBy = actorUserId;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Super-admin set license tier {Tier} for tenant {TenantId}",
            request.Tier,
            tenantId);
        return (await GetOverviewAsync(tenantId, cancellationToken).ConfigureAwait(false), null);
    }

    public async Task<(TenantLicenseConsistencyDto? Result, string? Error)> CheckDeploymentConsistencyAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _db.Tenants.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (tenant == null)
            return (null, "Tenant not found.");
        if (tenant.Status == TenantStatuses.Deleted)
            return (null, "Deleted tenants cannot be checked.");

        var warnings = new List<string>();
        var linked = await FindLinkedIssuedLicensesAsync(tenant, cancellationToken).ConfigureAwait(false);
        var now = DateTime.UtcNow;
        var active = linked
            .Where(il => LicenseSyncService.IsActiveIssuedLicense(il) && il.ExpiryAtUtc > now)
            .OrderByDescending(il => il.IssuedAtUtc)
            .FirstOrDefault();

        if (!tenant.LicenseValidUntilUtc.HasValue && string.IsNullOrWhiteSpace(tenant.LicenseKey))
        {
            warnings.Add("Mandant has no license end date or key; nothing to align with deployment licenses.");
            return (new TenantLicenseConsistencyDto(
                IsConsistent: warnings.Count == 0,
                warnings,
                tenant.LicenseValidUntilUtc,
                null,
                null,
                null,
                CanIssueDeploymentLicense: false), null);
        }

        if (active == null)
        {
            warnings.Add(
                "No active issued_licenses row is linked to this tenant (by license key, customer name, or [tenant:guid] marker).");
        }
        else
        {
            var tenantKey = tenant.LicenseKey?.Trim();
            if (!string.IsNullOrEmpty(tenantKey)
                && !IsSyntheticTierKey(tenantKey)
                && !string.Equals(tenantKey, active.LicenseKey, StringComparison.OrdinalIgnoreCase))
            {
                warnings.Add(
                    $"Mandant license key differs from linked issued license ({MaskKey(active.LicenseKey)}).");
            }

            if (tenant.LicenseValidUntilUtc.HasValue)
            {
                var tenantExpiry = DateTime.SpecifyKind(tenant.LicenseValidUntilUtc.Value, DateTimeKind.Utc);
                var issuedExpiry = DateTime.SpecifyKind(active.ExpiryAtUtc, DateTimeKind.Utc);
                if (Math.Abs((tenantExpiry - issuedExpiry).TotalHours) > 1)
                {
                    warnings.Add(
                        $"Mandant valid_until ({tenantExpiry:yyyy-MM-dd HH:mm} UTC) differs from issued expiry ({issuedExpiry:yyyy-MM-dd HH:mm} UTC).");
                }
            }
        }

        var canIssue = active == null
            && tenant.LicenseValidUntilUtc.HasValue
            && tenant.LicenseValidUntilUtc.Value > now;

        return (new TenantLicenseConsistencyDto(
            IsConsistent: warnings.Count == 0,
            warnings,
            tenant.LicenseValidUntilUtc,
            active?.Id,
            active?.LicenseKey,
            active?.ExpiryAtUtc,
            canIssue), null);
    }

    public async Task<(TenantLicenseIssueDeploymentResultDto? Result, string? Error)> IssueDeploymentLicenseAsync(
        Guid tenantId,
        string? actorUserId,
        CancellationToken cancellationToken = default)
    {
        var (check, checkError) = await CheckDeploymentConsistencyAsync(tenantId, cancellationToken)
            .ConfigureAwait(false);
        if (checkError != null)
            return (null, checkError);
        if (check == null)
            return (null, "Consistency check failed.");
        if (!check.CanIssueDeploymentLicense)
            return (null, "Deployment license cannot be issued: mandant has no future end date or an active issued row already exists.");

        var tenant = await LoadMutableTenantAsync(tenantId, cancellationToken).ConfigureAwait(false);
        if (tenant == null)
            return (null, "Tenant not found.");

        var expiry = DateTime.SpecifyKind(tenant.LicenseValidUntilUtc!.Value, DateTimeKind.Utc);
        GenerateLicenseResult issued;
        try
        {
            issued = await _licenseIssuance.IssueAsync(
                    new GenerateLicenseRequest(
                        TenantLicenseLink.BuildCustomerName(tenant),
                        expiry,
                        RequireFingerprint: false,
                        MachineHashHex: null,
                        FeatureIds: Tiers.FeaturesFor(Tiers.Basic)),
                    actorUserId,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (LicenseIssuanceUnavailableException ex)
        {
            _logger.LogWarning(ex, "Deployment license issuance unavailable for tenant {TenantId}", tenantId);
            return (null, ex.Message);
        }

        if (!issued.Success || string.IsNullOrWhiteSpace(issued.LicenseKey))
            return (null, issued.Message ?? "Failed to issue deployment license.");

        tenant.LicenseKey = issued.LicenseKey.Trim();
        tenant.LicenseValidUntilUtc = issued.ExpiryAtUtc.HasValue
            ? DateTime.SpecifyKind(issued.ExpiryAtUtc.Value, DateTimeKind.Utc)
            : expiry;
        tenant.UpdatedAt = DateTime.UtcNow;
        tenant.UpdatedBy = actorUserId;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var issuedRow = await _db.IssuedLicenses.AsNoTracking()
            .Where(il => il.LicenseKey == tenant.LicenseKey)
            .OrderByDescending(il => il.IssuedAtUtc)
            .Select(il => il.Id)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        _logger.LogInformation(
            "Issued deployment license for tenant {TenantId} slug {Slug} keyPrefix {Prefix}",
            tenantId,
            tenant.Slug,
            MaskKey(tenant.LicenseKey));

        var overview = await GetOverviewAsync(tenantId, cancellationToken).ConfigureAwait(false);
        return (new TenantLicenseIssueDeploymentResultDto(
            true,
            "Deployment license (JWT) created and linked to mandant.",
            tenant.LicenseKey,
            issuedRow == default ? null : issuedRow,
            overview), null);
    }

    private async Task<List<IssuedLicense>> FindLinkedIssuedLicensesAsync(
        Tenant tenant,
        CancellationToken cancellationToken)
    {
        var marker = TenantLicenseLink.Marker(tenant.Id);
        var key = tenant.LicenseKey?.Trim();

        var query = _db.IssuedLicenses.AsNoTracking().Where(il => !il.IsDeleted);

        if (!string.IsNullOrEmpty(key) && !IsSyntheticTierKey(key))
        {
            query = query.Where(il =>
                il.LicenseKey == key
                || il.CustomerName == tenant.Name
                || EF.Functions.ILike(il.CustomerName, $"%{marker}%"));
        }
        else
        {
            query = query.Where(il =>
                il.CustomerName == tenant.Name
                || EF.Functions.ILike(il.CustomerName, $"%{marker}%"));
        }

        return await query
            .OrderByDescending(il => il.IssuedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool IsSyntheticTierKey(string key) =>
        key.StartsWith("TIER:", StringComparison.OrdinalIgnoreCase);

    private static string MaskKey(string key) =>
        key.Length <= 16 ? key : key[..16] + "…";

    private async Task<Tenant?> LoadMutableTenantAsync(Guid tenantId, CancellationToken cancellationToken) =>
        await _db.Tenants.FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken).ConfigureAwait(false);

    private async Task<IReadOnlyList<TenantLicenseHistoryItemDto>> BuildHistoryAsync(
        Tenant tenant,
        CancellationToken cancellationToken)
    {
        var items = new List<TenantLicenseHistoryItemDto>();

        if (tenant.LicenseValidUntilUtc.HasValue && string.IsNullOrWhiteSpace(tenant.LicenseKey))
        {
            items.Add(new TenantLicenseHistoryItemDto(
                null,
                "trial",
                tenant.UpdatedAt ?? tenant.CreatedAt,
                $"Demo license until {tenant.LicenseValidUntilUtc:yyyy-MM-dd} UTC",
                null));
        }

        if (!string.IsNullOrWhiteSpace(tenant.LicenseKey))
        {
            var issuedRows = await _db.IssuedLicenses.AsNoTracking()
                .Where(il => il.LicenseKey == tenant.LicenseKey && !il.IsDeleted)
                .OrderByDescending(il => il.IssuedAtUtc)
                .Take(20)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            foreach (var row in issuedRows)
            {
                var features = LicenseFeatureIds.TryParseStoredFeatures(row.FeaturesJson);
                var tier = InferTier(features);
                items.Add(new TenantLicenseHistoryItemDto(
                    row.Id,
                    row.IsRevoked ? "revoked" : "issued",
                    row.IssuedAtUtc,
                    $"Issued to {row.CustomerName} until {row.ExpiryAtUtc:yyyy-MM-dd} UTC"
                        + (tier != null ? $" ({tier})" : string.Empty),
                    row.LicenseKey));
            }
        }

        var byName = await _db.IssuedLicenses.AsNoTracking()
            .Where(il => !il.IsDeleted && il.CustomerName == tenant.Name)
            .OrderByDescending(il => il.IssuedAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var marker = TenantLicenseLink.Marker(tenant.Id);
        var byMarker = await _db.IssuedLicenses.AsNoTracking()
            .Where(il => !il.IsDeleted && EF.Functions.ILike(il.CustomerName, $"%{marker}%"))
            .OrderByDescending(il => il.IssuedAtUtc)
            .Take(10)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var row in byName.Concat(byMarker).Where(r => items.All(i => i.IssuedLicenseId != r.Id)))
        {
            items.Add(new TenantLicenseHistoryItemDto(
                row.Id,
                "issued",
                row.IssuedAtUtc,
                $"Issued (name match) until {row.ExpiryAtUtc:yyyy-MM-dd} UTC",
                row.LicenseKey));
        }

        return items
            .OrderByDescending(i => i.AtUtc)
            .ToList();
    }

    private async Task<IReadOnlyList<string>> ResolveFeaturesAsync(
        Tenant tenant,
        CancellationToken cancellationToken)
    {
        var key = tenant.LicenseKey?.Trim();
        if (string.IsNullOrEmpty(key))
            return Tiers.FeaturesFor(Tiers.Basic);

        if (key.StartsWith("TIER:", StringComparison.OrdinalIgnoreCase))
        {
            var tier = key["TIER:".Length..];
            if (Tiers.IsKnown(tier))
                return Tiers.FeaturesFor(tier);
        }

        var issued = await _db.IssuedLicenses.AsNoTracking()
            .Where(il => il.LicenseKey == key && !il.IsDeleted)
            .OrderByDescending(il => il.IssuedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return issued == null
            ? Tiers.FeaturesFor(Tiers.Premium)
            : LicenseFeatureIds.ParseJsonArrayOrDefault(issued.FeaturesJson);
    }

    private static TenantLicenseStatusDto BuildStatus(
        Tenant tenant,
        IReadOnlyList<TenantLicenseHistoryItemDto> history,
        IReadOnlyList<string> features)
    {
        var (days, kind) = TenantLicenseStatusMapper.ComputeKindAndDays(
            tenant.LicenseValidUntilUtc,
            tenant.LicenseKey);

        _ = history;
        return new TenantLicenseStatusDto(
            kind,
            tenant.LicenseKey,
            tenant.LicenseValidUntilUtc,
            days,
            InferTier(features),
            features);
    }

    private static string? InferTier(IReadOnlyList<string>? features)
    {
        if (features == null || features.Count == 0)
            return null;
        if (features.Count >= LicenseFeatureIds.All.Count)
            return Tiers.Premium;
        if (features.Contains(LicenseFeatureIds.AdminRksv, StringComparer.OrdinalIgnoreCase))
            return Tiers.Standard;
        return Tiers.Basic;
    }
}
