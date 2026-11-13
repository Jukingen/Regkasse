using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Models.Constants;
using KasseAPI_Final.Models.Enums;
using KasseAPI_Final.Services.Caching;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Analytics;

public sealed class CustomerAnalyticsService : ICustomerAnalyticsService
{
    public static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly AppDbContext _db;
    private readonly ICacheService _cache;
    private readonly ILogger<CustomerAnalyticsService> _logger;

    public CustomerAnalyticsService(
        AppDbContext db,
        ICacheService cache,
        ILogger<CustomerAnalyticsService> logger)
    {
        _db = db;
        _cache = cache;
        _logger = logger;
    }

    public Task<CustomerAnalyticsDto> GetCustomerAnalyticsAsync(CancellationToken cancellationToken = default) =>
        _cache.GetOrCreateAsync(
            CacheKeys.CustomerAnalytics,
            LoadAsync,
            CacheTtl,
            cancellationToken);

    private async Task<CustomerAnalyticsDto> LoadAsync(CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);
        var sevenDaysLater = now.AddDays(7);
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var tenants = await _db.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Select(t => new
            {
                t.Id,
                t.Slug,
                t.Status,
                t.CreatedAt,
                t.LicenseValidUntilUtc,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        tenants = tenants
            .Where(t => t.Id != SystemTenantIds.Platform && !SystemTenantIds.IsPlatformSlug(t.Slug))
            .ToList();

        var sales = await _db.LicenseSales
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Select(s => new LicenseSaleRow(
                s.TenantId,
                s.LicenseType,
                s.LicensePlan,
                s.PriceNet,
                s.ValidFromUtc,
                s.ValidUntilUtc,
                s.Status,
                s.CancelledAtUtc,
                s.UpdatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var tenantIds = tenants.Select(t => t.Id).ToHashSet();
        sales = sales.Where(s => tenantIds.Contains(s.TenantId)).ToList();

        var currentSales = sales.Where(s => CoversInstant(s, now)).ToList();

        var salesByTenant = currentSales
            .GroupBy(s => s.TenantId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.ValidUntilUtc).First());

        var total = tenants.Count;
        var active = tenants.Count(t => string.Equals(t.Status, TenantStatuses.Active, StringComparison.OrdinalIgnoreCase));
        var inOnboarding = tenants.Count(t =>
            string.Equals(t.Status, TenantStatuses.InOnboarding, StringComparison.OrdinalIgnoreCase));
        var suspended = tenants.Count(t =>
            string.Equals(t.Status, TenantStatuses.Suspended, StringComparison.OrdinalIgnoreCase));

        var trial = 0;
        var paid = 0;
        var starter = 0;
        var business = 0;
        var plus = 0;
        foreach (var sale in salesByTenant.Values)
        {
            switch (sale.LicenseType)
            {
                case LicenseType.Trial:
                    trial++;
                    break;
                case LicenseType.Starter:
                    starter++;
                    paid++;
                    break;
                case LicenseType.Business:
                    business++;
                    paid++;
                    break;
                case LicenseType.Plus:
                    plus++;
                    paid++;
                    break;
            }
        }

        var expiringSoon = tenants.Count(t =>
            string.Equals(t.Status, TenantStatuses.Active, StringComparison.OrdinalIgnoreCase)
            && t.LicenseValidUntilUtc.HasValue
            && t.LicenseValidUntilUtc.Value > now
            && t.LicenseValidUntilUtc.Value <= sevenDaysLater);

        var expired = tenants.Count(t =>
            string.Equals(t.Status, TenantStatuses.Suspended, StringComparison.OrdinalIgnoreCase)
            && t.LicenseValidUntilUtc.HasValue
            && t.LicenseValidUntilUtc.Value <= now);

        var mrr = 0m;
        foreach (var sale in salesByTenant.Values)
        {
            if (sale.LicenseType == LicenseType.Trial)
                continue;
            mrr += ToMonthlyRecurring(sale.PriceNet, sale.LicensePlan, sale.ValidFromUtc, sale.ValidUntilUtc);
        }

        mrr = decimal.Round(mrr, 2, MidpointRounding.AwayFromZero);
        var newLast30 = tenants.Count(t => t.CreatedAt >= thirtyDaysAgo);

        var paidAtStart = sales
            .Where(s => IsPaidLicense(s.LicenseType) && CoversInstant(s, monthStart))
            .Select(s => s.TenantId)
            .ToHashSet();
        var paidNow = sales
            .Where(s => IsPaidLicense(s.LicenseType) && CoversInstant(s, now))
            .Select(s => s.TenantId)
            .ToHashSet();
        var lost = paidAtStart.Count(id => !paidNow.Contains(id));
        var churnRate = CalculateChurnRate(paidAtStart.Count, lost);
        var arpu = CalculateArpu(mrr, paid);
        var ltv = CalculateCustomerLtv(arpu, churnRate);

        var dto = new CustomerAnalyticsDto(
            TotalTenants: total,
            ActiveTenants: active,
            InOnboardingTenants: inOnboarding,
            SuspendedTenants: suspended,
            TrialTenants: trial,
            PaidTenants: paid,
            ExpiringSoon: expiringSoon,
            ExpiredTenants: expired,
            Mrr: mrr,
            NewTenantsLast30Days: newLast30,
            ChurnRate: churnRate,
            Arpu: arpu,
            PlanDistribution: new PlanDistributionDto(trial, starter, business, plus),
            CustomerLtv: ltv);

        _logger.LogDebug(
            "Customer analytics loaded: total={Total} active={Active} mrr={Mrr} churn={Churn} arpu={Arpu}",
            dto.TotalTenants,
            dto.ActiveTenants,
            dto.Mrr,
            dto.ChurnRate,
            dto.Arpu);

        return dto;
    }

    internal static decimal ToMonthlyRecurring(
        decimal priceNet,
        string licensePlan,
        DateTime validFromUtc,
        DateTime validUntilUtc)
    {
        var months = licensePlan switch
        {
            LicenseSalePlans.SixMonths => 6m,
            LicenseSalePlans.TwelveMonths => 12m,
            _ => EstimateMonths(validFromUtc, validUntilUtc),
        };

        if (months <= 0)
            months = 1m;

        return priceNet / months;
    }

    /// <summary>Monthly churn % = lost paid mandants / paid covering at month start × 100.</summary>
    internal static decimal CalculateChurnRate(int customersAtStart, int customersLost)
    {
        if (customersAtStart <= 0 || customersLost <= 0)
            return 0m;

        var lost = Math.Min(customersLost, customersAtStart);
        return decimal.Round((decimal)lost / customersAtStart * 100m, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>ARPU = MRR / paid tenants (trials excluded; MRR already excludes trial).</summary>
    internal static decimal CalculateArpu(decimal mrr, int paidTenants)
    {
        if (paidTenants <= 0)
            return 0m;

        return decimal.Round(mrr / paidTenants, 2, MidpointRounding.AwayFromZero);
    }

    /// <summary>Simple LTV = ARPU / monthly churn rate. Null when churn is zero (undefined).</summary>
    internal static decimal? CalculateCustomerLtv(decimal arpu, decimal churnRatePercent)
    {
        if (arpu <= 0m || churnRatePercent <= 0m)
            return null;

        return decimal.Round(arpu / (churnRatePercent / 100m), 2, MidpointRounding.AwayFromZero);
    }

    internal static bool CoversInstant(LicenseSaleRow sale, DateTime instantUtc)
    {
        if (sale.ValidFromUtc > instantUtc || sale.ValidUntilUtc <= instantUtc)
            return false;

        if (string.Equals(sale.Status, LicenseSaleStatuses.Active, StringComparison.OrdinalIgnoreCase))
            return true;

        var endedAt = sale.CancelledAtUtc ?? sale.UpdatedAt;
        return endedAt > instantUtc;
    }

    private static bool IsPaidLicense(LicenseType? type) =>
        type is LicenseType.Starter or LicenseType.Business or LicenseType.Plus;

    private static decimal EstimateMonths(DateTime from, DateTime until)
    {
        var days = (until - from).TotalDays;
        if (days <= 0)
            return 1m;
        return (decimal)Math.Max(1, Math.Round(days / 30.4375, MidpointRounding.AwayFromZero));
    }

    internal sealed record LicenseSaleRow(
        Guid TenantId,
        LicenseType? LicenseType,
        string LicensePlan,
        decimal PriceNet,
        DateTime ValidFromUtc,
        DateTime ValidUntilUtc,
        string Status,
        DateTime? CancelledAtUtc,
        DateTime UpdatedAt);
}
