using System.Security.Cryptography;
using System.Text;
using KasseAPI_Final.Models;

namespace KasseAPI_Final.Services.Order;

/// <summary>
/// JET Connect webhook check. <c>Authorization</c> is the tenant API key.
/// <c>X-JET-Connect-Hash</c> is checked only when that tenant stored an HMAC secret.
/// </summary>
public static class LieferandoWebhookAuth
{
    public static bool AuthorizationMatches(string? authorizationHeader, string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(authorizationHeader) || string.IsNullOrWhiteSpace(apiKey))
            return false;

        var header = authorizationHeader.Trim();
        var key = apiKey.Trim();
        if (FixedEquals(header, key))
            return true;

        const string bearer = "Bearer ";
        if (header.StartsWith(bearer, StringComparison.OrdinalIgnoreCase))
            return FixedEquals(header[bearer.Length..].Trim(), key);

        return false;
    }

    public static bool HashMatches(string rawBody, string secret, string? providedHeader)
    {
        if (string.IsNullOrWhiteSpace(secret))
            return false;

        if (OnlineOrderIntegrationSignature.IsValid(rawBody, secret, providedHeader))
            return true;

        var signature = ReadEnvelopeSignature(providedHeader);
        return signature is not null
               && OnlineOrderIntegrationSignature.IsValid(rawBody, secret, signature);
    }

    internal static string? ReadEnvelopeSignature(string? header)
    {
        if (string.IsNullOrWhiteSpace(header))
            return null;

        const string marker = "signature=";
        var start = header.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;

        var value = header[(start + marker.Length)..].Trim();
        var end = value.IndexOfAny([',', ' ']);
        if (end >= 0)
            value = value[..end];
        return value.Length == 0 ? null : value;
    }

    private static bool FixedEquals(string left, string right)
    {
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(left));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes(right));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
