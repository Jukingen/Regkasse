using KasseAPI_Final.Data;
using KasseAPI_Final.Models;
using Microsoft.EntityFrameworkCore;

namespace KasseAPI_Final.Services.Loyalty;

/// <summary>
/// Tenant-scoped customer loyalty: earn 1 point per whole EUR; redeem 100 points = 1 EUR discount.
/// Not fiscal — does not touch RKSV / PaymentService.
/// </summary>
public sealed class LoyaltyService : ILoyaltyService
{
    public const int PointsPerEuro = 1;
    public const int PointsToEuro = 100;

    public const string NotFoundCode = "LOYALTY_CUSTOMER_NOT_FOUND";
    public const string SystemCustomerCode = "LOYALTY_SYSTEM_CUSTOMER";
    public const string InvalidAmountCode = "LOYALTY_INVALID_AMOUNT";
    public const string InvalidPointsCode = "LOYALTY_INVALID_POINTS";
    public const string InsufficientCode = "LOYALTY_INSUFFICIENT_POINTS";

    private readonly IDbContextFactory<AppDbContext> _dbFactory;
    private readonly TimeProvider _time;
    private readonly ILogger<LoyaltyService> _logger;

    public LoyaltyService(
        IDbContextFactory<AppDbContext> dbFactory,
        TimeProvider time,
        ILogger<LoyaltyService> logger)
    {
        _dbFactory = dbFactory;
        _time = time;
        _logger = logger;
    }

    public async Task<LoyaltyResult> AddPointsAsync(
        Guid customerId,
        decimal amount,
        CancellationToken ct = default)
    {
        if (amount <= 0m)
            return LoyaltyResult.Fail(InvalidAmountCode, "Amount must be greater than zero");

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct);
        if (customer is null)
            return LoyaltyResult.Fail(NotFoundCode, "Customer not found");
        if (customer.IsSystem)
            return LoyaltyResult.Fail(SystemCustomerCode, "System customers cannot earn loyalty points");

        var result = ApplyAddPoints(customer, amount);
        if (!result.Succeeded)
            return result;

        customer.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Loyalty: awarded {Points} points to customer {CustomerId}; balance={Balance}",
            result.PointsChanged,
            customerId,
            result.Balance);

        return result;
    }

    public LoyaltyResult ApplyAddPoints(Customer customer, decimal amount)
    {
        if (customer is null)
            return LoyaltyResult.Fail(NotFoundCode, "Customer not found");
        if (customer.IsSystem)
            return LoyaltyResult.Fail(SystemCustomerCode, "System customers cannot earn loyalty points");
        if (amount <= 0m)
            return LoyaltyResult.Fail(InvalidAmountCode, "Amount must be greater than zero");

        var points = (int)Math.Floor(amount * PointsPerEuro);
        if (points <= 0)
            return LoyaltyResult.Fail(InvalidAmountCode, "Amount too small to earn points");

        customer.LoyaltyPoints += points;
        customer.TotalSpent += amount;

        return LoyaltyResult.Success(points, customer.LoyaltyPoints, points);
    }

    public async Task<LoyaltyResult> RedeemPointsAsync(
        Guid customerId,
        int points,
        CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var customer = await db.Customers.FirstOrDefaultAsync(c => c.Id == customerId, ct);
        if (customer is null)
            return LoyaltyResult.Fail(NotFoundCode, "Customer not found");
        if (customer.IsSystem)
            return LoyaltyResult.Fail(SystemCustomerCode, "System customers cannot redeem loyalty points");

        var result = ApplyRedeemPoints(customer, points);
        if (!result.Succeeded)
            return result;

        customer.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Loyalty: redeemed {Points} points for customer {CustomerId}; discount={Discount} EUR; balance={Balance}",
            points,
            customerId,
            result.Value,
            result.Balance);

        return result;
    }

    public LoyaltyResult ApplyRedeemPoints(Customer customer, int points)
    {
        if (customer is null)
            return LoyaltyResult.Fail(NotFoundCode, "Customer not found");
        if (customer.IsSystem)
            return LoyaltyResult.Fail(SystemCustomerCode, "System customers cannot redeem loyalty points");
        if (points <= 0)
            return LoyaltyResult.Fail(InvalidPointsCode, "Points must be greater than zero");
        if (customer.LoyaltyPoints < points)
            return LoyaltyResult.Fail(InsufficientCode, "Insufficient points");

        // Integer EUR discount: 100 points = 1 EUR
        var discount = points / PointsToEuro;
        customer.LoyaltyPoints -= points;

        return LoyaltyResult.Success(discount, customer.LoyaltyPoints, -points);
    }

    public async Task<int?> GetBalanceAsync(Guid customerId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var customer = await db.Customers.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == customerId, ct);
        return customer?.LoyaltyPoints;
    }
}
