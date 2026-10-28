using System.Security.Cryptography;
using System.Text;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Order;

/// <summary>
/// Provider-agnostic HMAC-SHA256 of the raw body.
/// Header <see cref="OnlineOrderIntegrationSignatureStatus.HeaderName"/> is
/// <see cref="OnlineOrderIntegrationSignatureStatus.PendingConfirmationNote"/>.
/// Does not call Stripe or the payment-gateway HMAC helper.
/// </summary>
public static class OnlineOrderIntegrationSignature
{
    public static bool IsValid(string rawBody, string? secret, string? providedHeader)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(providedHeader))
            return false;

        var provided = providedHeader.Trim();
        if (provided.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
            provided = provided["sha256=".Length..];

        byte[] providedBytes;
        try
        {
            providedBytes = Convert.FromHexString(provided);
        }
        catch (FormatException)
        {
            return false;
        }

        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(rawBody ?? string.Empty));
        return CryptographicOperations.FixedTimeEquals(hash, providedBytes);
    }

    public static string SignHex(string rawBody, string secret)
    {
        var hash = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret),
            Encoding.UTF8.GetBytes(rawBody ?? string.Empty));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
