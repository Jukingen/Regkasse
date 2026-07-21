using System.Text;
using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using KasseAPI_Final.Services.Loyalty;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Order;

public interface IOnlineOrderLoyaltyService
{
    /// <summary>
    /// Award loyalty points when an online order becomes completed and paid.
    /// Matches CRM customer by phone (digits) within the order tenant.
    /// </summary>
    Task<LoyaltyEarnResult> TryEarnOnCompletedAsync(OnlineOrder order, CancellationToken ct = default);
}

public sealed class LoyaltyEarnResult
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public Guid? CustomerId { get; init; }
    public int PointsAwarded { get; init; }
    public int LoyaltyPointsBalance { get; init; }

    public static LoyaltyEarnResult Skip(string code) =>
        new() { Succeeded = false, Code = code };

    public static LoyaltyEarnResult Ok(Guid customerId, int awarded, int balance) =>
        new()
        {
            Succeeded = true,
            CustomerId = customerId,
            PointsAwarded = awarded,
            LoyaltyPointsBalance = balance
        };
}

/// <summary>Online-order earn adapter over <see cref="ILoyaltyService"/> (1 point per whole EUR).</summary>
public sealed class OnlineOrderLoyaltyService : IOnlineOrderLoyaltyService
{
    public const string NotCompletedCode = "LOYALTY_NOT_COMPLETED";
    public const string NotPaidCode = "LOYALTY_NOT_PAID";
    public const string NoCustomerCode = "LOYALTY_NO_CUSTOMER";
    public const string AlreadyLinkedCode = "LOYALTY_ALREADY_APPLIED";

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly ILoyaltyService _loyalty;
    private readonly TimeProvider _time;
    private readonly ILogger<OnlineOrderLoyaltyService> _logger;

    public OnlineOrderLoyaltyService(
        IDbContextFactory<AppDbContext> dbFactory,
        ILoyaltyService loyalty,
        TimeProvider time,
        ILogger<OnlineOrderLoyaltyService> logger)
    {
        _dbFactory = dbFactory;
        _loyalty = loyalty;
        _time = time;
        _logger = logger;
    }

    public async Task<LoyaltyEarnResult> TryEarnOnCompletedAsync(
        OnlineOrder order,
        CancellationToken ct = default)
    {
        if (!string.Equals(order.OrderStatus, OnlineOrderStatuses.Completed, StringComparison.OrdinalIgnoreCase))
            return LoyaltyEarnResult.Skip(NotCompletedCode);

        if (!string.Equals(order.PaymentStatus, OnlineOrderPaymentStatuses.Paid, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(order.PaymentMethod, OnlineOrderPaymentMethods.Cash, StringComparison.OrdinalIgnoreCase))
        {
            return LoyaltyEarnResult.Skip(NotPaidCode);
        }

        if (order.Total <= 0m)
            return LoyaltyEarnResult.Skip(NotPaidCode);

        await using var db = await _dbFactory.CreateDbContextAsync(ct);

        var tracked = await db.OnlineOrders
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(o => o.Id == order.Id, ct);
        if (tracked is null)
            return LoyaltyEarnResult.Skip(NoCustomerCode);

        Customer? customer = null;
        if (tracked.CustomerId is Guid linkedId)
        {
            customer = await db.Customers
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == linkedId && c.TenantId == tracked.TenantId, ct);
        }

        if (customer is null)
        {
            var phoneDigits = DigitsOnly(tracked.CustomerPhone);
            if (phoneDigits.Length < 4)
                return LoyaltyEarnResult.Skip(NoCustomerCode);

            var candidates = await db.Customers
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tracked.TenantId && !c.IsSystem && c.IsActive)
                .ToListAsync(ct);

            customer = candidates.FirstOrDefault(c =>
            {
                var stored = DigitsOnly(c.Phone);
                return stored.Length > 0
                       && (stored == phoneDigits || stored.EndsWith(phoneDigits, StringComparison.Ordinal)
                           || phoneDigits.EndsWith(stored, StringComparison.Ordinal));
            });
        }

        if (customer is null)
            return LoyaltyEarnResult.Skip(NoCustomerCode);

        var firstEarn = tracked.CustomerId is null;
        tracked.CustomerId = customer.Id;

        if (!firstEarn)
        {
            await db.SaveChangesAsync(ct);
            return LoyaltyEarnResult.Skip(AlreadyLinkedCode);
        }

        var earn = _loyalty.ApplyAddPoints(customer, tracked.Total);
        if (!earn.Succeeded)
        {
            await db.SaveChangesAsync(ct);
            return LoyaltyEarnResult.Skip(earn.Code ?? NoCustomerCode);
        }

        var now = _time.GetUtcNow().UtcDateTime;
        customer.VisitCount += 1;
        customer.LastVisit = now;
        customer.UpdatedAt = now;
        tracked.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Loyalty: awarded {Points} points to customer {CustomerId} for online order {OrderId}",
            earn.PointsChanged,
            customer.Id,
            tracked.Id);

        return LoyaltyEarnResult.Ok(customer.Id, earn.PointsChanged, customer.LoyaltyPoints);
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
}
