using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
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

        var tenants = await _db.Tenants
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Select(t => new
            {
                t.Id,
                t.Status,
                t.CreatedAt,
                t.LicenseValidUntilUtc,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var activeSales = await _db.LicenseSales
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(s => s.Status == LicenseSaleStatuses.Active)
            .Select(s => new
            {
                s.TenantId,
                s.LicenseType,
                s.LicensePlan,
                s.PriceNet,
                s.ValidFromUtc,
                s.ValidUntilUtc,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var salesByTenant = activeSales
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
        foreach (var sale in salesByTenant.Values)
        {
            if (sale.LicenseType == LicenseType.Trial)
                trial++;
            else if (sale.LicenseType is LicenseType.Starter or LicenseType.Business or LicenseType.Plus)
                paid++;
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

        var newLast30 = tenants.Count(t => t.CreatedAt >= thirtyDaysAgo);

        var dto = new CustomerAnalyticsDto(
            TotalTenants: total,
            ActiveTenants: active,
            InOnboardingTenants: inOnboarding,
            SuspendedTenants: suspended,
            TrialTenants: trial,
            PaidTenants: paid,
            ExpiringSoon: expiringSoon,
            ExpiredTenants: expired,
            Mrr: decimal.Round(mrr, 2, MidpointRounding.AwayFromZero),
            NewTenantsLast30Days: newLast30);

        _logger.LogDebug(
            "Customer analytics loaded: total={Total} active={Active} mrr={Mrr}",
            dto.TotalTenants,
            dto.ActiveTenants,
            dto.Mrr);

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

    private static decimal EstimateMonths(DateTime from, DateTime until)
    {
        var days = (until - from).TotalDays;
        if (days <= 0)
            return 1m;
        return (decimal)Math.Max(1, Math.Round(days / 30.4375, MidpointRounding.AwayFromZero));
    }
}
