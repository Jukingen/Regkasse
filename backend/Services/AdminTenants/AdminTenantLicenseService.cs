using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
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
    private readonly ILogger<AdminTenantLicenseService> _logger;

    public AdminTenantLicenseService(AppDbContext db, ILogger<AdminTenantLicenseService> logger)
    {
        _db = db;
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
                .Where(il => il.LicenseKey == key && !il.IsDeleted)
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

        foreach (var row in byName.Where(r => items.All(i => i.IssuedLicenseId != r.Id)))
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
        var now = DateTime.UtcNow;
        var until = tenant.LicenseValidUntilUtc;
        int? days = null;
        string kind;
        if (!until.HasValue)
        {
            kind = "none";
        }
        else
        {
            days = (int)Math.Ceiling((until.Value - now).TotalDays);
            kind = days < 0 ? "expired" : string.IsNullOrWhiteSpace(tenant.LicenseKey) ? "trial" : "active";
        }

        _ = history;
        return new TenantLicenseStatusDto(
            kind,
            tenant.LicenseKey,
            until,
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
