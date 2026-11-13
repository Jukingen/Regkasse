namespace KasseAPI_Final.Services.Loyalty;

public sealed class LoyaltyResult
{
    public bool Succeeded { get; init; }
    public string? Code { get; init; }
    public string? Message { get; init; }

    /// <summary>Points awarded (earn) or EUR discount amount (redeem).</summary>
    public decimal Value { get; init; }

    public int Balance { get; init; }
    public int PointsChanged { get; init; }

    public static LoyaltyResult Fail(string code, string message) =>
        new() { Succeeded = false, Code = code, Message = message };

    public static LoyaltyResult Success(decimal value, int balance, int pointsChanged) =>
        new()
        {
            Succeeded = true,
            Value = value,
            Balance = balance,
            PointsChanged = pointsChanged
        };
}
