using System.Text;
using KasseAPI_Final.Data;
using KasseAPI_Final.DTOs;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Loyalty;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Order;

public interface IPublicCustomerDashboardService
{
    /// <summary>
    /// Lookup by tenant slug + phone digits. Returns null → HTTP 404 (no enumeration).
    /// Requires at least 6 phone digits.
    /// </summary>
    Task<PublicCustomerDashboardDto?> GetDashboardAsync(
        string tenantSlug,
        string phone,
        CancellationToken ct = default);
}

public sealed class PublicCustomerDashboardService : IPublicCustomerDashboardService
{
    public const int MinPhoneDigits = 6;

    private readonly IDbContextFactory<AppDbContext> _dbFactory;

    public PublicCustomerDashboardService(IDbContextFactory<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<PublicCustomerDashboardDto?> GetDashboardAsync(
        string tenantSlug,
        string phone,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tenantSlug) || string.IsNullOrWhiteSpace(phone))
            return null;

        var phoneDigits = DigitsOnly(phone);
        if (phoneDigits.Length < MinPhoneDigits)
            return null;

        var slug = tenantSlug.Trim().ToLowerInvariant();
        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var tenant = await db.Tenants.AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                t => t.Slug.ToLower() == slug && t.IsActive && t.DeletedAtUtc == null,
                ct);
        if (tenant is null)
            return null;

        var customers = await db.Customers.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenant.Id && !c.IsSystem && c.IsActive)
            .ToListAsync(ct);

        var customer = customers.FirstOrDefault(c => PhoneMatches(DigitsOnly(c.Phone), phoneDigits));

        var orders = await db.OnlineOrders.AsNoTracking()
            .IgnoreQueryFilters()
            .Where(o => o.TenantId == tenant.Id)
            .OrderByDescending(o => o.CreatedAt)
            .Take(200)
            .ToListAsync(ct);

        var matchedOrders = orders
            .Where(o => PhoneMatches(DigitsOnly(o.CustomerPhone), phoneDigits))
            .Take(50)
            .ToList();

        if (customer is null && matchedOrders.Count == 0)
            return null;

        var displayName = customer is not null
            ? MaskCustomerName(customer.Name)
            : MaskCustomerName(matchedOrders[0].CustomerName);

        var loyaltyPoints = customer?.LoyaltyPoints ?? 0;
        var totalSpent = customer?.TotalSpent
            ?? matchedOrders.Where(o =>
                    string.Equals(o.PaymentStatus, OnlineOrderPaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(o.OrderStatus, OnlineOrderStatuses.Completed, StringComparison.OrdinalIgnoreCase))
                .Sum(o => o.Total);

        return new PublicCustomerDashboardDto
        {
            CustomerDisplayName = displayName,
            LoyaltyPoints = loyaltyPoints,
            RedeemableEuro = loyaltyPoints / LoyaltyService.PointsToEuro,
            TotalSpent = totalSpent,
            TotalOrders = matchedOrders.Count,
            Orders = matchedOrders
                .Select(o => new PublicCustomerOrderSummaryDto
                {
                    OrderNumber = o.OrderNumber,
                    OrderStatus = o.OrderStatus,
                    Total = o.Total,
                    Currency = "EUR",
                    CreatedAt = o.CreatedAt
                })
                .ToList()
        };
    }

    private static bool PhoneMatches(string stored, string requested)
    {
        if (stored.Length == 0 || requested.Length == 0)
            return false;
        return string.Equals(stored, requested, StringComparison.Ordinal)
               || stored.EndsWith(requested, StringComparison.Ordinal)
               || requested.EndsWith(stored, StringComparison.Ordinal);
    }

    private static string DigitsOnly(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        var sb = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsDigit(ch))
                sb.Append(ch);
        }
        return sb.ToString();
    }

    private static string MaskCustomerName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;
        var trimmed = name.Trim();
        if (trimmed.Length <= 2)
            return trimmed[0] + "*";
        return trimmed[0] + new string('*', Math.Min(trimmed.Length - 2, 6)) + trimmed[^1];
    }
}
