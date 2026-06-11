namespace KasseAPI_Final.DTOs;

/// <summary>POS customer lookup response (QR scan).</summary>
public sealed class PosCustomerDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string CustomerNumber { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public int LoyaltyPoints { get; init; }
}
