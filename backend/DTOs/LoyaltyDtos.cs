using System.ComponentModel.DataAnnotations;

namespace KasseAPI_Final.DTOs;

public sealed class LoyaltyBalanceDto
{
    public Guid CustomerId { get; init; }
    public int LoyaltyPoints { get; init; }
}

public sealed class RedeemLoyaltyPointsRequest
{
    [Range(1, int.MaxValue)]
    public int Points { get; set; }
}

public sealed class RedeemLoyaltyPointsResponse
{
    public Guid CustomerId { get; init; }
    public int PointsRedeemed { get; init; }
    public decimal DiscountEuro { get; init; }
    public int LoyaltyPointsBalance { get; init; }
}
