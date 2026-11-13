using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Loyalty;

public interface ILoyaltyService
{
    /// <summary>1 point per whole EUR spent. Updates <see cref="Customer.LoyaltyPoints"/> and <see cref="Customer.TotalSpent"/>.</summary>
    Task<LoyaltyResult> AddPointsAsync(
        Guid customerId,
        decimal amount,
        CancellationToken ct = default);

    /// <summary>
    /// Same earn rules as <see cref="AddPointsAsync"/> but mutates an already-tracked customer
    /// without saving (caller owns the unit of work).
    /// </summary>
    LoyaltyResult ApplyAddPoints(Customer customer, decimal amount);

    /// <summary>100 points = 1 EUR discount. Deducts points; does not create a fiscal payment.</summary>
    Task<LoyaltyResult> RedeemPointsAsync(
        Guid customerId,
        int points,
        CancellationToken ct = default);

    /// <summary>Same redeem rules without saving (caller owns the unit of work).</summary>
    LoyaltyResult ApplyRedeemPoints(Customer customer, int points);

    Task<int?> GetBalanceAsync(Guid customerId, CancellationToken ct = default);
}
